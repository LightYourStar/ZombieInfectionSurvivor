using System.Collections.Generic;
using Game.Config;
using Game.Gameplay.Player;
using UnityEngine;

namespace Game.Gameplay.Skill
{
    /// <summary>
    /// 局内升级系统。
    /// 负责从可用升级池中随机抽取不重复选项，以及将玩家选择的升级效果应用到 <see cref="PlayerStats"/>。
    /// </summary>
    /// <remarks>
    /// 升级池固定为 4 种类型（<see cref="UpgradeType"/>），每种类型的增量值来自 <see cref="GameConfig"/>。
    /// <see cref="DrawOptions"/> 保证返回的选项互不重复（Property 13）。
    /// </remarks>
    public class UpgradeSystem : MonoBehaviour
    {
        // ==================== Inspector 注入 ====================

        [Header("配置")]
        [Tooltip("游戏配置，提供每次升级的增量值")]
        [SerializeField] private GameConfig m_config;

        // ==================== 运行时依赖 ====================

        /// <summary>玩家运行时属性，由 Initialize 注入</summary>
        private PlayerStats m_playerStats;

        /// <summary>可用升级选项池（每局开始时重建）</summary>
        private readonly List<UpgradeOption> m_availableOptions = new List<UpgradeOption>();

        /// <summary>DrawOptions 内部复用的临时列表，避免每次调用分配</summary>
        private readonly List<UpgradeOption> m_tempDrawPool = new List<UpgradeOption>();

        // ==================== 初始化 ====================

        /// <summary>
        /// 注入运行时依赖并重建升级池。由 GameSystemRunner 在进入 Playing 前调用。
        /// </summary>
        /// <param name="playerStats">玩家运行时属性</param>
        public void Initialize(PlayerStats playerStats)
        {
            m_playerStats = playerStats;
            RebuildOptionPool();
        }

        // ==================== 公开 API ====================

        /// <summary>
        /// 从可用池中随机抽取 <paramref name="count"/> 个不重复的升级选项。
        /// 若可用选项不足 <paramref name="count"/> 个，返回所有可用选项（可能少于请求数量）。
        /// 对应设计文档 Property 13：DrawOptions(3) 返回 3 个不同 UpgradeType 的选项。
        /// </summary>
        /// <param name="count">请求的选项数量，通常为 3</param>
        /// <returns>不重复的升级选项列表</returns>
        public List<UpgradeOption> DrawOptions(int count)
        {
            List<UpgradeOption> result = new List<UpgradeOption>();

            if (count <= 0 || m_availableOptions.Count == 0)
            {
                return result;
            }

            // 复制可用池到临时列表，通过 Fisher-Yates 洗牌取前 N 个
            m_tempDrawPool.Clear();
            m_tempDrawPool.AddRange(m_availableOptions);

            int drawCount = Mathf.Min(count, m_tempDrawPool.Count);
            for (int i = 0; i < drawCount; i++)
            {
                int randomIndex = Random.Range(i, m_tempDrawPool.Count);
                // 交换
                UpgradeOption temp = m_tempDrawPool[i];
                m_tempDrawPool[i] = m_tempDrawPool[randomIndex];
                m_tempDrawPool[randomIndex] = temp;

                result.Add(m_tempDrawPool[i]);
            }

            return result;
        }

        /// <summary>
        /// 应用选中的升级效果到 <see cref="PlayerStats"/>。
        /// </summary>
        /// <param name="option">玩家选择的升级选项</param>
        public void ApplyUpgrade(UpgradeOption option)
        {
            if (option == null)
            {
                Debug.LogWarning("[UpgradeSystem] ApplyUpgrade 收到空选项，已忽略");
                return;
            }

            if (m_playerStats == null)
            {
                Debug.LogError("[UpgradeSystem] PlayerStats 未注入，无法应用升级");
                return;
            }

            m_playerStats.ApplyUpgrade(option.Type, option.Value);
        }

        /// <summary>
        /// 重置升级系统状态（重建升级池）。通常在新一局开始时调用。
        /// </summary>
        public void Reset()
        {
            RebuildOptionPool();
        }

        // ==================== 内部辅助 ====================

        /// <summary>
        /// 根据 <see cref="GameConfig"/> 重建可用升级选项池。
        /// 每种 <see cref="UpgradeType"/> 对应一个选项，增量值来自配置。
        /// </summary>
        private void RebuildOptionPool()
        {
            m_availableOptions.Clear();

            if (m_config == null)
            {
                Debug.LogError("[UpgradeSystem] GameConfig 未赋值，升级池为空");
                return;
            }

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
        }
    }
}
