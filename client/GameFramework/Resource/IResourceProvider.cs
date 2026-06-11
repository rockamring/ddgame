using System;
using System.Threading;
using System.Threading.Tasks;

namespace GameFramework.Resource
{
    /// <summary>
    /// Backend interface for loading assets. Unity-specific implementations should live outside GameFramework.
    /// </summary>
    public interface IResourceProvider
    {
        string Name { get; }

        int Priority { get; }

        bool CanLoadSync { get; }

        bool CanLoad(string path, Type assetType);

        bool Exists(string path, Type assetType);

        T Load<T>(string path) where T : class;

        Task<T> LoadAsync<T>(string path, CancellationToken cancellationToken = default) where T : class;

        void Unload(string path, object asset);
    }
}
