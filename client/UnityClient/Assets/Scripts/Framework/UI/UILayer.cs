namespace GameFramework.UI
{
    /// <summary>
    /// UI层级枚举，决定窗口的显示顺序。
    /// 层级越高，显示在越上层。
    /// </summary>
    public enum UILayer
    {
        /// <summary>底层（游戏场景UI）</summary>
        Bottom = 0,

        /// <summary>中层（功能窗口）</summary>
        Middle = 1,

        /// <summary>顶层（系统窗口）</summary>
        Top = 2,

        /// <summary>弹窗层（模态窗口）</summary>
        Popup = 3,

        /// <summary>系统层（Loading、错误提示等）</summary>
        System = 4,
    }
}
