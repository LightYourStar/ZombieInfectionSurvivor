using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 结算面板。
    /// 显示本局感染总数、获得金币、获得经验。
    /// 点击继续按钮重置游戏开始新局。
    /// </summary>
    public class SettlementPanel : MonoBehaviour
    {
        [Header("UI 元素")]
        [Tooltip("感染总数文本")]
        [SerializeField] private Text m_infectionCountText;

        [Tooltip("获得金币文本")]
        [SerializeField] private Text m_goldText;

        [Tooltip("获得经验文本")]
        [SerializeField] private Text m_expText;

        [Tooltip("继续按钮")]
        [SerializeField] private Button m_continueButton;

        // ==================== 运行时状态 ====================

        /// <summary>继续按钮回调</summary>
        private Action m_onContinue;

        // ==================== 生命周期 ====================

        private void Awake()
        {
            if (m_continueButton != null)
            {
                m_continueButton.onClick.AddListener(OnContinueClicked);
            }
        }

        private void OnDestroy()
        {
            if (m_continueButton != null)
            {
                m_continueButton.onClick.RemoveListener(OnContinueClicked);
            }
        }

        // ==================== 公开 API ====================

        /// <summary>
        /// 展示结算数据。
        /// </summary>
        /// <param name="infectionCount">本局感染总数</param>
        /// <param name="gold">获得金币</param>
        /// <param name="exp">获得经验</param>
        /// <param name="onContinue">点击继续后的回调</param>
        public void ShowSettlement(int infectionCount, int gold, int exp, Action onContinue)
        {
            m_onContinue = onContinue;

            if (m_infectionCountText != null)
            {
                m_infectionCountText.text = $"感染: {infectionCount}";
            }
            if (m_goldText != null)
            {
                m_goldText.text = $"金币: {gold}";
            }
            if (m_expText != null)
            {
                m_expText.text = $"经验: {exp}";
            }
        }

        // ==================== 内部回调 ====================

        private void OnContinueClicked()
        {
            m_onContinue?.Invoke();
        }
    }
}
