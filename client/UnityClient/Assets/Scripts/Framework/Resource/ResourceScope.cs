using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameClient.Framework.Resource
{
    public sealed class ResourceScope : IDisposable
    {
        private readonly ResourceManager _manager;
        private readonly List<IDisposable> _handles = new();
        private bool _disposed;

        internal ResourceScope(ResourceManager manager, string? name)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            Name = string.IsNullOrWhiteSpace(name) ? "ResourceScope" : name;
        }

        public string Name { get; }

        public int Count => _handles.Count;

        [Obsolete("Use LoadHandle<T> so scoped ownership is visible at the call site.", true)]
        public T Load<T>(string path) where T : Object
        {
            throw new NotSupportedException("Use LoadHandle<T> instead.");
        }

        public ResourceHandle<T> LoadHandle<T>(string path) where T : Object
        {
            ThrowIfDisposed();

            var handle = _manager.LoadHandle<T>(path);
            _handles.Add(handle);
            return handle;
        }

        [Obsolete("Use LoadHandleAsync<T> so scoped ownership is visible at the call site.", true)]
        public async Task<T> LoadAsync<T>(
            string path,
            CancellationToken cancellationToken = default) where T : Object
        {
            await Task.Yield();
            throw new NotSupportedException("Use LoadHandleAsync<T> instead.");
        }

        public async Task<ResourceHandle<T>> LoadHandleAsync<T>(
            string path,
            CancellationToken cancellationToken = default) where T : Object
        {
            ThrowIfDisposed();

            var handle = await _manager.LoadHandleAsync<T>(path, cancellationToken);
            if (_disposed)
            {
                handle.Dispose();
                throw new ObjectDisposedException(Name);
            }

            _handles.Add(handle);
            return handle;
        }

        public void ReleaseAll()
        {
            for (var i = _handles.Count - 1; i >= 0; i--)
            {
                _handles[i].Dispose();
            }

            _handles.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            ReleaseAll();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(Name);
        }
    }
}
