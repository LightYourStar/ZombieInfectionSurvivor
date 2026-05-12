using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 游戏状态机，管理 Start / Playing / Settlement 三种状态的切换。
    /// 通过 <see cref="GameSystemRunner"/> 在 Inspector 中注入引用，场景内唯一实例。
    /// 对外同时提供实例级 <see cref="OnStateChanged"/> 事件（便于 GameSystemRunner 直接订阅）
    /// 以及通过 <see cref="GameEvents.RaiseStateChanged"/> 发布的全局事件（便于 UI、音效等无引用关系的订阅方监听）。
    /// </summary>
    public class GameStateManager : MonoBehaviour
    {
        // ==================== 状态字段 ====================

        [Header("初始状态")]
        [Tooltip("游戏启动后进入的默认状态，默认为 Start（开始界面）")]
        [SerializeField] private GameState m_initialState = GameState.Start;

        /// <summary>
        /// 当前游戏状态。
        /// 初始化前默认为 Start，<see cref="Awake"/> 中会按 <see cref="m_initialState"/> 设置一次。
        /// </summary>
        public GameState CurrentState { get; private set; } = GameState.Start;

        // ==================== 事件 ====================

        /// <summary>
        /// 实例级状态变化事件，仅在状态实际发生改变时触发，参数为切换后的新状态。
        /// </summary>
        public event Action<GameState> OnStateChanged;

        // ==================== 生命周期 ====================

        /// <summary>
        /// 在 Awake 阶段将 CurrentState 设置为 Inspector 配置的初始状态，
        /// 并广播一次状态变更事件，以便订阅方在首帧即可同步到正确状态。
        /// </summary>
        private void Awake()
        {
            // 直接赋值，避免 ChangeState 因相同状态被跳过导致订阅方接收不到初始广播
            CurrentState = m_initialState;
            NotifyStateChanged(m_initialState);
        }

        // ==================== 状态切换 ====================

        /// <summary>
        /// 切换到指定状态。
        /// 若目标状态与当前状态相同则视为空操作，不会重复触发事件。
        /// 切换时先更新 <see cref="CurrentState"/>，再依次触发实例事件和全局事件，
        /// 保证订阅方在回调中读取 CurrentState 时一定能拿到新状态。
        /// </summary>
        /// <param name="newState">目标状态</param>
        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState)
            {
                return;
            }

            CurrentState = newState;
            NotifyStateChanged(newState);
        }

        /// <summary>
        /// 统一广播状态变更，先触发实例事件，再触发全局事件。
        /// </summary>
        /// <param name="newState">切换后的新状态</param>
        private void NotifyStateChanged(GameState newState)
        {
            OnStateChanged?.Invoke(newState);
            GameEvents.RaiseStateChanged(newState);
        }
    }
}
