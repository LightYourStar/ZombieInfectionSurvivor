using System;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 结算面板，时间结束后展示本局成绩与通关状态。
    /// 通过 <see cref="ShowResult"/> 方法接收结算数据并显示。
    /// </summary>
    public class ResultPanel : MonoBehaviour
    {
        [Header("UI 元素")]
        [Tooltip("感染人数文本")]
        [SerializeField] private Text m_infectedCountText;

        [Tooltip("最大尸群数量文本")]
        [SerializeField] private Text m_maxZombieCountText;

        [Tooltip("评级文本")]
        [SerializeField] private Text m_ratingText;

        [Tooltip("通关状态文本（Victory / Failed）")]
        [SerializeField] private Text m_victoryStatusText;

        [Tooltip("重新开始按钮")]
        [SerializeField] private Button m_restartButton;

        private GameObject m_runtimeRoot;
        private static Font s_defaultFont;

        // ==================== 生命周期 ====================

        private void Awake()
        {
            EnsureUIBuilt();
            HideResult();
        }

        // ==================== 公开 API ====================

        /// <summary>
        /// 显示结算面板并填充结算数据。
        /// </summary>
        /// <param name="result">本局结算数据</param>
        /// <param name="onRestart">点击 Restart 按钮时的回调</param>
        public void ShowResult(SessionResult result, Action onRestart)
        {
            EnsureUIBuilt();

            if (m_infectedCountText != null)
                m_infectedCountText.text = result.InfectedCount.ToString();

            if (m_maxZombieCountText != null)
                m_maxZombieCountText.text = result.MaxZombieCount.ToString();

            if (m_ratingText != null)
                m_ratingText.text = result.Rating.ToString();

            if (m_victoryStatusText != null)
                m_victoryStatusText.text = result.IsVictory ? "Victory" : "Failed";

            // 移除之前的监听器，绑定新的回调
            m_restartButton.onClick.RemoveAllListeners();
            if (onRestart != null)
            {
                m_restartButton.onClick.AddListener(() => onRestart());
            }

            SetVisible(true);
        }

        /// <summary>
        /// 隐藏结算面板。
        /// </summary>
        public void HideResult()
        {
            SetVisible(false);
        }

        private void EnsureUIBuilt()
        {
            bool hasAllReferences =
                m_infectedCountText != null &&
                m_maxZombieCountText != null &&
                m_ratingText != null &&
                m_victoryStatusText != null &&
                m_restartButton != null;

            if (hasAllReferences)
            {
                return;
            }

            if (m_runtimeRoot != null)
            {
                return;
            }

            Transform parent = transform.parent != null ? transform.parent : transform;

            m_runtimeRoot = new GameObject("ResultPanelRuntime", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            m_runtimeRoot.transform.SetParent(parent, false);

            RectTransform rootRect = m_runtimeRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image rootImage = m_runtimeRoot.GetComponent<Image>();
            rootImage.color = new Color(0.05f, 0.05f, 0.1f, 0.94f);

            GameObject card = new GameObject(
                "ResultCard",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            card.transform.SetParent(m_runtimeRoot.transform, false);

            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(440f, 0f);

            Image cardImage = card.GetComponent<Image>();
            cardImage.color = new Color(0.12f, 0.13f, 0.18f, 0.98f);

            VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 28, 28);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = card.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            m_victoryStatusText = CreateStandaloneText(
                "VictoryStatusText",
                card.transform,
                "Victory",
                34,
                new Color(0.96f, 0.92f, 0.68f, 1f),
                FontStyle.Bold);

            m_infectedCountText = CreateStatRow(card.transform, "本局累计感染人数", "127");
            m_maxZombieCountText = CreateStatRow(card.transform, "最大僵尸数量", "45");
            m_ratingText = CreateStatRow(card.transform, "最终评级", "A");

            m_restartButton = CreateButton(card.transform, "RestartButton", "再来一局");
        }

        private Text CreateStatRow(Transform parent, string label, string exampleValue)
        {
            GameObject row = new GameObject(
                label + "Row",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            row.transform.SetParent(parent, false);

            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            rowLayout.preferredHeight = 42f;

            Text labelText = CreateStandaloneText(
                label + "Label",
                row.transform,
                label,
                22,
                new Color(0.82f, 0.84f, 0.9f, 1f),
                FontStyle.Normal);
            LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 220f;

            Text valueText = CreateStandaloneText(
                label + "Value",
                row.transform,
                exampleValue,
                24,
                Color.white,
                FontStyle.Bold);
            LayoutElement valueLayout = valueText.gameObject.AddComponent<LayoutElement>();
            valueLayout.preferredWidth = 120f;

            return valueText;
        }

        private Button CreateButton(Transform parent, string name, string label)
        {
            GameObject buttonGO = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonGO.transform.SetParent(parent, false);

            Image image = buttonGO.GetComponent<Image>();
            image.color = new Color(0.24f, 0.68f, 0.38f, 1f);

            LayoutElement layout = buttonGO.GetComponent<LayoutElement>();
            layout.preferredWidth = 220f;
            layout.preferredHeight = 56f;

            RectTransform rect = buttonGO.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220f, 56f);

            Text labelText = CreateStandaloneText(
                "Text",
                buttonGO.transform,
                label,
                24,
                Color.white,
                FontStyle.Bold);
            RectTransform labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return buttonGO.GetComponent<Button>();
        }

        private Text CreateStandaloneText(
            string name,
            Transform parent,
            string content,
            int fontSize,
            Color color,
            FontStyle fontStyle)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            text.text = content;
            text.font = GetDefaultFont();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, fontSize + 16f);

            return text;
        }

        private void SetVisible(bool visible)
        {
            if (m_runtimeRoot != null)
            {
                m_runtimeRoot.SetActive(visible);
                return;
            }

            gameObject.SetActive(visible);
        }

        private static Font GetDefaultFont()
        {
            if (s_defaultFont == null)
            {
                s_defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return s_defaultFont;
        }
    }
}
