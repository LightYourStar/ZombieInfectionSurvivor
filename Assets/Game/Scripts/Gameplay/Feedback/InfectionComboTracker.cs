using System;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Feedback
{
    /// <summary>
    /// 记录感染连击与大感染事件。
    /// </summary>
    public class InfectionComboTracker : MonoBehaviour
    {
        [Header("连击配置")]
        [Tooltip("超过该时间未发生感染则清空当前连击")]
        [SerializeField] private float m_comboTimeout = 1.5f;

        private const float BurstWindowDuration = 0.3f;
        private const int BurstEventThreshold = 5;

        private int m_currentCombo;
        private int m_maxCombo;
        private float m_timeSinceLastInfection;
        private bool m_comboActive;
        private int m_burstWindowCount;
        private float m_burstWindowTimer;

        public int CurrentCombo => m_currentCombo;
        public int MaxCombo => m_maxCombo;
        public float ComboTimeout => m_comboTimeout;

        public event Action<int> OnComboMilestone;
        public event Action<int> OnComboEnd;
        public event Action<int> OnBurstEvent;

        private void OnEnable()
        {
            GameEvents.OnInfectionSuccess += HandleInfection;
            GameEvents.OnSessionStateChanged += HandleSessionStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnInfectionSuccess -= HandleInfection;
            GameEvents.OnSessionStateChanged -= HandleSessionStateChanged;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            if (m_comboActive)
            {
                m_timeSinceLastInfection += deltaTime;
                if (m_timeSinceLastInfection >= m_comboTimeout)
                {
                    EndCombo();
                }
            }

            if (m_burstWindowCount <= 0)
            {
                return;
            }

            m_burstWindowTimer += deltaTime;
            if (m_burstWindowTimer >= BurstWindowDuration)
            {
                if (m_burstWindowCount >= BurstEventThreshold)
                {
                    OnBurstEvent?.Invoke(m_burstWindowCount);
                }

                m_burstWindowCount = 0;
                m_burstWindowTimer = 0f;
            }
        }

        public void Reset()
        {
            m_currentCombo = 0;
            m_maxCombo = 0;
            m_timeSinceLastInfection = 0f;
            m_comboActive = false;
            m_burstWindowCount = 0;
            m_burstWindowTimer = 0f;
        }

        private void HandleInfection(Vector2 position)
        {
            m_currentCombo++;
            m_timeSinceLastInfection = 0f;
            m_comboActive = true;

            if (m_currentCombo > m_maxCombo)
            {
                m_maxCombo = m_currentCombo;
            }

            if (m_currentCombo == 5 || m_currentCombo == 10 ||
                m_currentCombo == 25 || m_currentCombo == 50 || m_currentCombo == 100)
            {
                OnComboMilestone?.Invoke(m_currentCombo);
            }

            if (m_burstWindowCount == 0)
            {
                m_burstWindowTimer = 0f;
            }

            m_burstWindowCount++;
        }

        private void HandleSessionStateChanged(SessionState state)
        {
            if (state == SessionState.Playing)
            {
                Reset();
            }
        }

        private void EndCombo()
        {
            if (m_currentCombo > 0)
            {
                OnComboEnd?.Invoke(m_currentCombo);
            }

            m_currentCombo = 0;
            m_comboActive = false;
        }
    }
}
