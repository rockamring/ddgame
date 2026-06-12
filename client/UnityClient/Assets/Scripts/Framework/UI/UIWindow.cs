using System;

namespace GameFramework.UI
{
    /// <summary>
    /// UI窗口基类，所有UI窗口需继承此类。
    /// 生命周期：OnInit → OnOpen → OnClose → OnDestroy
    /// </summary>
    public abstract class UIWindow
    {
        private bool _isInitialized;

        /// <summary>
        /// 窗口名称
        /// </summary>
        public virtual string WindowName => GetType().Name;

        /// <summary>
        /// 窗口所在层级
        /// </summary>
        public abstract UILayer Layer { get; }

        /// <summary>
        /// 窗口是否已打开（可见）
        /// </summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// 窗口是否活跃（在前台）
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// 初始化窗口（仅执行一次）
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;

            _isInitialized = true;
            OnInit();
        }

        /// <summary>
        /// 打开窗口
        /// </summary>
        public void Open()
        {
            if (!_isInitialized)
                Initialize();

            if (IsOpen)
            {
                // 如果已打开，刷新
                Refresh();
                return;
            }

            IsOpen = true;
            IsActive = true;
            OnOpen();
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            IsActive = false;
            OnClose();
        }

        /// <summary>
        /// 刷新窗口内容
        /// </summary>
        public void Refresh()
        {
            if (IsOpen)
            {
                OnRefresh();
            }
        }

        /// <summary>
        /// 设置活跃状态（显示/隐藏）
        /// </summary>
        public void SetActive(bool active)
        {
            if (IsActive == active)
                return;

            IsActive = active;
            if (IsOpen)
            {
                if (active)
                    OnActivate();
                else
                    OnDeactivate();
            }
        }

        /// <summary>
        /// 销毁窗口（释放资源）
        /// </summary>
        public void Destroy()
        {
            if (IsOpen)
                Close();

            OnDestroy();
            _isInitialized = false;
        }

        // ---- 生命周期钩子 ----

        /// <summary>初始化时调用（仅一次）</summary>
        protected virtual void OnInit() { }

        /// <summary>打开时调用</summary>
        protected virtual void OnOpen() { }

        /// <summary>关闭时调用</summary>
        protected virtual void OnClose() { }

        /// <summary>刷新时调用</summary>
        protected virtual void OnRefresh() { }

        /// <summary>激活时调用</summary>
        protected virtual void OnActivate() { }

        /// <summary>失活时调用</summary>
        protected virtual void OnDeactivate() { }

        /// <summary>销毁时调用</summary>
        protected virtual void OnDestroy() { }
    }
}
