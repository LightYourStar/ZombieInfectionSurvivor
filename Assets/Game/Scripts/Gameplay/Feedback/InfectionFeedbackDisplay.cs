using System.Collections;
using Game.Core;
using Game.Gameplay.Feedback;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 感染反馈显示器。
    /// 显示连击里程碑、爆发提示和 Final Frenzy 屏幕边缘反馈。
    /// </summary>
    public class InfectionFeedbackDisplay : MonoBehaviour
    {
        [Header("依赖")]
        [SerializeField] private Game.Gameplay.Feedback.InfectionComboTracker m_comboTracker;

        [Header("连击提示")]
        [SerializeField] private Text m_comboText;
        [SerializeField] private RectTransform m_upgradePanelRect;
        [SerializeField] private float m_upgradePanelTopOffset = 56f;

        [Header("相机引用")]
        [SerializeField] private Camera m_camera;

        [Header("屏幕边缘脉冲")]
        [SerializeField] private float m_edgeThickness = 42f;
        [SerializeField] private float m_frenzyEdgeHoldAlpha = 0.1f;

        private Coroutine m_comboFadeCoroutine;
        private GameObject m_edgePulseRoot;
        private CanvasGroup m_edgePulseCanvasGroup;
        private Image[] m_edgeImages;
        private Coroutine m_edgePulseCoroutine;
        private bool m_frenzyEdgeActive;

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
            HideEdgePulseImmediate();
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
            GameEvents.OnSessionStateChanged -= HandleSessionStateChanged;
            GameEvents.OnFinalFrenzyStarted += HandleFinalFrenzyStarted;
            GameEvents.OnLevelUp += HandleLevelUp;
            GameEvents.OnSessionStateChanged += HandleSessionStateChanged;

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
            GameEvents.OnSessionStateChanged -= HandleSessionStateChanged;

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

            if (combo >= 100)
            {
                text = $"x{combo} 传奇!";
                color = new Color(1f, 0.84f, 0f);
                fontSize = 52;
            }
            else if (combo >= 50)
            {
                text = $"x{combo} 无人能挡!";
                color = new Color(1f, 0.84f, 0f);
                fontSize = 46;
            }
            else if (combo >= 25)
            {
                text = $"x{combo} 势不可挡!";
                color = new Color(1f, 0.2f, 0.1f);
                fontSize = 40;
            }
            else if (combo >= 10)
            {
                text = $"x{combo} 狂暴!";
                color = new Color(1f, 0.5f, 0f);
                fontSize = 34;
            }
            else
            {
                text = $"x{combo} 连击!";
                color = new Color(0.3f, 1f, 0.4f);
                fontSize = 28;
            }

            ShowFloatingText(text, color, fontSize);
            PulseScreenEdge(color, combo >= 50 ? 0.3f : 0.22f, 0.28f);
            if (GameAudioFeedback.Instance != null) GameAudioFeedback.Instance.PlayComboMilestone();
        }

        private void HandleBurstEvent(int count)
        {
            ShowFloatingText($"BURST x{count}!", new Color(1f, 1f, 0.3f), 34);
            PulseScreenEdge(new Color(1f, 0.86f, 0.12f, 1f), 0.18f, 0.24f);
            if (GameAudioFeedback.Instance != null) GameAudioFeedback.Instance.PlayBurst();
        }

        private void HandleFinalFrenzyStarted()
        {
            ShowFloatingText("FINAL FRENZY", new Color(1f, 0.36f, 0.12f), 48);
            StartFinalFrenzyEdgePulse();
            if (GameAudioFeedback.Instance != null) GameAudioFeedback.Instance.PlayFinalFrenzyStart();
        }

        private void HandleLevelUp(int level)
        {
            ShowFloatingText($"LEVEL {level}!", new Color(0.58f, 1f, 0.5f), 34);
        }

        private void HandleSessionStateChanged(SessionState state)
        {
            if (state != SessionState.Playing)
            {
                HideEdgePulseImmediate();
                return;
            }

            HideEdgePulseImmediate();
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

        private void PulseScreenEdge(Color color, float peakAlpha, float duration)
        {
            EnsureEdgePulseBuilt();
            if (m_edgePulseRoot == null)
            {
                return;
            }

            if (m_edgePulseCoroutine != null)
            {
                StopCoroutine(m_edgePulseCoroutine);
            }

            float finalAlpha = m_frenzyEdgeActive ? m_frenzyEdgeHoldAlpha : 0f;
            m_edgePulseCoroutine = StartCoroutine(EdgePulseRoutine(color, peakAlpha, duration, finalAlpha));
        }

        private void StartFinalFrenzyEdgePulse()
        {
            m_frenzyEdgeActive = true;
            PulseScreenEdge(new Color(1f, 0.24f, 0.06f, 1f), 0.42f, 0.72f);
        }

        private IEnumerator EdgePulseRoutine(Color color, float peakAlpha, float duration, float finalAlpha)
        {
            SetEdgeColor(color);
            m_edgePulseRoot.SetActive(true);

            float elapsed = 0f;
            float halfDuration = Mathf.Max(0.04f, duration * 0.5f);
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                m_edgePulseCanvasGroup.alpha = Mathf.Lerp(finalAlpha, peakAlpha, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                m_edgePulseCanvasGroup.alpha = Mathf.Lerp(peakAlpha, finalAlpha, t);
                yield return null;
            }

            m_edgePulseCanvasGroup.alpha = finalAlpha;
            if (finalAlpha <= 0f)
            {
                m_edgePulseRoot.SetActive(false);
            }

            m_edgePulseCoroutine = null;
        }

        private void HideEdgePulseImmediate()
        {
            m_frenzyEdgeActive = false;

            if (m_edgePulseCoroutine != null)
            {
                StopCoroutine(m_edgePulseCoroutine);
                m_edgePulseCoroutine = null;
            }

            if (m_edgePulseCanvasGroup != null)
            {
                m_edgePulseCanvasGroup.alpha = 0f;
            }

            if (m_edgePulseRoot != null)
            {
                m_edgePulseRoot.SetActive(false);
            }
        }

        private void EnsureEdgePulseBuilt()
        {
            if (m_edgePulseRoot != null)
            {
                return;
            }

            GameObject root = new GameObject("EdgePulse", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            m_edgePulseCanvasGroup = root.GetComponent<CanvasGroup>();
            m_edgePulseCanvasGroup.alpha = 0f;
            m_edgePulseCanvasGroup.blocksRaycasts = false;
            m_edgePulseCanvasGroup.interactable = false;

            m_edgeImages = new Image[4];
            m_edgeImages[0] = CreateEdgeImage(root.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, m_edgeThickness));
            m_edgeImages[1] = CreateEdgeImage(root.transform, "Bottom", Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, m_edgeThickness));
            m_edgeImages[2] = CreateEdgeImage(root.transform, "Left", Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(m_edgeThickness, 0f));
            m_edgeImages[3] = CreateEdgeImage(root.transform, "Right", new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(m_edgeThickness, 0f));

            m_edgePulseRoot = root;
            root.SetActive(false);
        }

        private Image CreateEdgeImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = Vector2.zero;

            Image image = go.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private void SetEdgeColor(Color color)
        {
            if (m_edgeImages == null)
            {
                return;
            }

            for (int i = 0; i < m_edgeImages.Length; i++)
            {
                if (m_edgeImages[i] != null)
                {
                    m_edgeImages[i].color = color;
                }
            }
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
