using System;

namespace GameFramework.Core.GameSystem
{
    /// <summary>
    /// 游戏模块基类，所有游戏模块继承此类。
    /// 具有完整的生命周期管理。
    /// </summary>
    public abstract class GameModule : IUpdateable
    {
        /// <summary>
        /// 模块是否已初始化
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 模块名称（默认使用类名）
        /// </summary>
        public virtual string ModuleName => GetType().Name;

        /// <summary>
        /// 更新优先级
        /// </summary>
        public virtual int UpdatePriority => 0;

        /// <summary>
        /// 是否启用更新
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 初始化模块
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized)
                return;

            IsInitialized = true;
            OnInit();
        }

        /// <summary>
        /// 帧更新
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!Enabled || !IsInitialized)
                return;

            OnUpdate(deltaTime);
        }

        /// <summary>
        /// 关闭模块
        /// </summary>
        public void Shutdown()
        {
            if (!IsInitialized)
                return;

            IsInitialized = false;
            OnShutdown();
        }

        // ---- 子类可重写的生命周期 ----

        protected virtual void OnInit() { }
        protected virtual void OnUpdate(float deltaTime) { }
        protected virtual void OnShutdown() { }
    }
}
