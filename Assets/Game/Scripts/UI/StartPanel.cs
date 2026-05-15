using Game.Core;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 开始界面面板。
    /// 显示开始按钮，点击后通知 GameStateManager 切换到 Playing 状态。
    /// </summary>
    public class StartPanel : MonoBehaviour
    {
        [Header("UI 元素")]
        [Tooltip("开始按钮")]
        [SerializeField] private Button m_startButton;

        [Tooltip("局外升级按钮，可为空，运行时自动创建")]
        [SerializeField] private Button m_metaUpgradeButton;

        [Header("依赖")]
        [Tooltip("游戏状态管理器")]
        [SerializeField] private GameStateManager m_stateManager;

        private Action m_onMetaUpgrade;
        private static Font s_defaultFont;

        private void Awake()
        {
            if (m_startButton != null)
            {
                m_startButton.onClick.AddListener(OnStartButtonClicked);
            }

            EnsureMetaUpgradeButton();
        }

        private void OnDestroy()
        {
            if (m_startButton != null)
            {
                m_startButton.onClick.RemoveListener(OnStartButtonClicked);
            }

            if (m_metaUpgradeButton != null)
            {
                m_metaUpgradeButton.onClick.RemoveListener(OnMetaUpgradeClicked);
            }
        }

        public void Initialize(Action onMetaUpgrade)
        {
            m_onMetaUpgrade = onMetaUpgrade;
            EnsureMetaUpgradeButton();
        }

        /// <summary>开始按钮点击回调，切换到 Playing 状态</summary>
        private void OnStartButtonClicked()
        {
            if (m_stateManager != null)
            {
                m_stateManager.ChangeState(GameState.Playing);
            }
        }

        private void OnMetaUpgradeClicked()
        {
            m_onMetaUpgrade?.Invoke();
        }

        private void EnsureMetaUpgradeButton()
        {
            if (m_metaUpgradeButton != null)
            {
                m_metaUpgradeButton.onClick.RemoveListener(OnMetaUpgradeClicked);
                m_metaUpgradeButton.onClick.AddListener(OnMetaUpgradeClicked);
                return;
            }

            Transform parent = m_startButton != null && m_startButton.transform.parent != null
                ? m_startButton.transform.parent
                : transform;

            GameObject buttonGO = new GameObject(
                "MetaUpgradeButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonGO.transform.SetParent(parent, false);

            RectTransform rect = buttonGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(280f, 58f);

            RectTransform startRect = m_startButton != null ? m_startButton.GetComponent<RectTransform>() : null;
            rect.anchoredPosition = startRect != null
                ? startRect.anchoredPosition + new Vector2(0f, -76f)
                : new Vector2(0f, -76f);

            Image image = buttonGO.GetComponent<Image>();
            image.color = new Color(0.24f, 0.38f, 0.68f, 1f);

            GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGO.transform.SetParent(buttonGO.transform, false);
            RectTransform textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text label = textGO.GetComponent<Text>();
            label.text = "局外升级";
            label.font = GetDefaultFont();
            label.fontSize = 24;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;

            m_metaUpgradeButton = buttonGO.GetComponent<Button>();
            m_metaUpgradeButton.onClick.AddListener(OnMetaUpgradeClicked);
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
