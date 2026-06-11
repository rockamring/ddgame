using System;
using System.Collections.Generic;
using System.Linq;
using GameFramework.Core.GameSystem;

namespace GameFramework.UI
{
    /// <summary>
    /// UI管理器，管理所有窗口的打开、关闭、层级排序。
    /// 单例模式，通过 GameApp.Services 获取。
    /// </summary>
    public class UIManager : GameModule
    {
        private readonly Dictionary<Type, UIWindow> _windows = new();
        private readonly Dictionary<UILayer, UIStack> _layerStacks = new();
        private readonly Dictionary<string, Type> _windowTypes = new();

        private static readonly UILayer[] s_layers = Enum.GetValues(typeof(UILayer)).Cast<UILayer>().ToArray();

        public override string ModuleName => "UIManager";

        public UIManager()
        {
            foreach (var layer in s_layers)
            {
                _layerStacks[layer] = new UIStack();
            }
        }

        protected override void OnInit()
        {
            // 扫描并注册所有 UIWindow 子类
            RegisterAllWindows();
        }

        protected override void OnUpdate(float deltaTime)
        {
            // UI模块的更新逻辑（动画、布局等预留）
        }

        /// <summary>
        /// 打开一个窗口
        /// </summary>
        public T Open<T>(Action<T>? onPrepared = null) where T : UIWindow, new()
        {
            var windowType = typeof(T);

            // 从缓存获取或创建新窗口
            if (!_windows.TryGetValue(windowType, out var window))
            {
                window = CreateWindow<T>();
            }

            var typedWindow = (T)window;
            onPrepared?.Invoke(typedWindow);

            // 推入对应层级的栈
            var stack = _layerStacks[window.Layer];
            stack.Push(window);

            return typedWindow;
        }

        /// <summary>
        /// 关闭指定窗口
        /// </summary>
        public void Close<T>() where T : UIWindow
        {
            var windowType = typeof(T);
            if (!_windows.TryGetValue(windowType, out var window))
                return;

            var stack = _layerStacks[window.Layer];

            // 如果在栈中，通过栈弹出
            if (stack.Contains(window))
            {
                while (stack.Top != window && stack.Count > 0)
                {
                    // 关闭覆盖在上面的窗口
                    var top = stack.Top;
                    if (top != null)
                    {
                        top.Close();
                        stack.Pop();
                    }
                }
                if (stack.Top == window)
                {
                    stack.Pop();
                }
            }
            else
            {
                window.Close();
            }
        }

        /// <summary>
        /// 关闭所有窗口
        /// </summary>
        public void CloseAll()
        {
            foreach (var layer in s_layers)
            {
                _layerStacks[layer].PopAll();
            }
        }

        /// <summary>
        /// 获取已打开的窗口
        /// </summary>
        public T? Get<T>() where T : UIWindow
        {
            var windowType = typeof(T);
            if (_windows.TryGetValue(windowType, out var window))
            {
                return window as T;
            }
            return null;
        }

        /// <summary>
        /// 判断窗口是否已打开
        /// </summary>
        public bool IsOpen<T>() where T : UIWindow
        {
            var window = Get<T>();
            return window != null && window.IsOpen;
        }

        /// <summary>
        /// 销毁窗口（从缓存移除）
        /// </summary>
        public void Destroy<T>() where T : UIWindow
        {
            var windowType = typeof(T);
            if (!_windows.TryGetValue(windowType, out var window))
                return;

            window.Destroy();
            _windows.Remove(windowType);
        }

        /// <summary>
        /// 预创建窗口（初始化但暂不打开）
        /// </summary>
        public T Preload<T>() where T : UIWindow, new()
        {
            var windowType = typeof(T);
            if (_windows.ContainsKey(windowType))
                return (T)_windows[windowType];

            var window = new T();
            window.Initialize();
            _windows[windowType] = window;
            return window;
        }

        private T CreateWindow<T>() where T : UIWindow, new()
        {
            var window = new T();
            window.Initialize();
            _windows[typeof(T)] = window;
            return window;
        }

        private void RegisterAllWindows()
        {
            // 通过反射查找所有 UIWindow 子类
            // 这里由子类或外部在初始化时注册
        }

        /// <summary>
        /// 手动注册窗口类型（替代反射）
        /// </summary>
        public void RegisterWindowType<T>() where T : UIWindow
        {
            _windowTypes[typeof(T).Name] = typeof(T);
        }
    }
}
