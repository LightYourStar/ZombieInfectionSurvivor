using System;
using System.Collections.Generic;
using Game.Config;
using UnityEngine;

namespace Game.Gameplay.Enemy
{
    /// <summary>
    /// 人类 AI：感知威胁时逃跑，无威胁时随机漫游。
    /// 挂载在 Human 预制体根节点上，与 <see cref="HumanUnit"/> 组件共存；<see cref="HumanUnit"/> 负责状态枚举与池生命周期，
    /// 本组件负责每帧的行为决策与位移推进。数值参数（感知半径、移动速度）全部来自 <see cref="GameConfig"/>，不硬编码。
    /// </summary>
    /// <remarks>
    /// 依赖注入约定：
    /// <list type="bullet">
    /// <item><see cref="Initialize(GameConfig, Transform, Func{IReadOnlyList{Vector2}})"/> 由上层系统（通常是 SpawnSystem，task 5.4）
    /// 在每次从对象池取出 Human 后调用，传入 <see cref="GameConfig"/>、玩家 <see cref="Transform"/> 以及一个惰性
    /// 获取当前活跃僵尸同伴位置列表的委托。</item>
    /// <item>使用委托而非直接持有僵尸列表，可避免 HumanAI 跨模块强耦合到 Zombie 命名空间，也便于上层按需过滤 / 缓存。</item>
    /// <item>未调用 Initialize 或 <see cref="GameConfig"/> 为空时，<see cref="UpdateAI"/> 会安全跳过所有位移逻辑。</item>
    /// </list>
    ///
    /// 行为规则（对应 Requirements 4.1 ~ 4.4）：
    /// <list type="number">
    /// <item>每帧遍历玩家与所有僵尸同伴位置，找出在感知半径内距离最近的威胁源。</item>
    /// <item>若存在威胁：切换 <see cref="HumanUnit"/> 为 Fleeing 状态，沿 (humanPos - threatPos) 的归一化方向逃离。</item>
    /// <item>若不存在威胁：切换为 Wandering 状态，按固定间隔刷新一个随机漫游方向。</item>
    /// <item>已被标记为感染的单位不再更新（等待 InfectionSystem 当帧回收）。</item>
    /// </list>
    /// </remarks>
    [RequireComponent(typeof(HumanUnit))]
    public class HumanAI : MonoBehaviour
    {
        // ==================== Inspector 字段 ====================

        [Header("绑定")]
        [Tooltip("同 GameObject 上的 HumanUnit 引用；留空时 Awake 中自动通过 GetComponent 补齐")]
        [SerializeField] private HumanUnit m_unit;

        [Header("漫游参数")]
        [Tooltip("无威胁时随机漫游方向的刷新间隔（秒）。值越小方向变化越频繁，人类行走轨迹越抖动")]
        [SerializeField, Min(0.1f)] private float m_wanderIntervalSeconds = 2f;

        // ==================== 运行时依赖（由 Initialize 注入） ====================

        /// <summary>游戏全局配置，提供感知半径和移动速度等运行时数值</summary>
        private GameConfig m_config;

        /// <summary>玩家 Transform，用于作为威胁源之一参与感知判定；为 null 时表示当前无玩家威胁</summary>
        private Transform m_playerTransform;

        /// <summary>
        /// 获取当前活跃僵尸同伴位置列表的委托。
        /// 使用委托而非具体列表引用，可在上层（SpawnSystem / InfectionSystem）灵活维护集合（缓存、过滤等），
        /// 同时避免 HumanAI 直接引用 Zombie 命名空间导致的跨模块耦合。
        /// </summary>
        private Func<IReadOnlyList<Vector2>> m_getZombiePositions;

        // ==================== 运行时状态 ====================

        /// <summary>
        /// 当前缓存的漫游方向，<see cref="CalculateWanderDirection"/> 直接返回此值。
        /// 由 <see cref="UpdateAI"/> 按 <see cref="m_wanderIntervalSeconds"/> 间隔刷新，保证人类不会每帧跳变方向。
        /// </summary>
        private Vector2 m_currentWanderDirection = Vector2.up;

        /// <summary>自上次刷新漫游方向以来累计的时间（秒）</summary>
        private float m_wanderTimer;

        // ==================== 生命周期 ====================

        /// <summary>
        /// Awake 中自动补齐 <see cref="HumanUnit"/> 引用并初始化一个漫游方向，
        /// 保证即使 <see cref="Initialize"/> 尚未调用，<see cref="CalculateWanderDirection"/> 返回的也是有效单位向量。
        /// </summary>
        private void Awake()
        {
            if (m_unit == null)
            {
                m_unit = GetComponent<HumanUnit>();
            }

            RefreshWanderDirection();
        }

        /// <summary>
        /// 对象池每次激活本单位时重置漫游计时与方向，避免沿用上一轮残留的方向导致行为异常。
        /// <see cref="HumanUnit.OnEnable"/> 负责重置行为状态，本组件只重置自身的漫游缓存。
        /// </summary>
        private void OnEnable()
        {
            m_wanderTimer = 0f;
            RefreshWanderDirection();
        }

        // ==================== 初始化 ====================

        /// <summary>
        /// 注入 AI 所需的运行时依赖。通常由 SpawnSystem 在从对象池取出 Human 后调用。
        /// </summary>
        /// <param name="config">全局配置；为 null 时 AI 将进入「未初始化」状态，<see cref="UpdateAI"/> 直接跳过</param>
        /// <param name="playerTransform">玩家 Transform；为 null 时表示当前场景中暂无玩家威胁源</param>
        /// <param name="getZombiePositions">
        /// 获取当前活跃僵尸同伴位置列表的惰性委托；为 null 时表示当前场景中暂无僵尸威胁源。
        /// 委托每帧被调用一次，调用方应保证其轻量（建议返回已缓存列表的只读视图）。
        /// </param>
        public void Initialize(GameConfig config, Transform playerTransform, Func<IReadOnlyList<Vector2>> getZombiePositions)
        {
            if (config == null)
            {
                Debug.LogError("[HumanAI] Initialize 收到空的 GameConfig，Human 将保持静止直到传入合法配置");
            }

            m_config = config;
            m_playerTransform = playerTransform;
            m_getZombiePositions = getZombiePositions;
        }

        // ==================== 公开纯函数 ====================

        /// <summary>
        /// 计算从 <paramref name="humanPos"/> 逃离 <paramref name="threatPos"/> 的归一化方向向量。
        /// 对应设计文档 Property 6：返回方向与 (H - T) 的点积应 &gt; 0（即方向远离威胁）。
        /// </summary>
        /// <param name="humanPos">人类当前位置</param>
        /// <param name="threatPos">威胁源位置（玩家或僵尸同伴）</param>
        /// <returns>
        /// 归一化的逃跑方向；当两点位置重合（<c>humanPos == threatPos</c>）时无法确定方向，返回 <see cref="Vector2.up"/>
        /// 作为保底值以避免出现 NaN 或零向量位移。
        /// </returns>
        public Vector2 CalculateFleeDirection(Vector2 humanPos, Vector2 threatPos)
        {
            Vector2 away = humanPos - threatPos;
            // 位置完全重合时 normalize 会产生 NaN，选一个确定的方向作为保底
            if (away.sqrMagnitude < Mathf.Epsilon)
            {
                return Vector2.up;
            }
            return away.normalized;
        }

        /// <summary>
        /// 获取当前缓存的随机漫游方向。返回的是单位向量。
        /// 方向刷新由 <see cref="UpdateAI"/> 在每帧按 <see cref="m_wanderIntervalSeconds"/> 驱动；
        /// 外部每帧调用本方法只读取缓存，不会自动刷新。
        /// </summary>
        /// <returns>当前漫游方向的单位向量</returns>
        public Vector2 CalculateWanderDirection()
        {
            return m_currentWanderDirection;
        }

        // ==================== 每帧更新 ====================

        /// <summary>
        /// 推进一帧 AI 行为：计算威胁、切换状态、按当前状态的方向移动。
        /// </summary>
        /// <param name="deltaTime">本帧时间增量</param>
        public void UpdateAI(float deltaTime)
        {
            if (m_config == null || m_unit == null || m_unit.IsInfected || deltaTime <= 0f)
            {
                return;
            }

            Vector2 selfPos = m_unit.Position;

            // 根据人类类型获取对应的感知半径和移动速度
            Config.HumanTypeConfig typeConfig = m_config.GetHumanTypeConfig(m_unit.UnitType);
            float perceptionRadius = typeConfig.PerceptionRadius;
            float moveSpeed = typeConfig.MoveSpeed;

            // 1. 在感知半径内寻找距离最近的威胁源（玩家或僵尸同伴）
            bool hasThreat = TryFindNearestThreatWithinRadius(
                selfPos,
                perceptionRadius,
                out Vector2 nearestThreatPos);

            Vector2 moveDirection;
            if (hasThreat)
            {
                // 2a. 有威胁：切换为逃跑状态，沿远离最近威胁的方向移动
                m_unit.EnterFleeing();
                moveDirection = CalculateFleeDirection(selfPos, nearestThreatPos);
            }
            else
            {
                // 2b. 无威胁：切换为漫游状态，按固定间隔刷新漫游方向
                m_unit.EnterWandering();
                m_wanderTimer += deltaTime;
                if (m_wanderTimer >= m_wanderIntervalSeconds)
                {
                    RefreshWanderDirection();
                    m_wanderTimer = 0f;
                }
                moveDirection = CalculateWanderDirection();
            }

            // 3. 应用位移
            Vector2 displacement = moveDirection * (moveSpeed * deltaTime);
            Vector3 currentPosition = transform.position;
            transform.position = new Vector3(
                currentPosition.x + displacement.x,
                currentPosition.y + displacement.y,
                currentPosition.z);
        }

        // ==================== 内部辅助 ====================

        /// <summary>
        /// 在注入的威胁源集合中查找距离 <paramref name="selfPos"/> 最近、且位于 <paramref name="radius"/> 范围内的威胁位置。
        /// 使用平方距离比较避免开方；严格小于半径的语义与 <c>MathUtils.IsWithinRange</c> 保持一致（Property 7）。
        /// </summary>
        /// <param name="selfPos">人类当前位置</param>
        /// <param name="radius">感知半径；非正数直接返回 false</param>
        /// <param name="nearestThreatPos">找到时输出最近威胁位置；未找到时为 <see cref="Vector2.zero"/></param>
        /// <returns>感知半径内至少存在一个威胁时返回 true</returns>
        private bool TryFindNearestThreatWithinRadius(Vector2 selfPos, float radius, out Vector2 nearestThreatPos)
        {
            nearestThreatPos = Vector2.zero;

            if (radius <= 0f)
            {
                return false;
            }

            float sqrRadius = radius * radius;
            float minSqrDist = float.PositiveInfinity;
            bool found = false;

            // 候选 1：玩家 Transform
            if (m_playerTransform != null)
            {
                Vector3 playerWorld = m_playerTransform.position;
                Vector2 playerPos = new Vector2(playerWorld.x, playerWorld.y);
                float sqrDist = (playerPos - selfPos).sqrMagnitude;
                if (sqrDist < sqrRadius && sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    nearestThreatPos = playerPos;
                    found = true;
                }
            }

            // 候选 2：僵尸同伴位置列表（通过委托惰性获取）
            if (m_getZombiePositions != null)
            {
                IReadOnlyList<Vector2> zombiePositions = m_getZombiePositions();
                if (zombiePositions != null)
                {
                    for (int i = 0; i < zombiePositions.Count; i++)
                    {
                        Vector2 zombiePos = zombiePositions[i];
                        float sqrDist = (zombiePos - selfPos).sqrMagnitude;
                        if (sqrDist < sqrRadius && sqrDist < minSqrDist)
                        {
                            minSqrDist = sqrDist;
                            nearestThreatPos = zombiePos;
                            found = true;
                        }
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// 刷新缓存的漫游方向。基于 <see cref="UnityEngine.Random.insideUnitCircle"/> 取一个随机方向并归一化；
        /// 极小概率随机结果为零向量时用 <see cref="Vector2.up"/> 兜底，避免后续位移出现 NaN。
        /// </summary>
        private void RefreshWanderDirection()
        {
            Vector2 random = UnityEngine.Random.insideUnitCircle;
            if (random.sqrMagnitude < Mathf.Epsilon)
            {
                m_currentWanderDirection = Vector2.up;
            }
            else
            {
                m_currentWanderDirection = random.normalized;
            }
        }
    }
}
