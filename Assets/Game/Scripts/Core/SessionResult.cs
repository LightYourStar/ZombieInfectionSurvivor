namespace Game.Core
{
    /// <summary>
    /// 单局结算数据类，封装一局游戏结束后的所有统计信息
    /// </summary>
    public class SessionResult
    {
        /// <summary>本局累计感染人数</summary>
        public int InfectedCount { get; }

        /// <summary>本局同时存在的最大僵尸数量</summary>
        public int MaxZombieCount { get; }

        /// <summary>本局最高连锁感染数（预留字段）</summary>
        public int MaxChainCount { get; }

        /// <summary>最终评级</summary>
        public SessionRating Rating { get; }

        /// <summary>是否通关（InfectedCount >= TargetInfectedCount）</summary>
        public bool IsVictory { get; }

        /// <summary>实际游戏时长（秒）</summary>
        public float ElapsedTime { get; }

        public SessionResult(int infectedCount, int maxZombieCount, int maxChainCount,
            SessionRating rating, bool isVictory, float elapsedTime)
        {
            InfectedCount = infectedCount;
            MaxZombieCount = maxZombieCount;
            MaxChainCount = maxChainCount;
            Rating = rating;
            IsVictory = isVictory;
            ElapsedTime = elapsedTime;
        }
    }
}
