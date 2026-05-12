namespace Game.Gameplay.Skill
{
    /// <summary>
    /// 升级项枚举，覆盖局内升级和局外永久升级两个维度使用的升级类型。
    /// 局内 <see cref="Game.Gameplay.Player.PlayerStats"/> 以此枚举作为 ApplyUpgrade 的路由键，
    /// 局外 MetaUpgrade 轨道（见 <see cref="Game.Config.MetaUpgradeConfig"/>）与该枚举一一对应。
    /// </summary>
    /// <remarks>
    /// 本枚举预先在此声明，便于任务 4.3 PlayerStats 先行引用；
    /// 任务 9.4 中 UpgradeOption 数据类会基于该枚举扩展局内升级选项定义。
    /// </remarks>
    public enum UpgradeType
    {
        /// <summary>感染半径加成</summary>
        InfectionRadius,

        /// <summary>玩家移动速度加成</summary>
        MoveSpeed,

        /// <summary>僵尸同伴数量上限加成</summary>
        ZombieCompanionCap,

        /// <summary>经验获取倍率加成</summary>
        ExpMultiplier
    }
}
