using System;
using Game.Config;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 单局倒计时系统。
    /// 从 <see cref="GameConfig.MatchDuration"/> 读取单局总时长，在 Playing 状态下每帧递减剩余时间；
    /// 归零时触发 <see cref="GameEvents.RaiseTimerEnd"/> 驱动结算流程。
    /// 本组件不订阅 Unity Update，由 <see cref="GameSystemRunner"/> 统一调度 <see cref="UpdateTimer"/>。
    /// </summary>
    public class TimerSystem : MonoBehaviour
    {
        // ==================== Inspector 注入 ====================

        [Header("配置")]
        [Tooltip("游戏配置，提供单局时长 MatchDuration")]
        [SerializeField] private GameConfig m_config;

        // ==================== 运行时状态 ====================

        /// <summary>当前剩余时间（秒）</summary>
        private float m_remainingTime;

        /// <summary>倒计时是否正在运行</summary>
        private bool m_isRunning;

        // ==================== 公开属性 ====================

        /// <summary>当前剩余时间（秒），供 HUD 显示</summary>
        public float RemainingTime => m_remainingTime;

        /// <summary>倒计时是否正在运行</summary>
        public bool IsRunning => m_isRunning;

        // ==================== 事件 ====================

        /// <summary>
        /// 剩余时间变化事件，每帧触发一次（仅在运行中），参数为当前剩余秒数。
        /// 供 HUDPanel 订阅以实时刷新倒计时显示。
        /// </summary>
        public event Action<float> OnTimeChanged;

        // ==================== 公开 API ====================

        /// <summary>
        /// 启动倒计时。从 <see cref="GameConfig.MatchDuration"/> 读取总时长并开始递减。
        /// 若 GameConfig 未注入，使用 180 秒作为安全回退值。
        /// </summary>
        public void StartTimer()
        {
            float duration = 180f;
            if (m_config != null)
            {
                duration = m_config.MatchDuration;
            }
            else
            {
                Debug.LogError("[TimerSystem] GameConfig 未赋值，使用默认 180 秒作为单局时长");
            }

            m_remainingTime = duration;
            m_isRunning = true;
            OnTimeChanged?.Invoke(m_remainingTime);
        }

        /// <summary>
        /// 停止倒计时（不重置剩余时间）。
        /// 通常在暂停或升级面板弹出时调用。
        /// </summary>
        public void StopTimer()
        {
            m_isRunning = false;
        }

        /// <summary>
        /// 重置倒计时到满时长并停止运行。
        /// 通常在新一局开始前调用。
        /// </summary>
        public void Reset()
        {
            StopTimer();
            float duration = m_config != null ? m_config.MatchDuration : 180f;
            m_remainingTime = duration;
        }

        /// <summary>
        /// 每帧推进倒计时。由 GameSystemRunner 在 Playing 状态下调用。
        /// 归零时自动停止并触发 <see cref="GameEvents.RaiseTimerEnd"/>。
        /// </summary>
        /// <param name="deltaTime">本帧时间增量</param>
        public void UpdateTimer(float deltaTime)
        {
            if (!m_isRunning || deltaTime <= 0f)
            {
                return;
            }

            m_remainingTime -= deltaTime;

            if (m_remainingTime <= 0f)
            {
                m_remainingTime = 0f;
                m_isRunning = false;
                OnTimeChanged?.Invoke(0f);
                GameEvents.RaiseTimerEnd();
            }
            else
            {
                OnTimeChanged?.Invoke(m_remainingTime);
            }
        }
    }
}
