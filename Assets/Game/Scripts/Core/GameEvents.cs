using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 游戏状态机的三种状态，供 <see cref="GameEvents.OnStateChanged"/> 使用
    /// 将其定义在 Core 命名空间顶层，便于全局事件在不直接依赖 GameStateManager 的前提下发布状态变更
    /// GameStateManager 可直接引用该枚举，无需重新声明
    /// </summary>
    public enum GameState
    {
        /// <summary>开始界面</summary>
        Start,
        /// <summary>游戏进行中</summary>
        Playing,
        /// <summary>结算界面</summary>
        Settlement
    }

    /// <summary>
    /// 全局事件总线
    /// 各子系统通过订阅/触发这些静态事件实现松耦合通讯，避免子系统之间的直接引用
    /// 事件命名遵循 On{动作/名词} 规范，触发方法统一以 Raise 前缀暴露，保证事件仅能由声明方负责广播
    /// </summary>
    public static class GameEvents
    {
        // ==================== 感染事件 ====================

        /// <summary>
        /// 感染成功事件，参数为被感染单位的世界坐标，便于 VFX、音效、统计等消费
        /// </summary>
        public static event Action<Vector2> OnInfectionSuccess;

        /// <summary>
        /// 触发感染成功事件
        /// </summary>
        /// <param name="position">被感染单位的世界坐标</param>
        public static void RaiseInfectionSuccess(Vector2 position)
        {
            OnInfectionSuccess?.Invoke(position);
        }

        // ==================== 升级事件 ====================

        /// <summary>
        /// 玩家升级事件，参数为升级后的新等级，由 ExperienceSystem 在经验达到阈值时触发
        /// </summary>
        public static event Action<int> OnLevelUp;

        /// <summary>
        /// 触发玩家升级事件
        /// </summary>
        /// <param name="newLevel">升级后的新等级</param>
        public static void RaiseLevelUp(int newLevel)
        {
            OnLevelUp?.Invoke(newLevel);
        }

        // ==================== 状态切换事件 ====================

        /// <summary>
        /// 游戏状态切换事件，参数为切换后的新状态，由 GameStateManager 在 ChangeState 后触发
        /// </summary>
        public static event Action<GameState> OnStateChanged;

        /// <summary>
        /// 触发游戏状态切换事件
        /// </summary>
        /// <param name="newState">切换后的新状态</param>
        public static void RaiseStateChanged(GameState newState)
        {
            OnStateChanged?.Invoke(newState);
        }

        // ==================== 倒计时事件 ====================

        /// <summary>
        /// 单局倒计时归零事件，由 TimerSystem 在倒计时结束时触发，用于驱动结算流程
        /// </summary>
        public static event Action OnTimerEnd;

        /// <summary>
        /// 触发单局倒计时归零事件
        /// </summary>
        public static void RaiseTimerEnd()
        {
            OnTimerEnd?.Invoke();
        }

        // ==================== 资源数值变化事件 ====================

        /// <summary>
        /// 当前累计经验值变化事件，参数为变化后的当前经验值，用于驱动 HUD 刷新
        /// </summary>
        public static event Action<int> OnExpChanged;

        /// <summary>
        /// 触发经验值变化事件
        /// </summary>
        /// <param name="currentExp">变化后的当前累计经验值</param>
        public static void RaiseExpChanged(int currentExp)
        {
            OnExpChanged?.Invoke(currentExp);
        }

        /// <summary>
        /// 当前金币数量变化事件，参数为变化后的当前金币总量，用于驱动 HUD 刷新
        /// </summary>
        public static event Action<int> OnGoldChanged;

        /// <summary>
        /// 触发金币数量变化事件
        /// </summary>
        /// <param name="currentGold">变化后的当前金币总量</param>
        public static void RaiseGoldChanged(int currentGold)
        {
            OnGoldChanged?.Invoke(currentGold);
        }

        // ==================== 清理 ====================

        /// <summary>
        /// 清空所有事件订阅
        /// 通常在场景卸载或重新进入一局前调用，避免残留的订阅引用导致内存泄漏或逻辑错误
        /// </summary>
        public static void ClearAllSubscriptions()
        {
            OnInfectionSuccess = null;
            OnLevelUp = null;
            OnStateChanged = null;
            OnTimerEnd = null;
            OnExpChanged = null;
            OnGoldChanged = null;
        }
    }
}
