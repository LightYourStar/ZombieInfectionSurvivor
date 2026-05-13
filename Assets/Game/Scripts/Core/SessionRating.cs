namespace Game.Core
{
    /// <summary>
    /// 单局评级枚举，根据最终感染人数与阈值配置计算得出
    /// </summary>
    public enum SessionRating
    {
        /// <summary>未达标（InfectedCount 小于 TargetInfectedCount）</summary>
        C,
        /// <summary>达标（>= Target, 小于 AScore）</summary>
        B,
        /// <summary>优秀（>= AScore, 小于 SScore）</summary>
        A,
        /// <summary>卓越（>= SScore, 小于 SSScore）</summary>
        S,
        /// <summary>满分（>= SSScore）</summary>
        SS
    }
}
