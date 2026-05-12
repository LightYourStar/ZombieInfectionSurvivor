using Game.Core;
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

        [Header("依赖")]
        [Tooltip("游戏状态管理器")]
        [SerializeField] private GameStateManager m_stateManager;

        private void Awake()
        {
            if (m_startButton != null)
            {
                m_startButton.onClick.AddListener(OnStartButtonClicked);
            }
        }

        private void OnDestroy()
        {
            if (m_startButton != null)
            {
                m_startButton.onClick.RemoveListener(OnStartButtonClicked);
            }
        }

        /// <summary>开始按钮点击回调，切换到 Playing 状态</summary>
        private void OnStartButtonClicked()
        {
            if (m_stateManager != null)
            {
                m_stateManager.ChangeState(GameState.Playing);
            }
        }
    }
}
