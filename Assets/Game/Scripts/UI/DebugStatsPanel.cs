using System.Text;
using Game.Config;
using Game.Core;
using Game.Gameplay.Feedback;
using Game.Gameplay.Infection;
using Game.Gameplay.Player;
using Game.Gameplay.Skill;
using Game.Gameplay.Wave;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 开发环境用的调试统计面板，使用 OnGUI 绘制，不依赖 Canvas。
    /// 包含实时统计和单局验收摘要两个区块。
    /// </summary>
    public class DebugStatsPanel : MonoBehaviour
    {
        [Header("依赖引用")]
        [SerializeField] private TimerSystem m_timerSystem;
        [SerializeField] private InfectionSystem m_infectionSystem;
        [SerializeField] private GameSessionController m_sessionController;
        [SerializeField] private InfectionComboTracker m_comboTracker;
        [SerializeField] private UpgradeSystem m_upgradeSystem;
        [SerializeField] private GameConfig m_gameConfig;
        [SerializeField] private HumanClusterSpawner m_clusterSpawner;

        private PlayerStats m_playerStats;
        private bool m_visible = true;
        private int m_lastBurstCount;
        private int m_maxObservedZombieCount;
        private GUIStyle m_labelStyle;
        private GUIStyle m_headerStyle;

        // ==================== 单局验收摘要数据 ====================

        /// <summary>30 秒时的感染数（-1 表示尚未记录）</summary>
        private int m_infected30s = -1;

        /// <summary>60 秒时的感染数</summary>
        private int m_infected60s = -1;

        /// <summary>120 秒时的感染数</summary>
        private int m_infected120s = -1;

        /// <summary>180 秒（结算时）的感染总数</summary>
        private int m_infectedFinal = -1;

        /// <summary>摘要中的最高僵尸数（结算时快照）</summary>
        private int m_summaryMaxZombie;

        /// <summary>摘要中的最高连击（结算时快照）</summary>
        private int m_summaryMaxCombo;

        /// <summary>摘要中的最终感染总数（用于达标判定）</summary>
        private int m_summaryFinalInfected;

        /// <summary>是否已完成本局摘要（结算后为 true，新局开始时重置）</summary>
        private bool m_summaryFinalized;

        // ==================== 狂潮触发追踪 ====================

        /// <summary>狂潮是否已在本局触发</summary>
        private bool m_frenzyTriggered;

        /// <summary>狂潮触发时的已用时间</summary>
        private float m_frenzyElapsedTime;

        /// <summary>狂潮触发时的剩余时间</summary>
        private float m_frenzyRemainingTime;

        // ==================== 初始化 ====================

        public void Initialize(
            TimerSystem timerSystem,
            InfectionSystem infectionSystem,
            GameSessionController sessionController,
            InfectionComboTracker comboTracker,
            UpgradeSystem upgradeSystem,
            PlayerStats playerStats,
            GameConfig gameConfig,
            HumanClusterSpawner clusterSpawner = null)
        {
            m_timerSystem = timerSystem;
            m_infectionSystem = infectionSystem;
            m_sessionController = sessionController;
            m_comboTracker = comboTracker;
            m_upgradeSystem = upgradeSystem;
            m_playerStats = playerStats;
            m_gameConfig = gameConfig;
            m_clusterSpawner = clusterSpawner;

            if (isActiveAndEnabled)
            {
                UnsubscribeEvents();
                SubscribeEvents();
            }

            ResetForNewSession();
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void Update()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return;
#else
            if (Input.GetKeyDown(KeyCode.F3))
            {
                m_visible = !m_visible;
            }

            if (m_infectionSystem != null)
            {
                int activeZombieCount = m_infectionSystem.ActiveZombieCount;
                if (activeZombieCount > m_maxObservedZombieCount)
                {
                    m_maxObservedZombieCount = activeZombieCount;
                }
            }

            // 记录时间节点感染数
            RecordTimeCheckpoints();
#endif
        }

        private void OnGUI()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return;
#else
            if (!m_visible)
            {
                return;
            }

            EnsureStyles();

            float x = 10f;
            float y = 10f;
            float width = 480f;
            float lineHeight = 20f;

            // ===== 实时统计区 =====
            int liveLineCount = 14;
            GUI.Box(new Rect(x - 6f, y - 6f, width + 12f, lineHeight * liveLineCount + 16f), string.Empty);

            GUI.Label(new Rect(x, y, width, lineHeight), "=== DEBUG STATS (F3) ===", m_headerStyle);
            y += lineHeight;

            float totalDuration = m_timerSystem != null
                ? m_timerSystem.TotalDuration
                : (m_gameConfig != null ? m_gameConfig.MatchDuration : 180f);
            float remainingTime = m_timerSystem != null ? m_timerSystem.RemainingTime : 0f;
            float elapsedTime = Mathf.Clamp(totalDuration - remainingTime, 0f, totalDuration);
            GUI.Label(new Rect(x, y, width, lineHeight), $"时间: 已用 {elapsedTime:F1}s / 剩余 {remainingTime:F1}s / 总时长 {totalDuration:F1}s", m_labelStyle);
            y += lineHeight;

            int infectedCount = m_sessionController != null ? m_sessionController.InfectedCount : 0;
            GUI.Label(new Rect(x, y, width, lineHeight), $"感染总数: {infectedCount}", m_labelStyle);
            y += lineHeight;

            int activeZombieCountNow = m_infectionSystem != null ? m_infectionSystem.ActiveZombieCount : 0;
            GUI.Label(new Rect(x, y, width, lineHeight), $"当前僵尸数: {activeZombieCountNow} / 最高僵尸数: {m_maxObservedZombieCount}", m_labelStyle);
            y += lineHeight;

            int currentCombo = m_comboTracker != null ? m_comboTracker.CurrentCombo : 0;
            int maxCombo = m_comboTracker != null ? m_comboTracker.MaxCombo : 0;
            GUI.Label(new Rect(x, y, width, lineHeight), $"当前连击: x{currentCombo} / 最高连击: x{maxCombo}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"最近一次普通 InfectionBurst 数量: {m_lastBurstCount}", m_labelStyle);
            y += lineHeight;

            string rating = m_sessionController != null ? m_sessionController.CurrentRating.ToString() : "N/A";
            GUI.Label(new Rect(x, y, width, lineHeight), $"当前评级: {rating}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"2.0 升级: {BuildSessionUpgradeText()}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"当前属性: {BuildPlayerStatsText()}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"断流计时: 距上次感染 {GetSecondsSinceLastInfectionText()}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"断流状态: {GetFlowStateText()}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"最近补流: {GetFlowTopUpText()}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"最近刷怪来源: {GetLastSpawnSourceText()}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"最近热点/位置: {GetLastSpawnDetailText()}", m_labelStyle);
            y += lineHeight;

            // ===== 单局验收摘要区 =====
            y += 8f; // 间距
            int summaryLineCount = 14;
            GUI.Box(new Rect(x - 6f, y - 6f, width + 12f, lineHeight * summaryLineCount + 16f), string.Empty);

            GUI.Label(new Rect(x, y, width, lineHeight), "=== 单局验收摘要 ===", m_headerStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"30秒感染：{FormatCheckpoint(m_infected30s)}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"60秒感染：{FormatCheckpoint(m_infected60s)}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"120秒感染：{FormatCheckpoint(m_infected120s)}", m_labelStyle);
            y += lineHeight;

            int finalInfected = m_summaryFinalized ? m_summaryFinalInfected : infectedCount;
            GUI.Label(new Rect(x, y, width, lineHeight), $"180秒总感染：{(m_summaryFinalized ? finalInfected.ToString() : "进行中...")}", m_labelStyle);
            y += lineHeight;

            int dispMaxZombie = m_summaryFinalized ? m_summaryMaxZombie : m_maxObservedZombieCount;
            GUI.Label(new Rect(x, y, width, lineHeight), $"最高僵尸数：{dispMaxZombie}", m_labelStyle);
            y += lineHeight;

            int dispMaxCombo = m_summaryFinalized ? m_summaryMaxCombo : maxCombo;
            GUI.Label(new Rect(x, y, width, lineHeight), $"最高连击：x{dispMaxCombo}", m_labelStyle);
            y += lineHeight;

            // 达标状态
            string thresholdText = BuildThresholdText(finalInfected);
            GUI.Label(new Rect(x, y, width, lineHeight), $"达标：{thresholdText}", m_labelStyle);
            y += lineHeight;

            // 狂潮触发信息
            if (m_frenzyTriggered)
            {
                GUI.Label(new Rect(x, y, width, lineHeight), $"狂潮触发：已用 {m_frenzyElapsedTime:F1}s / 剩余 {m_frenzyRemainingTime:F1}s", m_labelStyle);
            }
            else
            {
                GUI.Label(new Rect(x, y, width, lineHeight), "狂潮触发：未触发", m_labelStyle);
            }
            y += lineHeight;

            // 狂潮提前升级状态
            bool hasFrenzyEarlyUpgrade = m_upgradeSystem != null && m_upgradeSystem.SessionState != null
                && m_upgradeSystem.SessionState.FinalFrenzyEarlyStacks > 0;
            string frenzyEarlyText = hasFrenzyEarlyUpgrade
                ? $"狂潮提前升级：已获取 x{m_upgradeSystem.SessionState.FinalFrenzyEarlyStacks}（触发前获取：{(m_frenzyTriggered ? "是" : "否")}）"
                : "狂潮提前升级：未获取";
            GUI.Label(new Rect(x, y, width, lineHeight), frenzyEarlyText, m_labelStyle);
            y += lineHeight;

            // 主观评价占位
            GUI.Label(new Rect(x, y, width, lineHeight), "开局：____  中盘：____", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), "狂潮：____  结算：____", m_labelStyle);
            y += lineHeight;
#endif
        }

        // ==================== 时间节点记录 ====================

        private void RecordTimeCheckpoints()
        {
            if (m_summaryFinalized)
            {
                return;
            }
            if (m_sessionController == null || m_sessionController.CurrentState != SessionState.Playing)
            {
                return;
            }
            if (m_timerSystem == null)
            {
                return;
            }

            float totalDuration = m_timerSystem.TotalDuration;
            float remaining = m_timerSystem.RemainingTime;
            float elapsed = totalDuration - remaining;

            int currentInfected = m_sessionController.InfectedCount;

            if (m_infected30s < 0 && elapsed >= 30f)
            {
                m_infected30s = currentInfected;
            }
            if (m_infected60s < 0 && elapsed >= 60f)
            {
                m_infected60s = currentInfected;
            }
            if (m_infected120s < 0 && elapsed >= 120f)
            {
                m_infected120s = currentInfected;
            }
        }

        // ==================== 事件处理 ====================

        private void SubscribeEvents()
        {
            GameEvents.OnSessionStateChanged -= HandleSessionStateChanged;
            GameEvents.OnSessionStateChanged += HandleSessionStateChanged;
            GameEvents.OnFinalFrenzyStarted -= HandleFrenzyTriggered;
            GameEvents.OnFinalFrenzyStarted += HandleFrenzyTriggered;

            if (m_infectionSystem != null)
            {
                m_infectionSystem.OnInfectionBurstResolved -= HandleInfectionBurstResolved;
                m_infectionSystem.OnInfectionBurstResolved += HandleInfectionBurstResolved;
            }
        }

        private void UnsubscribeEvents()
        {
            GameEvents.OnSessionStateChanged -= HandleSessionStateChanged;
            GameEvents.OnFinalFrenzyStarted -= HandleFrenzyTriggered;

            if (m_infectionSystem != null)
            {
                m_infectionSystem.OnInfectionBurstResolved -= HandleInfectionBurstResolved;
            }
        }

        private void HandleSessionStateChanged(SessionState state)
        {
            if (state == SessionState.Playing)
            {
                ResetForNewSession();
            }
            else if (state == SessionState.Result)
            {
                FinalizeSessionSummary();
            }
        }

        private void HandleInfectionBurstResolved(int count)
        {
            m_lastBurstCount = count;
        }

        // ==================== 摘要管理 ====================

        private void ResetForNewSession()
        {
            m_lastBurstCount = 0;
            m_maxObservedZombieCount = 0;
            m_infected30s = -1;
            m_infected60s = -1;
            m_infected120s = -1;
            m_infectedFinal = -1;
            m_summaryMaxZombie = 0;
            m_summaryMaxCombo = 0;
            m_summaryFinalInfected = 0;
            m_summaryFinalized = false;
            m_frenzyTriggered = false;
            m_frenzyElapsedTime = 0f;
            m_frenzyRemainingTime = 0f;
        }

        private void HandleFrenzyTriggered()
        {
            if (m_frenzyTriggered)
            {
                return; // 只记录一次
            }
            m_frenzyTriggered = true;

            if (m_timerSystem != null)
            {
                float totalDuration = m_timerSystem.TotalDuration;
                m_frenzyRemainingTime = m_timerSystem.RemainingTime;
                m_frenzyElapsedTime = totalDuration - m_frenzyRemainingTime;
            }
        }

        /// <summary>
        /// 结算时快照摘要数据，之后不再更新直到下一局开始。
        /// </summary>
        private void FinalizeSessionSummary()
        {
            m_summaryFinalized = true;
            m_summaryFinalInfected = m_sessionController != null ? m_sessionController.InfectedCount : 0;
            m_summaryMaxZombie = m_maxObservedZombieCount;
            m_summaryMaxCombo = m_comboTracker != null ? m_comboTracker.MaxCombo : 0;
            m_infectedFinal = m_summaryFinalInfected;
        }

        // ==================== 辅助方法 ====================

        private static string FormatCheckpoint(int value)
        {
            return value >= 0 ? value.ToString() : "未到";
        }

        private string BuildThresholdText(int infected)
        {
            int t1 = m_gameConfig != null ? m_gameConfig.TargetInfectedCount : 240;
            int t2 = m_gameConfig != null ? m_gameConfig.AScoreInfectedCount : 320;
            int t3 = m_gameConfig != null ? m_gameConfig.SScoreInfectedCount : 440;
            int t4 = m_gameConfig != null ? m_gameConfig.SSScoreInfectedCount : 560;

            string s1 = infected >= t1 ? "Y" : "N";
            string s2 = infected >= t2 ? "Y" : "N";
            string s3 = infected >= t3 ? "Y" : "N";
            string s4 = infected >= t4 ? "Y" : "N";

            return $"{t1}{s1} / {t2}{s2} / {t3}{s3} / {t4}{s4}";
        }

        private string BuildSessionUpgradeText()
        {
            if (m_upgradeSystem == null || m_upgradeSystem.SessionState == null)
            {
                return "无";
            }

            SessionUpgradeState state = m_upgradeSystem.SessionState;
            StringBuilder builder = new StringBuilder();

            AppendUpgrade(builder, state.ChainPlusOneStacks > 0, $"连锁+1 x{state.ChainPlusOneStacks}");
            AppendUpgrade(builder, state.BurstRadiusUpStacks > 0, $"爆发半径 x{state.BurstRadiusUpStacks}");
            AppendUpgrade(builder, state.NewbornRushDurationUpStacks > 0, $"新生冲刺 x{state.NewbornRushDurationUpStacks}");
            AppendUpgrade(builder, state.ZombiePerceptionUpStacks > 0, $"僵尸感知 x{state.ZombiePerceptionUpStacks}");
            AppendUpgrade(builder, state.FinalFrenzyEarlyStacks > 0, $"狂潮提前 x{state.FinalFrenzyEarlyStacks}");
            AppendUpgrade(builder, state.EchoBurstStacks > 0, $"回响爆发 x{state.EchoBurstStacks}");

            return builder.Length > 0 ? builder.ToString() : "无";
        }

        private string BuildPlayerStatsText()
        {
            if (m_playerStats == null)
            {
                return "未绑定";
            }

            return $"感染半径 {m_playerStats.InfectionRadius:F2} | 移动速度 {m_playerStats.MoveSpeed:F2} | 僵尸上限 {m_playerStats.ZombieCompanionCap} | 经验倍率 x{m_playerStats.ExpMultiplier:F2}";
        }

        private string GetSecondsSinceLastInfectionText()
        {
            return m_clusterSpawner != null
                ? $"{m_clusterSpawner.SecondsSinceLastInfection:F1}s"
                : "未绑定";
        }

        private string GetFlowStateText()
        {
            if (m_clusterSpawner == null)
            {
                return "未绑定";
            }

            switch (m_clusterSpawner.CurrentFlowState)
            {
                case InfectionFlowState.LightBreak:
                    return "轻度断流";
                case InfectionFlowState.SevereBreak:
                    return "严重断流";
                default:
                    return "正常";
            }
        }

        private string GetFlowTopUpText()
        {
            if (m_clusterSpawner == null)
            {
                return "未绑定";
            }

            string triggered = m_clusterSpawner.RecentFlowTopUpTriggered ? "已触发" : "未触发";
            float cooldown = m_clusterSpawner.FlowTopUpCooldownRemaining;
            return cooldown > 0f ? $"{triggered} / 冷却 {cooldown:F1}s" : $"{triggered} / 可触发";
        }

        private string GetLastSpawnSourceText()
        {
            return m_clusterSpawner != null ? m_clusterSpawner.LastSpawnSourceText : "未绑定";
        }

        private string GetLastSpawnDetailText()
        {
            if (m_clusterSpawner == null)
            {
                return "未绑定";
            }

            Vector2 position = m_clusterSpawner.LastSpawnPosition;
            return $"{m_clusterSpawner.LastSpawnDetailText} / ({position.x:F1}, {position.y:F1})";
        }

        private static void AppendUpgrade(StringBuilder builder, bool condition, string text)
        {
            if (!condition) return;
            if (builder.Length > 0) builder.Append(" | ");
            builder.Append(text);
        }

        private void EnsureStyles()
        {
            if (m_labelStyle != null) return;

            m_labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            m_labelStyle.normal.textColor = Color.white;

            m_headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            m_headerStyle.normal.textColor = Color.yellow;
        }
    }
}
