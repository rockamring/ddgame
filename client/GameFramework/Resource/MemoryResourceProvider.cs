using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GameFramework.Resource
{
    /// <summary>
    /// In-memory provider for runtime demos and tests.
    /// </summary>
    public class MemoryResourceProvider : IResourceProvider
    {
        private readonly Dictionary<string, object> _assets = new(StringComparer.OrdinalIgnoreCase);

        public MemoryResourceProvider(string name = "Memory", int priority = 0, bool canLoadSync = true)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Provider name cannot be empty.", nameof(name));

            Name = name;
            Priority = priority;
            CanLoadSync = canLoadSync;
        }

        public string Name { get; }

        public int Priority { get; }

        public bool CanLoadSync { get; }

        public void Add<T>(string path, T asset) where T : class
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Resource path cannot be empty.", nameof(path));
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            _assets[NormalizePath(path)] = asset;
        }

        public bool CanLoad(string path, Type assetType)
        {
            return Exists(path, assetType);
        }

        public bool Exists(string path, Type assetType)
        {
            if (string.IsNullOrWhiteSpace(path) || assetType == null)
                return false;

            return _assets.TryGetValue(NormalizePath(path), out var asset) && assetType.IsInstanceOfType(asset);
        }

        public T Load<T>(string path) where T : class
        {
            if (!CanLoadSync)
                throw new NotSupportedException("This provider does not support synchronous loading.");

            return GetAsset<T>(path);
        }

        public async Task<T> LoadAsync<T>(string path, CancellationToken cancellationToken = default) where T : class
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return GetAsset<T>(path);
        }

        public void Unload(string path, object asset)
        {
            // Memory assets are owned by the provider; unloading from ResourceManager only releases cache references.
        }

        private T GetAsset<T>(string path) where T : class
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ResourceLoadException("Resource path cannot be empty.");

            var normalizedPath = NormalizePath(path);
            if (!_assets.TryGetValue(normalizedPath, out var asset))
                throw new ResourceLoadException($"Resource was not found: {normalizedPath}");

            if (asset is T typedAsset)
                return typedAsset;

            throw new ResourceLoadException(
                $"Resource '{normalizedPath}' is '{asset.GetType().FullName}', not '{typeof(T).FullName}'.");
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').Trim();
        }
    }
}
