using System;
using System.Collections;
using System.Collections.Generic;
using Game.Gameplay.Feedback;
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
        private Coroutine m_selectionCoroutine;
        private bool m_selectionLocked;

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
            m_selectionLocked = false;

            for (int i = 0; i < m_optionButtons.Length; i++)
            {
                if (m_optionButtons[i] != null)
                {
                    m_optionButtons[i].interactable = true;
                    m_optionButtons[i].transform.localScale = Vector3.one;
                }

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
            if (m_selectionLocked)
            {
                return;
            }

            if (m_currentOptions == null || index < 0 || index >= m_currentOptions.Count)
            {
                return;
            }

            UpgradeOption selected = m_currentOptions[index];
            if (m_selectionCoroutine != null)
            {
                StopCoroutine(m_selectionCoroutine);
            }

            m_selectionCoroutine = StartCoroutine(PlaySelectionFeedback(index, selected));
        }

        private IEnumerator PlaySelectionFeedback(int index, UpgradeOption selected)
        {
            m_selectionLocked = true;
            SetButtonsInteractable(false);

            if (GameAudioFeedback.Instance != null)
            {
                GameAudioFeedback.Instance.PlayUpgradeSelected();
            }

            Transform selectedTransform = index >= 0 && index < m_optionButtons.Length && m_optionButtons[index] != null
                ? m_optionButtons[index].transform
                : null;

            if (selectedTransform != null)
            {
                Vector3 originalScale = selectedTransform.localScale;
                yield return ScaleSelectedCard(selectedTransform, originalScale, originalScale * 1.1f, 0.1f);
                yield return ScaleSelectedCard(selectedTransform, selectedTransform.localScale, originalScale, 0.08f);
            }

            m_selectionCoroutine = null;
            m_onOptionSelected?.Invoke(selected);
        }

        private IEnumerator ScaleSelectedCard(Transform target, Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, duration);

            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                target.localScale = Vector3.Lerp(from, to, 1f - Mathf.Pow(1f - t, 3f));
                yield return null;
            }

            target.localScale = to;
        }

        private void SetButtonsInteractable(bool interactable)
        {
            for (int i = 0; i < m_optionButtons.Length; i++)
            {
                if (m_optionButtons[i] != null)
                {
                    m_optionButtons[i].interactable = interactable;
                }
            }
        }
    }
}
