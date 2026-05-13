using System;
using Game.Config;
using Game.Gameplay.Infection;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 单局状态管理控制器。
    /// 负责管理 Ready / Playing / Result 三种状态以及局内统计数据（感染计数、最大僵尸数、评级等）。
    /// 不替代 GameStateManager，而是在其上层封装单局逻辑。
    /// 由 GameSystemRunner 统一初始化和调度。
    /// </summary>
    public class GameSessionController : MonoBehaviour
    {
        // ==================== Inspector 注入 ====================

        [Header("依赖引用")]
        [Tooltip("游戏配置，提供目标感染数、评级阈值等")]
        [SerializeField] private GameConfig m_config;

        [Tooltip("单局倒计时系统")]
        [SerializeField] private TimerSystem m_timerSystem;

        [Tooltip("感染判定系统，用于获取当前活跃僵尸数量")]
        [SerializeField] private InfectionSystem m_infectionSystem;

        // ==================== 运行时状态 ====================

        /// <summary>当前单局状态</summary>
        private SessionState m_currentState = SessionState.Ready;

        /// <summary>本局累计感染人数</summary>
        private int m_infectedCount;

        /// <summary>本局同时存在的最大僵尸数量</summary>
        private int m_maxZombieCount;

        /// <summary>本局最高连锁感染数（预留字段）</summary>
        private int m_maxChainCount;

        /// <summary>是否达成通关条件</summary>
        private bool m_isVictory;

        /// <summary>当前评级</summary>
        private SessionRating m_currentRating = SessionRating.C;

        /// <summary>单局已用时间（秒）</summary>
        private float m_elapsedTime;

        /// <summary>单局总时长（秒），从 GameConfig 读取</summary>
        private float m_gameDuration;

        // ==================== 公开属性 ====================

        /// <summary>当前单局状态</summary>
        public SessionState CurrentState => m_currentState;

        /// <summary>本局累计感染人数</summary>
        public int InfectedCount => m_infectedCount;

        /// <summary>本局同时存在的最大僵尸数量</summary>
        public int MaxZombieCount => m_maxZombieCount;

        /// <summary>本局最高连锁感染数（预留字段）</summary>
        public int MaxChainCount => m_maxChainCount;

        /// <summary>是否达成通关条件（InfectedCount >= TargetInfectedCount）</summary>
        public bool IsVictory => m_isVictory;

        /// <summary>当前评级</summary>
        public SessionRating CurrentRating => m_currentRating;

        /// <summary>剩余时间（秒），从 TimerSystem 读取</summary>
        /// <summary>Target infected count required for victory.</summary>
        public int TargetInfectedCount => m_config != null ? m_config.TargetInfectedCount : 80;

        /// <summary>Remaining session time in seconds.</summary>
        public float RemainingTime => m_timerSystem != null ? m_timerSystem.RemainingTime : 0f;

        // ==================== 事件 ====================

        /// <summary>感染计数变化事件，参数为当前感染人数</summary>
        public event Action<int> OnInfectedCountChanged;

        /// <summary>评级变化事件</summary>
        public event Action<SessionRating> OnRatingChanged;

        /// <summary>达成通关条件事件</summary>
        public event Action OnVictoryAchieved;

        /// <summary>单局结束事件，参数为结算数据</summary>
        public event Action<SessionResult> OnSessionEnd;

        // ==================== 公开 API ====================

        /// <summary>
        /// 初始化控制器。由 GameSystemRunner 在系统初始化阶段调用。
        /// 订阅感染成功事件和倒计时结束事件。
        /// </summary>
        public void Initialize()
        {
            if (m_config == null)
            {
                Debug.LogError("[GameSessionController] GameConfig 未赋值，使用安全默认值");
            }

            m_gameDuration = m_config != null ? m_config.MatchDuration : 180f;

            // 订阅全局事件
            GameEvents.OnInfectionSuccess += HandleInfectionSuccess;
            GameEvents.OnTimerEnd += HandleTimerEnd;

            m_currentState = SessionState.Ready;
            GameEvents.RaiseSessionStateChanged(m_currentState);
        }

        /// <summary>
        /// 开始新一局。将状态从 Ready 切换到 Playing，重置所有计数并启动倒计时。
        /// </summary>
        public void StartSession()
        {
            // 重置所有计数
            m_infectedCount = 0;
            m_maxZombieCount = 0;
            m_maxChainCount = 0;
            m_isVictory = false;
            m_currentRating = SessionRating.C;
            m_elapsedTime = 0f;
            m_gameDuration = m_config != null ? m_config.MatchDuration : 180f;

            // 切换状态
            m_currentState = SessionState.Playing;
            GameEvents.RaiseSessionStateChanged(m_currentState);

            // 启动倒计时
            if (m_timerSystem != null)
            {
                m_timerSystem.StartTimer();
            }
            else
            {
                Debug.LogWarning("[GameSessionController] TimerSystem 未赋值，跳过倒计时逻辑");
            }

            // 通知感染计数初始值
            int target = m_config != null ? m_config.TargetInfectedCount : 80;
            OnInfectedCountChanged?.Invoke(m_infectedCount);
            GameEvents.RaiseInfectionCountChanged(m_infectedCount, target);
        }

        /// <summary>
        /// 结束当前局。将状态从 Playing 切换到 Result，计算最终评级并触发结算事件。
        /// 通常由 TimerEnd 事件触发。
        /// </summary>
        public void EndSession()
        {
            if (m_currentState != SessionState.Playing)
            {
                return;
            }

            // 停止倒计时
            if (m_timerSystem != null)
            {
                m_timerSystem.StopTimer();
            }

            // 计算最终评级
            m_currentRating = CalculateRating(m_infectedCount);

            // 计算已用时间
            m_elapsedTime = m_gameDuration - (m_timerSystem != null ? m_timerSystem.RemainingTime : 0f);

            // 切换状态
            m_currentState = SessionState.Result;
            GameEvents.RaiseSessionStateChanged(m_currentState);

            // 构建结算数据并触发事件
            var result = new SessionResult(
                m_infectedCount,
                m_maxZombieCount,
                m_maxChainCount,
                m_currentRating,
                m_isVictory,
                m_elapsedTime
            );

            OnSessionEnd?.Invoke(result);
        }

        /// <summary>
        /// 重新开始。将状态从 Result 切换回 Ready，然后立即开始新一局。
        /// </summary>
        public void RestartSession()
        {
            // 切换到 Ready
            m_currentState = SessionState.Ready;
            GameEvents.RaiseSessionStateChanged(m_currentState);

            // 立即开始新一局
            StartSession();
        }

        /// <summary>
        /// 评级计算纯函数。根据感染人数和 GameConfig 中的阈值配置返回对应评级。
        /// 可被 HUD 实时预览复用。
        /// </summary>
        /// <param name="infectedCount">感染人数</param>
        /// <returns>对应的评级</returns>
        public SessionRating CalculateRating(int infectedCount)
        {
            int target = m_config != null ? m_config.TargetInfectedCount : 80;
            int aScore = m_config != null ? m_config.AScoreInfectedCount : 150;
            int sScore = m_config != null ? m_config.SScoreInfectedCount : 250;
            int ssScore = m_config != null ? m_config.SSScoreInfectedCount : 350;

            return CalculateRating(infectedCount, target, aScore, sScore, ssScore);
        }

        /// <summary>
        /// 评级计算静态纯函数。直接接受阈值参数，不依赖任何实例状态。
        /// 便于属性测试和外部系统复用（无需 MonoBehaviour 实例）。
        /// </summary>
        /// <param name="infectedCount">感染人数</param>
        /// <param name="target">通关目标感染人数（C/B 分界）</param>
        /// <param name="aScore">A 级阈值</param>
        /// <param name="sScore">S 级阈值</param>
        /// <param name="ssScore">SS 级阈值</param>
        /// <returns>对应的评级</returns>
        public static SessionRating CalculateRating(int infectedCount, int target, int aScore, int sScore, int ssScore)
        {
            if (infectedCount >= ssScore)
                return SessionRating.SS;
            if (infectedCount >= sScore)
                return SessionRating.S;
            if (infectedCount >= aScore)
                return SessionRating.A;
            if (infectedCount >= target)
                return SessionRating.B;

            return SessionRating.C;
        }

        // ==================== 内部方法 ====================

        /// <summary>
        /// 处理感染成功事件。仅在 Playing 状态下递增感染计数并更新最大僵尸数。
        /// </summary>
        /// <param name="position">被感染单位的世界坐标（本方法不使用）</param>
        private void HandleInfectionSuccess(Vector2 position)
        {
            if (m_currentState != SessionState.Playing)
            {
                return;
            }

            // 递增感染计数
            m_infectedCount++;

            // 更新最大僵尸数量（当前活跃僵尸数 = 已感染数 + 玩家自身，但设计要求追踪活跃僵尸数）
            if (m_infectionSystem != null)
            {
                int currentActiveZombies = m_infectionSystem.ActiveZombieCount;
                if (currentActiveZombies > m_maxZombieCount)
                {
                    m_maxZombieCount = currentActiveZombies;
                }
            }

            // 更新评级
            SessionRating newRating = CalculateRating(m_infectedCount);
            if (newRating != m_currentRating)
            {
                m_currentRating = newRating;
                OnRatingChanged?.Invoke(m_currentRating);
            }

            // 检查是否达成通关条件
            int target = m_config != null ? m_config.TargetInfectedCount : 80;
            if (!m_isVictory && m_infectedCount >= target)
            {
                m_isVictory = true;
                OnVictoryAchieved?.Invoke();
            }

            // 触发事件通知
            OnInfectedCountChanged?.Invoke(m_infectedCount);
            GameEvents.RaiseInfectionCountChanged(m_infectedCount, target);
        }

        /// <summary>
        /// 处理倒计时结束事件。触发结算流程。
        /// </summary>
        private void HandleTimerEnd()
        {
            EndSession();
        }

        // ==================== Unity 生命周期 ====================

        private void OnDestroy()
        {
            // 取消订阅全局事件，避免内存泄漏
            GameEvents.OnInfectionSuccess -= HandleInfectionSuccess;
            GameEvents.OnTimerEnd -= HandleTimerEnd;
        }
    }
}
