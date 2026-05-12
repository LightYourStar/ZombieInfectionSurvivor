using System;
using System.Collections.Generic;
using Game.Config;
using Game.Gameplay.Enemy;
using Game.Utility;
using UnityEngine;

namespace Game.Gameplay.Zombie
{
    /// <summary>
    /// 僵尸同伴 AI：感知范围内无 Human 目标时跟随玩家，有目标时追击距离最近的 Human。
    /// 挂载在 ZombieCompanion 预制体根节点上，与 <see cref="ZombieCompanionUnit"/> 组件共存；
    /// <see cref="ZombieCompanionUnit"/> 负责状态枚举与池生命周期，本组件负责每帧的行为决策与位移推进。
    /// 数值参数（移动速度、感知范围）全部来自 <see cref="GameConfig"/>，不硬编码。
    /// </summary>
    /// <remarks>
    /// 依赖注入约定：
    /// <list type="bullet">
    /// <item><see cref="Initialize(GameConfig, Transform, Func{IReadOnlyList{HumanUnit}})"/> 由上层系统（通常是 InfectionSystem，
    /// task 6.2）在每次从对象池取出 ZombieCompanion 后调用，传入 <see cref="GameConfig"/>、玩家 <see cref="Transform"/>
    /// 以及一个惰性获取当前活跃 Human 列表的委托。</item>
    /// <item>使用委托而非直接持有 Human 列表，可避免 ZombieCompanionAI 跨模块强持有 SpawnSystem 的内部集合，
    /// 也便于上层按需缓存 / 过滤。</item>
    /// <item>未调用 Initialize 或 <see cref="GameConfig"/> 为空时，<see cref="UpdateAI"/> 会安全跳过所有位移逻辑。</item>
    /// </list>
    ///
    /// 行为规则（对应 Requirements 6.1 ~ 6.3）：
    /// <list type="number">
    /// <item>每帧遍历当前活跃 Human 列表，找出在感知半径内距离最近、且未被标记为感染的 Human。</item>
    /// <item>若存在可追击目标：切换 <see cref="ZombieCompanionUnit"/> 为 Chasing 状态，沿 (targetPos - selfPos) 的归一化方向移动。</item>
    /// <item>若不存在目标：切换为 Following 状态，沿玩家方向移动以保持跟随。</item>
    /// <item>所有数值（感知半径、移动速度）均从 <see cref="GameConfig"/> 读取。</item>
    /// </list>
    /// </remarks>
    [RequireComponent(typeof(ZombieCompanionUnit))]
    public class ZombieCompanionAI : MonoBehaviour
    {
        // ==================== Inspector 字段 ====================

        [Header("绑定")]
        [Tooltip("同 GameObject 上的 ZombieCompanionUnit 引用；留空时 Awake 中自动通过 GetComponent 补齐")]
        [SerializeField] private ZombieCompanionUnit m_unit;

        // ==================== 运行时依赖（由 Initialize 注入） ====================

        /// <summary>游戏全局配置，提供感知范围和移动速度等运行时数值</summary>
        private GameConfig m_config;

        /// <summary>玩家 Transform，作为无目标时的跟随对象；为 null 时僵尸同伴在无目标情况下保持静止</summary>
        private Transform m_playerTransform;

        /// <summary>
        /// 获取当前活跃 Human 列表的委托。
        /// 使用委托而非具体列表引用，可在上层（SpawnSystem / InfectionSystem）灵活维护集合（缓存、过滤等），
        /// 同时避免 ZombieCompanionAI 直接引用刷怪系统的内部数据结构导致的跨模块耦合。
        /// </summary>
        private Func<IReadOnlyList<HumanUnit>> m_getActiveHumans;

        // ==================== 生命周期 ====================

        /// <summary>
        /// Awake 中自动补齐 <see cref="ZombieCompanionUnit"/> 引用，保证在 Initialize 之前也能安全访问单位自身状态。
        /// </summary>
        private void Awake()
        {
            if (m_unit == null)
            {
                m_unit = GetComponent<ZombieCompanionUnit>();
            }
        }

        // ==================== 初始化 ====================

        /// <summary>
        /// 注入 AI 所需的运行时依赖。通常由 InfectionSystem 在从对象池取出 ZombieCompanion 后调用。
        /// </summary>
        /// <param name="config">全局配置；为 null 时 AI 将进入「未初始化」状态，<see cref="UpdateAI"/> 直接跳过</param>
        /// <param name="playerTransform">玩家 Transform；为 null 时表示无跟随目标，无目标情况下僵尸同伴保持静止</param>
        /// <param name="getActiveHumans">
        /// 获取当前活跃 Human 列表的惰性委托；为 null 时视作当前无可追击目标，僵尸同伴只会执行跟随行为。
        /// 委托每帧被调用一次，调用方应保证其轻量（建议返回已缓存列表的只读视图）。
        /// </param>
        public void Initialize(GameConfig config, Transform playerTransform, Func<IReadOnlyList<HumanUnit>> getActiveHumans)
        {
            if (config == null)
            {
                Debug.LogError("[ZombieCompanionAI] Initialize 收到空的 GameConfig，ZombieCompanion 将保持静止直到传入合法配置");
            }

            m_config = config;
            m_playerTransform = playerTransform;
            m_getActiveHumans = getActiveHumans;
        }

        // ==================== 公开纯函数 ====================

        /// <summary>
        /// 从候选 Human 集合中选择距离 <paramref name="selfPos"/> 最近、且未被标记为感染的那一个。
        /// 对应设计文档 Property 10：返回的 Human 应是（合法）集合中与 Z 距离最小的那个。
        /// </summary>
        /// <param name="selfPos">僵尸同伴自身的二维位置</param>
        /// <param name="candidates">候选 Human 集合；为 null 或空时返回 null</param>
        /// <returns>距离最近且未被感染的 Human；无合法候选时返回 null</returns>
        /// <remarks>
        /// 实现上未直接调用 <see cref="MathUtils.FindNearest{T}"/>，因为本方法需要在遍历时同时过滤掉
        /// <see cref="HumanUnit.IsInfected"/> 为 true 的候选（当帧即将被 InfectionSystem 回收的单位）。
        /// 单次 for 循环可以一次性完成过滤 + 最近查找，避免额外列表分配。
        /// </remarks>
        public HumanUnit FindNearestTarget(Vector2 selfPos, IReadOnlyList<HumanUnit> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            HumanUnit nearest = null;
            float minSqrDist = float.PositiveInfinity;

            for (int i = 0; i < candidates.Count; i++)
            {
                HumanUnit candidate = candidates[i];
                // 跳过 null 引用（池中已回收或尚未初始化的实例）
                if (candidate == null)
                {
                    continue;
                }
                // 跳过已被标记为感染的 Human，避免追击一个当帧即将消失的目标
                if (candidate.IsInfected)
                {
                    continue;
                }

                float sqrDist = (candidate.Position - selfPos).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 计算从 <paramref name="selfPos"/> 指向 <paramref name="targetPos"/> 的归一化移动方向。
        /// </summary>
        /// <param name="selfPos">僵尸同伴自身位置</param>
        /// <param name="targetPos">目标位置（玩家或 Human）</param>
        /// <returns>
        /// 归一化的朝向向量；当两点位置重合（<c>selfPos == targetPos</c>）时无法确定方向，
        /// 返回 <see cref="Vector2.zero"/> 表示本帧不位移，避免产生 NaN。
        /// </returns>
        public Vector2 CalculateMoveDirection(Vector2 selfPos, Vector2 targetPos)
        {
            Vector2 toTarget = targetPos - selfPos;
            // 位置完全重合时已到达目标，不需要再位移
            if (toTarget.sqrMagnitude < Mathf.Epsilon)
            {
                return Vector2.zero;
            }
            return toTarget.normalized;
        }

        // ==================== 每帧更新 ====================

        /// <summary>
        /// 推进一帧 AI 行为：在感知范围内寻找最近 Human，有则追击、无则跟随玩家，并应用位移。
        /// 由 GameSystemRunner / 上层系统统一调度，本组件不订阅 Unity 的 Update。
        ///
        /// 当以下任一条件不满足时方法提前返回：
        /// <list type="bullet">
        /// <item><see cref="Initialize"/> 未调用或传入了空的 <see cref="GameConfig"/>；</item>
        /// <item><see cref="ZombieCompanionUnit"/> 引用缺失；</item>
        /// <item><paramref name="deltaTime"/> 为非正数（暂停帧或时序异常）。</item>
        /// </list>
        /// </summary>
        /// <param name="deltaTime">本帧时间增量，通常为 <see cref="Time.deltaTime"/></param>
        public void UpdateAI(float deltaTime)
        {
            if (m_config == null || m_unit == null || deltaTime <= 0f)
            {
                return;
            }

            Vector2 selfPos = m_unit.Position;

            // 1. 在感知范围内寻找最近的可追击 Human
            HumanUnit target = TryFindTargetWithinPerception(selfPos);

            Vector2 moveDirection;
            if (target != null)
            {
                // 2a. 有目标：切换为追击状态，沿目标方向移动
                m_unit.EnterChasing();
                moveDirection = CalculateMoveDirection(selfPos, target.Position);
            }
            else
            {
                // 2b. 无目标：切换为跟随状态，沿玩家方向移动；玩家缺失时保持静止
                m_unit.EnterFollowing();
                if (m_playerTransform != null)
                {
                    Vector3 playerWorld = m_playerTransform.position;
                    Vector2 playerPos = new Vector2(playerWorld.x, playerWorld.y);
                    moveDirection = CalculateMoveDirection(selfPos, playerPos);
                }
                else
                {
                    moveDirection = Vector2.zero;
                }
            }

            // 3. 应用位移：displacement = direction * zombieCompanionSpeed * deltaTime（direction 已为单位向量或零向量）
            if (moveDirection.sqrMagnitude < Mathf.Epsilon)
            {
                return;
            }

            Vector2 displacement = moveDirection * (m_config.ZombieCompanionSpeed * deltaTime);
            Vector3 currentPosition = transform.position;
            transform.position = new Vector3(
                currentPosition.x + displacement.x,
                currentPosition.y + displacement.y,
                currentPosition.z);
        }

        // ==================== 内部辅助 ====================

        /// <summary>
        /// 从当前活跃 Human 列表中找到距离 <paramref name="selfPos"/> 最近、且位于感知半径内、且未被感染的 Human。
        /// 使用 <see cref="FindNearestTarget"/> 拿到全局最近候选后，再通过 <see cref="MathUtils.IsWithinRange"/>
        /// 进行严格小于半径的范围校验（语义与设计文档 Property 7 保持一致）。
        /// </summary>
        /// <param name="selfPos">僵尸同伴自身位置</param>
        /// <returns>感知半径内的最近 Human；无合法目标时返回 null</returns>
        private HumanUnit TryFindTargetWithinPerception(Vector2 selfPos)
        {
            if (m_getActiveHumans == null)
            {
                return null;
            }

            IReadOnlyList<HumanUnit> activeHumans = m_getActiveHumans();
            if (activeHumans == null || activeHumans.Count == 0)
            {
                return null;
            }

            HumanUnit nearest = FindNearestTarget(selfPos, activeHumans);
            if (nearest == null)
            {
                return null;
            }

            // 范围判定单独一步，避免把感知半径耦合进纯函数 FindNearestTarget
            if (!MathUtils.IsWithinRange(selfPos, nearest.Position, m_config.ZombieCompanionPerceptionRadius))
            {
                return null;
            }

            return nearest;
        }
    }
}
