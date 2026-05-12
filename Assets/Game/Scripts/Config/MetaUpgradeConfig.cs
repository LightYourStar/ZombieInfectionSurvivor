using System;
using UnityEngine;

namespace Game.Config
{
    /// <summary>
    /// 局外永久升级配置，定义每条升级轨道的每级花费与累计加成数值
    /// 对应 4 种升级项：基础移动速度、基础感染半径、僵尸同伴上限、经验倍率
    /// </summary>
    [CreateAssetMenu(fileName = "MetaUpgradeConfig", menuName = "Game/MetaUpgradeConfig")]
    public class MetaUpgradeConfig : ScriptableObject
    {
        /// <summary>
        /// 单条升级轨道的数值配置
        /// </summary>
        [Serializable]
        public class UpgradeTrack
        {
            [Tooltip("每级升级花费的金币数组，Costs[i] 表示从第 i 级升到第 i+1 级所需金币")]
            [SerializeField] private int[] m_costs = Array.Empty<int>();

            [Tooltip("每级累计加成值数组，BonusPerLevel[i] 表示达到第 i 级时的累计加成；BonusPerLevel[0] 通常为 0")]
            [SerializeField] private float[] m_bonusPerLevel = Array.Empty<float>();

            /// <summary>
            /// 该轨道允许的最大等级（由 BonusPerLevel 长度决定，等于 Length - 1）
            /// </summary>
            public int MaxLevel => Mathf.Max(0, m_bonusPerLevel.Length - 1);

            /// <summary>
            /// 获取从指定等级升级到下一级所需金币，超出范围返回 -1 表示无法继续升级
            /// </summary>
            public int GetCost(int currentLevel)
            {
                if (currentLevel < 0 || currentLevel >= m_costs.Length)
                {
                    return -1;
                }
                return m_costs[currentLevel];
            }

            /// <summary>
            /// 获取指定等级的累计加成数值，越界时钳制到有效范围
            /// </summary>
            public float GetBonus(int level)
            {
                if (m_bonusPerLevel.Length == 0)
                {
                    return 0f;
                }
                int clampedLevel = Mathf.Clamp(level, 0, m_bonusPerLevel.Length - 1);
                return m_bonusPerLevel[clampedLevel];
            }
        }

        [Header("基础移动速度升级轨道")]
        [SerializeField] private UpgradeTrack m_moveSpeedTrack = new UpgradeTrack();

        [Header("基础感染半径升级轨道")]
        [SerializeField] private UpgradeTrack m_infectionRadiusTrack = new UpgradeTrack();

        [Header("僵尸同伴上限升级轨道")]
        [SerializeField] private UpgradeTrack m_zombieCapTrack = new UpgradeTrack();

        [Header("经验倍率升级轨道")]
        [SerializeField] private UpgradeTrack m_expMultiplierTrack = new UpgradeTrack();

        /// <summary>
        /// 基础移动速度升级轨道配置
        /// </summary>
        public UpgradeTrack MoveSpeedTrack => m_moveSpeedTrack;

        /// <summary>
        /// 基础感染半径升级轨道配置
        /// </summary>
        public UpgradeTrack InfectionRadiusTrack => m_infectionRadiusTrack;

        /// <summary>
        /// 僵尸同伴上限升级轨道配置
        /// </summary>
        public UpgradeTrack ZombieCapTrack => m_zombieCapTrack;

        /// <summary>
        /// 经验倍率升级轨道配置
        /// </summary>
        public UpgradeTrack ExpMultiplierTrack => m_expMultiplierTrack;
    }
}
