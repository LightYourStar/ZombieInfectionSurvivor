namespace Game.Gameplay.Zombie
{
    /// <summary>
    /// 僵尸同伴类型枚举。
    /// 不同类型在移动速度、感知范围上有差异，影响尸群的整体追击和扩张效率。
    /// </summary>
    public enum ZombieType
    {
        /// <summary>普通僵尸：标准速度、标准感知范围，由 Civilian 转化而来</summary>
        Normal = 0,

        /// <summary>敏捷僵尸：速度更快、感知范围更大，由 Runner 转化而来</summary>
        Runner = 1,

        /// <summary>强壮僵尸：速度略慢、感染价值更高（感染半径更大），由 Guard 转化而来</summary>
        Brute = 2
    }
}
