using System;
using Game.Config;
using Game.Core;
using Game.Gameplay.Player;
using UnityEngine;

namespace Game.Gameplay.Skill
{
    /// <summary>
    /// 经验与金币管理系统。
    /// 负责单局内经验值的累加、经验倍率应用、升级阈值判定以及金币计数。
    /// 当经验达到当前等级阈值时触发升级事件，由 UpgradeSystem / UpgradePanel 响应。
    /// </summary>
    /// <remarks>
    /// 经验模型：
    /// <list type="bullet">
    /// <item><see cref="m_currentExp"/> 表示「当前等级内已累积的经验」，每次升级时扣除阈值。</item>
    /// <item><see cref="m_currentLevel"/> 从 1 开始，每次升级 +1。</item>
    /// <item>阈值来自 <see cref="GameConfig.ExpThresholds"/>，下标 0 对应 1 级升 2 级所需经验。</item>
    /// <item>超出阈值数组范围时视为已达最高等级，不再触发升级。</item>
    /// </list>
    /// </remarks>
    public class ExperienceSystem : MonoBehaviour
    {
        // ==================== Inspector 注入 ====================

        [Header("配置")]
        [Tooltip("游戏配置，提供 ExpThresholds 等参数")]
        [SerializeField] private GameConfig m_config;

        // ==================== 运行时依赖 ====================

        /// <summary>玩家运行时属性，提供 ExpMultiplier；由 Initialize 注入</summary>
        private PlayerStats m_playerStats;

        // ==================== 运行时状态 ====================

        /// <summary>当前等级内已累积的经验值</summary>
        private int m_currentExp;

        /// <summary>本局累计获得的金币</summary>
        private int m_currentGold;

        /// <summary>当前等级，从 1 开始</summary>
        private int m_currentLevel = 1;

        // ==================== 公开属性 ====================

        /// <summary>当前等级内已累积的经验值</summary>
        public int CurrentExp => m_currentExp;

        /// <summary>本局累计获得的金币</summary>
        public int CurrentGold => m_currentGold;

        /// <summary>当前等级</summary>
        public int CurrentLevel => m_currentLevel;

        // ==================== 事件 ====================

        /// <summary>
        /// 升级事件（实例级），参数为升级后的新等级。
        /// UpgradeSystem 订阅此事件以弹出升级面板。
        /// </summary>
        public event Action<int> OnLevelUp;

        // ==================== 初始化 ====================

        /// <summary>
        /// 注入运行时依赖并重置状态。由 GameSystemRunner 在进入 Playing 前调用。
        /// </summary>
        /// <param name="playerStats">玩家运行时属性；为 null 时经验倍率默认为 1</param>
        public void Initialize(PlayerStats playerStats)
        {
            m_playerStats = playerStats;
            ResetState();
        }

        // ==================== 公开 API ====================

        /// <summary>
        /// 增加经验值（应用经验倍率后累加）。
        /// 若累加后达到升级阈值，会在同一帧内循环触发多次升级（处理大量经验一次性灌入的场景）。
        /// </summary>
        /// <param name="amount">基础经验值（未乘倍率），必须为正数</param>
        public void AddExp(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            // 应用经验倍率
            float multiplier = m_playerStats != null ? m_playerStats.ExpMultiplier : 1f;
            int scaledAmount = Mathf.RoundToInt(amount * multiplier);
            if (scaledAmount <= 0)
            {
                scaledAmount = 1; // 保底至少获得 1 点经验
            }

            m_currentExp += scaledAmount;
            GameEvents.RaiseExpChanged(m_currentExp);

            // 循环检查升级：处理一次性获得大量经验跨越多级的情况
            while (HasReachedThreshold())
            {
                int threshold = GetCurrentThreshold();
                m_currentExp -= threshold;
                m_currentLevel++;

                // 触发实例级事件和全局事件
                OnLevelUp?.Invoke(m_currentLevel);
                GameEvents.RaiseLevelUp(m_currentLevel);
                GameEvents.RaiseExpChanged(m_currentExp);
            }
        }

        /// <summary>
        /// 增加金币。
        /// </summary>
        /// <param name="amount">金币数量，必须为正数</param>
        public void AddGold(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            m_currentGold += amount;
            GameEvents.RaiseGoldChanged(m_currentGold);
        }

        /// <summary>
        /// 判断当前经验是否达到升级阈值。
        /// 对应设计文档 Property 12：E &gt;= T 时返回 true，E &lt; T 时返回 false。
        /// 已达最高等级（阈值数组越界）时始终返回 false。
        /// </summary>
        /// <returns>是否达到当前等级的升级阈值</returns>
        public bool HasReachedThreshold()
        {
            int threshold = GetCurrentThreshold();
            if (threshold == int.MaxValue)
            {
                return false; // 已达最高等级
            }
            return m_currentExp >= threshold;
        }

        /// <summary>
        /// 重置经验、金币和等级到初始状态。通常在新一局开始时调用。
        /// </summary>
        public void Reset()
        {
            ResetState();
        }

        // ==================== 内部辅助 ====================

        /// <summary>
        /// 获取当前等级对应的升级阈值。
        /// 阈值数组下标 0 对应 1 级升 2 级，即 index = m_currentLevel - 1。
        /// 越界时返回 int.MaxValue 表示无法再升级。
        /// </summary>
        private int GetCurrentThreshold()
        {
            if (m_config == null || m_config.ExpThresholds == null)
            {
                return int.MaxValue;
            }

            int index = m_currentLevel - 1;
            if (index < 0 || index >= m_config.ExpThresholds.Length)
            {
                return int.MaxValue;
            }

            return m_config.ExpThresholds[index];
        }

        /// <summary>
        /// 内部重置方法，清空经验、金币、等级。
        /// </summary>
        private void ResetState()
        {
            m_currentExp = 0;
            m_currentGold = 0;
            m_currentLevel = 1;
        }
    }
}
