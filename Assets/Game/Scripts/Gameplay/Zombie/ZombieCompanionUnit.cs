using UnityEngine;

namespace Game.Gameplay.Zombie
{
    /// <summary>
    /// 僵尸同伴单位的行为状态。
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
    /// 挂载在 ZombieCompanion 预制体根节点上，负责承载运行时状态和类型信息。
    /// </summary>
    public class ZombieCompanionUnit : MonoBehaviour
    {
        // ==================== 运行时状态 ====================

        private ZombieCompanionState m_currentState = ZombieCompanionState.Following;

        /// <summary>僵尸类型（运行时由 InfectionSystem 在转化时设置）</summary>
        private ZombieType m_zombieType = ZombieType.Normal;

        /// <summary>当前行为状态。</summary>
        public ZombieCompanionState CurrentState => m_currentState;

        /// <summary>僵尸类型。</summary>
        public ZombieType UnitType => m_zombieType;

        /// <summary>
        /// 僵尸同伴当前的 2D 世界坐标。
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

        private void OnEnable()
        {
            ResetState();
        }

        // ==================== 状态切换 ====================

        public void ResetState()
        {
            m_currentState = ZombieCompanionState.Following;
            // 类型不在 ResetState 中重置，由 SetZombieType 在转化时设置
        }

        /// <summary>
        /// 设置僵尸类型。由 InfectionSystem 在转化时调用。
        /// </summary>
        public void SetZombieType(ZombieType type)
        {
            m_zombieType = type;
        }

        public void EnterFollowing()
        {
            m_currentState = ZombieCompanionState.Following;
        }

        public void EnterChasing()
        {
            m_currentState = ZombieCompanionState.Chasing;
        }
    }
}
