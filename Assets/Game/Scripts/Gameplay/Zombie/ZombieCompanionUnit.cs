using UnityEngine;

namespace Game.Gameplay.Zombie
{
    /// <summary>
    /// 僵尸同伴单位的行为状态。
    /// 由 <see cref="ZombieCompanionUnit"/> 暴露，供 <see cref="ZombieCompanionAI"/> 等系统读取和切换。
    /// </summary>
    public enum ZombieCompanionState
    {
        /// <summary>感知范围内无 Human 目标，朝玩家位置跟随</summary>
        Following,
        /// <summary>感知范围内存在 Human 目标，朝最近 Human 追击</summary>
        Chasing
    }

    /// <summary>
    /// 僵尸同伴单位组件。
    /// 挂载在 ZombieCompanion 预制体根节点上，负责承载 ZombieCompanion 的运行时状态（当前行为状态）
    /// 以及对象池激活/回收时的状态重置。
    /// 具体的移动与感知逻辑由 <see cref="ZombieCompanionAI"/>（task 7.2）实现；数值参数由 <c>GameConfig</c> 统一管理，本组件自身不读取配置。
    /// </summary>
    /// <remarks>
    /// 与 <c>ObjectPoolManager</c> 的协作约定：
    /// <list type="bullet">
    /// <item>对象池 <c>Get()</c> 会激活 GameObject，触发 <see cref="OnEnable"/>，在此统一 <see cref="ResetState"/>，保证每次取出都是干净状态。</item>
    /// <item>对象池 <c>Return()</c> 会禁用 GameObject，触发 <see cref="OnDisable"/>，此时不需要额外清理逻辑（无外部订阅）。</item>
    /// <item>InfectionSystem 在感染转化时从池中取出 ZombieCompanion 并放置到被感染 Human 的位置；新取出的单位默认处于 Following 状态。</item>
    /// </list>
    /// </remarks>
    public class ZombieCompanionUnit : MonoBehaviour
    {
        // ==================== 运行时状态 ====================

        /// <summary>当前行为状态；私有 set 保证仅能通过本组件公开方法变更，避免外部随意写坏状态</summary>
        private ZombieCompanionState m_currentState = ZombieCompanionState.Following;

        /// <summary>当前行为状态。</summary>
        public ZombieCompanionState CurrentState => m_currentState;

        /// <summary>
        /// 僵尸同伴当前的 2D 世界坐标。
        /// 取 <see cref="Transform.position"/> 的 (x, y) 分量，适配 2D 俯视角（XY 平面）场景。
        /// </summary>
        public Vector2 Position
        {
            get
            {
                Vector3 worldPos = transform.position;
                return new Vector2(worldPos.x, worldPos.y);
            }
        }

        // ==================== 生命周期 ====================

        /// <summary>
        /// 被对象池激活时触发。
        /// 统一调用 <see cref="ResetState"/> 保证每次从池中取出的 ZombieCompanion 都处于干净的 Following 状态，
        /// 避免上一轮使用残留的 Chasing 状态污染新一轮行为。
        /// </summary>
        private void OnEnable()
        {
            ResetState();
        }

        // ==================== 状态切换 ====================

        /// <summary>
        /// 将状态重置为 Following。
        /// 在激活时自动调用；外部系统一般无需主动调用，除非需要在不禁用 GameObject 的前提下清除追击状态。
        /// </summary>
        public void ResetState()
        {
            m_currentState = ZombieCompanionState.Following;
        }

        /// <summary>
        /// 切换到跟随状态。
        /// 由 <see cref="ZombieCompanionAI"/> 在感知范围内不再存在 Human 目标时调用。
        /// </summary>
        public void EnterFollowing()
        {
            m_currentState = ZombieCompanionState.Following;
        }

        /// <summary>
        /// 切换到追击状态。
        /// 由 <see cref="ZombieCompanionAI"/> 在感知范围内发现 Human 目标时调用。
        /// </summary>
        public void EnterChasing()
        {
            m_currentState = ZombieCompanionState.Chasing;
        }
    }
}
