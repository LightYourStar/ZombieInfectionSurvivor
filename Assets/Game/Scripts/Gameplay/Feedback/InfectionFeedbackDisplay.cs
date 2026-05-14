using System.Collections;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 感染反馈显示器。
    /// 在开发环境中显示连击里程碑文本与大感染事件反馈。
    /// </summary>
    public class InfectionFeedbackDisplay : MonoBehaviour
    {
        [Header("依赖")]
        [SerializeField] private Game.Gameplay.Feedback.InfectionComboTracker m_comboTracker;

        [Header("连击提示")]
        [SerializeField] private Text m_comboText;
        [SerializeField] private RectTransform m_upgradePanelRect;
        [SerializeField] private float m_upgradePanelTopOffset = 56f;

        [Header("屏幕震动")]
        [SerializeField] private Camera m_camera;
        [SerializeField] private float m_shakeIntensity = 0.15f;
        [SerializeField] private float m_shakeDuration = 0.12f;

        private Coroutine m_comboFadeCoroutine;
        private Vector3 m_cameraOriginalPos;
        private Coroutine m_shakeCoroutine;

        private void Awake()
        {
            EnsureComboText();
            if (m_camera == null)
            {
                m_camera = Camera.main;
            }
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        public void Initialize(Game.Gameplay.Feedback.InfectionComboTracker comboTracker, Camera camera, RectTransform upgradePanelRect)
        {
            m_comboTracker = comboTracker;
            if (camera != null)
            {
                m_camera = camera;
            }

            m_upgradePanelRect = upgradePanelRect;

            EnsureComboText();
            UpdateFloatingTextPosition();

            if (isActiveAndEnabled)
            {
                UnsubscribeEvents();
                SubscribeEvents();
            }
        }

        private void SubscribeEvents()
        {
            GameEvents.OnFinalFrenzyStarted -= HandleFinalFrenzyStarted;
            GameEvents.OnLevelUp -= HandleLevelUp;
            GameEvents.OnFinalFrenzyStarted += HandleFinalFrenzyStarted;
            GameEvents.OnLevelUp += HandleLevelUp;

            if (m_comboTracker != null)
            {
                m_comboTracker.OnComboMilestone -= ShowComboMilestone;
                m_comboTracker.OnBurstEvent -= HandleBurstEvent;
                m_comboTracker.OnComboMilestone += ShowComboMilestone;
                m_comboTracker.OnBurstEvent += HandleBurstEvent;
            }
        }

        private void UnsubscribeEvents()
        {
            GameEvents.OnFinalFrenzyStarted -= HandleFinalFrenzyStarted;
            GameEvents.OnLevelUp -= HandleLevelUp;

            if (m_comboTracker != null)
            {
                m_comboTracker.OnComboMilestone -= ShowComboMilestone;
                m_comboTracker.OnBurstEvent -= HandleBurstEvent;
            }
        }

        private void ShowComboMilestone(int combo)
        {
            EnsureComboText();
            if (m_comboText == null)
            {
                return;
            }

            string text;
            Color color;
            int fontSize;

            if (combo >= 50)
            {
                text = $"x{combo} LEGENDARY!";
                color = new Color(1f, 0.84f, 0f);
                fontSize = 48;
            }
            else if (combo >= 20)
            {
                text = $"x{combo} UNSTOPPABLE!";
                color = new Color(1f, 0.2f, 0.1f);
                fontSize = 42;
            }
            else if (combo >= 10)
            {
                text = $"x{combo} RAMPAGE!";
                color = new Color(1f, 0.5f, 0f);
                fontSize = 36;
            }
            else
            {
                text = $"x{combo} COMBO!";
                color = new Color(0.3f, 1f, 0.4f);
                fontSize = 30;
            }

            ShowFloatingText(text, color, fontSize);
        }

        private void HandleBurstEvent(int count)
        {
            ShowFloatingText($"BURST x{count}!", new Color(1f, 1f, 0.3f), 34);
        }

        private void HandleFinalFrenzyStarted()
        {
            ShowFloatingText("FINAL FRENZY!", new Color(1f, 0.36f, 0.12f), 44);
        }

        private void HandleLevelUp(int level)
        {
            ShowFloatingText($"LEVEL {level}!", new Color(0.58f, 1f, 0.5f), 34);
        }

        private void ShowFloatingText(string text, Color color, int fontSize)
        {
            EnsureComboText();
            if (m_comboText == null)
            {
                return;
            }

            m_comboText.text = text;
            m_comboText.color = color;
            m_comboText.fontSize = fontSize;
            UpdateFloatingTextPosition();
            m_comboText.gameObject.SetActive(true);

            if (m_comboFadeCoroutine != null)
            {
                StopCoroutine(m_comboFadeCoroutine);
            }

            m_comboFadeCoroutine = StartCoroutine(ComboFadeAnimation());
        }

        private IEnumerator ComboFadeAnimation()
        {
            if (m_comboText == null)
            {
                yield break;
            }

            RectTransform rect = m_comboText.GetComponent<RectTransform>();
            float elapsed = 0f;
            float popDuration = 0.15f;
            Color startColor = m_comboText.color;

            while (elapsed < popDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / popDuration);
                float scale = Mathf.Lerp(1.8f, 1f, t);
                rect.localScale = new Vector3(scale, scale, 1f);

                Color popColor = startColor;
                popColor.a = 1f;
                m_comboText.color = popColor;
                yield return null;
            }

            rect.localScale = Vector3.one;
            m_comboText.color = startColor;

            yield return new WaitForSeconds(1f);

            float fadeDuration = 0.4f;
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                Color color = startColor;
                color.a = 1f - t;
                m_comboText.color = color;
                yield return null;
            }

            m_comboText.gameObject.SetActive(false);
            m_comboFadeCoroutine = null;
        }

        /// <summary>
        /// 屏幕震动已禁用：直接移动摄像机在 2D 俯视角中体验像卡顿而非震动。
        /// 保留方法签名以备后续替换为更合适的反馈方式（如 UI 抖动或后处理）。
        /// </summary>
        private IEnumerator ScreenShake()
        {
            // [已禁用] 震屏在移动端 2D 俯视角中体验为卡顿，暂不启用
            yield break;
        }

        private void EnsureComboText()
        {
            if (m_comboText != null)
            {
                return;
            }

            GameObject go = new GameObject("ComboText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(transform, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.84f);
            rect.anchorMax = new Vector2(0.5f, 0.84f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(500f, 80f);

            Text text = go.GetComponent<Text>();
            text.text = string.Empty;
            text.fontSize = 30;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            m_comboText = text;
            go.SetActive(false);
            UpdateFloatingTextPosition();
        }

        private void UpdateFloatingTextPosition()
        {
            if (m_comboText == null)
            {
                return;
            }

            RectTransform textRect = m_comboText.rectTransform;
            RectTransform rootRect = transform as RectTransform;
            if (textRect == null || rootRect == null)
            {
                return;
            }

            if (m_upgradePanelRect == null)
            {
                textRect.anchorMin = new Vector2(0.5f, 0.84f);
                textRect.anchorMax = new Vector2(0.5f, 0.84f);
                textRect.pivot = new Vector2(0.5f, 0.5f);
                textRect.anchoredPosition = Vector2.zero;
                return;
            }

            Vector3[] corners = new Vector3[4];
            m_upgradePanelRect.GetWorldCorners(corners);
            Vector3 topCenterWorld = (corners[1] + corners[2]) * 0.5f;
            Vector2 topCenterScreen = RectTransformUtility.WorldToScreenPoint(null, topCenterWorld);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, topCenterScreen, null, out Vector2 localPoint))
            {
                textRect.anchorMin = new Vector2(0.5f, 0.5f);
                textRect.anchorMax = new Vector2(0.5f, 0.5f);
                textRect.pivot = new Vector2(0.5f, 0.5f);
                textRect.anchoredPosition = localPoint + Vector2.up * m_upgradePanelTopOffset;
            }
        }
    }
}
