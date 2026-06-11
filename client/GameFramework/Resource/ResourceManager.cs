using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameFramework.Core.GameSystem;

namespace GameFramework.Resource
{
    public class ResourceManager : GameModule
    {
        private readonly object _syncRoot = new();
        private readonly Dictionary<string, ResourceEntry> _cache = new();
        private readonly Dictionary<string, Task<ResourceEntry>> _loadingTasks = new();
        private readonly List<IResourceProvider> _providers = new();

        public override string ModuleName => "ResourceManager";

        public IResourceProvider? Provider
        {
            get
            {
                lock (_syncRoot)
                {
                    return _providers.FirstOrDefault();
                }
            }
        }

        public IReadOnlyList<IResourceProvider> Providers
        {
            get
            {
                lock (_syncRoot)
                {
                    return _providers.ToList();
                }
            }
        }

        public void SetProvider(IResourceProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            Clear();
            lock (_syncRoot)
            {
                _providers.Clear();
                AddProviderInternal(provider);
            }
        }

        public void AddProvider(IResourceProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            lock (_syncRoot)
            {
                if (_providers.Any(existing => existing.Name == provider.Name))
                    throw new InvalidOperationException($"Resource provider '{provider.Name}' is already registered.");

                AddProviderInternal(provider);
            }
        }

        public bool RemoveProvider(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Provider name cannot be empty.", nameof(name));

            lock (_syncRoot)
            {
                var provider = _providers.FirstOrDefault(existing => existing.Name == name);
                if (provider == null)
                    return false;

                if (_cache.Values.Any(entry => ReferenceEquals(entry.Provider, provider)))
                {
                    throw new InvalidOperationException(
                        $"Resource provider '{name}' cannot be removed while it has loaded resources.");
                }

                _providers.Remove(provider);
                return true;
            }
        }

        public T Load<T>(string path) where T : class
        {
            return LoadHandle<T>(path).Asset;
        }

        public async Task<T> LoadAsync<T>(string path, CancellationToken cancellationToken = default) where T : class
        {
            var handle = await LoadHandleAsync<T>(path, cancellationToken).ConfigureAwait(false);
            return handle.Asset;
        }

        public ResourceHandle<T> LoadHandle<T>(string path) where T : class
        {
            var normalizedPath = NormalizePath(path);
            var key = BuildKey<T>(normalizedPath);

            lock (_syncRoot)
            {
                if (_cache.TryGetValue(key, out var cachedEntry))
                {
                    cachedEntry.RefCount++;
                    return CreateHandle<T>(cachedEntry);
                }
            }

            var provider = FindProvider(normalizedPath, typeof(T), requireSync: true);
            if (!provider.CanLoadSync)
                throw new NotSupportedException(
                    $"Resource provider '{provider.Name}' does not support synchronous loading.");

            try
            {
                var asset = provider.Load<T>(normalizedPath);
                lock (_syncRoot)
                {
                    if (_cache.TryGetValue(key, out var cachedEntry))
                    {
                        cachedEntry.RefCount++;
                        provider.Unload(normalizedPath, asset);
                        return CreateHandle<T>(cachedEntry);
                    }

                    var entry = new ResourceEntry(key, normalizedPath, typeof(T), asset, provider, 1);
                    _cache[key] = entry;
                    return CreateHandle<T>(entry);
                }
            }
            catch (Exception ex) when (ex is not ResourceLoadException)
            {
                throw new ResourceLoadException($"Failed to load resource '{normalizedPath}'.", ex);
            }
        }

        public async Task<ResourceHandle<T>> LoadHandleAsync<T>(
            string path,
            CancellationToken cancellationToken = default) where T : class
        {
            var normalizedPath = NormalizePath(path);
            var key = BuildKey<T>(normalizedPath);

            lock (_syncRoot)
            {
                if (_cache.TryGetValue(key, out var cachedEntry))
                {
                    cachedEntry.RefCount++;
                    return CreateHandle<T>(cachedEntry);
                }
            }

            var loadingTask = GetOrCreateLoadTask<T>(key, normalizedPath, cancellationToken);
            var entry = await loadingTask.ConfigureAwait(false);

            lock (_syncRoot)
            {
                entry.RefCount++;
                return CreateHandle<T>(entry);
            }
        }

        public void Retain(string path)
        {
            var entry = FindSingleEntry(path);
            lock (_syncRoot)
            {
                entry.RefCount++;
            }
        }

        public void Retain<T>(string path) where T : class
        {
            var entry = GetCachedEntry<T>(NormalizePath(path));
            lock (_syncRoot)
            {
                entry.RefCount++;
            }
        }

        public void Release(string path)
        {
            Release(FindSingleEntry(path));
        }

        public void Release<T>(string path) where T : class
        {
            Release(GetCachedEntry<T>(NormalizePath(path)));
        }

        public void UnloadUnused()
        {
            List<ResourceEntry> unusedEntries;
            lock (_syncRoot)
            {
                unusedEntries = _cache.Values.Where(entry => entry.RefCount <= 0).ToList();
            }

            foreach (var entry in unusedEntries)
            {
                UnloadEntry(entry);
            }
        }

        public void Clear()
        {
            List<ResourceEntry> entries;
            lock (_syncRoot)
            {
                entries = _cache.Values.ToList();
                _cache.Clear();
                _loadingTasks.Clear();
            }

            foreach (var entry in entries)
            {
                entry.Provider.Unload(entry.Path, entry.Asset);
            }
        }

        public bool IsLoaded(string path)
        {
            var normalizedPath = NormalizePath(path);
            lock (_syncRoot)
            {
                return _cache.Values.Any(entry => entry.Path == normalizedPath);
            }
        }

        public bool IsLoaded<T>(string path) where T : class
        {
            var key = BuildKey<T>(NormalizePath(path));
            lock (_syncRoot)
            {
                return _cache.ContainsKey(key);
            }
        }

        protected override void OnShutdown()
        {
            Clear();
        }

        private Task<ResourceEntry> GetOrCreateLoadTask<T>(
            string key,
            string normalizedPath,
            CancellationToken cancellationToken) where T : class
        {
            lock (_syncRoot)
            {
                if (_loadingTasks.TryGetValue(key, out var existingTask))
                    return existingTask;

                var task = LoadEntryAsync<T>(key, normalizedPath, cancellationToken);
                _loadingTasks[key] = task;
                return task;
            }
        }

        private async Task<ResourceEntry> LoadEntryAsync<T>(
            string key,
            string normalizedPath,
            CancellationToken cancellationToken) where T : class
        {
            try
            {
                var provider = FindProvider(normalizedPath, typeof(T), requireSync: false);
                var asset = await provider.LoadAsync<T>(normalizedPath, cancellationToken).ConfigureAwait(false);

                lock (_syncRoot)
                {
                    if (_cache.TryGetValue(key, out var cachedEntry))
                    {
                        provider.Unload(normalizedPath, asset);
                        return cachedEntry;
                    }

                    var entry = new ResourceEntry(key, normalizedPath, typeof(T), asset, provider, 0);
                    _cache[key] = entry;
                    return entry;
                }
            }
            catch (Exception ex) when (ex is not ResourceLoadException)
            {
                throw new ResourceLoadException($"Failed to load resource '{normalizedPath}'.", ex);
            }
            finally
            {
                lock (_syncRoot)
                {
                    _loadingTasks.Remove(key);
                }
            }
        }

        private ResourceHandle<T> CreateHandle<T>(ResourceEntry entry) where T : class
        {
            if (entry.Asset is not T asset)
            {
                throw new ResourceLoadException(
                    $"Resource '{entry.Path}' is '{entry.AssetType.FullName}', not '{typeof(T).FullName}'.");
            }

            return new ResourceHandle<T>(this, entry.Path, asset, () => GetRefCount(entry.Key));
        }

        private int GetRefCount(string key)
        {
            lock (_syncRoot)
            {
                return _cache.TryGetValue(key, out var entry) ? entry.RefCount : 0;
            }
        }

        private ResourceEntry FindSingleEntry(string path)
        {
            var normalizedPath = NormalizePath(path);
            lock (_syncRoot)
            {
                var entries = _cache.Values.Where(entry => entry.Path == normalizedPath).ToList();
                if (entries.Count == 0)
                    throw new ResourceLoadException($"Resource is not loaded: {normalizedPath}");
                if (entries.Count > 1)
                    throw new InvalidOperationException(
                        $"Multiple typed resources are loaded for '{normalizedPath}'. Use the typed overload.");

                return entries[0];
            }
        }

        private ResourceEntry GetCachedEntry<T>(string normalizedPath) where T : class
        {
            var key = BuildKey<T>(normalizedPath);
            lock (_syncRoot)
            {
                if (_cache.TryGetValue(key, out var entry))
                    return entry;
            }

            throw new ResourceLoadException($"Resource is not loaded: {normalizedPath}");
        }

        private void Release(ResourceEntry entry)
        {
            var shouldUnload = false;
            lock (_syncRoot)
            {
                if (!_cache.TryGetValue(entry.Key, out var cachedEntry))
                    return;

                cachedEntry.RefCount--;
                shouldUnload = cachedEntry.RefCount <= 0;
            }

            if (shouldUnload)
                UnloadEntry(entry);
        }

        private void UnloadEntry(ResourceEntry entry)
        {
            lock (_syncRoot)
            {
                if (!_cache.TryGetValue(entry.Key, out var cachedEntry) || !ReferenceEquals(cachedEntry, entry))
                    return;

                _cache.Remove(entry.Key);
            }

            entry.Provider.Unload(entry.Path, entry.Asset);
        }

        private IResourceProvider FindProvider(string normalizedPath, Type assetType, bool requireSync)
        {
            IResourceProvider? provider;
            lock (_syncRoot)
            {
                provider = _providers.FirstOrDefault(candidate => candidate.CanLoad(normalizedPath, assetType));
            }

            if (provider == null)
                throw new ResourceLoadException(
                    $"No resource provider can load '{normalizedPath}' as '{assetType.FullName}'.");

            if (requireSync && !provider.CanLoadSync)
            {
                throw new NotSupportedException(
                    $"Resource provider '{provider.Name}' does not support synchronous loading.");
            }

            return provider;
        }

        private void AddProviderInternal(IResourceProvider provider)
        {
            _providers.Add(provider);
            _providers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        private static string BuildKey<T>(string normalizedPath)
        {
            return $"{typeof(T).FullName}:{normalizedPath}";
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Resource path cannot be empty.", nameof(path));

            return path.Replace('\\', '/').Trim();
        }

        private sealed class ResourceEntry
        {
            public ResourceEntry(
                string key,
                string path,
                Type assetType,
                object asset,
                IResourceProvider provider,
                int refCount)
            {
                Key = key;
                Path = path;
                AssetType = assetType;
                Asset = asset;
                Provider = provider;
                RefCount = refCount;
            }

            public string Key { get; }
            public string Path { get; }
            public Type AssetType { get; }
            public object Asset { get; }
            public IResourceProvider Provider { get; }
            public int RefCount { get; set; }
        }
    }
}
