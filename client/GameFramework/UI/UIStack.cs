using System.Collections.Generic;

namespace GameFramework.UI
{
    /// <summary>
    /// UI窗口导航栈，管理窗口的推入/弹出导航。
    /// 支持回退到前一个窗口。
    /// </summary>
    public class UIStack
    {
        private readonly LinkedList<UIWindow> _stack = new();

        /// <summary>
        /// 栈中窗口数量
        /// </summary>
        public int Count => _stack.Count;

        /// <summary>
        /// 栈顶窗口（当前显示的窗口）
        /// </summary>
        public UIWindow? Top => _stack.Last?.Value;

        /// <summary>
        /// 推入窗口到栈顶
        /// </summary>
        public void Push(UIWindow window)
        {
            // 失活当前栈顶
            if (_stack.Last != null && _stack.Last.Value.IsOpen)
            {
                _stack.Last.Value.SetActive(false);
            }

            _stack.AddLast(window);

            if (!window.IsOpen)
            {
                window.Open();
            }
            window.SetActive(true);
        }

        /// <summary>
        /// 弹出栈顶窗口
        /// </summary>
        public void Pop()
        {
            if (_stack.Count == 0)
                return;

            var top = _stack.Last!.Value;
            top.Close();
            _stack.RemoveLast();

            // 恢复前一个窗口
            if (_stack.Last != null)
            {
                var previous = _stack.Last.Value;
                if (previous.IsOpen)
                {
                    previous.SetActive(true);
                }
                else
                {
                    previous.Open();
                }
            }
        }

        /// <summary>
        /// 弹出直到指定窗口成为栈顶
        /// </summary>
        public void PopTo(UIWindow window)
        {
            while (_stack.Count > 0 && Top != window)
            {
                Pop();
            }
        }

        /// <summary>
        /// 弹出所有窗口
        /// </summary>
        public void PopAll()
        {
            while (_stack.Count > 0)
            {
                Pop();
            }
        }

        /// <summary>
        /// 判断栈中是否包含某窗口
        /// </summary>
        public bool Contains(UIWindow window)
        {
            return _stack.Contains(window);
        }

        /// <summary>
        /// 清空栈（不关闭窗口）
        /// </summary>
        public void Clear()
        {
            _stack.Clear();
        }
    }
}
