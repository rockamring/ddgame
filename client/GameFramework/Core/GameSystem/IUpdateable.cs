namespace GameFramework.Core.GameSystem
{
    /// <summary>
    /// 可更新接口，所有需要每帧驱动的对象实现此接口
    /// </summary>
    public interface IUpdateable
    {
        /// <summary>
        /// 帧更新
        /// </summary>
        /// <param name="deltaTime">距上一帧的时间间隔（秒）</param>
        void Update(float deltaTime);

        /// <summary>
        /// 更新优先级（越大越先更新）
        /// </summary>
        int UpdatePriority => 0;

        /// <summary>
        /// 是否启用更新
        /// </summary>
        bool Enabled => true;
    }
}
