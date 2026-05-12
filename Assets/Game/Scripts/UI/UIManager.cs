using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// UI 面板管理器。
    /// 管理所有 UI 面板（StartPanel、HUDPanel、UpgradePanel、SettlementPanel）的显示/隐藏。
    /// 通过 Inspector 注入各面板引用，根据游戏状态切换面板可见性。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        // ==================== Inspector 注入 ====================

        [Header("面板引用")]
        [Tooltip("开始界面面板")]
        [SerializeField] private GameObject m_startPanel;

        [Tooltip("游戏内 HUD 面板")]
        [SerializeField] private GameObject m_hudPanel;

        [Tooltip("升级选择面板")]
        [SerializeField] private GameObject m_upgradePanel;

        [Tooltip("结算面板")]
        [SerializeField] private GameObject m_settlementPanel;

        // ==================== 初始化 ====================

        /// <summary>
        /// 初始化 UI 管理器，隐藏所有面板后显示开始界面。
        /// </summary>
        public void Initialize()
        {
            HideAll();
            ShowStartPanel();
        }

        // ==================== 面板控制 ====================

        /// <summary>显示开始界面，隐藏其他面板</summary>
        public void ShowStartPanel()
        {
            HideAll();
            SetActive(m_startPanel, true);
        }

        /// <summary>显示游戏内 HUD，隐藏其他面板</summary>
        public void ShowHUD()
        {
            HideAll();
            SetActive(m_hudPanel, true);
        }

        /// <summary>显示升级选择面板（叠加在 HUD 之上）</summary>
        public void ShowUpgradePanel()
        {
            SetActive(m_upgradePanel, true);
        }

        /// <summary>隐藏升级选择面板</summary>
        public void HideUpgradePanel()
        {
            SetActive(m_upgradePanel, false);
        }

        /// <summary>显示结算面板，隐藏其他面板</summary>
        public void ShowSettlementPanel()
        {
            HideAll();
            SetActive(m_settlementPanel, true);
        }

        /// <summary>隐藏所有面板</summary>
        public void HideAll()
        {
            SetActive(m_startPanel, false);
            SetActive(m_hudPanel, false);
            SetActive(m_upgradePanel, false);
            SetActive(m_settlementPanel, false);
        }

        // ==================== 内部辅助 ====================

        /// <summary>安全设置 GameObject 激活状态，null 时忽略</summary>
        private void SetActive(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }
    }
}
