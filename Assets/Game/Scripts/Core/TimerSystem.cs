using System;
using Game.Config;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 单局倒计时系统。
    /// 由 GameSystemRunner 在 Playing 状态下统一调度 UpdateTimer。
    /// </summary>
    public class TimerSystem : MonoBehaviour
    {
        [Header("配置")]
        [Tooltip("游戏配置，提供单局总时长 MatchDuration")]
        [SerializeField] private GameConfig m_config;

        private float m_remainingTime;
        private bool m_isRunning;

        public float RemainingTime => m_remainingTime;
        public float TotalDuration => m_config != null ? m_config.MatchDuration : 180f;
        public bool IsRunning => m_isRunning;

        public event Action<float> OnTimeChanged;

        public void StartTimer()
        {
            float duration = TotalDuration;
            if (m_config == null)
            {
                Debug.LogError("[TimerSystem] GameConfig 未赋值，使用默认 180 秒作为单局时长");
            }

            m_remainingTime = duration;
            m_isRunning = true;
            OnTimeChanged?.Invoke(m_remainingTime);
        }

        public void StopTimer()
        {
            m_isRunning = false;
        }

        public void ResumeTimer()
        {
            m_isRunning = true;
        }

        public void Reset()
        {
            StopTimer();
            m_remainingTime = TotalDuration;
        }

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
