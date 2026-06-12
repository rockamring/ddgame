using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameFramework.Core.GameSystem;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameClient.Framework.Resource
{
    [DisallowMultipleComponent]
    public sealed class ResourceBinder : MonoBehaviour
    {
        private const string ImageSpriteSlot = "Image.sprite";
        private const string SpriteRendererSpriteSlot = "SpriteRenderer.sprite";

        private readonly Dictionary<BindingKey, IDisposable> _bindings = new();
        private readonly Dictionary<BindingKey, int> _bindingVersions = new();
        private readonly MaterialPropertyBlock _propertyBlock = new();
        private int _nextBindingVersion;

        private ResourceManager? resourceManager;

        public ResourceManager ResourceManager
        {
            get
            {
                if (resourceManager != null)
                    return resourceManager;

                resourceManager = GameApp.Instance.GetModule<ResourceManager>();
                if (resourceManager == null)
                    throw new InvalidOperationException("ResourceManager is not registered.");

                return resourceManager;
            }

            set { resourceManager = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        public async Task SetImageSpriteAsync(
            Image image,
            string path,
            CancellationToken cancellationToken = default)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            var key = CreateKey(image, ImageSpriteSlot);
            var version = BeginBinding(key);
            var handle = await ResourceManager.LoadHandleAsync<Sprite>(path, cancellationToken);
            if (image == null || !IsBindingCurrent(key, version))
            {
                handle.Dispose();
                return;
            }

            image.sprite = handle.Asset;
            ReplaceBinding(key, version, handle);
        }

        public void ClearImageSprite(Image image)
        {
            if (image == null)
                return;

            var key = CreateKey(image, ImageSpriteSlot);
            BeginBinding(key);
            image.sprite = null;
            ClearBinding(key);
        }

        public async Task SetSpriteRendererSpriteAsync(
            SpriteRenderer spriteRenderer,
            string path,
            CancellationToken cancellationToken = default)
        {
            if (spriteRenderer == null)
                throw new ArgumentNullException(nameof(spriteRenderer));

            var key = CreateKey(spriteRenderer, SpriteRendererSpriteSlot);
            var version = BeginBinding(key);
            var handle = await ResourceManager.LoadHandleAsync<Sprite>(path, cancellationToken);
            if (spriteRenderer == null || !IsBindingCurrent(key, version))
            {
                handle.Dispose();
                return;
            }

            spriteRenderer.sprite = handle.Asset;
            ReplaceBinding(key, version, handle);
        }

        public void ClearSpriteRendererSprite(SpriteRenderer spriteRenderer)
        {
            if (spriteRenderer == null)
                return;

            var key = CreateKey(spriteRenderer, SpriteRendererSpriteSlot);
            BeginBinding(key);
            spriteRenderer.sprite = null;
            ClearBinding(key);
        }

        public Task SetMaterialTextureAsync(
            Material material,
            string propertyName,
            string path,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("Material texture property name cannot be empty.", nameof(propertyName));

            return SetMaterialTextureAsync(material, Shader.PropertyToID(propertyName), path, cancellationToken);
        }

        public async Task SetMaterialTextureAsync(
            Material material,
            int propertyId,
            string path,
            CancellationToken cancellationToken = default)
        {
            if (material == null)
                throw new ArgumentNullException(nameof(material));

            var key = CreateKey(material, BuildTextureSlot(propertyId));
            var version = BeginBinding(key);
            var handle = await ResourceManager.LoadHandleAsync<Texture>(path, cancellationToken);
            if (material == null || !IsBindingCurrent(key, version))
            {
                handle.Dispose();
                return;
            }

            material.SetTexture(propertyId, handle.Asset);
            ReplaceBinding(key, version, handle);
        }

        public void ClearMaterialTexture(Material material, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("Material texture property name cannot be empty.", nameof(propertyName));

            ClearMaterialTexture(material, Shader.PropertyToID(propertyName));
        }

        public void ClearMaterialTexture(Material material, int propertyId)
        {
            if (material == null)
                return;

            var key = CreateKey(material, BuildTextureSlot(propertyId));
            BeginBinding(key);
            material.SetTexture(propertyId, null);
            ClearBinding(key);
        }

        public Task SetRendererTextureAsync(
            Renderer renderer,
            string propertyName,
            string path,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("Renderer texture property name cannot be empty.", nameof(propertyName));

            return SetRendererTextureAsync(renderer, Shader.PropertyToID(propertyName), path, cancellationToken);
        }

        public async Task SetRendererTextureAsync(
            Renderer renderer,
            int propertyId,
            string path,
            CancellationToken cancellationToken = default)
        {
            if (renderer == null)
                throw new ArgumentNullException(nameof(renderer));

            var key = CreateKey(renderer, BuildTextureSlot(propertyId));
            var version = BeginBinding(key);
            var handle = await ResourceManager.LoadHandleAsync<Texture>(path, cancellationToken);
            if (renderer == null || !IsBindingCurrent(key, version))
            {
                handle.Dispose();
                return;
            }

            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetTexture(propertyId, handle.Asset);
            renderer.SetPropertyBlock(_propertyBlock);

            ReplaceBinding(key, version, handle);
        }

        public void ClearRendererTexture(Renderer renderer, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("Renderer texture property name cannot be empty.", nameof(propertyName));

            ClearRendererTexture(renderer, Shader.PropertyToID(propertyName));
        }

        public void ClearRendererTexture(Renderer renderer, int propertyId)
        {
            if (renderer == null)
                return;

            var key = CreateKey(renderer, BuildTextureSlot(propertyId));
            BeginBinding(key);
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetTexture(propertyId, null);
            renderer.SetPropertyBlock(_propertyBlock);
            ClearBinding(key);
        }

        public void ClearBinding(Object target, string slot)
        {
            if (target == null)
                return;

            var key = CreateKey(target, slot);
            BeginBinding(key);
            ClearBinding(key);
        }

        public void ClearTarget(Object target)
        {
            if (target == null)
                return;

            var targetId = target.GetInstanceID();
            var keys = new List<BindingKey>();
            foreach (var key in _bindings.Keys)
            {
                if (key.TargetId == targetId)
                    keys.Add(key);
            }

            foreach (var key in keys)
            {
                BeginBinding(key);
                ClearBinding(key);
            }
        }

        public void ClearAll()
        {
            _bindingVersions.Clear();
            foreach (var handle in _bindings.Values)
            {
                handle.Dispose();
            }

            _bindings.Clear();
        }

        private void OnDestroy()
        {
            ClearAll();
        }

        private int BeginBinding(BindingKey key)
        {
            var version = ++_nextBindingVersion;
            _bindingVersions[key] = version;
            return version;
        }

        private bool IsBindingCurrent(BindingKey key, int version)
        {
            return _bindingVersions.TryGetValue(key, out var currentVersion)
                && currentVersion == version;
        }

        private void ReplaceBinding(BindingKey key, int version, IDisposable handle)
        {
            if (!IsBindingCurrent(key, version))
            {
                handle.Dispose();
                return;
            }

            if (_bindings.TryGetValue(key, out var oldHandle))
                oldHandle.Dispose();

            _bindings[key] = handle;
        }

        private void ClearBinding(BindingKey key)
        {
            if (!_bindings.TryGetValue(key, out var handle))
                return;

            _bindings.Remove(key);
            handle.Dispose();
        }

        private static BindingKey CreateKey(Object target, string slot)
        {
            return new BindingKey(target.GetInstanceID(), slot);
        }

        private static string BuildTextureSlot(int propertyId)
        {
            return $"Texture:{propertyId}";
        }

        private readonly struct BindingKey : IEquatable<BindingKey>
        {
            public BindingKey(int targetId, string slot)
            {
                TargetId = targetId;
                Slot = slot;
            }

            public int TargetId { get; }

            public string Slot { get; }

            public bool Equals(BindingKey other)
            {
                return TargetId == other.TargetId && Slot == other.Slot;
            }

            public override bool Equals(object? obj)
            {
                return obj is BindingKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (TargetId * 397) ^ Slot.GetHashCode();
                }
            }
        }
    }
}
