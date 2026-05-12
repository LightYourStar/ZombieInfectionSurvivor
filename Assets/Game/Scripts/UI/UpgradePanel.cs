using System;
using System.Collections.Generic;
using Game.Gameplay.Skill;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 升级选择面板。
    /// 显示三个升级选项按钮，玩家选择后通知 UpgradeSystem 应用并关闭面板。
    /// </summary>
    public class UpgradePanel : MonoBehaviour
    {
        [Header("UI 元素")]
        [Tooltip("三个升级选项按钮（按顺序对应 DrawOptions 返回的选项）")]
        [SerializeField] private Button[] m_optionButtons = new Button[3];

        [Tooltip("三个升级选项的文本标签")]
        [SerializeField] private Text[] m_optionTexts = new Text[3];

        // ==================== 运行时状态 ====================

        /// <summary>当前展示的选项列表</summary>
        private List<UpgradeOption> m_currentOptions;

        /// <summary>选择回调，由外部（GameSystemRunner）注入</summary>
        private Action<UpgradeOption> m_onOptionSelected;

        // ==================== 生命周期 ====================

        private void Awake()
        {
            for (int i = 0; i < m_optionButtons.Length; i++)
            {
                if (m_optionButtons[i] != null)
                {
                    int index = i; // 闭包捕获
                    m_optionButtons[i].onClick.AddListener(() => OnButtonClicked(index));
                }
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < m_optionButtons.Length; i++)
            {
                if (m_optionButtons[i] != null)
                {
                    m_optionButtons[i].onClick.RemoveAllListeners();
                }
            }
        }

        // ==================== 公开 API ====================

        /// <summary>
        /// 展示升级选项。
        /// </summary>
        /// <param name="options">由 UpgradeSystem.DrawOptions 返回的选项列表</param>
        /// <param name="onSelected">玩家选择后的回调</param>
        public void ShowOptions(List<UpgradeOption> options, Action<UpgradeOption> onSelected)
        {
            m_currentOptions = options;
            m_onOptionSelected = onSelected;

            for (int i = 0; i < m_optionButtons.Length; i++)
            {
                if (i < options.Count)
                {
                    if (m_optionButtons[i] != null)
                    {
                        m_optionButtons[i].gameObject.SetActive(true);
                    }
                    if (m_optionTexts[i] != null)
                    {
                        m_optionTexts[i].text = options[i].DisplayName;
                    }
                }
                else
                {
                    if (m_optionButtons[i] != null)
                    {
                        m_optionButtons[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        // ==================== 内部回调 ====================

        private void OnButtonClicked(int index)
        {
            if (m_currentOptions == null || index < 0 || index >= m_currentOptions.Count)
            {
                return;
            }

            UpgradeOption selected = m_currentOptions[index];
            m_onOptionSelected?.Invoke(selected);
        }
    }
}
