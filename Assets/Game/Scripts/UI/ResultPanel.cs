using System;
using Game.Core;
using Game.Gameplay.Feedback;
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
        [SerializeField] private Text m_infectedCountText;
        [SerializeField] private Text m_maxZombieCountText;
        [SerializeField] private Text m_ratingText;
        [SerializeField] private Text m_victoryStatusText;
        [SerializeField] private Button m_restartButton;

        // 新增结算字段（运行时自动创建）
        private Text m_performanceSummaryText;
        private Text m_maxComboText;
        private Text m_frenzyInfectedText;
        private Text m_playerDirectInfectedText;
        private Text m_zombieInfectedText;
        private Text m_burstInfectedText;
        private Text m_upgradeSummaryText;

        private GameObject m_runtimeRoot;
        private static Font s_defaultFont;
        private const float MobileCardWidth = 440f;
        private const float MobileCardHeight = 680f;

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

            if (m_maxComboText != null)
                m_maxComboText.text = result.MaxCombo > 0 ? $"x{result.MaxCombo}" : "无";

            if (m_frenzyInfectedText != null)
                m_frenzyInfectedText.text = result.FrenzyInfectedCount > 0 ? result.FrenzyInfectedCount.ToString() : "未触发";

            if (m_playerDirectInfectedText != null)
                m_playerDirectInfectedText.text = result.PlayerDirectInfectedCount.ToString();

            if (m_zombieInfectedText != null)
                m_zombieInfectedText.text = result.ZombieInfectedCount.ToString();

            if (m_burstInfectedText != null)
                m_burstInfectedText.text = result.BurstInfectedCount.ToString();

            if (m_ratingText != null)
            {
                m_ratingText.text = result.Rating.ToString();
                ApplyRatingPresentation(result.Rating);
            }

            if (m_victoryStatusText != null)
            {
                m_victoryStatusText.text = result.IsVictory ? "胜利" : "未达成";
                m_victoryStatusText.color = result.IsVictory
                    ? new Color(0.96f, 0.92f, 0.68f, 1f)
                    : new Color(0.88f, 0.88f, 0.92f, 1f);
            }

            if (m_performanceSummaryText != null)
                m_performanceSummaryText.text = BuildPerformanceSummary(result);

            if (m_upgradeSummaryText != null)
                m_upgradeSummaryText.text = result.UpgradeSummary;

            if (m_restartButton != null)
            {
                m_restartButton.onClick.RemoveAllListeners();
                if (onRestart != null)
                {
                    m_restartButton.onClick.AddListener(() => onRestart());
                }
            }

            SetVisible(true);

            if (GameAudioFeedback.Instance != null)
            {
                GameAudioFeedback.Instance.PlayResult();
            }
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
                m_maxComboText != null &&
                m_performanceSummaryText != null &&
                m_playerDirectInfectedText != null &&
                m_zombieInfectedText != null &&
                m_burstInfectedText != null &&
                m_ratingText != null &&
                m_victoryStatusText != null &&
                m_restartButton != null;

            if (hasAllReferences)
            {
                return;
            }

            if (m_runtimeRoot != null)
            {
                Destroy(m_runtimeRoot);
                m_runtimeRoot = null;
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

            GameObject safeRoot = new GameObject("ResultSafeArea", typeof(RectTransform));
            safeRoot.transform.SetParent(m_runtimeRoot.transform, false);
            RectTransform safeRect = safeRoot.GetComponent<RectTransform>();
            MobileSafeAreaUtility.ApplySafeArea(safeRect, 24f);

            GameObject card = new GameObject(
                "ResultCard",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(VerticalLayoutGroup));
            card.transform.SetParent(safeRoot.transform, false);

            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(MobileCardWidth, MobileCardHeight);

            Image cardImage = card.GetComponent<Image>();
            cardImage.color = new Color(0.12f, 0.13f, 0.18f, 0.98f);

            VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 22, 22);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            m_victoryStatusText = CreateStandaloneText(
                "VictoryStatusText",
                card.transform,
                "胜利",
                30,
                new Color(0.96f, 0.92f, 0.68f, 1f),
                FontStyle.Bold);
            SetPreferredHeight(m_victoryStatusText.gameObject, 42f);

            m_performanceSummaryText = CreateStandaloneText(
                "PerformanceSummaryText",
                card.transform,
                "Good run",
                18,
                new Color(1f, 0.82f, 0.28f, 1f),
                FontStyle.Bold);
            SetPreferredHeight(m_performanceSummaryText.gameObject, 30f);

            m_ratingText = CreateRatingText(card.transform);

            Transform statGrid = CreateStatGrid(card.transform);
            m_infectedCountText = CreateStatTile(statGrid, "总感染", "127");
            m_maxZombieCountText = CreateStatTile(statGrid, "最高僵尸", "45");
            m_maxComboText = CreateStatTile(statGrid, "最高连击", "x12");
            m_playerDirectInfectedText = CreateStatTile(statGrid, "主角感染", "32");
            m_zombieInfectedText = CreateStatTile(statGrid, "僵尸感染", "72");
            m_burstInfectedText = CreateStatTile(statGrid, "爆发感染", "23");
            m_frenzyInfectedText = CreateStatTile(statGrid, "狂潮感染", "38");
            m_upgradeSummaryText = CreateWideStatRow(card.transform, "本局升级", "无");

            m_restartButton = CreateButton(card.transform, "RestartButton", "再来一局");
        }

        private Text CreateRatingText(Transform parent)
        {
            Text text = CreateStandaloneText(
                "RatingValue",
                parent,
                "A",
                44,
                Color.white,
                FontStyle.Bold);
            SetPreferredHeight(text.gameObject, 66f);
            return text;
        }

        private Transform CreateStatGrid(Transform parent)
        {
            GameObject grid = new GameObject(
                "HighlightStats",
                typeof(RectTransform),
                typeof(GridLayoutGroup),
                typeof(LayoutElement));
            grid.transform.SetParent(parent, false);

            GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(190f, 54f);
            layout.spacing = new Vector2(10f, 8f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            layout.childAlignment = TextAnchor.MiddleCenter;

            LayoutElement gridLayout = grid.GetComponent<LayoutElement>();
            gridLayout.preferredHeight = 232f;

            return grid.transform;
        }

        private Text CreateStatTile(Transform parent, string label, string exampleValue)
        {
            GameObject tile = new GameObject(
                label + "Tile",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup));
            tile.transform.SetParent(parent, false);

            VerticalLayoutGroup layout = tile.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 0f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Text labelText = CreateStandaloneText(
                "Label",
                tile.transform,
                label,
                15,
                new Color(0.72f, 0.75f, 0.84f, 1f),
                FontStyle.Normal);
            SetPreferredHeight(labelText.gameObject, 22f);

            Text valueText = CreateStandaloneText(
                "Value",
                tile.transform,
                exampleValue,
                22,
                Color.white,
                FontStyle.Bold);
            SetPreferredHeight(valueText.gameObject, 28f);

            return valueText;
        }

        /// <summary>
        /// 创建宽版统计行，用于升级摘要等长文本。标签在上，内容在下，支持换行。
        /// </summary>
        private Text CreateWideStatRow(Transform parent, string label, string exampleValue)
        {
            GameObject row = new GameObject(
                label + "Row",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(LayoutElement),
                typeof(ContentSizeFitter));
            row.transform.SetParent(parent, false);

            VerticalLayoutGroup vlayout = row.GetComponent<VerticalLayoutGroup>();
            vlayout.spacing = 4f;
            vlayout.childAlignment = TextAnchor.UpperCenter;
            vlayout.childControlWidth = true;
            vlayout.childControlHeight = true;
            vlayout.childForceExpandWidth = true;
            vlayout.childForceExpandHeight = false;

            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            rowLayout.preferredHeight = 74f;

            ContentSizeFitter rowFitter = row.GetComponent<ContentSizeFitter>();
            rowFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateStandaloneText(
                label + "Label",
                row.transform,
                label,
                15,
                new Color(0.7f, 0.72f, 0.8f, 1f),
                FontStyle.Normal);

            // 值文本：允许换行，高度自适应
            GameObject valueGo = new GameObject(label + "Value", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            valueGo.transform.SetParent(row.transform, false);

            Text valueText = valueGo.GetComponent<Text>();
            valueText.text = exampleValue;
            valueText.font = GetDefaultFont();
            valueText.fontSize = 15;
            valueText.fontStyle = FontStyle.Normal;
            valueText.alignment = TextAnchor.UpperCenter;
            valueText.color = new Color(0.95f, 0.95f, 0.8f, 1f);
            valueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            valueText.verticalOverflow = VerticalWrapMode.Overflow;

            LayoutElement valueLayout = valueGo.GetComponent<LayoutElement>();
            valueLayout.preferredHeight = 40f;

            RectTransform valueRect = valueGo.GetComponent<RectTransform>();
            valueRect.sizeDelta = new Vector2(0f, 40f);

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
            layout.preferredWidth = 280f;
            layout.preferredHeight = 62f;

            RectTransform rect = buttonGO.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(280f, 62f);

            Text labelText = CreateStandaloneText(
                "Text",
                buttonGO.transform,
                label,
                26,
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

        private void SetPreferredHeight(GameObject go, float height)
        {
            LayoutElement layout = go.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = go.AddComponent<LayoutElement>();
            }

            layout.preferredHeight = height;
        }

        private void ApplyRatingPresentation(SessionRating rating)
        {
            if (m_ratingText == null)
            {
                return;
            }

            m_ratingText.color = GetRatingColor(rating);
            m_ratingText.fontStyle = FontStyle.Bold;
            m_ratingText.fontSize = rating == SessionRating.SS ? 54 : (rating == SessionRating.S ? 50 : 42);
        }

        private Color GetRatingColor(SessionRating rating)
        {
            switch (rating)
            {
                case SessionRating.SS:
                    return new Color(1f, 0.82f, 0.16f, 1f);
                case SessionRating.S:
                    return new Color(1f, 0.48f, 0.16f, 1f);
                case SessionRating.A:
                    return new Color(0.48f, 0.95f, 0.56f, 1f);
                case SessionRating.B:
                    return new Color(0.74f, 0.88f, 1f, 1f);
                default:
                    return Color.white;
            }
        }

        private string BuildPerformanceSummary(SessionResult result)
        {
            if (!result.IsVictory)
            {
                return "差一点成型，开局再快些";
            }

            switch (result.Rating)
            {
                case SessionRating.SS:
                    return "神局爆发，尸潮彻底失控";
                case SessionRating.S:
                    return "强势收割，节奏很稳";
                case SessionRating.A:
                    return result.FrenzyInfectedCount >= 80 ? "狂潮收尾漂亮" : "稳定通关，继续冲 S";
                case SessionRating.B:
                    return result.MaxCombo >= 20 ? "连锁已经成型" : "稳稳过关，连锁还能更高";
                default:
                    return "完成本局，继续压缩起势时间";
            }
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
