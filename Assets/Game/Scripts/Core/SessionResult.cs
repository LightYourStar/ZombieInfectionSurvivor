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

        /// <summary>本局最高连击数</summary>
        public int MaxCombo { get; }

        /// <summary>狂潮阶段新增感染数（Final Frenzy 期间感染的人数）</summary>
        public int FrenzyInfectedCount { get; }

        /// <summary>主角直接感染数</summary>
        public int PlayerDirectInfectedCount { get; }

        /// <summary>僵尸同伴感染数</summary>
        public int ZombieInfectedCount { get; }

        /// <summary>爆发感染数（包含普通爆发与回响爆发）</summary>
        public int BurstInfectedCount { get; }

        /// <summary>最终评级</summary>
        public SessionRating Rating { get; }

        /// <summary>是否通关（InfectedCount >= TargetInfectedCount）</summary>
        public bool IsVictory { get; }

        /// <summary>实际游戏时长（秒）</summary>
        public float ElapsedTime { get; }

        /// <summary>本局升级摘要文本</summary>
        public string UpgradeSummary { get; }

        public SessionResult(int infectedCount, int maxZombieCount, int maxCombo,
            int frenzyInfectedCount, SessionRating rating, bool isVictory,
            float elapsedTime, string upgradeSummary,
            int playerDirectInfectedCount = 0,
            int zombieInfectedCount = 0,
            int burstInfectedCount = 0)
        {
            InfectedCount = infectedCount;
            MaxZombieCount = maxZombieCount;
            MaxCombo = maxCombo;
            FrenzyInfectedCount = frenzyInfectedCount;
            PlayerDirectInfectedCount = playerDirectInfectedCount;
            ZombieInfectedCount = zombieInfectedCount;
            BurstInfectedCount = burstInfectedCount;
            Rating = rating;
            IsVictory = isVictory;
            ElapsedTime = elapsedTime;
            UpgradeSummary = upgradeSummary ?? "无";
        }
    }
}
