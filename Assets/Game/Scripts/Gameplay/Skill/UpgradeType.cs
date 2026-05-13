namespace Game.Gameplay.Skill
{
    /// <summary>
    /// 升级项枚举，覆盖局内升级和局外永久升级两个维度使用的升级类型。
    /// </summary>
    public enum UpgradeType
    {
        /// <summary>感染半径加成</summary>
        InfectionRadius,

        /// <summary>玩家移动速度加成</summary>
        MoveSpeed,

        /// <summary>僵尸同伴数量上限加成</summary>
        ZombieCompanionCap,

        /// <summary>经验获取倍率加成</summary>
        ExpMultiplier,

        // ==================== 2.0 新增升级类型 ====================

        /// <summary>连锁强化：感染爆发额外目标 +1</summary>
        ChainPlusOne,

        /// <summary>扩散毒圈：感染爆发半径 +20%</summary>
        BurstRadiusUp,

        /// <summary>新生狂奔：新生僵尸冲刺时间 +0.35 秒</summary>
        NewbornRushDurationUp,

        /// <summary>尸群嗅觉：僵尸感知范围 +25%</summary>
        ZombiePerceptionUp,

        /// <summary>狂潮提前：最终狂潮提前 10 秒开始</summary>
        FinalFrenzyEarly,

        /// <summary>回响爆发：每第 5 次感染额外触发一次小范围爆发</summary>
        EchoBurst
    }
}
