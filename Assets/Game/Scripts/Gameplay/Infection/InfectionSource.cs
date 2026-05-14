namespace Game.Gameplay.Infection
{
    /// <summary>
    /// 感染来源标记，用于 Debug 统计区分不同感染路径。
    /// 不影响感染逻辑本身，仅用于分析。
    /// </summary>
    public enum InfectionSource
    {
        /// <summary>玩家主角直接感染</summary>
        Player,

        /// <summary>僵尸同伴自动感染</summary>
        Zombie,

        /// <summary>普通感染爆发（Chain Infection Burst）</summary>
        InfectionBurst,

        /// <summary>回响爆发（EchoBurst）</summary>
        EchoBurst
    }
}
