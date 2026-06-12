using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameClient.Framework.Resource
{
    public sealed class ResourceHandle<T> : IDisposable where T : Object
    {
        private readonly ResourceManager _manager;
        private readonly Func<int> _getRefCount;
        private bool _disposed;

        internal ResourceHandle(
            ResourceManager manager,
            string path,
            T asset,
            Func<int> getRefCount)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Asset = asset != null ? asset : throw new ArgumentNullException(nameof(asset));
            _getRefCount = getRefCount ?? throw new ArgumentNullException(nameof(getRefCount));
        }

        public string Path { get; }

        public T Asset { get; }

        public int RefCount => _disposed ? 0 : _getRefCount();

        public bool IsLoaded => !_disposed && _manager.IsLoaded<T>(Path);

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _manager.Release<T>(Path);
        }
    }
}
