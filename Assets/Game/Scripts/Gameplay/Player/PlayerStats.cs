using Game.Config;
using Game.Gameplay.Skill;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 玩家运行时属性，采用「基础值 + 加成值」模型。
    /// 基础值来自 <see cref="GameConfig"/> 与 <see cref="MetaUpgradeData"/>（局外永久升级），
    /// 加成值来自单局内通过 <see cref="ApplyUpgrade"/> 应用的升级效果，在 <see cref="Reset"/> 时清零。
    /// 本类为纯 C# 对象（非 MonoBehaviour），由 PlayerController 等 MonoBehaviour 在 Inspector 外部持有。
    /// </summary>
    /// <remarks>
    /// 读取约定：四个 Get 属性均返回「基础值 + 加成值」的当前合成值；
    /// <see cref="ZombieCompanionCap"/> 按 int 取整返回，兼容浮点数加成；
    /// <see cref="ExpMultiplier"/> 至少返回 0，避免出现负倍率。
    /// 局外升级等级到加成数值的映射由任务 9.7 的 MetaUpgradeSystem 完成，
    /// 当前实现直接读取 <see cref="MetaUpgradeData"/> 中已经解算好的等级字段并按 0 加成处理，
    /// 等 MetaUpgradeSystem 就绪后会替换为查表结果。
    /// </remarks>
    public class PlayerStats
    {
        // ==================== 基础值（由 Initialize 写入） ====================

        /// <summary>玩家基础移动速度 = GameConfig.PlayerBaseSpeed + 局外升级加成</summary>
        private float m_baseMoveSpeed;

        /// <summary>玩家基础感染半径 = GameConfig.PlayerBaseInfectionRadius + 局外升级加成</summary>
        private float m_baseInfectionRadius;

        /// <summary>僵尸同伴基础数量上限 = GameConfig.ZombieCompanionMaxCount + 局外升级加成</summary>
        private int m_baseZombieCompanionCap;

        /// <summary>基础经验倍率，默认 1（局外升级以加法方式叠加到其上）</summary>
        private float m_baseExpMultiplier = 1f;

        // ==================== 加成值（由 ApplyUpgrade 累加） ====================

        /// <summary>单局内累计的移动速度加成</summary>
        private float m_moveSpeedBonus;

        /// <summary>单局内累计的感染半径加成</summary>
        private float m_infectionRadiusBonus;

        /// <summary>单局内累计的僵尸同伴上限加成（允许浮点累加，读取时取整）</summary>
        private float m_zombieCompanionCapBonus;

        /// <summary>单局内累计的经验倍率加成</summary>
        private float m_expMultiplierBonus;

        // ==================== 对外属性 ====================

        /// <summary>当前移动速度 = 基础移动速度 + 局内累计加成</summary>
        public float MoveSpeed => m_baseMoveSpeed + m_moveSpeedBonus;

        /// <summary>当前感染半径 = 基础感染半径 + 局内累计加成</summary>
        public float InfectionRadius => m_baseInfectionRadius + m_infectionRadiusBonus;

        /// <summary>当前僵尸同伴数量上限（向下取整），= 基础上限 + 局内累计加成</summary>
        public int ZombieCompanionCap => m_baseZombieCompanionCap + Mathf.FloorToInt(m_zombieCompanionCapBonus);

        /// <summary>当前经验倍率，钳制为非负值</summary>
        public float ExpMultiplier => Mathf.Max(0f, m_baseExpMultiplier + m_expMultiplierBonus);

        // ==================== 初始化与重置 ====================

        /// <summary>
        /// 使用 <see cref="GameConfig"/> 的默认数值作为基础值，并叠加 <see cref="MetaUpgradeData"/> 中已解算的局外加成。
        /// 调用前内部加成（由 ApplyUpgrade 累加的部分）会被清零，确保 Initialize 后立即读取属性值即为「基础值 + 局外加成」。
        /// </summary>
        /// <param name="config">全局配置，提供各项基础数值</param>
        /// <param name="metaData">局外永久升级数据；可为 null，按等级 0（无加成）处理</param>
        public void Initialize(GameConfig config, MetaUpgradeData metaData)
        {
            if (config == null)
            {
                Debug.LogError("[PlayerStats] Initialize 收到空的 GameConfig，玩家属性将保持默认值 0");
                ResetBaseValuesToZero();
                ClearBonuses();
                return;
            }

            // 基础值来自 GameConfig
            m_baseMoveSpeed = config.PlayerBaseSpeed;
            m_baseInfectionRadius = config.PlayerBaseInfectionRadius;
            m_baseZombieCompanionCap = config.ZombieCompanionMaxCount;
            m_baseExpMultiplier = 1f;

            // 叠加局外永久升级加成
            if (metaData != null)
            {
                m_baseMoveSpeed += metaData.SpeedLevel * 0.1f;
                m_baseInfectionRadius += metaData.InfectionRadiusLevel * 0.05f;
                m_baseZombieCompanionCap += metaData.ZombieCapLevel * 2;
                m_baseExpMultiplier += 0f; // 经验倍率暂不做局外升级
            }

            ClearBonuses();
        }

        /// <summary>
        /// 使用局外升级系统重新计算基础属性，优先读取 MetaUpgradeConfig 中的加成配置。
        /// </summary>
        public void Initialize(GameConfig config, MetaUpgradeSystem metaUpgradeSystem)
        {
            if (config == null)
            {
                Debug.LogError("[PlayerStats] Initialize 收到空的 GameConfig，玩家属性将保持默认值 0");
                ResetBaseValuesToZero();
                ClearBonuses();
                return;
            }

            m_baseMoveSpeed = config.PlayerBaseSpeed;
            m_baseInfectionRadius = config.PlayerBaseInfectionRadius;
            m_baseZombieCompanionCap = config.ZombieCompanionMaxCount;
            m_baseExpMultiplier = 1f;

            if (metaUpgradeSystem != null)
            {
                m_baseMoveSpeed += metaUpgradeSystem.GetBonus(UpgradeType.MoveSpeed);
                m_baseInfectionRadius += metaUpgradeSystem.GetBonus(UpgradeType.InfectionRadius);
                m_baseZombieCompanionCap += Mathf.RoundToInt(metaUpgradeSystem.GetBonus(UpgradeType.ZombieCompanionCap));
            }

            ClearBonuses();
        }

        /// <summary>
        /// 应用一次局内升级加成。加成以「增量累加」的方式叠加到对应属性上，
        /// 调用后立即可通过 <see cref="MoveSpeed"/> 等属性读取到新值。
        /// </summary>
        /// <param name="type">升级类型，决定加成落到哪项属性</param>
        /// <param name="value">本次升级带来的增量值（正负均可，上层决定语义）</param>
        public void ApplyUpgrade(UpgradeType type, float value)
        {
            switch (type)
            {
                case UpgradeType.MoveSpeed:
                    m_moveSpeedBonus += value;
                    break;
                case UpgradeType.InfectionRadius:
                    m_infectionRadiusBonus += value;
                    break;
                case UpgradeType.ZombieCompanionCap:
                    m_zombieCompanionCapBonus += value;
                    break;
                case UpgradeType.ExpMultiplier:
                    m_expMultiplierBonus += value;
                    break;
                default:
                    Debug.LogWarning($"[PlayerStats] ApplyUpgrade 收到未识别的 UpgradeType: {type}");
                    break;
            }
        }

        /// <summary>
        /// 重置局内累计加成为 0，使属性值回到「基础值 + 局外加成」状态。
        /// 基础值保持不变，若需要重新读取 GameConfig/MetaUpgradeData 请重新调用 <see cref="Initialize"/>。
        /// 常用于单局结束、重新开局之前清理本局升级效果。
        /// </summary>
        public void Reset()
        {
            ClearBonuses();
        }

        // ==================== 内部辅助 ====================

        /// <summary>
        /// 清空所有局内累计加成。
        /// </summary>
        private void ClearBonuses()
        {
            m_moveSpeedBonus = 0f;
            m_infectionRadiusBonus = 0f;
            m_zombieCompanionCapBonus = 0f;
            m_expMultiplierBonus = 0f;
        }

        /// <summary>
        /// 将所有基础值归零，用于 <see cref="Initialize"/> 收到空配置时的保护性降级处理。
        /// </summary>
        private void ResetBaseValuesToZero()
        {
            m_baseMoveSpeed = 0f;
            m_baseInfectionRadius = 0f;
            m_baseZombieCompanionCap = 0;
            m_baseExpMultiplier = 0f;
        }
    }
}
