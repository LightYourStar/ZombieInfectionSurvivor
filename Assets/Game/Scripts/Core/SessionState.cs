namespace Game.Core
{
    /// <summary>
    /// 单局状态枚举，表示一局游戏的生命周期阶段
    /// </summary>
    public enum SessionState
    {
        /// <summary>准备阶段，等待开始</summary>
        Ready,
        /// <summary>游戏进行中</summary>
        Playing,
        /// <summary>结算阶段</summary>
        Result
    }
}
