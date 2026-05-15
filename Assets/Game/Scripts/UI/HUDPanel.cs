using System.Collections;
using System.Reflection;
using Game.Config;
using Game.Core;
using Game.Gameplay.Skill;
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

        [Header("目标提示")]
        [Tooltip("下一目标提示文本（可选，未赋值时自动创建）")]
        [SerializeField] private Text m_nextGoalText;

        [Header("移动端布局")]
        [SerializeField] private bool m_applyMobileLayout = true;
        [SerializeField] private bool m_showResourceTextsOnMobile = false;
        [SerializeField] private float m_safeAreaPadding = 24f;

        // ==================== 运行时依赖 ====================

        /// <summary>TimerSystem 引用，用于订阅时间变化事件</summary>
        private TimerSystem m_timerSystem;

        /// <summary>GameSessionController 引用，用于订阅单局事件</summary>
        private GameSessionController m_sessionController;

        /// <summary>缓存狂潮动画协程引用，用于在 HideFrenzyIndicator 时停止</summary>
        private Coroutine m_frenzyCoroutine;

        /// <summary>FrenzyIndicator 的初始锚点位置（动画结束后用于下一局重置）</summary>
        private Vector2 m_frenzyIndicatorInitialPos = Vector2.zero;

        private GameObject m_stageToastRoot;
        private Text m_stageToastText;
        private CanvasGroup m_stageToastCanvasGroup;
        private Coroutine m_stageToastCoroutine;
        private bool m_introStageShown;
        private bool m_midStageShown;
        private bool m_preFrenzyWarningShown;
        private bool m_finalPushShown;
        private bool m_isSessionPlaying;
        private bool m_frenzyActive;
        private GameConfig m_cachedGameConfig;
        private UpgradeSystem m_cachedUpgradeSystem;

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
            ApplyMobileLayout();
        }

        private void OnEnable()
        {
            ApplyMobileLayout();

            GameEvents.OnExpChanged += UpdateExp;
            GameEvents.OnGoldChanged += UpdateGold;
            GameEvents.OnLevelUp += UpdateLevel;
            GameEvents.OnInfectionCountChanged += UpdateInfectionCount;
            GameEvents.OnFinalFrenzyStarted += ShowFrenzyIndicator;
            GameEvents.OnSessionStateChanged += HandleSessionStateChanged;

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

                if (m_sessionController.CurrentState == SessionState.Playing)
                {
                    BeginStagePrompts();
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
            GameEvents.OnSessionStateChanged -= HandleSessionStateChanged;

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

            UpdateStagePrompts(remainingSeconds);
        }

        private void UpdateExp(int currentExp)
        {
            if (m_expText != null)
            {
                m_expText.gameObject.SetActive(!m_applyMobileLayout || m_showResourceTextsOnMobile);
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
                m_goldText.gameObject.SetActive(!m_applyMobileLayout || m_showResourceTextsOnMobile);
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

            // 更新下一目标提示
            UpdateNextGoalText();
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
                m_ratingPreviewText.color = GetRatingColor(rating);
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
        /// 显示末日狂潮提示，带放大淡入动画。
        /// 订阅 <see cref="GameEvents.OnFinalFrenzyStarted"/> 事件。
        /// </summary>
        public void ShowFrenzyIndicator()
        {
            EnsureFrenzyIndicatorBuilt();
            if (m_frenzyIndicator == null)
            {
                return;
            }

            m_frenzyActive = true;

            // 停止上一次可能还在播放的动画
            if (m_frenzyCoroutine != null)
            {
                StopCoroutine(m_frenzyCoroutine);
                m_frenzyCoroutine = null;
            }

            // 重置到初始状态，避免上一局动画残留
            RectTransform rect = m_frenzyIndicator.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = m_frenzyIndicatorInitialPos;
                rect.localScale = Vector3.one;
            }

            CanvasGroup cg = m_frenzyIndicator.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = m_frenzyIndicator.AddComponent<CanvasGroup>();
            }
            cg.alpha = 0f;

            m_frenzyIndicator.SetActive(true);
            m_frenzyCoroutine = StartCoroutine(FrenzyIndicatorAnimation());
            ShowStageToast("FINAL FRENZY STARTED", new Color(1f, 0.35f, 0.12f, 1f), 2.2f);

            // 狂潮开始时立刻刷新目标提示，切换为冲刺语气
            UpdateNextGoalText();
        }

        /// <summary>
        /// 隐藏末日狂潮提示，并停止正在播放的动画协程。
        /// </summary>
        public void HideFrenzyIndicator()
        {
            if (m_frenzyCoroutine != null)
            {
                StopCoroutine(m_frenzyCoroutine);
                m_frenzyCoroutine = null;
            }

            if (m_frenzyIndicator != null)
            {
                m_frenzyIndicator.SetActive(false);
            }

            m_frenzyActive = false;
        }

        public void ShowUpgradeFeedback(string upgradeName)
        {
            if (string.IsNullOrWhiteSpace(upgradeName))
            {
                ShowStageToast("UPGRADE ACQUIRED", new Color(0.6f, 1f, 0.55f, 1f), 1.5f);
                return;
            }

            ShowStageToast($"UPGRADE: {upgradeName}", new Color(0.6f, 1f, 0.55f, 1f), 1.7f);
        }

        /// <summary>
        /// 末日狂潮提示动画：放大淡入 → 保持 → 缩小到角落持续显示。
        /// </summary>
        private IEnumerator FrenzyIndicatorAnimation()
        {
            if (m_frenzyIndicator == null)
            {
                yield break;
            }

            RectTransform rect = m_frenzyIndicator.GetComponent<RectTransform>();
            CanvasGroup cg = m_frenzyIndicator.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = m_frenzyIndicator.AddComponent<CanvasGroup>();
            }

            // 阶段 1：放大淡入（0.3 秒）
            float fadeInDuration = 0.3f;
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeInDuration);
                float scale = Mathf.Lerp(2.5f, 1f, t);
                rect.localScale = new Vector3(scale, scale, 1f);
                cg.alpha = t;
                yield return null;
            }
            rect.localScale = Vector3.one;
            cg.alpha = 1f;

            // 阶段 2：保持 1.5 秒
            yield return new WaitForSeconds(1.5f);

            // 阶段 3：缩小并移到右上角（0.4 秒）
            float shrinkDuration = 0.4f;
            elapsed = 0f;
            Vector2 startPos = rect.anchoredPosition;
            Vector2 endPos = new Vector2(116f, -108f);
            while (elapsed < shrinkDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / shrinkDuration);
                float scale = Mathf.Lerp(1f, 0.6f, t);
                rect.localScale = new Vector3(scale, scale, 1f);
                rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                cg.alpha = Mathf.Lerp(1f, 0.7f, t);
                yield return null;
            }
            rect.localScale = new Vector3(0.6f, 0.6f, 1f);
            rect.anchoredPosition = endPos;
            cg.alpha = 0.7f;

            // 动画完成，清空协程引用
            m_frenzyCoroutine = null;
        }

        // ==================== 目标提示 ====================

        /// <summary>
        /// 更新"下一目标提示"文本，从 GameSessionController 获取。
        /// </summary>
        private void UpdateNextGoalText()
        {
            EnsureNextGoalText();
            if (m_nextGoalText != null && m_sessionController != null)
            {
                m_nextGoalText.text = m_sessionController.GetNextGoalText();
            }
        }

        /// <summary>
        /// 如果 m_nextGoalText 未赋值，运行时自动创建。
        /// </summary>
        private void EnsureNextGoalText()
        {
            if (m_nextGoalText != null)
            {
                return;
            }

            GameObject go = new GameObject("NextGoalText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(transform, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.89f);
            rect.anchorMax = new Vector2(0.5f, 0.89f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(360f, 32f);

            Text text = go.GetComponent<Text>();
            text.text = "";
            text.fontSize = 16;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.9f, 0.9f, 0.6f, 1f);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            m_nextGoalText = text;
            ApplyMobileLayoutToText(m_nextGoalText, new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(360f, 32f), 16, TextAnchor.MiddleCenter);
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
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -108f);
            rect.sizeDelta = new Vector2(360f, 54f);

            Text text = go.GetComponent<Text>();
            text.text = "末日狂潮";
            text.fontSize = 28;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.3f, 0.1f, 1f);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 添加 CanvasGroup 用于动画控制
            CanvasGroup cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            m_frenzyIndicator = go;
            m_frenzyIndicatorInitialPos = new Vector2(0f, -108f); // 记录初始位置
            go.SetActive(false);
        }

        private void HandleSessionStateChanged(SessionState state)
        {
            if (state == SessionState.Playing)
            {
                BeginStagePrompts();
                return;
            }

            m_isSessionPlaying = false;
        }

        private void BeginStagePrompts()
        {
            m_isSessionPlaying = true;
            m_introStageShown = false;
            m_midStageShown = false;
            m_preFrenzyWarningShown = false;
            m_finalPushShown = false;
            m_frenzyActive = false;
            HideStageToast();
        }

        private void UpdateStagePrompts(float remainingSeconds)
        {
            if (!m_isSessionPlaying)
            {
                return;
            }

            float totalDuration = m_timerSystem != null ? Mathf.Max(1f, m_timerSystem.TotalDuration) : 180f;
            float elapsed = Mathf.Max(0f, totalDuration - remainingSeconds);

            if (!m_introStageShown && elapsed >= 1f)
            {
                m_introStageShown = true;
                ShowStageToast("OUTBREAK STARTED", new Color(1f, 0.72f, 0.28f, 1f), 1.4f);
            }

            if (!m_midStageShown && elapsed >= 30f)
            {
                m_midStageShown = true;
                ShowStageToast("CHAIN ONLINE", new Color(0.5f, 1f, 0.55f, 1f), 1.4f);
            }

            float frenzyStartRemainingTime = GetFrenzyStartRemainingTime();
            float frenzyWarningTime = frenzyStartRemainingTime + 12f;
            if (!m_preFrenzyWarningShown && !m_frenzyActive &&
                remainingSeconds <= frenzyWarningTime && remainingSeconds > frenzyStartRemainingTime)
            {
                m_preFrenzyWarningShown = true;
                ShowStageToast("FRENZY INCOMING", new Color(1f, 0.56f, 0.2f, 1f), 1.6f);
            }

            if (!m_finalPushShown && remainingSeconds <= 15f)
            {
                m_finalPushShown = true;
                ShowStageToast("FINAL PUSH", new Color(1f, 0.92f, 0.35f, 1f), 1.5f);
            }
        }

        private void ShowStageToast(string message, Color color, float holdDuration)
        {
            EnsureStageToastBuilt();
            if (m_stageToastRoot == null || m_stageToastText == null || m_stageToastCanvasGroup == null)
            {
                return;
            }

            m_stageToastText.text = message;
            m_stageToastText.color = color;
            m_stageToastRoot.SetActive(true);

            if (m_stageToastCoroutine != null)
            {
                StopCoroutine(m_stageToastCoroutine);
            }

            m_stageToastCoroutine = StartCoroutine(StageToastAnimation(holdDuration));
        }

        private void HideStageToast()
        {
            if (m_stageToastCoroutine != null)
            {
                StopCoroutine(m_stageToastCoroutine);
                m_stageToastCoroutine = null;
            }

            if (m_stageToastRoot != null)
            {
                m_stageToastRoot.SetActive(false);
            }

            if (m_stageToastCanvasGroup != null)
            {
                m_stageToastCanvasGroup.alpha = 0f;
            }
        }

        private IEnumerator StageToastAnimation(float holdDuration)
        {
            RectTransform rect = m_stageToastRoot.GetComponent<RectTransform>();
            float elapsed = 0f;
            const float fadeInDuration = 0.12f;
            const float fadeOutDuration = 0.25f;

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeInDuration);
                m_stageToastCanvasGroup.alpha = t;
                rect.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, t);
                yield return null;
            }

            m_stageToastCanvasGroup.alpha = 1f;
            rect.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(holdDuration);

            elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeOutDuration);
                m_stageToastCanvasGroup.alpha = 1f - t;
                yield return null;
            }

            m_stageToastCanvasGroup.alpha = 0f;
            m_stageToastRoot.SetActive(false);
            m_stageToastCoroutine = null;
        }

        private void EnsureStageToastBuilt()
        {
            if (m_stageToastText != null)
            {
                return;
            }

            GameObject root = new GameObject("StageToast", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = new Vector2(0f, -150f);
            rootRect.sizeDelta = new Vector2(360f, 42f);

            Image bg = root.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.035f, 0.03f, 0.72f);

            CanvasGroup cg = root.GetComponent<CanvasGroup>();
            cg.alpha = 0f;

            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(root.transform, false);

            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textGo.GetComponent<Text>();
            text.text = string.Empty;
            text.fontSize = 20;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            m_stageToastRoot = root;
            m_stageToastText = text;
            m_stageToastCanvasGroup = cg;
            root.SetActive(false);
        }

        private void ApplyMobileLayout()
        {
            if (!m_applyMobileLayout)
            {
                return;
            }

            RectTransform root = transform as RectTransform;
            MobileSafeAreaUtility.ApplySafeArea(root, m_safeAreaPadding);

            ApplyMobileLayoutToText(m_timeText, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(180f, 42f), 30, TextAnchor.MiddleCenter);
            ApplyMobileLayoutToText(m_infectionCountText, new Vector2(0f, 1f), new Vector2(82f, -34f), new Vector2(150f, 34f), 22, TextAnchor.MiddleLeft);
            ApplyMobileLayoutToText(m_ratingPreviewText, new Vector2(1f, 1f), new Vector2(-58f, -34f), new Vector2(92f, 34f), 24, TextAnchor.MiddleRight);
            ApplyMobileLayoutToText(m_levelText, new Vector2(0f, 1f), new Vector2(82f, -68f), new Vector2(150f, 28f), 17, TextAnchor.MiddleLeft);
            ApplyMobileLayoutToText(m_nextGoalText, new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(360f, 32f), 16, TextAnchor.MiddleCenter);
            ApplyMobileLayoutToObject(m_frenzyIndicator, new Vector2(0.5f, 1f), new Vector2(0f, -108f), new Vector2(360f, 54f), 28);
            ApplyMobileLayoutToObject(m_victoryIndicator, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(320f, 44f), 22);

            if (m_expText != null)
            {
                m_expText.gameObject.SetActive(m_showResourceTextsOnMobile);
                ApplyMobileLayoutToText(m_expText, new Vector2(0f, 1f), new Vector2(82f, -96f), new Vector2(150f, 24f), 14, TextAnchor.MiddleLeft);
            }

            if (m_goldText != null)
            {
                m_goldText.gameObject.SetActive(m_showResourceTextsOnMobile);
                ApplyMobileLayoutToText(m_goldText, new Vector2(0f, 1f), new Vector2(82f, -120f), new Vector2(150f, 24f), 14, TextAnchor.MiddleLeft);
            }
        }

        private void ApplyMobileLayoutToText(Text text, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment)
        {
            if (text == null)
            {
                return;
            }

            RectTransform rect = text.rectTransform;
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private void ApplyMobileLayoutToObject(GameObject target, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, int fontSize)
        {
            if (target == null)
            {
                return;
            }

            RectTransform rect = target.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = anchor;
                rect.anchorMax = anchor;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = size;
            }

            Text text = target.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.fontSize = fontSize;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }

        private float GetFrenzyStartRemainingTime()
        {
            GameConfig config = ResolveGameConfig();
            float startRemainingTime = config != null ? config.FinalFrenzyStartRemainingTime : 40f;

            UpgradeSystem upgradeSystem = ResolveUpgradeSystem();
            if (upgradeSystem != null && upgradeSystem.SessionState != null)
            {
                startRemainingTime = upgradeSystem.SessionState.GetFinalFrenzyStartTime(startRemainingTime);
            }

            return startRemainingTime;
        }

        private GameConfig ResolveGameConfig()
        {
            if (m_cachedGameConfig != null)
            {
                return m_cachedGameConfig;
            }

            if (m_sessionController == null)
            {
                return null;
            }

            FieldInfo field = typeof(GameSessionController).GetField("m_config", BindingFlags.Instance | BindingFlags.NonPublic);
            m_cachedGameConfig = field != null ? field.GetValue(m_sessionController) as GameConfig : null;
            return m_cachedGameConfig;
        }

        private UpgradeSystem ResolveUpgradeSystem()
        {
            if (m_cachedUpgradeSystem != null)
            {
                return m_cachedUpgradeSystem;
            }

            if (m_sessionController == null)
            {
                return null;
            }

            FieldInfo field = typeof(GameSessionController).GetField("m_upgradeSystem", BindingFlags.Instance | BindingFlags.NonPublic);
            m_cachedUpgradeSystem = field != null ? field.GetValue(m_sessionController) as UpgradeSystem : null;
            return m_cachedUpgradeSystem;
        }

        private Color GetRatingColor(SessionRating rating)
        {
            switch (rating)
            {
                case SessionRating.SS:
                    return new Color(1f, 0.82f, 0.18f, 1f);
                case SessionRating.S:
                    return new Color(1f, 0.48f, 0.18f, 1f);
                case SessionRating.A:
                    return new Color(0.45f, 0.95f, 0.55f, 1f);
                case SessionRating.B:
                    return new Color(0.7f, 0.86f, 1f, 1f);
                default:
                    return Color.white;
            }
        }
    }
}
