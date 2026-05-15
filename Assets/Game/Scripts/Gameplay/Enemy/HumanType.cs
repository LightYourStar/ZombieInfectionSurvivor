namespace Game.Gameplay.Enemy
{
    /// <summary>
    /// 人类单位类型枚举。
    /// 不同类型在移动速度、感知半径、感染后转化的僵尸类型上有差异，
    /// 为玩家提供"先感染谁"的决策空间。
    /// </summary>
    public enum HumanType
    {
        /// <summary>普通人：基础扩张燃料，数量最多，感染后转化为 NormalZombie</summary>
        Civilian = 0,

        /// <summary>奔跑者：移动速度更快，感染后转化为 RunnerZombie，提升尸群追击能力</summary>
        Runner = 1,

        /// <summary>守卫：移动较慢但有短暂抗感染时间，感染后转化为 BruteZombie</summary>
        Guard = 2
    }
}
