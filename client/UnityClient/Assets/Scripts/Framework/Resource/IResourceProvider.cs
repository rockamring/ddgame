using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameClient.Framework.Resource
{
    public interface IResourceProvider
    {
        string Name { get; }

        int Priority { get; }

        bool CanLoadSync { get; }

        bool CanLoad(string path, Type assetType);

        bool Exists(string path, Type assetType);

        ResourceLoadResult<T> Load<T>(string path) where T : Object;

        Task<ResourceLoadResult<T>> LoadAsync<T>(
            string path,
            CancellationToken cancellationToken = default) where T : Object;

        void Unload(string path, Object asset, object? releaseToken);
    }
}
