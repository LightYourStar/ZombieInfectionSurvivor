using System.Text;
using Game.Config;
using Game.Core;
using Game.Gameplay.Feedback;
using Game.Gameplay.Infection;
using Game.Gameplay.Player;
using Game.Gameplay.Skill;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 开发环境用的调试统计面板，使用 OnGUI 绘制，不依赖 Canvas。
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

        private PlayerStats m_playerStats;
        private bool m_visible = true;
        private int m_lastBurstCount;
        private int m_maxObservedZombieCount;
        private GUIStyle m_labelStyle;
        private GUIStyle m_headerStyle;

        public void Initialize(
            TimerSystem timerSystem,
            InfectionSystem infectionSystem,
            GameSessionController sessionController,
            InfectionComboTracker comboTracker,
            UpgradeSystem upgradeSystem,
            PlayerStats playerStats,
            GameConfig gameConfig)
        {
            m_timerSystem = timerSystem;
            m_infectionSystem = infectionSystem;
            m_sessionController = sessionController;
            m_comboTracker = comboTracker;
            m_upgradeSystem = upgradeSystem;
            m_playerStats = playerStats;
            m_gameConfig = gameConfig;

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
            int lineCount = 9;

            GUI.Box(new Rect(x - 6f, y - 6f, width + 12f, lineHeight * lineCount + 16f), string.Empty);

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
#endif
        }

        private void SubscribeEvents()
        {
            GameEvents.OnSessionStateChanged -= HandleSessionStateChanged;
            GameEvents.OnSessionStateChanged += HandleSessionStateChanged;

            if (m_infectionSystem != null)
            {
                m_infectionSystem.OnInfectionBurstResolved -= HandleInfectionBurstResolved;
                m_infectionSystem.OnInfectionBurstResolved += HandleInfectionBurstResolved;
            }
        }

        private void UnsubscribeEvents()
        {
            GameEvents.OnSessionStateChanged -= HandleSessionStateChanged;

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
        }

        private void HandleInfectionBurstResolved(int count)
        {
            m_lastBurstCount = count;
        }

        private void ResetForNewSession()
        {
            m_lastBurstCount = 0;
            m_maxObservedZombieCount = 0;
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
            AppendUpgrade(builder, state.FinalFrenzyEarlyStacks > 0, $"Final Frenzy 提前 x{state.FinalFrenzyEarlyStacks}");
            AppendUpgrade(builder, state.EchoBurstStacks > 0, $"EchoBurst x{state.EchoBurstStacks}");

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

        private static void AppendUpgrade(StringBuilder builder, bool condition, string text)
        {
            if (!condition)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append(" | ");
            }

            builder.Append(text);
        }

        private void EnsureStyles()
        {
            if (m_labelStyle != null)
            {
                return;
            }

            m_labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14
            };
            m_labelStyle.normal.textColor = Color.white;

            m_headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            m_headerStyle.normal.textColor = Color.yellow;
        }
    }
}
