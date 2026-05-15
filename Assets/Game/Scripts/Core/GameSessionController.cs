using System;
using Game.Config;
using Game.Gameplay.Feedback;
using Game.Gameplay.Infection;
using Game.Gameplay.Skill;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 单局状态管理控制器。
    /// 负责管理 Ready / Playing / Result 三种状态以及局内统计数据。
    /// </summary>
    public class GameSessionController : MonoBehaviour
    {
        // ==================== Inspector 注入 ====================

        [Header("依赖引用")]
        [SerializeField] private GameConfig m_config;
        [SerializeField] private TimerSystem m_timerSystem;
        [SerializeField] private InfectionSystem m_infectionSystem;

        // ==================== 运行时依赖（由外部注入） ====================

        /// <summary>连击追踪器，用于获取 MaxCombo</summary>
        private InfectionComboTracker m_comboTracker;

        /// <summary>升级系统，用于获取升级摘要</summary>
        private UpgradeSystem m_upgradeSystem;

        // ==================== 运行时状态 ====================

        private SessionState m_currentState = SessionState.Ready;
        private int m_infectedCount;
        private int m_maxZombieCount;
        private bool m_isVictory;
        private SessionRating m_currentRating = SessionRating.C;
        private float m_elapsedTime;
        private float m_gameDuration;

        /// <summary>狂潮开始时的感染总数，用于计算狂潮阶段新增感染</summary>
        private int m_infectedCountAtFrenzyStart;

        /// <summary>是否已进入狂潮阶段</summary>
        private bool m_frenzyStarted;

        // ==================== 公开属性 ====================

        public SessionState CurrentState => m_currentState;
        public int InfectedCount => m_infectedCount;
        public int MaxZombieCount => m_maxZombieCount;
        public bool IsVictory => m_isVictory;
        public SessionRating CurrentRating => m_currentRating;
        public int TargetInfectedCount => m_config != null ? m_config.TargetInfectedCount : 240;
        public float RemainingTime => m_timerSystem != null ? m_timerSystem.RemainingTime : 0f;

        /// <summary>是否处于狂潮阶段</summary>
        public bool IsFrenzyActive => m_frenzyStarted;

        // ==================== 事件 ====================

        public event Action<int> OnInfectedCountChanged;
        public event Action<SessionRating> OnRatingChanged;
        public event Action OnVictoryAchieved;
        public event Action<SessionResult> OnSessionEnd;

        // ==================== 公开 API ====================

        /// <summary>
        /// 初始化控制器。可选注入 ComboTracker 和 UpgradeSystem。
        /// </summary>
        public void Initialize(InfectionComboTracker comboTracker = null, UpgradeSystem upgradeSystem = null)
        {
            m_comboTracker = comboTracker;
            m_upgradeSystem = upgradeSystem;

            if (m_config == null)
            {
                Debug.LogError("[GameSessionController] GameConfig 未赋值");
            }

            m_gameDuration = m_config != null ? m_config.MatchDuration : 180f;

            GameEvents.OnInfectionSuccess -= HandleInfectionSuccess;
            GameEvents.OnInfectionSuccess += HandleInfectionSuccess;
            GameEvents.OnTimerEnd -= HandleTimerEnd;
            GameEvents.OnTimerEnd += HandleTimerEnd;
            GameEvents.OnFinalFrenzyStarted -= HandleFrenzyStarted;
            GameEvents.OnFinalFrenzyStarted += HandleFrenzyStarted;

            m_currentState = SessionState.Ready;
            GameEvents.RaiseSessionStateChanged(m_currentState);
        }

        public void StartSession()
        {
            m_infectedCount = 0;
            m_maxZombieCount = 0;
            m_isVictory = false;
            m_currentRating = SessionRating.C;
            m_elapsedTime = 0f;
            m_frenzyStarted = false;
            m_infectedCountAtFrenzyStart = 0;
            m_gameDuration = m_config != null ? m_config.MatchDuration : 180f;

            m_currentState = SessionState.Playing;
            GameEvents.RaiseSessionStateChanged(m_currentState);

            int target = m_config != null ? m_config.TargetInfectedCount : 240;
            OnInfectedCountChanged?.Invoke(m_infectedCount);
            GameEvents.RaiseInfectionCountChanged(m_infectedCount, target);
        }

        public void EndSession()
        {
            if (m_currentState != SessionState.Playing)
            {
                return;
            }

            if (m_timerSystem != null)
            {
                m_timerSystem.StopTimer();
            }

            m_currentRating = CalculateRating(m_infectedCount);
            m_elapsedTime = m_gameDuration - (m_timerSystem != null ? m_timerSystem.RemainingTime : 0f);

            m_currentState = SessionState.Result;
            GameEvents.RaiseSessionStateChanged(m_currentState);

            // 收集结算数据
            int maxCombo = m_comboTracker != null ? m_comboTracker.MaxCombo : 0;
            int frenzyInfected = m_frenzyStarted ? (m_infectedCount - m_infectedCountAtFrenzyStart) : 0;
            int playerDirectInfected = m_infectionSystem != null ? m_infectionSystem.PlayerDirectInfections : 0;
            int zombieInfected = m_infectionSystem != null ? m_infectionSystem.ZombieInfections : 0;
            int burstInfected = m_infectionSystem != null
                ? m_infectionSystem.BurstInfections + m_infectionSystem.EchoBurstInfections
                : 0;
            string upgradeSummary = BuildUpgradeSummary();

            var result = new SessionResult(
                m_infectedCount,
                m_maxZombieCount,
                maxCombo,
                frenzyInfected,
                m_currentRating,
                m_isVictory,
                m_elapsedTime,
                upgradeSummary,
                playerDirectInfected,
                zombieInfected,
                burstInfected
            );

            OnSessionEnd?.Invoke(result);
        }

        public void RestartSession()
        {
            m_currentState = SessionState.Ready;
            GameEvents.RaiseSessionStateChanged(m_currentState);
            StartSession();
        }

        public SessionRating CalculateRating(int infectedCount)
        {
            int target = m_config != null ? m_config.TargetInfectedCount : 240;
            int aScore = m_config != null ? m_config.AScoreInfectedCount : 320;
            int sScore = m_config != null ? m_config.SScoreInfectedCount : 440;
            int ssScore = m_config != null ? m_config.SSScoreInfectedCount : 560;
            return CalculateRating(infectedCount, target, aScore, sScore, ssScore);
        }

        public static SessionRating CalculateRating(int infectedCount, int target, int aScore, int sScore, int ssScore)
        {
            if (infectedCount >= ssScore) return SessionRating.SS;
            if (infectedCount >= sScore) return SessionRating.S;
            if (infectedCount >= aScore) return SessionRating.A;
            if (infectedCount >= target) return SessionRating.B;
            return SessionRating.C;
        }

        /// <summary>
        /// 获取"下一目标提示"文本，供 HUD 使用。
        /// </summary>
        public string GetNextGoalText()
        {
            if (m_config == null) return "";

            int target = m_config.TargetInfectedCount;
            int aScore = m_config.AScoreInfectedCount;
            int sScore = m_config.SScoreInfectedCount;
            int ssScore = m_config.SSScoreInfectedCount;

            string prefix = m_frenzyStarted ? "狂潮冲刺：" : "";

            if (m_infectedCount >= ssScore)
            {
                return prefix + "已达 SS，继续冲高";
            }
            if (m_infectedCount >= sScore)
            {
                return prefix + $"距 SS 还差 {ssScore - m_infectedCount}";
            }
            if (m_infectedCount >= aScore)
            {
                return prefix + $"距 S 还差 {sScore - m_infectedCount}";
            }
            if (m_infectedCount >= target)
            {
                return prefix + $"距 A 还差 {aScore - m_infectedCount}";
            }
            return prefix + $"距通关还差 {target - m_infectedCount}";
        }

        // ==================== 内部方法 ====================

        private void HandleInfectionSuccess(Vector2 position)
        {
            if (m_currentState != SessionState.Playing) return;

            m_infectedCount++;

            if (m_infectionSystem != null)
            {
                int active = m_infectionSystem.ActiveZombieCount;
                if (active > m_maxZombieCount) m_maxZombieCount = active;
            }

            SessionRating newRating = CalculateRating(m_infectedCount);
            if (newRating != m_currentRating)
            {
                m_currentRating = newRating;
                OnRatingChanged?.Invoke(m_currentRating);
            }

            int target = m_config != null ? m_config.TargetInfectedCount : 240;
            if (!m_isVictory && m_infectedCount >= target)
            {
                m_isVictory = true;
                OnVictoryAchieved?.Invoke();
            }

            OnInfectedCountChanged?.Invoke(m_infectedCount);
            GameEvents.RaiseInfectionCountChanged(m_infectedCount, target);
        }

        private void HandleTimerEnd()
        {
            EndSession();
        }

        private void HandleFrenzyStarted()
        {
            m_frenzyStarted = true;
            m_infectedCountAtFrenzyStart = m_infectedCount;
        }

        private string BuildUpgradeSummary()
        {
            if (m_upgradeSystem == null || m_upgradeSystem.SessionState == null)
            {
                return "无";
            }

            SessionUpgradeState ss = m_upgradeSystem.SessionState;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            if (ss.ChainPlusOneStacks > 0) sb.Append($"连锁+1 x{ss.ChainPlusOneStacks}｜");
            if (ss.BurstRadiusUpStacks > 0) sb.Append($"爆发半径 x{ss.BurstRadiusUpStacks}｜");
            if (ss.NewbornRushDurationUpStacks > 0) sb.Append($"新生冲刺 x{ss.NewbornRushDurationUpStacks}｜");
            if (ss.ZombiePerceptionUpStacks > 0) sb.Append($"僵尸感知 x{ss.ZombiePerceptionUpStacks}｜");
            if (ss.FinalFrenzyEarlyStacks > 0) sb.Append("狂潮提前｜");
            if (ss.EchoBurstStacks > 0) sb.Append("回响爆发｜");

            if (sb.Length == 0) return "无";

            // 移除末尾的 ｜
            if (sb.Length > 0 && sb[sb.Length - 1] == '｜')
            {
                sb.Remove(sb.Length - 1, 1);
            }

            return sb.ToString();
        }

        private void OnDestroy()
        {
            GameEvents.OnInfectionSuccess -= HandleInfectionSuccess;
            GameEvents.OnTimerEnd -= HandleTimerEnd;
            GameEvents.OnFinalFrenzyStarted -= HandleFrenzyStarted;
        }
    }
}
