using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameFramework.Core.GameSystem;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameClient.Framework.Resource
{
    public sealed class ResourceManager : GameModule
    {
        private readonly Dictionary<string, ResourceEntry> _cache = new();
        private readonly Dictionary<string, Task<ResourceEntry>> _loadingTasks = new();
        private IResourceProvider[] _providers = Array.Empty<IResourceProvider>();
        private IReadOnlyList<IResourceProvider> _providerView =
            Array.AsReadOnly(Array.Empty<IResourceProvider>());

        public override string ModuleName => "ResourceManager";

        public IResourceProvider? Provider => _providers.Length > 0 ? _providers[0] : null;

        public IReadOnlyList<IResourceProvider> Providers => _providerView;

        public void SetProvider(IResourceProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            Clear();
            SetProviders(new[] { provider });
        }

        public void AddProvider(IResourceProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            if (_providers.Any(existing => existing.Name == provider.Name))
                throw new InvalidOperationException($"Resource provider '{provider.Name}' is already registered.");

            SetProviders(_providers
                .Concat(new[] { provider })
                .OrderByDescending(existing => existing.Priority)
                .ToArray());
        }

        public bool RemoveProvider(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Provider name cannot be empty.", nameof(name));

            var provider = _providers.FirstOrDefault(existing => existing.Name == name);
            if (provider == null)
                return false;

            if (_cache.Values.Any(entry => ReferenceEquals(entry.Provider, provider)))
            {
                throw new InvalidOperationException(
                    $"Resource provider '{name}' cannot be removed while it has loaded resources.");
            }

            SetProviders(_providers.Where(existing => !ReferenceEquals(existing, provider)).ToArray());
            return true;
        }

        public T Load<T>(string path) where T : Object
        {
            return LoadHandle<T>(path).Asset;
        }

        public async Task<T> LoadAsync<T>(
            string path,
            CancellationToken cancellationToken = default) where T : Object
        {
            var handle = await LoadHandleAsync<T>(path, cancellationToken);
            return handle.Asset;
        }

        public ResourceHandle<T> LoadHandle<T>(string path) where T : Object
        {
            var normalizedPath = NormalizePath(path);
            var key = BuildKey<T>(normalizedPath);

            if (_cache.TryGetValue(key, out var cachedEntry))
            {
                cachedEntry.RefCount++;
                return CreateHandle<T>(cachedEntry);
            }

            var provider = FindProvider(normalizedPath, typeof(T), requireSync: true);
            if (!provider.CanLoadSync)
                throw new NotSupportedException(
                    $"Resource provider '{provider.Name}' does not support synchronous loading.");

            try
            {
                var result = provider.Load<T>(normalizedPath);
                var entry = new ResourceEntry(
                    key,
                    normalizedPath,
                    typeof(T),
                    result.Asset,
                    provider,
                    result.ReleaseToken,
                    1);

                _cache[key] = entry;
                return CreateHandle<T>(entry);
            }
            catch (Exception ex) when (ex is not ResourceLoadException)
            {
                throw new ResourceLoadException($"Failed to load resource '{normalizedPath}'.", ex);
            }
        }

        public async Task<ResourceHandle<T>> LoadHandleAsync<T>(
            string path,
            CancellationToken cancellationToken = default) where T : Object
        {
            var normalizedPath = NormalizePath(path);
            var key = BuildKey<T>(normalizedPath);

            if (_cache.TryGetValue(key, out var cachedEntry))
            {
                cachedEntry.RefCount++;
                return CreateHandle<T>(cachedEntry);
            }

            var loadingTask = GetOrCreateLoadTask<T>(key, normalizedPath, cancellationToken);
            var entry = await loadingTask;

            entry.RefCount++;
            return CreateHandle<T>(entry);
        }

        public void Retain<T>(string path) where T : Object
        {
            var entry = GetCachedEntry<T>(NormalizePath(path));
            entry.RefCount++;
        }

        public void Release<T>(string path) where T : Object
        {
            Release(GetCachedEntry<T>(NormalizePath(path)));
        }

        public void UnloadUnused()
        {
            var unusedEntries = _cache.Values.Where(entry => entry.RefCount <= 0).ToArray();
            foreach (var entry in unusedEntries)
                UnloadEntry(entry);
        }

        public void Clear()
        {
            var entries = _cache.Values.ToArray();
            _cache.Clear();
            _loadingTasks.Clear();

            foreach (var entry in entries)
                entry.Provider.Unload(entry.Path, entry.Asset, entry.ReleaseToken);
        }

        public bool IsLoaded<T>(string path) where T : Object
        {
            var key = BuildKey<T>(NormalizePath(path));
            return _cache.ContainsKey(key);
        }

        protected override void OnShutdown()
        {
            Clear();
        }

        private Task<ResourceEntry> GetOrCreateLoadTask<T>(
            string key,
            string normalizedPath,
            CancellationToken cancellationToken) where T : Object
        {
            if (_loadingTasks.TryGetValue(key, out var existingTask))
                return existingTask;

            var task = LoadEntryAsync<T>(key, normalizedPath, cancellationToken);
            _loadingTasks[key] = task;
            return task;
        }

        private async Task<ResourceEntry> LoadEntryAsync<T>(
            string key,
            string normalizedPath,
            CancellationToken cancellationToken) where T : Object
        {
            var provider = FindProvider(normalizedPath, typeof(T), requireSync: false);

            try
            {
                var result = await provider.LoadAsync<T>(normalizedPath, cancellationToken);

                if (_cache.TryGetValue(key, out var cachedEntry))
                {
                    provider.Unload(normalizedPath, result.Asset, result.ReleaseToken);
                    return cachedEntry;
                }

                var entry = new ResourceEntry(
                    key,
                    normalizedPath,
                    typeof(T),
                    result.Asset,
                    provider,
                    result.ReleaseToken,
                    0);

                _cache[key] = entry;
                return entry;
            }
            catch (Exception ex) when (ex is not ResourceLoadException)
            {
                throw new ResourceLoadException($"Failed to load resource '{normalizedPath}'.", ex);
            }
            finally
            {
                _loadingTasks.Remove(key);
            }
        }

        private ResourceHandle<T> CreateHandle<T>(ResourceEntry entry) where T : Object
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
            return _cache.TryGetValue(key, out var entry) ? entry.RefCount : 0;
        }

        private ResourceEntry GetCachedEntry<T>(string normalizedPath) where T : Object
        {
            var key = BuildKey<T>(normalizedPath);
            if (_cache.TryGetValue(key, out var entry))
                return entry;

            throw new ResourceLoadException($"Resource is not loaded: {normalizedPath}");
        }

        private void Release(ResourceEntry entry)
        {
            if (!_cache.TryGetValue(entry.Key, out var cachedEntry))
                return;

            cachedEntry.RefCount--;
            if (cachedEntry.RefCount <= 0)
                UnloadEntry(entry);
        }

        private void UnloadEntry(ResourceEntry entry)
        {
            if (!_cache.TryGetValue(entry.Key, out var cachedEntry) || !ReferenceEquals(cachedEntry, entry))
                return;

            _cache.Remove(entry.Key);
            entry.Provider.Unload(entry.Path, entry.Asset, entry.ReleaseToken);
        }

        private IResourceProvider FindProvider(string normalizedPath, Type assetType, bool requireSync)
        {
            var provider = _providers.FirstOrDefault(candidate => candidate.CanLoad(normalizedPath, assetType));
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

        private void SetProviders(IResourceProvider[] providers)
        {
            _providers = providers;
            _providerView = Array.AsReadOnly(providers);
        }

        private static string BuildKey<T>(string normalizedPath) where T : Object
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
                Object asset,
                IResourceProvider provider,
                object? releaseToken,
                int refCount)
            {
                Key = key;
                Path = path;
                AssetType = assetType;
                Asset = asset;
                Provider = provider;
                ReleaseToken = releaseToken;
                RefCount = refCount;
            }

            public string Key { get; }
            public string Path { get; }
            public Type AssetType { get; }
            public Object Asset { get; }
            public IResourceProvider Provider { get; }
            public object? ReleaseToken { get; }
            public int RefCount { get; set; }
        }
    }
}
