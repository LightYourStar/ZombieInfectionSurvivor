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
    /// </summary>
    public class HumanUnit : MonoBehaviour
    {
        // ==================== 运行时状态 ====================

        /// <summary>当前行为状态</summary>
        private HumanState m_currentState = HumanState.Wandering;

        /// <summary>生成保护时间剩余（秒），> 0 时不参与感染判定</summary>
        private float m_spawnGraceRemaining;

        /// <summary>人类类型（运行时由生成系统设置）</summary>
        private HumanType m_humanType = HumanType.Civilian;

        /// <summary>感染抵抗剩余时间（秒），> 0 时免疫感染</summary>
        private float m_infectionResistRemaining;

        /// <summary>是否已触发过感染抵抗（每次进入感染范围只触发一次）</summary>
        private bool m_resistTriggered;

        /// <summary>当前行为状态。</summary>
        public HumanState CurrentState => m_currentState;

        /// <summary>是否已被标记为感染。</summary>
        public bool IsInfected => m_currentState == HumanState.Infected;

        /// <summary>是否处于生成保护期</summary>
        public bool IsInSpawnGrace => m_spawnGraceRemaining > 0f;

        /// <summary>人类类型。</summary>
        public HumanType UnitType => m_humanType;

        /// <summary>是否正在抵抗感染（抵抗时间 > 0）</summary>
        public bool IsResistingInfection => m_infectionResistRemaining > 0f;

        /// <summary>
        /// 人类当前的 2D 世界坐标。
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
            m_currentState = HumanState.Wandering;
            m_spawnGraceRemaining = 0f;
            m_infectionResistRemaining = 0f;
            m_resistTriggered = false;
            // 注意：类型不在 ResetState 中重置，由 SetHumanType 在生成时设置
        }

        /// <summary>
        /// 设置人类类型。由生成系统在从对象池取出后调用。
        /// </summary>
        public void SetHumanType(HumanType type)
        {
            m_humanType = type;
            m_resistTriggered = false;
        }

        /// <summary>
        /// 设置生成保护时间。
        /// </summary>
        public void SetSpawnGrace(float duration)
        {
            m_spawnGraceRemaining = Mathf.Max(0f, duration);
        }

        /// <summary>
        /// 尝试触发感染抵抗。如果该类型有抵抗时间且尚未触发过，则开始抵抗倒计时。
        /// </summary>
        /// <param name="resistDuration">抵抗持续时间（秒）</param>
        /// <returns>true 表示正在抵抗（本次感染应被阻止），false 表示无抵抗或已过期</returns>
        public bool TryTriggerResist(float resistDuration)
        {
            if (resistDuration <= 0f)
                return false;

            if (!m_resistTriggered)
            {
                m_resistTriggered = true;
                m_infectionResistRemaining = resistDuration;
                return true;
            }

            // 已触发过，检查是否仍在抵抗中
            return m_infectionResistRemaining > 0f;
        }

        private void LateUpdate()
        {
            if (m_spawnGraceRemaining > 0f)
            {
                m_spawnGraceRemaining -= Time.deltaTime;
            }
            if (m_infectionResistRemaining > 0f)
            {
                m_infectionResistRemaining -= Time.deltaTime;
            }
        }

        public void EnterFleeing()
        {
            if (IsInfected) return;
            m_currentState = HumanState.Fleeing;
        }

        public void EnterWandering()
        {
            if (IsInfected) return;
            m_currentState = HumanState.Wandering;
        }

        public void MarkInfected()
        {
            m_currentState = HumanState.Infected;
        }
    }
}
