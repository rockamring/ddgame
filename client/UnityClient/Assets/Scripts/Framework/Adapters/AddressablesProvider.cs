using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace GameClient.Framework.Resource
{
    public sealed class AddressablesProvider : IResourceProvider
    {
        private const string AddressablesScheme = "addressables://";
        private const string ShortScheme = "addr://";

        private readonly bool _loadUnschemedKeys;

        public AddressablesProvider(bool loadUnschemedKeys = false)
        {
            _loadUnschemedKeys = loadUnschemedKeys;
        }

        public string Name => "Addressables";

        public int Priority => 100;

        public bool CanLoadSync => false;

        public bool CanLoad(string path, Type assetType)
        {
            if (string.IsNullOrWhiteSpace(path) || assetType == null)
                return false;

            if (!typeof(Object).IsAssignableFrom(assetType))
                return false;

            if (HasAddressablesScheme(path))
                return true;

            return _loadUnschemedKeys && !HasUnsupportedScheme(path);
        }

        public bool Exists(string path, Type assetType)
        {
            if (!CanLoad(path, assetType))
                return false;

            var key = NormalizeKey(path);
            foreach (var locator in Addressables.ResourceLocators)
            {
                if (locator.Locate(key, assetType, out var locations) && locations.Count > 0)
                    return true;
            }

            return false;
        }

        public ResourceLoadResult<T> Load<T>(string path) where T : Object
        {
            throw new NotSupportedException("AddressablesProvider only supports asynchronous loading.");
        }

        public async Task<ResourceLoadResult<T>> LoadAsync<T>(
            string path,
            CancellationToken cancellationToken = default) where T : Object
        {
            var assetType = typeof(T);
            if (!CanLoad(path, assetType))
                throw new ResourceLoadException($"Addressables cannot load '{path}' as '{assetType.FullName}'.");

            var key = NormalizeKey(path);
            var handle = Addressables.LoadAssetAsync<T>(key);

            try
            {
                while (!handle.IsDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                cancellationToken.ThrowIfCancellationRequested();

                var asset = handle.Result;
                if (handle.Status != AsyncOperationStatus.Succeeded || asset == null)
                {
                    throw new ResourceLoadException($"Addressables asset was not found: {key}");
                }

                return new ResourceLoadResult<T>(asset, handle);
            }
            catch
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
                throw;
            }
        }

        public void Unload(string path, Object asset, object? releaseToken)
        {
            if (releaseToken is AsyncOperationHandle handle && handle.IsValid())
            {
                Addressables.Release(handle);
                return;
            }

            if (asset != null)
                Addressables.Release(asset);
        }

        private static bool HasAddressablesScheme(string path)
        {
            return path.StartsWith(AddressablesScheme, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(ShortScheme, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasUnsupportedScheme(string path)
        {
            return path.Contains("://") && !HasAddressablesScheme(path);
        }

        private static string NormalizeKey(string path)
        {
            var normalized = path.Replace('\\', '/').Trim();
            if (normalized.StartsWith(AddressablesScheme, StringComparison.OrdinalIgnoreCase))
                return normalized.Substring(AddressablesScheme.Length);
            if (normalized.StartsWith(ShortScheme, StringComparison.OrdinalIgnoreCase))
                return normalized.Substring(ShortScheme.Length);
            return normalized;
        }
    }
}
