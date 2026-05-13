using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 游戏内 HUD 面板。
    /// 实时显示剩余时间、当前经验/等级、金币数量、感染进度、评级预览与目标达成提示。
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

        [Header("感染进度 UI")]
        [Tooltip("感染计数文本，格式为 当前数/目标数")]
        [SerializeField] private Text m_infectionCountText;

        [Tooltip("评级预览文本，显示当前实时评级")]
        [SerializeField] private Text m_ratingPreviewText;

        [Tooltip("目标达成提示对象")]
        [SerializeField] private GameObject m_victoryIndicator;

        [Header("末日狂潮 UI")]
        [Tooltip("末日狂潮提示对象，进入 Final Frenzy 时显示")]
        [SerializeField] private GameObject m_frenzyIndicator;

        // ==================== 运行时依赖 ====================

        /// <summary>TimerSystem 引用，用于订阅时间变化事件</summary>
        private TimerSystem m_timerSystem;

        /// <summary>GameSessionController 引用，用于订阅单局事件</summary>
        private GameSessionController m_sessionController;

        // ==================== 初始化 ====================

        /// <summary>
        /// 注入 TimerSystem 引用并订阅事件。
        /// </summary>
        public void Initialize(TimerSystem timerSystem)
        {
            m_timerSystem = timerSystem;
        }

        /// <summary>
        /// 注入 TimerSystem 和 GameSessionController 引用并订阅事件。
        /// </summary>
        public void Initialize(TimerSystem timerSystem, GameSessionController sessionController)
        {
            m_timerSystem = timerSystem;
            m_sessionController = sessionController;

            // 初始隐藏目标达成提示和末日狂潮提示
            HideVictoryIndicator();
            HideFrenzyIndicator();
        }

        private void OnEnable()
        {
            GameEvents.OnExpChanged += UpdateExp;
            GameEvents.OnGoldChanged += UpdateGold;
            GameEvents.OnLevelUp += UpdateLevel;
            GameEvents.OnInfectionCountChanged += UpdateInfectionCount;
            GameEvents.OnFinalFrenzyStarted += ShowFrenzyIndicator;

            if (m_timerSystem != null)
            {
                m_timerSystem.OnTimeChanged += UpdateTime;
                UpdateTime(m_timerSystem.RemainingTime);
            }

            if (m_sessionController != null)
            {
                m_sessionController.OnRatingChanged += UpdateRatingPreview;
                m_sessionController.OnVictoryAchieved += ShowVictoryIndicator;
                UpdateInfectionCount(m_sessionController.InfectedCount, m_sessionController.TargetInfectedCount);
                UpdateRatingPreview(m_sessionController.CurrentRating);

                if (m_sessionController.IsVictory)
                {
                    ShowVictoryIndicator();
                }
                else
                {
                    HideVictoryIndicator();
                }
            }
        }

        private void OnDisable()
        {
            GameEvents.OnExpChanged -= UpdateExp;
            GameEvents.OnGoldChanged -= UpdateGold;
            GameEvents.OnLevelUp -= UpdateLevel;
            GameEvents.OnInfectionCountChanged -= UpdateInfectionCount;
            GameEvents.OnFinalFrenzyStarted -= ShowFrenzyIndicator;

            if (m_timerSystem != null)
            {
                m_timerSystem.OnTimeChanged -= UpdateTime;
            }

            if (m_sessionController != null)
            {
                m_sessionController.OnRatingChanged -= UpdateRatingPreview;
                m_sessionController.OnVictoryAchieved -= ShowVictoryIndicator;
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

        // ==================== 感染进度刷新方法 ====================

        /// <summary>
        /// 更新感染计数显示，格式为 "当前数/目标数"。
        /// 订阅 <see cref="GameEvents.OnInfectionCountChanged"/> 事件。
        /// </summary>
        /// <param name="current">当前感染人数</param>
        /// <param name="target">目标感染人数</param>
        public void UpdateInfectionCount(int current, int target)
        {
            if (m_infectionCountText != null)
            {
                m_infectionCountText.text = $"{current}/{target}";
            }
        }

        /// <summary>
        /// 更新评级预览显示。
        /// 订阅 <see cref="GameSessionController.OnRatingChanged"/> 事件。
        /// </summary>
        /// <param name="rating">当前实时评级</param>
        public void UpdateRatingPreview(SessionRating rating)
        {
            if (m_ratingPreviewText != null)
            {
                m_ratingPreviewText.text = rating.ToString();
            }
        }

        /// <summary>
        /// 显示目标达成提示。
        /// 订阅 <see cref="GameSessionController.OnVictoryAchieved"/> 事件。
        /// </summary>
        public void ShowVictoryIndicator()
        {
            if (m_victoryIndicator != null)
            {
                m_victoryIndicator.SetActive(true);
            }
        }

        /// <summary>
        /// 隐藏目标达成提示。
        /// </summary>
        public void HideVictoryIndicator()
        {
            if (m_victoryIndicator != null)
            {
                m_victoryIndicator.SetActive(false);
            }
        }

        /// <summary>
        /// 显示末日狂潮提示。
        /// 订阅 <see cref="GameEvents.OnFinalFrenzyStarted"/> 事件。
        /// </summary>
        public void ShowFrenzyIndicator()
        {
            EnsureFrenzyIndicatorBuilt();
            if (m_frenzyIndicator != null)
            {
                m_frenzyIndicator.SetActive(true);
            }
        }

        /// <summary>
        /// 隐藏末日狂潮提示。
        /// </summary>
        public void HideFrenzyIndicator()
        {
            if (m_frenzyIndicator != null)
            {
                m_frenzyIndicator.SetActive(false);
            }
        }

        /// <summary>
        /// 如果 m_frenzyIndicator 未在 Inspector 中赋值，则在运行时自动创建一个简单的文本提示。
        /// </summary>
        private void EnsureFrenzyIndicatorBuilt()
        {
            if (m_frenzyIndicator != null)
            {
                return;
            }

            // 在 HUDPanel 下创建一个居中偏上的 "FINAL FRENZY" 文本
            GameObject go = new GameObject("FrenzyIndicator", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(transform, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.85f);
            rect.anchorMax = new Vector2(0.5f, 0.85f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(400f, 60f);

            Text text = go.GetComponent<Text>();
            text.text = "FINAL FRENZY";
            text.fontSize = 32;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.3f, 0.1f, 1f); // 醒目的橙红色
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            m_frenzyIndicator = go;
            go.SetActive(false); // 默认隐藏
        }
    }
}
