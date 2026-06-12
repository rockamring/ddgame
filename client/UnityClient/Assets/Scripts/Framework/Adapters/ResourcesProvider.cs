using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameClient.Framework.Resource
{
    public sealed class ResourcesProvider : IResourceProvider
    {
        private const string ResourceScheme = "res://";

        public string Name => "Resources";

        public int Priority => -100;

        public bool CanLoadSync => true;

        public bool CanLoad(string path, Type assetType)
        {
            if (string.IsNullOrWhiteSpace(path) || assetType == null)
                return false;

            if (!typeof(Object).IsAssignableFrom(assetType))
                return false;

            return !HasUnsupportedScheme(path);
        }

        public bool Exists(string path, Type assetType)
        {
            if (!CanLoad(path, assetType))
                return false;

            var asset = Resources.Load(NormalizePath(path), assetType);
            return asset != null;
        }

        public ResourceLoadResult<T> Load<T>(string path) where T : Object
        {
            var assetType = typeof(T);
            if (!CanLoad(path, assetType))
                throw new ResourceLoadException($"Unity Resources cannot load '{path}' as '{assetType.FullName}'.");

            var asset = Resources.Load(NormalizePath(path), assetType);
            if (asset == null)
                throw new ResourceLoadException($"Unity Resources asset was not found: {path}");

            var typedAsset = asset as T
                ?? throw new ResourceLoadException($"Unity Resources asset '{path}' is not '{assetType.FullName}'.");

            return new ResourceLoadResult<T>(typedAsset);
        }

        public async Task<ResourceLoadResult<T>> LoadAsync<T>(
            string path,
            CancellationToken cancellationToken = default) where T : Object
        {
            var assetType = typeof(T);
            if (!CanLoad(path, assetType))
                throw new ResourceLoadException($"Unity Resources cannot load '{path}' as '{assetType.FullName}'.");

            var request = Resources.LoadAsync(NormalizePath(path), assetType);
            while (!request.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (request.asset == null)
                throw new ResourceLoadException($"Unity Resources asset was not found: {path}");

            var typedAsset = request.asset as T
                ?? throw new ResourceLoadException($"Unity Resources asset '{path}' is not '{assetType.FullName}'.");

            return new ResourceLoadResult<T>(typedAsset);
        }

        public void Unload(string path, Object asset, object? releaseToken)
        {
            if (asset == null)
                return;

            if (asset is GameObject)
                return;

            Resources.UnloadAsset(asset);
        }

        private static bool HasUnsupportedScheme(string path)
        {
            return path.Contains("://") && !path.StartsWith(ResourceScheme, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            var normalized = path.Replace('\\', '/').Trim();
            if (normalized.StartsWith(ResourceScheme, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(ResourceScheme.Length);
            return normalized;
        }
    }
}
