using UnityEngine;

namespace Game.Gameplay.Enemy
{
    /// <summary>
    /// 人类单位的行为状态。
    /// 由 <see cref="HumanUnit"/> 暴露，供 <see cref="HumanAI"/>、InfectionSystem 等系统读取和切换。
    /// </summary>
    public enum HumanState
    {
        /// <summary>未感知到威胁，执行随机漫游</summary>
        Wandering,
        /// <summary>感知到玩家或僵尸同伴，沿远离威胁的方向逃跑</summary>
        Fleeing,
        /// <summary>已被标记为感染（当帧将被 InfectionSystem 回收并生成 ZombieCompanion）</summary>
        Infected
    }

    /// <summary>
    /// 人类单位组件。
    /// 挂载在 Human 预制体根节点上，负责承载 Human 的运行时状态（当前行为状态、是否已被感染）
    /// 以及对象池激活/回收时的状态重置。
    /// 具体的移动与感知逻辑由 <see cref="HumanAI"/>（task 5.2）实现；数值参数由 <c>GameConfig</c> 统一管理，本组件自身不读取配置。
    /// </summary>
    /// <remarks>
    /// 与 <c>ObjectPoolManager</c> 的协作约定：
    /// <list type="bullet">
    /// <item>对象池 <c>Get()</c> 会激活 GameObject，触发 <see cref="OnEnable"/>，在此统一 <see cref="ResetState"/>，保证每次取出都是干净状态。</item>
    /// <item>对象池 <c>Return()</c> 会禁用 GameObject，触发 <see cref="OnDisable"/>，此时不需要额外清理逻辑（无外部订阅）。</item>
    /// <item>InfectionSystem 在距离判定通过后调用 <see cref="MarkInfected"/>，随后立即将本单位归还池；状态枚举中的 Infected 仅作为一帧内的标记。</item>
    /// </list>
    /// </remarks>
    public class HumanUnit : MonoBehaviour
    {
        // ==================== 运行时状态 ====================

        /// <summary>当前行为状态；私有 set 保证仅能通过本组件公开方法变更，避免外部随意写坏状态</summary>
        private HumanState m_currentState = HumanState.Wandering;

        /// <summary>当前行为状态。</summary>
        public HumanState CurrentState => m_currentState;

        /// <summary>是否已被标记为感染。便于外部在不关心具体枚举的场景下快速判断。</summary>
        public bool IsInfected => m_currentState == HumanState.Infected;

        /// <summary>
        /// 人类当前的 2D 世界坐标。
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
        /// 统一调用 <see cref="ResetState"/> 保证每次从池中取出的 Human 都处于干净的 Wandering 状态，
        /// 避免上一轮使用残留的 Infected/Fleeing 状态污染新一轮行为。
        /// </summary>
        private void OnEnable()
        {
            ResetState();
        }

        // ==================== 状态切换 ====================

        /// <summary>
        /// 将状态重置为 Wandering。
        /// 在激活时自动调用；外部系统一般无需主动调用，除非需要在不禁用 GameObject 的前提下清除感染/逃跑状态。
        /// </summary>
        public void ResetState()
        {
            m_currentState = HumanState.Wandering;
        }

        /// <summary>
        /// 切换到逃跑状态。
        /// 由 <see cref="HumanAI"/> 在感知范围内出现威胁时调用。
        /// 已被感染的单位（<see cref="IsInfected"/> 为 true）不再接受状态切换，保持 Infected 直到被回收。
        /// </summary>
        public void EnterFleeing()
        {
            if (IsInfected)
            {
                return;
            }
            m_currentState = HumanState.Fleeing;
        }

        /// <summary>
        /// 切换到漫游状态。
        /// 由 <see cref="HumanAI"/> 在感知范围内不再存在威胁时调用。
        /// 已被感染的单位不再接受状态切换。
        /// </summary>
        public void EnterWandering()
        {
            if (IsInfected)
            {
                return;
            }
            m_currentState = HumanState.Wandering;
        }

        /// <summary>
        /// 将本单位标记为已感染。
        /// 由 InfectionSystem（task 6.2）在距离判定通过后调用，作为回收前的一帧标记。
        /// 幂等：对已感染的单位再次调用不产生副作用。
        /// </summary>
        public void MarkInfected()
        {
            m_currentState = HumanState.Infected;
        }
    }
}
