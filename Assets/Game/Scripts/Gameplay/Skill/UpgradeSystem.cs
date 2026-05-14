using System.Collections.Generic;
using Game.Config;
using Game.Gameplay.Player;
using UnityEngine;

namespace Game.Gameplay.Skill
{
    /// <summary>
    /// 局内升级系统。
    /// 负责从可用升级池中随机抽取不重复选项，以及将玩家选择的升级效果应用到 PlayerStats 或 SessionUpgradeState。
    /// 支持原有 4 种属性升级 + 6 种 2.0 新增机制升级。
    /// </summary>
    public class UpgradeSystem : MonoBehaviour
    {
        // ==================== Inspector 注入 ====================

        [Header("配置")]
        [Tooltip("游戏配置，提供每次升级的增量值")]
        [SerializeField] private GameConfig m_config;

        // ==================== 运行时依赖 ====================

        /// <summary>玩家运行时属性，由 Initialize 注入</summary>
        private PlayerStats m_playerStats;

        /// <summary>当前局升级状态，由 Initialize 注入</summary>
        private SessionUpgradeState m_sessionState;

        /// <summary>可用升级选项池（每局开始时重建，达到上限的选项会被移除）</summary>
        private readonly List<UpgradeOption> m_availableOptions = new List<UpgradeOption>();

        /// <summary>DrawOptions 内部复用的临时列表，避免每次调用分配</summary>
        private readonly List<UpgradeOption> m_tempDrawPool = new List<UpgradeOption>();

        // ==================== 公开属性 ====================

        /// <summary>当前局升级状态，供外部系统读取修正值</summary>
        public SessionUpgradeState SessionState => m_sessionState;

        // ==================== 初始化 ====================

        /// <summary>
        /// 注入运行时依赖并重建升级池。由 GameSystemRunner 在进入 Playing 前调用。
        /// </summary>
        /// <param name="playerStats">玩家运行时属性</param>
        public void Initialize(PlayerStats playerStats)
        {
            m_playerStats = playerStats;
            m_sessionState = new SessionUpgradeState();
            RebuildOptionPool();
        }

        // ==================== 公开 API ====================

        /// <summary>
        /// 从可用池中随机抽取 count 个不重复的升级选项。
        /// 已达到最大叠加次数的选项不会出现在候选中。
        /// </summary>
        public List<UpgradeOption> DrawOptions(int count)
        {
            List<UpgradeOption> result = new List<UpgradeOption>();

            if (count <= 0 || m_availableOptions.Count == 0)
            {
                return result;
            }

            // 过滤掉已达上限的 2.0 升级
            m_tempDrawPool.Clear();
            for (int i = 0; i < m_availableOptions.Count; i++)
            {
                UpgradeOption opt = m_availableOptions[i];
                if (m_sessionState != null && m_sessionState.IsMaxed(opt.Type))
                {
                    continue;
                }
                m_tempDrawPool.Add(opt);
            }

            if (m_tempDrawPool.Count == 0)
            {
                return result;
            }

            // Fisher-Yates 洗牌取前 N 个
            int drawCount = Mathf.Min(count, m_tempDrawPool.Count);
            for (int i = 0; i < drawCount; i++)
            {
                int randomIndex = Random.Range(i, m_tempDrawPool.Count);
                UpgradeOption temp = m_tempDrawPool[i];
                m_tempDrawPool[i] = m_tempDrawPool[randomIndex];
                m_tempDrawPool[randomIndex] = temp;

                result.Add(m_tempDrawPool[i]);
            }

            return result;
        }

        /// <summary>
        /// 应用选中的升级效果。
        /// 原有 4 种类型走 PlayerStats.ApplyUpgrade；新增 6 种走 SessionUpgradeState.TryApply。
        /// </summary>
        public void ApplyUpgrade(UpgradeOption option)
        {
            if (option == null)
            {
                Debug.LogWarning("[UpgradeSystem] ApplyUpgrade 收到空选项，已忽略");
                return;
            }

            // 判断是否为 2.0 新增类型
            switch (option.Type)
            {
                case UpgradeType.ChainPlusOne:
                case UpgradeType.BurstRadiusUp:
                case UpgradeType.NewbornRushDurationUp:
                case UpgradeType.ZombiePerceptionUp:
                case UpgradeType.FinalFrenzyEarly:
                case UpgradeType.EchoBurst:
                    if (m_sessionState != null)
                    {
                        m_sessionState.TryApply(option.Type);
                    }
                    return;

                default:
                    // 原有类型走 PlayerStats
                    if (m_playerStats == null)
                    {
                        Debug.LogError("[UpgradeSystem] PlayerStats 未注入，无法应用升级");
                        return;
                    }
                    m_playerStats.ApplyUpgrade(option.Type, option.Value);
                    return;
            }
        }

        /// <summary>
        /// 重置升级系统状态（重建升级池 + 重置 SessionUpgradeState）。
        /// </summary>
        public void Reset()
        {
            if (m_sessionState != null)
            {
                m_sessionState.Reset();
            }
            RebuildOptionPool();
        }

        // ==================== 内部辅助 ====================

        /// <summary>
        /// 重建可用升级选项池，包含原有 4 种 + 新增 6 种。
        /// </summary>
        private void RebuildOptionPool()
        {
            m_availableOptions.Clear();

            if (m_config == null)
            {
                Debug.LogError("[UpgradeSystem] GameConfig 未赋值，升级池为空");
                return;
            }

            // 原有 4 种属性升级
            m_availableOptions.Add(new UpgradeOption(
                UpgradeType.InfectionRadius,
                m_config.InfectionRadiusPerUpgrade,
                $"感染半径 +{m_config.InfectionRadiusPerUpgrade:F1}"));

            m_availableOptions.Add(new UpgradeOption(
                UpgradeType.MoveSpeed,
                m_config.SpeedPerUpgrade,
                $"移动速度 +{m_config.SpeedPerUpgrade:F1}"));

            m_availableOptions.Add(new UpgradeOption(
                UpgradeType.ZombieCompanionCap,
                m_config.ZombieCapPerUpgrade,
                $"僵尸上限 +{m_config.ZombieCapPerUpgrade}"));

            m_availableOptions.Add(new UpgradeOption(
                UpgradeType.ExpMultiplier,
                m_config.ExpMultiplierPerUpgrade,
                $"经验倍率 +{m_config.ExpMultiplierPerUpgrade * 100:F0}%"));

            // 2.0 新增 6 种机制升级
            m_availableOptions.Add(new UpgradeOption(
                UpgradeType.ChainPlusOne,
                1f,
                "连锁强化：爆发额外感染 +1"));

            m_availableOptions.Add(new UpgradeOption(
                UpgradeType.BurstRadiusUp,
                0.2f,
                "扩散毒圈：爆发半径 +20%"));

            m_availableOptions.Add(new UpgradeOption(
                UpgradeType.NewbornRushDurationUp,
                0.25f,
                "新生狂奔：冲刺时间 +0.25s"));

            m_availableOptions.Add(new UpgradeOption(
                UpgradeType.ZombiePerceptionUp,
                0.2f,
                "尸群嗅觉：感知范围 +20%"));

            m_availableOptions.Add(new UpgradeOption(
                UpgradeType.FinalFrenzyEarly,
                10f,
                "狂潮提前：末日狂潮提前 10s"));

            m_availableOptions.Add(new UpgradeOption(
                UpgradeType.EchoBurst,
                1f,
                "回响爆发：每 5 次感染触发额外爆发"));
        }
    }
}
