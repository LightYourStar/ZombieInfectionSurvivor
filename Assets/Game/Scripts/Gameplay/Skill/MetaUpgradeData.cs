using System;

namespace Game.Gameplay.Skill
{
    /// <summary>
    /// 局外永久升级的运行时数据结构，使用纯 POCO，便于在 PlayerPrefs JSON 持久化与系统间传递。
    /// 字段集合与 <see cref="Game.Config.MetaUpgradeConfig"/> 的四条升级轨道一一对应，
    /// 再加上累计金币总量，用于结算时累加。
    /// </summary>
    /// <remarks>
    /// 本类在任务 4.3 阶段先行声明，仅承载数据本身；
    /// 将等级映射到具体加成数值的职责由任务 9.7 的 MetaUpgradeSystem 负责完成，
    /// 届时会结合 <see cref="Game.Config.MetaUpgradeConfig"/> 查表得到最终加成。
    /// </remarks>
    [Serializable]
    public class MetaUpgradeData
    {
        /// <summary>玩家累计持有的金币总量，用于购买局外升级</summary>
        public int TotalGold;

        /// <summary>基础移动速度升级等级</summary>
        public int SpeedLevel;

        /// <summary>基础感染半径升级等级</summary>
        public int InfectionRadiusLevel;

        /// <summary>僵尸同伴数量上限升级等级</summary>
        public int ZombieCapLevel;

        /// <summary>经验倍率升级等级</summary>
        public int ExpMultiplierLevel;
    }
}
