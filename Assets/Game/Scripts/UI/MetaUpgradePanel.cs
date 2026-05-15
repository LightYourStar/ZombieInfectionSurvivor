using System;
using Game.Gameplay.Skill;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 局外永久升级面板，运行时自动构建最小可用 UI。
    /// </summary>
    public class MetaUpgradePanel : MonoBehaviour
    {
        private readonly UpgradeType[] m_upgradeTypes =
        {
            UpgradeType.MoveSpeed,
            UpgradeType.InfectionRadius,
            UpgradeType.ZombieCompanionCap
        };

        private readonly UpgradeRow[] m_rows = new UpgradeRow[3];

        private MetaUpgradeSystem m_metaUpgradeSystem;
        private Action m_onUpgradePurchased;
        private Text m_goldText;
        private Text m_hintText;
        private GameObject m_runtimeRoot;
        private static Font s_defaultFont;

        public void Initialize(MetaUpgradeSystem metaUpgradeSystem, Action onUpgradePurchased)
        {
            m_metaUpgradeSystem = metaUpgradeSystem;
            m_onUpgradePurchased = onUpgradePurchased;
            EnsureUIBuilt();
            Refresh();
            HidePanel();
        }

        public void ShowPanel()
        {
            EnsureUIBuilt();
            Refresh();
            SetVisible(true);
        }

        public void HidePanel()
        {
            SetVisible(false);
        }

        private void EnsureUIBuilt()
        {
            if (m_runtimeRoot != null)
            {
                return;
            }

            m_runtimeRoot = new GameObject("MetaUpgradePanelRuntime", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            m_runtimeRoot.transform.SetParent(transform, false);

            RectTransform rootRect = m_runtimeRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image rootImage = m_runtimeRoot.GetComponent<Image>();
            rootImage.color = new Color(0.04f, 0.04f, 0.08f, 0.94f);

            GameObject safeRoot = new GameObject("MetaUpgradeSafeArea", typeof(RectTransform));
            safeRoot.transform.SetParent(m_runtimeRoot.transform, false);
            RectTransform safeRect = safeRoot.GetComponent<RectTransform>();
            MobileSafeAreaUtility.ApplySafeArea(safeRect, 24f);

            GameObject card = new GameObject(
                "MetaUpgradeCard",
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
            cardRect.sizeDelta = new Vector2(450f, 720f);

            Image cardImage = card.GetComponent<Image>();
            cardImage.color = new Color(0.12f, 0.14f, 0.2f, 0.98f);

            VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 22, 22);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Text title = CreateText("Title", card.transform, "局外升级", 30, new Color(1f, 0.86f, 0.28f, 1f), FontStyle.Bold);
            SetPreferredHeight(title.gameObject, 44f);

            m_goldText = CreateText("GoldText", card.transform, "金币: 0", 22, new Color(1f, 0.92f, 0.35f, 1f), FontStyle.Bold);
            SetPreferredHeight(m_goldText.gameObject, 34f);

            for (int i = 0; i < m_upgradeTypes.Length; i++)
            {
                m_rows[i] = CreateUpgradeRow(card.transform, m_upgradeTypes[i]);
            }

            m_hintText = CreateText("HintText", card.transform, string.Empty, 16, new Color(0.9f, 0.74f, 0.32f, 1f), FontStyle.Normal);
            SetPreferredHeight(m_hintText.gameObject, 28f);

            Button closeButton = CreateButton(card.transform, "CloseButton", "返回", new Color(0.26f, 0.32f, 0.42f, 1f));
            closeButton.onClick.AddListener(HidePanel);
        }

        private UpgradeRow CreateUpgradeRow(Transform parent, UpgradeType type)
        {
            GameObject rowRoot = new GameObject(
                type + "Row",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(VerticalLayoutGroup),
                typeof(LayoutElement));
            rowRoot.transform.SetParent(parent, false);

            Image image = rowRoot.GetComponent<Image>();
            image.color = new Color(0.18f, 0.2f, 0.29f, 1f);

            VerticalLayoutGroup layout = rowRoot.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 10, 10);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            LayoutElement rootLayout = rowRoot.GetComponent<LayoutElement>();
            rootLayout.preferredHeight = 142f;

            Text titleText = CreateText("Title", rowRoot.transform, GetDisplayName(type), 21, Color.white, FontStyle.Bold);
            titleText.alignment = TextAnchor.MiddleLeft;
            SetPreferredHeight(titleText.gameObject, 28f);

            Text descText = CreateText("Description", rowRoot.transform, GetDescription(type), 15, new Color(0.73f, 0.77f, 0.86f, 1f), FontStyle.Normal);
            descText.alignment = TextAnchor.MiddleLeft;
            SetPreferredHeight(descText.gameObject, 24f);

            Text levelText = CreateText("Level", rowRoot.transform, "Lv.0 / 5", 15, new Color(0.9f, 0.94f, 1f, 1f), FontStyle.Normal);
            levelText.alignment = TextAnchor.MiddleLeft;
            SetPreferredHeight(levelText.gameObject, 22f);

            Text nextText = CreateText("NextEffect", rowRoot.transform, "下一级: -", 15, new Color(0.78f, 1f, 0.66f, 1f), FontStyle.Normal);
            nextText.alignment = TextAnchor.MiddleLeft;
            SetPreferredHeight(nextText.gameObject, 22f);

            Button button = CreateButton(rowRoot.transform, "UpgradeButton", "升级", new Color(0.24f, 0.68f, 0.38f, 1f));
            button.onClick.AddListener(() => OnUpgradeClicked(type));

            return new UpgradeRow(levelText, nextText, button, button.GetComponentInChildren<Text>(true));
        }

        private void OnUpgradeClicked(UpgradeType type)
        {
            if (m_metaUpgradeSystem == null)
            {
                SetHint("局外升级系统未绑定");
                return;
            }

            if (m_metaUpgradeSystem.TryUpgrade(type))
            {
                SetHint("升级成功");
                m_onUpgradePurchased?.Invoke();
                Refresh();
                return;
            }

            SetHint("金币不足或已满级");
            Refresh();
        }

        private void Refresh()
        {
            if (m_metaUpgradeSystem == null)
            {
                if (m_goldText != null)
                {
                    m_goldText.text = "金币: 未绑定";
                }
                return;
            }

            MetaUpgradeData data = m_metaUpgradeSystem.Load();
            if (m_goldText != null)
            {
                m_goldText.text = $"金币: {data.TotalGold}";
            }

            for (int i = 0; i < m_upgradeTypes.Length; i++)
            {
                UpgradeType type = m_upgradeTypes[i];
                UpgradeRow row = m_rows[i];
                if (row == null)
                {
                    continue;
                }

                int level = m_metaUpgradeSystem.GetLevel(type);
                int maxLevel = m_metaUpgradeSystem.GetMaxLevel(type);
                int nextCost = m_metaUpgradeSystem.GetNextCost(type);
                float currentBonus = m_metaUpgradeSystem.GetBonusAtLevel(type, level);
                float nextBonus = m_metaUpgradeSystem.GetBonusAtLevel(type, Mathf.Min(level + 1, maxLevel));
                bool isMax = level >= maxLevel;
                bool canBuy = !isMax && nextCost >= 0 && data.TotalGold >= nextCost;

                row.LevelText.text = $"Lv.{level} / {maxLevel}";
                row.NextText.text = isMax
                    ? $"已满级: {FormatBonus(type, currentBonus)}"
                    : $"下一级: {FormatBonus(type, currentBonus)} -> {FormatBonus(type, nextBonus)}";

                row.Button.interactable = canBuy;
                Image buttonImage = row.Button.GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.color = canBuy
                        ? new Color(0.24f, 0.68f, 0.38f, 1f)
                        : new Color(0.28f, 0.3f, 0.36f, 1f);
                }

                if (row.ButtonText != null)
                {
                    row.ButtonText.text = isMax ? "已满级" : $"升级 {nextCost}";
                }
            }
        }

        private void SetHint(string message)
        {
            if (m_hintText != null)
            {
                m_hintText.text = message;
            }
        }

        private static string GetDisplayName(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.MoveSpeed:
                    return "初始移动速度";
                case UpgradeType.InfectionRadius:
                    return "初始感染半径";
                case UpgradeType.ZombieCompanionCap:
                    return "僵尸上限";
                default:
                    return type.ToString();
            }
        }

        private static string GetDescription(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.MoveSpeed:
                    return "每级小幅提高开局移动速度";
                case UpgradeType.InfectionRadius:
                    return "每级小幅提高开局感染半径";
                case UpgradeType.ZombieCompanionCap:
                    return "每级提高可同时保留的僵尸数量";
                default:
                    return string.Empty;
            }
        }

        private static string FormatBonus(UpgradeType type, float bonus)
        {
            switch (type)
            {
                case UpgradeType.MoveSpeed:
                    return $"+{bonus:F1} 速度";
                case UpgradeType.InfectionRadius:
                    return $"+{bonus:F2} 半径";
                case UpgradeType.ZombieCompanionCap:
                    return $"+{Mathf.RoundToInt(bonus)} 上限";
                default:
                    return $"+{bonus:F2}";
            }
        }

        private Button CreateButton(Transform parent, string name, string label, Color color)
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
            image.color = color;

            LayoutElement layout = buttonGO.GetComponent<LayoutElement>();
            layout.preferredHeight = 46f;

            Text labelText = CreateText("Text", buttonGO.transform, label, 20, Color.white, FontStyle.Bold);
            RectTransform labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return buttonGO.GetComponent<Button>();
        }

        private Text CreateText(string name, Transform parent, string content, int fontSize, Color color, FontStyle style)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            text.text = content;
            text.font = GetDefaultFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            return text;
        }

        private static void SetPreferredHeight(GameObject go, float height)
        {
            LayoutElement layout = go.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = go.AddComponent<LayoutElement>();
            }
            layout.preferredHeight = height;
        }

        private void SetVisible(bool visible)
        {
            if (m_runtimeRoot != null)
            {
                m_runtimeRoot.SetActive(visible);
            }
        }

        private static Font GetDefaultFont()
        {
            if (s_defaultFont == null)
            {
                s_defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return s_defaultFont;
        }

        private sealed class UpgradeRow
        {
            public UpgradeRow(Text levelText, Text nextText, Button button, Text buttonText)
            {
                LevelText = levelText;
                NextText = nextText;
                Button = button;
                ButtonText = buttonText;
            }

            public Text LevelText { get; }
            public Text NextText { get; }
            public Button Button { get; }
            public Text ButtonText { get; }
        }
    }
}
