using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 游戏内 HUD 面板。
    /// 实时显示剩余时间、当前经验/等级、金币数量。
    /// 通过订阅 <see cref="GameEvents"/> 事件自动刷新显示。
    /// </summary>
    public class HUDPanel : MonoBehaviour
    {
        [Header("UI 元素")]
        [Tooltip("剩余时间文本")]
        [SerializeField] private Text m_timeText;

        [Tooltip("经验值文本")]
        [SerializeField] private Text m_expText;

        [Tooltip("等级文本")]
        [SerializeField] private Text m_levelText;

        [Tooltip("金币文本")]
        [SerializeField] private Text m_goldText;

        // ==================== 运行时依赖 ====================

        /// <summary>TimerSystem 引用，用于订阅时间变化事件</summary>
        private TimerSystem m_timerSystem;

        // ==================== 初始化 ====================

        /// <summary>
        /// 注入 TimerSystem 引用并订阅事件。
        /// </summary>
        public void Initialize(TimerSystem timerSystem)
        {
            m_timerSystem = timerSystem;
        }

        private void OnEnable()
        {
            GameEvents.OnExpChanged += UpdateExp;
            GameEvents.OnGoldChanged += UpdateGold;
            GameEvents.OnLevelUp += UpdateLevel;

            if (m_timerSystem != null)
            {
                m_timerSystem.OnTimeChanged += UpdateTime;
            }
        }

        private void OnDisable()
        {
            GameEvents.OnExpChanged -= UpdateExp;
            GameEvents.OnGoldChanged -= UpdateGold;
            GameEvents.OnLevelUp -= UpdateLevel;

            if (m_timerSystem != null)
            {
                m_timerSystem.OnTimeChanged -= UpdateTime;
            }
        }

        // ==================== 刷新方法 ====================

        private void UpdateTime(float remainingSeconds)
        {
            if (m_timeText != null)
            {
                int minutes = Mathf.FloorToInt(remainingSeconds / 60f);
                int seconds = Mathf.FloorToInt(remainingSeconds % 60f);
                m_timeText.text = $"{minutes:00}:{seconds:00}";
            }
        }

        private void UpdateExp(int currentExp)
        {
            if (m_expText != null)
            {
                m_expText.text = $"EXP: {currentExp}";
            }
        }

        private void UpdateLevel(int newLevel)
        {
            if (m_levelText != null)
            {
                m_levelText.text = $"Lv.{newLevel}";
            }
        }

        private void UpdateGold(int currentGold)
        {
            if (m_goldText != null)
            {
                m_goldText.text = $"金币: {currentGold}";
            }
        }
    }
}
