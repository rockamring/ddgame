using System;
using System.Collections.Generic;

namespace GameFramework.Core
{
    /// <summary>
    /// 服务定位器，提供服务的注册与获取。
    /// 游戏各系统通过此容器解耦。
    /// </summary>
    public class ServiceLocator
    {
        private readonly Dictionary<Type, object> _services = new();

        /// <summary>
        /// 注册一个服务实例
        /// </summary>
        public void Register<T>(T service) where T : class
        {
            var type = typeof(T);
            if (_services.ContainsKey(type))
            {
                throw new InvalidOperationException($"Service of type {type.Name} is already registered.");
            }
            _services[type] = service;
        }

        /// <summary>
        /// 注册或替换一个服务实例
        /// </summary>
        public void RegisterOrReplace<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
        }

        /// <summary>
        /// 获取指定类型的服务实例
        /// </summary>
        public T Get<T>() where T : class
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var service))
            {
                return (T)service;
            }
            throw new InvalidOperationException($"Service of type {type.Name} is not registered.");
        }

        /// <summary>
        /// 尝试获取服务实例
        /// </summary>
        public bool TryGet<T>(out T? service) where T : class
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var obj))
            {
                service = (T)obj;
                return true;
            }
            service = null;
            return false;
        }

        /// <summary>
        /// 检查服务是否已注册
        /// </summary>
        public bool IsRegistered<T>() where T : class
        {
            return _services.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 移除一个服务
        /// </summary>
        public void Unregister<T>() where T : class
        {
            _services.Remove(typeof(T));
        }

        /// <summary>
        /// 清空所有服务
        /// </summary>
        public void Clear()
        {
            _services.Clear();
        }
    }
}
