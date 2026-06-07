using System;
using System.Collections.Generic;
using System.Linq;

namespace GameFramework.Core.GameSystem
{
    /// <summary>
    /// 游戏主应用类，驱动游戏主循环、管理模块生命周期。
    /// 单例模式，全局唯一入口。
    /// </summary>
    public class GameApp
    {
        private static GameApp? s_instance;

        private readonly List<GameModule> _modules = new();
        private readonly ServiceLocator _services = new();
        private readonly GameStateMachine _stateMachine = new();
        private bool _isRunning;

        /// <summary>
        /// 全局单例
        /// </summary>
        public static GameApp Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new GameApp();
                }
                return s_instance;
            }
        }

        /// <summary>
        /// 服务定位器
        /// </summary>
        public ServiceLocator Services => _services;

        /// <summary>
        /// 状态机
        /// </summary>
        public GameStateMachine StateMachine => _stateMachine;

        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 上一帧的增量时间
        /// </summary>
        public float DeltaTime { get; private set; }

        /// <summary>
        /// 总运行时间（秒）
        /// </summary>
        public float TotalTime { get; private set; }

        /// <summary>
        /// 帧率
        /// </summary>
        public float FrameRate { get; private set; }

        private GameApp()
        {
        }

        /// <summary>
        /// 初始化应用
        /// </summary>
        public void Initialize()
        {
            _isRunning = true;
            TotalTime = 0f;
            DeltaTime = 0f;

            // 注册自身为服务
            _services.RegisterOrReplace(this);

            // 初始化所有已注册模块
            foreach (var module in _modules)
            {
                module.Initialize();
            }
        }

        /// <summary>
        /// 注册模块
        /// </summary>
        public T RegisterModule<T>(T module) where T : GameModule
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));

            _modules.Add(module);

            // 按优先级排序（大的在前）
            _modules.Sort((a, b) => b.UpdatePriority.CompareTo(a.UpdatePriority));

            // 如果已经在运行，立即初始化
            if (_isRunning)
            {
                module.Initialize();
            }

            // 同时注册到服务容器
            _services.RegisterOrReplace(module);

            return module;
        }

        /// <summary>
        /// 移除模块
        /// </summary>
        public void UnregisterModule<T>() where T : GameModule
        {
            var module = _modules.OfType<T>().FirstOrDefault();
            if (module != null)
            {
                module.Shutdown();
                _modules.Remove(module);
            }
        }

        /// <summary>
        /// 获取已注册的模块
        /// </summary>
        public T? GetModule<T>() where T : GameModule
        {
            return _modules.OfType<T>().FirstOrDefault();
        }

        /// <summary>
        /// 每帧调用一次 —— 驱动游戏逻辑更新
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_isRunning)
                return;

            DeltaTime = deltaTime;
            TotalTime += deltaTime;

            // 帧率计算
            FrameRate = deltaTime > 0 ? 1f / deltaTime : 0f;

            // 更新所有模块
            foreach (var module in _modules)
            {
                module.Update(deltaTime);
            }

            // 更新状态机
            _stateMachine.Update(deltaTime);
        }

        /// <summary>
        /// 关闭应用
        /// </summary>
        public void Shutdown()
        {
            if (!_isRunning)
                return;

            _isRunning = false;

            // 逆序关闭模块
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                _modules[i].Shutdown();
            }

            _stateMachine.Clear();
            _services.Clear();
        }
    }
}
