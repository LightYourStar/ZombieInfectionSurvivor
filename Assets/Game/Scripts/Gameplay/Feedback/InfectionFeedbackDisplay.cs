using System.Collections;
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

        [Header("屏幕震动")]
        [SerializeField] private Camera m_camera;
        [SerializeField] private float m_shakeIntensity = 0.15f;
        [SerializeField] private float m_shakeDuration = 0.12f;

        private Coroutine m_comboFadeCoroutine;
        private Coroutine m_shakeCoroutine;
        private Vector3 m_cameraOriginalPos;

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

        public void Initialize(Game.Gameplay.Feedback.InfectionComboTracker comboTracker, Camera camera)
        {
            m_comboTracker = comboTracker;
            if (camera != null)
            {
                m_camera = camera;
            }

            EnsureComboText();

            if (isActiveAndEnabled)
            {
                UnsubscribeEvents();
                SubscribeEvents();
            }
        }

        private void SubscribeEvents()
        {
            if (m_comboTracker == null)
            {
                return;
            }

            m_comboTracker.OnComboMilestone -= ShowComboMilestone;
            m_comboTracker.OnBurstEvent -= HandleBurstEvent;
            m_comboTracker.OnComboMilestone += ShowComboMilestone;
            m_comboTracker.OnBurstEvent += HandleBurstEvent;
        }

        private void UnsubscribeEvents()
        {
            if (m_comboTracker == null)
            {
                return;
            }

            m_comboTracker.OnComboMilestone -= ShowComboMilestone;
            m_comboTracker.OnBurstEvent -= HandleBurstEvent;
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
            if (m_camera != null)
            {
                if (m_shakeCoroutine != null)
                {
                    StopCoroutine(m_shakeCoroutine);
                    m_camera.transform.position = m_cameraOriginalPos;
                }

                m_shakeCoroutine = StartCoroutine(ScreenShake());
            }

            ShowFloatingText($"BURST x{count}!", new Color(1f, 1f, 0.3f), 34);
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

        private IEnumerator ScreenShake()
        {
            if (m_camera == null)
            {
                yield break;
            }

            m_cameraOriginalPos = m_camera.transform.position;
            float elapsed = 0f;

            while (elapsed < m_shakeDuration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - Mathf.Clamp01(elapsed / m_shakeDuration);
                float offsetX = Random.Range(-m_shakeIntensity, m_shakeIntensity) * t;
                float offsetY = Random.Range(-m_shakeIntensity, m_shakeIntensity) * t;
                m_camera.transform.position = m_cameraOriginalPos + new Vector3(offsetX, offsetY, 0f);
                yield return null;
            }

            m_camera.transform.position = m_cameraOriginalPos;
            m_shakeCoroutine = null;
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
            rect.anchorMin = new Vector2(0.5f, 0.65f);
            rect.anchorMax = new Vector2(0.5f, 0.65f);
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
        }
    }
}
