using System;
using System.Collections.Generic;
using Game.Config;
using Game.Gameplay.Enemy;
using Game.Gameplay.Skill;
using Game.Gameplay.Wave;
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

        [SerializeField] private MapRuntimeController m_mapRuntimeController;

        [SerializeField, Min(0f)] private float m_blockerClearance = 0.45f;

        // ==================== 运行时依赖（由 Initialize 注入） ====================

        /// <summary>游戏全局配置，提供感知范围和移动速度等运行时数值</summary>
        private GameConfig m_config;

        /// <summary>玩家 Transform，作为无目标时的跟随对象；为 null 时僵尸同伴在无目标情况下保持静止</summary>
        private Transform m_playerTransform;

        /// <summary>当前局升级状态，提供感知范围和冲刺时间修正</summary>
        private SessionUpgradeState m_sessionState;

        /// <summary>
        /// 获取当前活跃 Human 列表的委托。
        /// 使用委托而非具体列表引用，可在上层（SpawnSystem / InfectionSystem）灵活维护集合（缓存、过滤等），
        /// 同时避免 ZombieCompanionAI 直接引用刷怪系统的内部数据结构导致的跨模块耦合。
        /// </summary>
        private Func<IReadOnlyList<HumanUnit>> m_getActiveHumans;

        private MapRuntimeController m_cachedMapRuntimeController;

        // ==================== 尸潮行为状态 ====================

        /// <summary>当前尸潮行为状态</summary>
        public enum SwarmState { Follow, Hunt, Return, Frenzy }

        /// <summary>当前行为状态</summary>
        private SwarmState m_swarmState = SwarmState.Follow;

        /// <summary>跟随槽位偏移（相对于玩家位置的随机偏移）</summary>
        private Vector2 m_followSlotOffset;

        /// <summary>槽位刷新计时器</summary>
        private float m_slotRefreshTimer;

        /// <summary>槽位刷新间隔（秒）</summary>
        private const float SlotRefreshInterval = 3f;

        /// <summary>跟随最小距离</summary>
        private const float FollowMinRadius = 2f;

        /// <summary>跟随最大距离</summary>
        private const float FollowMaxRadius = 5.5f;

        /// <summary>Hunt 最大脱离玩家距离，超过则 Return</summary>
        private const float MaxDetachDistance = 12f;

        /// <summary>Hunt 目标最大距离（相对于玩家）</summary>
        private const float MaxHuntTargetDistFromPlayer = 10f;

        /// <summary>全局活跃 Hunter 计数（静态共享）</summary>
        private static int s_activeHunterCount;

        /// <summary>最大同时 Hunt 僵尸数</summary>
        private const int MaxActiveHunters = 15;

        /// <summary>是否已注册为 Hunter</summary>
        private bool m_isRegisteredHunter;

        /// <summary>当前行为状态（供 Debug 读取）</summary>
        public SwarmState CurrentSwarmState => m_swarmState;

        /// <summary>全局活跃 Hunter 数量（供 Debug 读取）</summary>
        public static int ActiveHunterCount => s_activeHunterCount;

        // ==================== 卡住检测 ====================

        /// <summary>上一帧位置，用于检测移动距离</summary>
        private Vector2 m_lastRecordedPos;

        /// <summary>卡住计时器（秒）</summary>
        private float m_stuckTimer;

        /// <summary>卡住判定阈值（秒）</summary>
        private const float StuckThreshold = 1.0f;

        /// <summary>卡住判定最小移动距离</summary>
        private const float StuckMinMoveDist = 0.1f;

        /// <summary>连续卡住恢复次数</summary>
        private int m_consecutiveStuckCount;

        /// <summary>紧急传送冷却时间</summary>
        private float m_lastTeleportTime = -999f;

        /// <summary>紧急传送最小间隔（秒）</summary>
        private const float TeleportCooldown = 5f;

        /// <summary>是否当前被判定为卡住</summary>
        public bool IsStuck => m_stuckTimer >= StuckThreshold;

        /// <summary>全局卡住恢复计数（供 Debug）</summary>
        public static int TotalStuckRecoveryCount { get; private set; }

        /// <summary>全局槽位重分配计数（供 Debug）</summary>
        public static int TotalReassignedSlotCount { get; private set; }

        /// <summary>全局紧急传送计数（供 Debug）</summary>
        public static int TotalEmergencyTeleportCount { get; private set; }

        /// <summary>重置全局 Debug 计数（新局开始时调用）</summary>
        public static void ResetGlobalDebugCounters()
        {
            s_activeHunterCount = 0;
            TotalStuckRecoveryCount = 0;
            TotalReassignedSlotCount = 0;
            TotalEmergencyTeleportCount = 0;
            TotalVisibleTeleportBlockedCount = 0;
        }

        // ==================== 新生冲刺状态 ====================

        /// <summary>冲刺剩余时间（秒），> 0 时处于冲刺状态</summary>
        private float m_rushRemainingTime;

        /// <summary>是否处于新生冲刺状态</summary>
        public bool IsRushing => m_rushRemainingTime > 0f;

        /// <summary>冲刺视觉：缓存原始颜色用于恢复</summary>
        private Color m_originalColor = Color.white;

        /// <summary>冲刺视觉：是否已应用冲刺视觉效果</summary>
        private bool m_rushVisualActive;

        /// <summary>冲刺高亮颜色</summary>
        private static readonly Color s_rushColor = new Color(1f, 0.4f, 0.2f, 1f);

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
        public void Initialize(GameConfig config, Transform playerTransform, Func<IReadOnlyList<HumanUnit>> getActiveHumans, SessionUpgradeState sessionState = null)
        {
            if (config == null)
            {
                Debug.LogError("[ZombieCompanionAI] Initialize 收到空的 GameConfig，ZombieCompanion 将保持静止直到传入合法配置");
            }

            m_config = config;
            m_playerTransform = playerTransform;
            m_getActiveHumans = getActiveHumans;
            m_sessionState = sessionState;

            // 重置冲刺状态（对象池复用时清理残留）
            m_rushRemainingTime = 0f;

            // 初始化尸潮状态
            UnregisterHunter();
            m_swarmState = SwarmState.Follow;
            RefreshFollowSlot();
            m_slotRefreshTimer = UnityEngine.Random.Range(0f, SlotRefreshInterval);

            // 重置卡住检测
            m_stuckTimer = 0f;
            m_consecutiveStuckCount = 0;
            m_lastRecordedPos = m_unit != null ? m_unit.Position : Vector2.zero;
        }

        /// <summary>
        /// 启动新生冲刺。由 InfectionSystem 在生成新 ZombieCompanion 后调用。
        /// 冲刺期间移动速度乘以倍率，优先朝最近 Human 移动。
        /// </summary>
        public void StartNewbornRush()
        {
            if (m_config == null || !m_config.EnableNewbornRush)
            {
                return;
            }
            float duration = m_config.NewbornRushDuration;
            if (m_sessionState != null)
            {
                duration = m_sessionState.GetNewbornRushDuration(duration);
            }
            m_rushRemainingTime = duration;
            ApplyRushVisual();
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

            // 新生冲刺（Frenzy）状态处理
            if (m_rushRemainingTime > 0f)
            {
                m_swarmState = SwarmState.Frenzy;
                m_rushRemainingTime -= deltaTime;
                if (m_rushRemainingTime <= 0f)
                {
                    RemoveRushVisual();
                    m_swarmState = SwarmState.Follow;
                }
                else
                {
                    UpdateRushBehavior(selfPos, deltaTime);
                    return;
                }
            }

            Vector2 playerPos = m_playerTransform != null
                ? new Vector2(m_playerTransform.position.x, m_playerTransform.position.y)
                : selfPos;

            float distToPlayer = (selfPos - playerPos).magnitude;

            // Return 检查：距离玩家太远则强制返回
            if (distToPlayer > MaxDetachDistance && m_swarmState != SwarmState.Return)
            {
                UnregisterHunter();
                m_swarmState = SwarmState.Return;
            }

            // 状态机更新
            switch (m_swarmState)
            {
                case SwarmState.Follow:
                    UpdateFollowState(selfPos, playerPos, distToPlayer, deltaTime);
                    break;
                case SwarmState.Hunt:
                    UpdateHuntState(selfPos, playerPos, distToPlayer, deltaTime);
                    break;
                case SwarmState.Return:
                    UpdateReturnState(selfPos, playerPos, distToPlayer, deltaTime);
                    break;
            }

            // 卡住检测（在状态更新后执行）
            UpdateStuckDetection(selfPos, deltaTime);
        }

        private void UpdateFollowState(Vector2 selfPos, Vector2 playerPos, float distToPlayer, float deltaTime)
        {
            m_unit.EnterFollowing();

            // 定期刷新槽位（1.5-3 秒随机间隔）
            m_slotRefreshTimer += deltaTime;
            if (m_slotRefreshTimer >= SlotRefreshInterval)
            {
                RefreshFollowSlot();
                m_slotRefreshTimer = UnityEngine.Random.Range(-1.5f, 0f); // 随机化下次刷新时机
                m_slotRefreshTimer = 0f;
            }

            // 尝试转为 Hunt：只有边缘僵尸（距玩家 > FollowMinRadius）且 Hunter 名额未满
            if (distToPlayer > FollowMinRadius && s_activeHunterCount < MaxActiveHunters)
            {
                HumanUnit target = TryFindHuntTarget(selfPos, playerPos);
                if (target != null)
                {
                    RegisterHunter();
                    m_swarmState = SwarmState.Hunt;
                    UpdateHuntState(selfPos, playerPos, distToPlayer, deltaTime);
                    return;
                }
            }

            // 朝跟随槽位移动
            Vector2 slotTarget = playerPos + m_followSlotOffset;
            float sqrDistToSlot = (slotTarget - selfPos).sqrMagnitude;
            float stopDist = 0.8f;

            if (sqrDistToSlot <= stopDist * stopDist)
            {
                // 已到达槽位，微小漂移
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * 0.2f * deltaTime;
                MoveWithCollision(selfPos, jitter);
                return;
            }

            Vector2 moveDir = CalculateMoveDirection(selfPos, slotTarget);
            Vector2 displacement = moveDir * (m_config.ZombieCompanionSpeed * deltaTime);
            MoveWithCollision(selfPos, displacement);
        }

        private void UpdateHuntState(Vector2 selfPos, Vector2 playerPos, float distToPlayer, float deltaTime)
        {
            m_unit.EnterChasing();

            // 检查是否应该放弃 Hunt
            if (distToPlayer > MaxDetachDistance)
            {
                UnregisterHunter();
                m_swarmState = SwarmState.Return;
                return;
            }

            HumanUnit target = TryFindHuntTarget(selfPos, playerPos);
            if (target == null)
            {
                // 目标丢失，回到 Follow
                UnregisterHunter();
                m_swarmState = SwarmState.Follow;
                return;
            }

            Vector2 targetPos = target.Position;
            float sqrDistToTarget = (targetPos - selfPos).sqrMagnitude;
            float stopDist = 0.3f;

            if (sqrDistToTarget <= stopDist * stopDist)
            {
                return; // 感染系统会处理转化
            }

            Vector2 moveDir = CalculateMoveDirection(selfPos, targetPos);
            Vector2 displacement = moveDir * (m_config.ZombieCompanionSpeed * deltaTime);
            MoveWithCollision(selfPos, displacement);
        }

        private void UpdateReturnState(Vector2 selfPos, Vector2 playerPos, float distToPlayer, float deltaTime)
        {
            m_unit.EnterFollowing();

            // 回到跟随范围后切换为 Follow
            if (distToPlayer <= FollowMaxRadius)
            {
                m_swarmState = SwarmState.Follow;
                RefreshFollowSlot();
                return;
            }

            // 朝跟随槽位移动（不是玩家中心）
            Vector2 slotTarget = playerPos + m_followSlotOffset;
            Vector2 moveDir = CalculateMoveDirection(selfPos, slotTarget);
            // Return 时稍快一点追上
            float returnSpeed = m_config.ZombieCompanionSpeed * 1.3f;
            Vector2 displacement = moveDir * (returnSpeed * deltaTime);
            MoveWithCollision(selfPos, displacement);
        }

        /// <summary>
        /// 寻找可 Hunt 的目标：必须在感知范围内，且目标距玩家不超过 MaxHuntTargetDistFromPlayer。
        /// </summary>
        private HumanUnit TryFindHuntTarget(Vector2 selfPos, Vector2 playerPos)
        {
            if (m_getActiveHumans == null) return null;

            IReadOnlyList<HumanUnit> activeHumans = m_getActiveHumans();
            if (activeHumans == null || activeHumans.Count == 0) return null;

            float perceptionRadius = m_config.ZombieCompanionPerceptionRadius;
            if (m_sessionState != null)
            {
                perceptionRadius = m_sessionState.GetZombiePerceptionRadius(perceptionRadius);
            }

            float sqrPerception = perceptionRadius * perceptionRadius;
            float sqrMaxFromPlayer = MaxHuntTargetDistFromPlayer * MaxHuntTargetDistFromPlayer;

            HumanUnit best = null;
            float bestSqrDist = float.PositiveInfinity;

            for (int i = 0; i < activeHumans.Count; i++)
            {
                HumanUnit human = activeHumans[i];
                if (human == null || human.IsInfected || human.IsInSpawnGrace) continue;

                Vector2 humanPos = human.Position;

                // 目标必须在僵尸感知范围内
                float sqrDistToSelf = (humanPos - selfPos).sqrMagnitude;
                if (sqrDistToSelf >= sqrPerception) continue;

                // 目标必须在玩家附近一定范围内
                float sqrDistToPlayer = (humanPos - playerPos).sqrMagnitude;
                if (sqrDistToPlayer >= sqrMaxFromPlayer) continue;

                if (sqrDistToSelf < bestSqrDist)
                {
                    bestSqrDist = sqrDistToSelf;
                    best = human;
                }
            }

            return best;
        }

        private void RefreshFollowSlot()
        {
            MapRuntimeController map = ResolveMapRuntimeController();
            Vector2 playerPos = m_playerTransform != null
                ? new Vector2(m_playerTransform.position.x, m_playerTransform.position.y)
                : Vector2.zero;

            // 尝试最多 10 次找到安全槽位
            for (int attempt = 0; attempt < 10; attempt++)
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float radius = UnityEngine.Random.Range(FollowMinRadius, FollowMaxRadius);
                Vector2 offset = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                Vector2 worldPos = playerPos + offset;

                // 安全检查：不在阻挡内
                if (map != null)
                {
                    worldPos = map.ClampToMap(worldPos, m_blockerClearance);
                    if (map.IsPointBlocked(worldPos, m_blockerClearance))
                    {
                        continue;
                    }
                }

                m_followSlotOffset = offset;
                TotalReassignedSlotCount++;
                return;
            }

            // 所有尝试失败，使用玩家正后方作为安全方向
            m_followSlotOffset = new Vector2(0f, -FollowMinRadius);
            TotalReassignedSlotCount++;
        }

        private void RegisterHunter()
        {
            if (!m_isRegisteredHunter)
            {
                m_isRegisteredHunter = true;
                s_activeHunterCount++;
            }
        }

        private void UnregisterHunter()
        {
            if (m_isRegisteredHunter)
            {
                m_isRegisteredHunter = false;
                s_activeHunterCount = Mathf.Max(0, s_activeHunterCount - 1);
            }
        }

        // ==================== 卡住检测与恢复 ====================

        private void UpdateStuckDetection(Vector2 currentPos, float deltaTime)
        {
            float movedDist = (currentPos - m_lastRecordedPos).magnitude;

            if (movedDist < StuckMinMoveDist)
            {
                m_stuckTimer += deltaTime;
            }
            else
            {
                m_stuckTimer = 0f;
                m_consecutiveStuckCount = 0;
            }

            m_lastRecordedPos = currentPos;

            // 卡住判定
            if (m_stuckTimer >= StuckThreshold)
            {
                HandleStuckRecovery(currentPos);
                m_stuckTimer = 0f;
            }
        }

        private void HandleStuckRecovery(Vector2 currentPos)
        {
            m_consecutiveStuckCount++;
            TotalStuckRecoveryCount++;

            // 按状态处理：只重分配目标/切换状态，不直接移动位置
            switch (m_swarmState)
            {
                case SwarmState.Hunt:
                    // 放弃当前目标，切换到 Return
                    UnregisterHunter();
                    m_swarmState = SwarmState.Return;
                    RefreshFollowSlot();
                    break;

                case SwarmState.Return:
                    // 重新选择一个不同方向的槽位
                    RefreshFollowSlot();
                    break;

                case SwarmState.Follow:
                    // 重新分配槽位
                    RefreshFollowSlot();
                    break;

                case SwarmState.Frenzy:
                    // 结束冲刺，切换到 Return
                    m_rushRemainingTime = 0f;
                    RemoveRushVisual();
                    m_swarmState = SwarmState.Return;
                    RefreshFollowSlot();
                    break;
            }

            // 紧急传送：仅在极端条件下（连续卡住 5s+、距玩家 >18m、不在摄像机视野内）
            if (m_consecutiveStuckCount >= 5)
            {
                TryEmergencyTeleportIfSafe(currentPos);
            }
        }

        /// <summary>
        /// 紧急传送：仅在僵尸不在摄像机视野内、距玩家超过 18m 时才允许。
        /// 避免玩家看到僵尸瞬移。
        /// </summary>
        private void TryEmergencyTeleportIfSafe(Vector2 currentPos)
        {
            if (m_playerTransform == null) return;

            float elapsed = Time.time;
            if (elapsed - m_lastTeleportTime < TeleportCooldown) return;

            Vector2 playerPos = new Vector2(m_playerTransform.position.x, m_playerTransform.position.y);
            float distToPlayer = (currentPos - playerPos).magnitude;

            // 条件 1：距玩家超过 18m
            if (distToPlayer < 18f)
            {
                TotalVisibleTeleportBlockedCount++;
                return;
            }

            // 条件 2：不在摄像机视野内
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 viewportPos = cam.WorldToViewportPoint(new Vector3(currentPos.x, currentPos.y, 0f));
                bool inView = viewportPos.x >= -0.1f && viewportPos.x <= 1.1f &&
                              viewportPos.y >= -0.1f && viewportPos.y <= 1.1f &&
                              viewportPos.z > 0f;
                if (inView)
                {
                    TotalVisibleTeleportBlockedCount++;
                    return;
                }
            }

            // 执行传送
            MapRuntimeController map = ResolveMapRuntimeController();
            for (int i = 0; i < 8; i++)
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float dist = UnityEngine.Random.Range(FollowMinRadius, FollowMaxRadius);
                Vector2 candidate = playerPos + new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);

                if (map != null)
                {
                    candidate = map.ClampToMap(candidate, m_blockerClearance);
                    if (map.IsPointBlocked(candidate, m_blockerClearance)) continue;
                }

                ApplyWorldPosition(candidate);
                m_swarmState = SwarmState.Follow;
                m_followSlotOffset = candidate - playerPos;
                m_consecutiveStuckCount = 0;
                m_lastTeleportTime = elapsed;
                TotalEmergencyTeleportCount++;
                return;
            }

            // 所有尝试失败，不传送，等下一次
            TotalVisibleTeleportBlockedCount++;
        }

        /// <summary>全局：因在视野内而被阻止传送的次数（供 Debug）</summary>
        public static int TotalVisibleTeleportBlockedCount { get; private set; }

        private void OnDisable()
        {
            // 对象池回收时释放 Hunter 名额
            UnregisterHunter();
        }

        // ==================== 内部辅助 ====================

        /// <summary>
        /// 新生冲刺行为：以加速倍率朝最近 Human 移动。
        /// 如果没有目标则朝玩家方向冲刺，都没有则冲刺提前结束。
        /// </summary>
        private void UpdateRushBehavior(Vector2 selfPos, float deltaTime)
        {
            // 寻找最近 Human（不限感知范围，冲刺期间全图搜索最近目标）
            HumanUnit rushTarget = null;
            if (m_getActiveHumans != null)
            {
                IReadOnlyList<HumanUnit> activeHumans = m_getActiveHumans();
                if (activeHumans != null)
                {
                    rushTarget = FindNearestTarget(selfPos, activeHumans);
                }
            }

            Vector2 moveDirection;
            if (rushTarget != null)
            {
                m_unit.EnterChasing();
                moveDirection = CalculateMoveDirection(selfPos, rushTarget.Position);
            }
            else if (m_playerTransform != null)
            {
                // 无目标时朝玩家方向冲刺
                m_unit.EnterFollowing();
                Vector2 playerPos = new Vector2(m_playerTransform.position.x, m_playerTransform.position.y);
                moveDirection = CalculateMoveDirection(selfPos, playerPos);
            }
            else
            {
                moveDirection = Vector2.zero;
            }

            if (moveDirection.sqrMagnitude < Mathf.Epsilon)
            {
                return;
            }

            // 冲刺速度 = 基础速度 * 倍率
            float rushSpeed = m_config.ZombieCompanionSpeed * m_config.NewbornRushSpeedMultiplier;
            Vector2 displacement = moveDirection * (rushSpeed * deltaTime);
            MoveWithCollision(selfPos, displacement);
        }

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
            float perceptionRadius = m_config.ZombieCompanionPerceptionRadius;
            if (m_sessionState != null)
            {
                perceptionRadius = m_sessionState.GetZombiePerceptionRadius(perceptionRadius);
            }
            if (!MathUtils.IsWithinRange(selfPos, nearest.Position, perceptionRadius))
            {
                return null;
            }

            return nearest;
        }

        private bool m_mapNullWarningLogged;

        private void MoveWithCollision(Vector2 currentPosition, Vector2 displacement)
        {
            Vector2 targetPosition = currentPosition + displacement;
            MapRuntimeController map = ResolveMapRuntimeController();
            if (map == null)
            {
                if (!m_mapNullWarningLogged)
                {
                    m_mapNullWarningLogged = true;
                    Debug.LogWarning("[ZombieCompanionAI] MapRuntimeController 未找到，僵尸将无视地形碰撞。请确保场景中存在 MapRuntimeController。");
                }
                ApplyWorldPosition(targetPosition);
                return;
            }

            float clearance = Mathf.Max(0f, m_blockerClearance);
            Vector2 clampedCurrent = map.ClampToMap(currentPosition, clearance);
            Vector2 clampedTarget = map.ClampToMap(targetPosition, clearance);

            // 直线移动
            if (!map.IsPointBlocked(clampedTarget, clearance) &&
                map.HasDirectPath(clampedCurrent, clampedTarget, clearance))
            {
                ApplyWorldPosition(clampedTarget);
                return;
            }

            // X 轴分量
            Vector2 xOnlyTarget = map.ClampToMap(new Vector2(currentPosition.x + displacement.x, currentPosition.y), clearance);
            if (!map.IsPointBlocked(xOnlyTarget, clearance) &&
                map.HasDirectPath(clampedCurrent, xOnlyTarget, clearance))
            {
                ApplyWorldPosition(xOnlyTarget);
                return;
            }

            // Y 轴分量
            Vector2 yOnlyTarget = map.ClampToMap(new Vector2(currentPosition.x, currentPosition.y + displacement.y), clearance);
            if (!map.IsPointBlocked(yOnlyTarget, clearance) &&
                map.HasDirectPath(clampedCurrent, yOnlyTarget, clearance))
            {
                ApplyWorldPosition(yOnlyTarget);
                return;
            }

            // 侧向脱困：尝试垂直于移动方向的左右偏移
            float mag = displacement.magnitude;
            if (mag > Mathf.Epsilon)
            {
                Vector2 perpendicular = new Vector2(-displacement.y, displacement.x).normalized * mag * 0.7f;

                // 尝试左偏
                Vector2 leftTarget = map.ClampToMap(currentPosition + perpendicular, clearance);
                if (!map.IsPointBlocked(leftTarget, clearance) &&
                    map.HasDirectPath(clampedCurrent, leftTarget, clearance))
                {
                    ApplyWorldPosition(leftTarget);
                    return;
                }

                // 尝试右偏
                Vector2 rightTarget = map.ClampToMap(currentPosition - perpendicular, clearance);
                if (!map.IsPointBlocked(rightTarget, clearance) &&
                    map.HasDirectPath(clampedCurrent, rightTarget, clearance))
                {
                    ApplyWorldPosition(rightTarget);
                    return;
                }
            }

            // 所有方向都被阻挡，保持原位（等待卡住检测重分配目标）
            ApplyWorldPosition(clampedCurrent);
        }

        private void ApplyWorldPosition(Vector2 position)
        {
            Vector3 currentPosition = transform.position;
            transform.position = new Vector3(position.x, position.y, currentPosition.z);
        }

        private MapRuntimeController ResolveMapRuntimeController()
        {
            if (m_mapRuntimeController != null)
            {
                return m_mapRuntimeController;
            }

            if (m_cachedMapRuntimeController == null)
            {
                m_cachedMapRuntimeController = FindObjectOfType<MapRuntimeController>();
            }

            return m_cachedMapRuntimeController;
        }

        // ==================== 冲刺视觉反馈 ====================

        /// <summary>MaterialPropertyBlock 复用实例，避免每帧分配</summary>
        private MaterialPropertyBlock m_propBlock;

        private static readonly int s_colorPropertyId = Shader.PropertyToID("_Color");

        /// <summary>
        /// 应用冲刺视觉效果：使用 MaterialPropertyBlock 修改颜色，不污染共享材质。
        /// </summary>
        private void ApplyRushVisual()
        {
            if (m_rushVisualActive)
            {
                return;
            }

            // SpriteRenderer：直接修改 color 属性（SpriteRenderer 不共享材质颜色）
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                m_originalColor = sr.color;
                sr.color = s_rushColor;
                m_rushVisualActive = true;
                return;
            }

            // MeshRenderer：使用 MaterialPropertyBlock，不创建材质实例，不污染共享材质
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null)
            {
                if (m_propBlock == null)
                {
                    m_propBlock = new MaterialPropertyBlock();
                }
                mr.GetPropertyBlock(m_propBlock);
                // 读取当前颜色作为原始颜色（PropertyBlock 为空时取材质颜色）
                m_originalColor = mr.sharedMaterial != null ? mr.sharedMaterial.color : Color.white;
                m_propBlock.SetColor(s_colorPropertyId, s_rushColor);
                mr.SetPropertyBlock(m_propBlock);
                m_rushVisualActive = true;
            }
        }

        /// <summary>
        /// 移除冲刺视觉效果，恢复原始颜色。
        /// </summary>
        private void RemoveRushVisual()
        {
            if (!m_rushVisualActive)
            {
                return;
            }

            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = m_originalColor;
                m_rushVisualActive = false;
                return;
            }

            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null)
            {
                if (m_propBlock == null)
                {
                    m_propBlock = new MaterialPropertyBlock();
                }
                m_propBlock.SetColor(s_colorPropertyId, m_originalColor);
                mr.SetPropertyBlock(m_propBlock);
                m_rushVisualActive = false;
            }
        }
    }
}
