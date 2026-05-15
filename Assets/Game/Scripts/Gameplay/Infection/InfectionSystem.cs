using System;
using System.Collections.Generic;
using Game.Config;
using Game.Core;
using Game.Gameplay.Enemy;
using Game.Gameplay.Player;
using Game.Gameplay.Skill;
using Game.Gameplay.Wave;
using Game.Gameplay.Zombie;
using Game.Utility;
using UnityEngine;

namespace Game.Gameplay.Infection
{
    /// <summary>
    /// 感染判定与转化系统。
    /// 每帧遍历当前活跃 Human 列表，判定其与玩家或僵尸同伴之间的距离是否进入感染范围；
    /// 进入范围的 Human 会被回收并原地生成一个 ZombieCompanion，同时触发 <see cref="GameEvents.OnInfectionSuccess"/>。
    /// 转化受 <see cref="PlayerStats.ZombieCompanionCap"/> 上限约束。
    /// </summary>
    /// <remarks>
    /// 协作约定：
    /// <list type="bullet">
    /// <item><see cref="Initialize"/> 由上层（GameSystemRunner）在进入 Playing 状态前调用以注入 <see cref="PlayerStats"/>。</item>
    /// <item><see cref="SpawnSystem"/> 是活跃 Human 列表的权威来源；感染发生时通过 <see cref="SpawnSystem.ReturnHuman"/> 回收。</item>
    /// <item>本系统同时维护活跃 ZombieCompanion 列表，供 HumanAI 感知以及 <see cref="GetActiveZombiePositions"/> 查询使用。</item>
    /// <item><see cref="UpdateInfectionCheck"/> / <see cref="UpdateZombieAIs"/> 由 GameSystemRunner 统一调度，不订阅 Unity Update。</item>
    /// </list>
    /// </remarks>
    public class InfectionSystem : MonoBehaviour
    {
        // ==================== Inspector 注入 ====================

        [Header("依赖引用")]
        [Tooltip("对象池管理器，提供 GetZombie / ReturnZombie")]
        [SerializeField] private ObjectPoolManager m_poolManager;

        [Tooltip("人类刷新系统，提供活跃 Human 列表并负责回收被感染的 Human")]
        [SerializeField] private SpawnSystem m_spawnSystem;

        [Tooltip("游戏配置，用于传递给新生成 ZombieCompanion 的 AI 组件")]
        [SerializeField] private GameConfig m_config;

        [Tooltip("玩家 Transform，作为感染源与僵尸同伴 AI 的跟随目标")]
        [SerializeField] private Transform m_playerTransform;

        [SerializeField] private MapRuntimeController m_mapRuntimeController;

        [Header("僵尸站位")]
        [Tooltip("僵尸同伴之间希望保持的最小间距，用于运行时解重叠")]
        [SerializeField, Min(0f)] private float m_zombieSeparationRadius = 0.8f;

        [Tooltip("每帧解重叠迭代次数；值越大分离越稳定，但开销也略高")]
        [SerializeField, Min(1)] private int m_zombieSeparationIterations = 2;

        // ==================== 运行时状态 ====================

        /// <summary>玩家运行时属性，提供感染半径与僵尸同伴上限；由 <see cref="Initialize"/> 注入</summary>
        private PlayerStats m_playerStats;

        /// <summary>当前局升级状态，提供 2.0 升级修正值；由 <see cref="Initialize"/> 注入</summary>
        private SessionUpgradeState m_sessionUpgradeState;

        /// <summary>当前场上活跃的 ZombieCompanion 列表</summary>
        private readonly List<ZombieCompanionUnit> m_activeZombies = new List<ZombieCompanionUnit>();

        /// <summary>供 <see cref="GetActiveZombiePositions"/> 复用的位置缓存，避免每帧分配</summary>
        private readonly List<Vector2> m_zombiePositionsCache = new List<Vector2>();

        /// <summary>每帧感染判定时用于暂存本帧将被感染的 Human，避免遍历活跃列表时发生修改</summary>
        private readonly List<HumanUnit> m_pendingInfectionBuffer = new List<HumanUnit>();

        /// <summary>感染爆发时用于暂存二次感染目标，避免递归和 GC 分配</summary>
        private readonly List<HumanUnit> m_burstBuffer = new List<HumanUnit>();

        /// <summary>回响爆发独立缓冲区，避免与普通 Burst 共用 m_burstBuffer 导致互相 Clear</summary>
        private readonly List<HumanUnit> m_echoBurstBuffer = new List<HumanUnit>();

        /// <summary>标记当前帧是否正在执行普通感染爆发，防止 Burst 递归</summary>
        private bool m_isBurstInProgress;

        /// <summary>标记当前帧是否正在执行回响爆发，防止 EchoBurst 递归触发自身</summary>
        private bool m_isEchoBurstInProgress;

        private MapRuntimeController m_cachedMapRuntimeController;

        /// <summary>当前正在执行的感染来源，在 TryInfect 调用前设置</summary>
        private InfectionSource m_currentInfectionSource = InfectionSource.Player;

        // ==================== 感染来源统计 ====================

        /// <summary>本局玩家直接感染次数</summary>
        private int m_playerDirectInfections;

        /// <summary>本局僵尸自动感染次数</summary>
        private int m_zombieInfections;

        /// <summary>本局普通爆发感染次数</summary>
        private int m_burstInfections;

        /// <summary>本局回响爆发感染次数</summary>
        private int m_echoBurstInfections;

        /// <summary>上次玩家直接感染的时间戳</summary>
        private float m_lastPlayerDirectInfectionTime = -999f;

        /// <summary>本局最长玩家未直接感染间隔</summary>
        private float m_maxPlayerDirectInfectionGap;

        /// <summary>本局已用时间（由外部通过 UpdateElapsedTime 更新）</summary>
        private float m_elapsedTime;

        // ==================== 公开属性 ====================

        /// <summary>当前活跃 ZombieCompanion 数量（已清理无效引用后）</summary>
        public int ActiveZombieCount
        {
            get
            {
                CleanupInactiveZombies();
                return m_activeZombies.Count;
            }
        }

        /// <summary>
        /// 活跃 ZombieCompanion 的只读列表。
        /// 供调试、结算统计等外部系统查询使用；调用方不得修改返回集合。
        /// </summary>
        public IReadOnlyList<ZombieCompanionUnit> ActiveZombies => m_activeZombies;

        public event Action<int> OnInfectionBurstResolved;

        // ==================== 感染来源统计公开属性 ====================

        public int PlayerDirectInfections => m_playerDirectInfections;
        public int ZombieInfections => m_zombieInfections;
        public int BurstInfections => m_burstInfections;
        public int EchoBurstInfections => m_echoBurstInfections;
        public float LastPlayerDirectInfectionTime => m_lastPlayerDirectInfectionTime;
        public float MaxPlayerDirectInfectionGap => m_maxPlayerDirectInfectionGap;
        public float ElapsedTime => m_elapsedTime;

        /// <summary>距上次玩家直接感染的秒数</summary>
        public float SecondsSincePlayerDirectInfection =>
            m_lastPlayerDirectInfectionTime < 0f ? m_elapsedTime : (m_elapsedTime - m_lastPlayerDirectInfectionTime);

        // ==================== 初始化 ====================

        /// <summary>
        /// 注入运行时依赖 <see cref="PlayerStats"/>。
        /// 由 GameSystemRunner 在进入 Playing 状态前调用。
        /// </summary>
        /// <param name="playerStats">玩家运行时属性；为 null 时感染检测将被跳过</param>
        /// <param name="sessionState">当前局升级状态；为 null 时使用 GameConfig 基础值</param>
        public void Initialize(PlayerStats playerStats, SessionUpgradeState sessionState = null)
        {
            if (playerStats == null)
            {
                Debug.LogError("[InfectionSystem] Initialize 收到空的 PlayerStats，感染检测将被跳过");
            }
            m_playerStats = playerStats;
            m_sessionUpgradeState = sessionState;
        }

        // ==================== 公开 API ====================

        /// <summary>
        /// 获取当前所有活跃 ZombieCompanion 的二维位置列表（内部缓存复用，避免每帧分配）。
        /// 可作为 HumanAI 威胁源委托的实现：每次调用都会清空并重建缓存。
        /// </summary>
        /// <returns>只读的位置列表视图；调用方不应修改</returns>
        public IReadOnlyList<Vector2> GetActiveZombiePositions()
        {
            m_zombiePositionsCache.Clear();
            for (int i = 0; i < m_activeZombies.Count; i++)
            {
                ZombieCompanionUnit zombie = m_activeZombies[i];
                if (zombie == null)
                {
                    continue;
                }
                m_zombiePositionsCache.Add(zombie.Position);
            }
            return m_zombiePositionsCache;
        }

        /// <summary>
        /// 判定两点距离是否严格小于 <paramref name="radius"/>。
        /// 直接转发到 <see cref="MathUtils.IsWithinRange"/>，语义与设计文档 Property 7 保持一致：
        /// 当且仅当 distance(source, target) &lt; radius 时返回 true。
        /// </summary>
        public bool IsInInfectionRange(Vector2 source, Vector2 target, float radius)
        {
            return MathUtils.IsWithinRange(source, target, radius);
        }

        /// <summary>
        /// 尝试将指定 Human 转化为 ZombieCompanion。
        /// 成功时：标记 Human 为感染、回收到 Human 池、从 Zombie 池取出新单位放到被感染 Human 原位置，
        /// 并触发 <see cref="GameEvents.OnInfectionSuccess"/>。
        /// 失败场景：Human 为 null、PlayerStats 未注入、已达到 <see cref="PlayerStats.ZombieCompanionCap"/> 上限、
        /// 或 Zombie 池未能返回有效实例。
        /// </summary>
        /// <param name="human">目标 Human</param>
        /// <returns>转化成功返回 true；否则 false 且不修改任何单位数量</returns>
        public bool TryInfect(HumanUnit human)
        {
            if (human == null)
            {
                return false;
            }
            if (human.IsInfected)
            {
                return false;
            }
            if (m_playerStats == null)
            {
                Debug.LogError("[InfectionSystem] TryInfect 时 PlayerStats 尚未注入，感染失败");
                return false;
            }

            // 感染抵抗检查：Guard 类型有短暂抗感染时间
            if (m_config != null)
            {
                var humanTypeConfig = m_config.GetHumanTypeConfig(human.UnitType);
                if (humanTypeConfig.InfectionResistDuration > 0f)
                {
                    if (human.TryTriggerResist(humanTypeConfig.InfectionResistDuration))
                    {
                        return false; // 正在抵抗，本次感染失败
                    }
                }
            }

            // 先缓存位置和类型，标记后 Human 即将被回收
            Vector2 pos = human.Position;
            HumanType humanType = human.UnitType;

            human.MarkInfected();

            if (m_spawnSystem != null)
            {
                m_spawnSystem.ReturnHuman(human);
            }
            else
            {
                Debug.LogError("[InfectionSystem] SpawnSystem 未赋值，无法正确回收被感染的 Human");
            }

            // 无论是否达到上限，感染成功都触发事件（给经验金币），保持割草爽感不中断
            GameEvents.RaiseInfectionSuccess(pos);

            // 记录感染来源统计
            switch (m_currentInfectionSource)
            {
                case InfectionSource.Player:
                    m_playerDirectInfections++;
                    float gap = m_lastPlayerDirectInfectionTime < 0f
                        ? m_elapsedTime
                        : (m_elapsedTime - m_lastPlayerDirectInfectionTime);
                    if (gap > m_maxPlayerDirectInfectionGap)
                    {
                        m_maxPlayerDirectInfectionGap = gap;
                    }
                    m_lastPlayerDirectInfectionTime = m_elapsedTime;
                    break;
                case InfectionSource.Zombie:
                    m_zombieInfections++;
                    break;
                case InfectionSource.InfectionBurst:
                    m_burstInfections++;
                    break;
                case InfectionSource.EchoBurst:
                    m_echoBurstInfections++;
                    break;
            }

            // 感染爆发：以感染点为中心做一次小范围二次感染检测（仅一层，不递归）
            if (!m_isBurstInProgress && m_config != null && m_config.EnableInfectionBurst)
            {
                TryInfectionBurst(pos);
            }

            // 回响爆发计数：所有非 EchoBurst 来源的感染都计入（包括玩家直接感染和普通 Burst 产生的感染）。
            // 只有 EchoBurst 自己触发的感染不计入，避免递归。
            if (!m_isEchoBurstInProgress && m_sessionUpgradeState != null && m_sessionUpgradeState.IsEchoBurstActive)
            {
                m_sessionUpgradeState.InfectionCounter++;
                if (m_sessionUpgradeState.InfectionCounter % 5 == 0)
                {
                    TryEchoBurst(pos);
                }
            }

            // 上限约束（Requirement 5.4）：达到上限时不生成新 ZombieCompanion，但感染本身仍然成功
            CleanupInactiveZombies();
            if (m_activeZombies.Count >= m_playerStats.ZombieCompanionCap)
            {
                return true;
            }

            if (m_poolManager == null)
            {
                Debug.LogError("[InfectionSystem] ObjectPoolManager 未赋值，无法生成 ZombieCompanion");
                return true; // 感染已成功（人类已消灭），只是无法生成僵尸
            }

            ZombieCompanionUnit zombie = m_poolManager.GetZombie();
            if (zombie == null)
            {
                Debug.LogError("[InfectionSystem] ObjectPoolManager.GetZombie 返回 null，转化失败");
                return true; // 感染已成功
            }

            // 保留原 Prefab 的 Z 轴（2D 场景一般为 0）；显式写入以避免继承到错误深度
            zombie.transform.position = new Vector3(pos.x, pos.y, 0f);

            // 设置僵尸类型（根据人类类型映射）
            ZombieType zombieType = GameConfig.GetConvertedZombieType(humanType);
            zombie.SetZombieType(zombieType);

            // 应用类型视觉（颜色和缩放）
            ApplyZombieTypeVisual(zombie, zombieType);

            m_activeZombies.Add(zombie);

            // 为新生成的 ZombieCompanion 注入 AI 依赖
            ZombieCompanionAI ai = zombie.GetComponent<ZombieCompanionAI>();
            if (ai != null)
            {
                ai.Initialize(m_config, m_playerTransform, GetActiveHumansLazy, m_sessionUpgradeState);

                // 新生僵尸冲刺：刚转化的僵尸获得短时间加速
                ai.StartNewbornRush();
            }
            else
            {
                Debug.LogWarning("[InfectionSystem] ZombieCompanion 预制体缺少 ZombieCompanionAI 组件，新单位将无 AI 行为");
            }

            return true;
        }

        /// <summary>
        /// 每帧检测活跃 Human 与玩家 / 任意僵尸同伴之间的距离，收集所有进入感染范围的 Human 并一次性执行转化。
        /// 先收集再转化的目的：避免在遍历 <see cref="SpawnSystem.ActiveHumans"/> 的过程中调用 <see cref="TryInfect"/>
        /// 间接修改活跃列表导致索引错乱或枚举异常。
        /// </summary>
        public void UpdateInfectionCheck()
        {
            if (m_playerStats == null || m_spawnSystem == null)
            {
                return;
            }

            bool hasPlayer = m_playerTransform != null;
            Vector2 playerPos = Vector2.zero;
            if (hasPlayer)
            {
                Vector3 playerWorld = m_playerTransform.position;
                playerPos = new Vector2(playerWorld.x, playerWorld.y);
            }

            float radius = m_playerStats.InfectionRadius;

            m_pendingInfectionBuffer.Clear();

            // 用于区分玩家直接感染和僵尸感染
            int playerInfectionCount = 0;

            IReadOnlyList<HumanUnit> activeHumans = m_spawnSystem.ActiveHumans;
            for (int i = 0; i < activeHumans.Count; i++)
            {
                HumanUnit human = activeHumans[i];
                if (human == null || human.IsInfected || human.IsInSpawnGrace)
                {
                    continue;
                }

                // 玩家作为感染源（Requirement 5.1）— 玩家感染优先入队
                if (hasPlayer && IsInInfectionRange(human.Position, playerPos, radius))
                {
                    // 插入到列表前部，保证 playerInfectionCount 准确
                    m_pendingInfectionBuffer.Insert(playerInfectionCount, human);
                    playerInfectionCount++;
                    continue;
                }

                // 僵尸同伴作为感染源（Requirement 5.2）；命中一个即可，避免重复入队
                for (int j = 0; j < m_activeZombies.Count; j++)
                {
                    ZombieCompanionUnit zombie = m_activeZombies[j];
                    if (zombie == null)
                    {
                        continue;
                    }
                    // BruteZombie 有更大的感染半径
                    float zombieRadius = radius;
                    if (m_config != null)
                    {
                        var ztConfig = m_config.GetZombieTypeConfig(zombie.UnitType);
                        zombieRadius = radius * ztConfig.InfectionRadiusMultiplier;
                    }
                    if (IsInInfectionRange(human.Position, zombie.Position, zombieRadius))
                    {
                        m_pendingInfectionBuffer.Add(human);
                        break;
                    }
                }
            }

            // 统一执行转化，前 playerInfectionCount 个标记为 Player 来源，其余为 Zombie 来源
            for (int i = 0; i < m_pendingInfectionBuffer.Count; i++)
            {
                m_currentInfectionSource = (i < playerInfectionCount)
                    ? InfectionSource.Player
                    : InfectionSource.Zombie;
                TryInfect(m_pendingInfectionBuffer[i]);
            }
            m_pendingInfectionBuffer.Clear();
        }

        /// <summary>
        /// 统一推进所有活跃 ZombieCompanion 的 AI 行为。
        /// <see cref="ZombieCompanionAI"/> 不订阅 Unity Update，由本方法在 Playing 状态下每帧调用一次。
        /// </summary>
        /// <param name="deltaTime">本帧时间增量</param>
        public void UpdateZombieAIs(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            // 每帧清理无效僵尸引用
            CleanupInactiveZombies();

            // 安全检查：超限 Warning（限频，每 60 帧最多一次）
            if (m_playerStats != null && m_activeZombies.Count > m_playerStats.ZombieCompanionCap)
            {
                if (Time.frameCount % 60 == 0)
                {
                    int baseCap = m_config != null ? m_config.ZombieCompanionMaxCount : 0;
                    int bonus = m_playerStats.ZombieCompanionCap - baseCap;
                    Debug.LogWarning($"[InfectionSystem] 僵尸数超限: Count={m_activeZombies.Count}, Cap={m_playerStats.ZombieCompanionCap} (base={baseCap}, bonus=+{bonus})");
                }
            }

            for (int i = 0; i < m_activeZombies.Count; i++)
            {
                ZombieCompanionUnit zombie = m_activeZombies[i];
                if (zombie == null)
                {
                    continue;
                }
                ZombieCompanionAI ai = zombie.GetComponent<ZombieCompanionAI>();
                if (ai != null)
                {
                    ai.UpdateAI(deltaTime);
                }

                ClampZombieToMap(zombie);
            }

            ResolveZombieOverlaps();
        }

        private void ResolveZombieOverlaps()
        {
            if (m_zombieSeparationRadius <= 0f || m_activeZombies.Count < 2)
            {
                return;
            }

            float minDistance = m_zombieSeparationRadius;
            float minDistanceSqr = minDistance * minDistance;

            for (int iteration = 0; iteration < m_zombieSeparationIterations; iteration++)
            {
                for (int i = 0; i < m_activeZombies.Count; i++)
                {
                    ZombieCompanionUnit a = m_activeZombies[i];
                    if (a == null)
                    {
                        continue;
                    }

                    for (int j = i + 1; j < m_activeZombies.Count; j++)
                    {
                        ZombieCompanionUnit b = m_activeZombies[j];
                        if (b == null)
                        {
                            continue;
                        }

                        Vector2 posA = a.Position;
                        Vector2 posB = b.Position;
                        Vector2 delta = posB - posA;
                        float sqrDistance = delta.sqrMagnitude;
                        if (sqrDistance >= minDistanceSqr)
                        {
                            continue;
                        }

                        Vector2 pushDirection;
                        float distance;
                        if (sqrDistance > Mathf.Epsilon)
                        {
                            distance = Mathf.Sqrt(sqrDistance);
                            pushDirection = delta / distance;
                        }
                        else
                        {
                            distance = 0f;
                            pushDirection = GetDeterministicSplitDirection(a, b);
                        }

                        float overlap = minDistance - distance;
                        if (overlap <= 0f)
                        {
                            continue;
                        }

                        Vector2 correction = pushDirection * (overlap * 0.5f);
                        ApplyZombiePosition(a, posA - correction);
                        ApplyZombiePosition(b, posB + correction);
                    }
                }
            }
        }

        /// <summary>
        /// 重置系统状态，通常在单局结束或重新开始时调用。
        /// 将所有活跃 ZombieCompanion 归还池并清空列表；位置缓存与感染缓冲区一同清空。
        /// </summary>
        public void Reset()
        {
            if (m_poolManager != null)
            {
                for (int i = 0; i < m_activeZombies.Count; i++)
                {
                    ZombieCompanionUnit zombie = m_activeZombies[i];
                    if (zombie != null)
                    {
                        m_poolManager.ReturnZombie(zombie);
                    }
                }
            }

            m_activeZombies.Clear();
            m_zombiePositionsCache.Clear();
            m_pendingInfectionBuffer.Clear();
            m_burstBuffer.Clear();
            m_echoBurstBuffer.Clear();
            m_isBurstInProgress = false;
            m_isEchoBurstInProgress = false;

            // 重置感染来源统计
            m_playerDirectInfections = 0;
            m_zombieInfections = 0;
            m_burstInfections = 0;
            m_echoBurstInfections = 0;
            m_lastPlayerDirectInfectionTime = -999f;
            m_maxPlayerDirectInfectionGap = 0f;
            m_elapsedTime = 0f;
        }

        /// <summary>
        /// 由 GameSystemRunner 每帧调用，更新已用时间用于统计间隔。
        /// </summary>
        public void UpdateElapsedTime(float deltaTime)
        {
            if (deltaTime > 0f)
            {
                m_elapsedTime += deltaTime;
            }
        }

        // ==================== 内部辅助 ====================

        /// <summary>
        /// 感染爆发：以感染点为中心，在 BurstRadius 范围内寻找额外的 Human 并感染。
        /// 单次爆发最多额外感染 BurstMaxTargets 个目标。
        /// 通过 m_isBurstInProgress 标记防止递归无限传播（爆发触发的 TryInfect 不会再次触发爆发）。
        /// </summary>
        /// <param name="burstCenter">爆发中心位置（被感染 Human 的原始位置）</param>
        private void TryInfectionBurst(Vector2 burstCenter)
        {
            if (m_spawnSystem == null || m_config == null)
            {
                return;
            }

            float burstRadius = m_config.InfectionBurstRadius;
            int maxTargets = m_config.InfectionBurstMaxTargets;

            // 应用 SessionUpgradeState 修正
            if (m_sessionUpgradeState != null)
            {
                burstRadius = m_sessionUpgradeState.GetBurstRadius(burstRadius);
                maxTargets = m_sessionUpgradeState.GetBurstMaxTargets(maxTargets);
            }

            if (burstRadius <= 0f || maxTargets <= 0)
            {
                return;
            }

            float sqrBurstRadius = burstRadius * burstRadius;

            // 收集爆发范围内的候选目标
            m_burstBuffer.Clear();
            IReadOnlyList<HumanUnit> activeHumans = m_spawnSystem.ActiveHumans;
            for (int i = 0; i < activeHumans.Count; i++)
            {
                HumanUnit human = activeHumans[i];
                if (human == null || human.IsInfected)
                {
                    continue;
                }

                float sqrDist = (human.Position - burstCenter).sqrMagnitude;
                if (sqrDist < sqrBurstRadius)
                {
                    m_burstBuffer.Add(human);
                    if (m_burstBuffer.Count >= maxTargets)
                    {
                        break;
                    }
                }
            }

            // 标记爆发进行中，防止递归
            int actualInfectedCount = 0;
            m_isBurstInProgress = true;
            m_currentInfectionSource = InfectionSource.InfectionBurst;

            try
            {
                for (int i = 0; i < m_burstBuffer.Count; i++)
                {
                    if (TryInfect(m_burstBuffer[i]))
                    {
                        actualInfectedCount++;
                    }
                }
            }
            finally
            {
                m_isBurstInProgress = false;
                m_burstBuffer.Clear();
            }

            if (actualInfectedCount > 0)
            {
                OnInfectionBurstResolved?.Invoke(actualInfectedCount);
                GameEvents.RaiseInfectionBurstVisual(burstCenter, burstRadius, actualInfectedCount);
            }
        }

        /// <summary>
        /// 回响爆发：使用较小范围（当前爆发半径的 70%），最多额外感染 1 个目标。
        /// 使用独立的 m_echoBurstBuffer 避免与普通 Burst 的 m_burstBuffer 冲突。
        /// 设计选择：EchoBurst 触发的感染不会再触发普通 InfectionBurst，
        /// 确保"额外 1 个目标"的承诺不会因连锁而膨胀。
        /// </summary>
        private void TryEchoBurst(Vector2 burstCenter)
        {
            if (m_spawnSystem == null || m_config == null)
            {
                return;
            }

            float baseRadius = m_config.InfectionBurstRadius;
            if (m_sessionUpgradeState != null)
            {
                baseRadius = m_sessionUpgradeState.GetBurstRadius(baseRadius);
            }

            float echoRadius = baseRadius * 0.65f;
            int echoMaxTargets = 1;

            if (echoRadius <= 0f)
            {
                return;
            }

            float sqrEchoRadius = echoRadius * echoRadius;
            bool wasBurstInProgress = m_isBurstInProgress;
            bool wasEchoBurstInProgress = m_isEchoBurstInProgress;

            m_echoBurstBuffer.Clear();
            IReadOnlyList<HumanUnit> activeHumans = m_spawnSystem.ActiveHumans;
            for (int i = 0; i < activeHumans.Count; i++)
            {
                HumanUnit human = activeHumans[i];
                if (human == null || human.IsInfected)
                {
                    continue;
                }

                float sqrDist = (human.Position - burstCenter).sqrMagnitude;
                if (sqrDist < sqrEchoRadius)
                {
                    m_echoBurstBuffer.Add(human);
                    if (m_echoBurstBuffer.Count >= echoMaxTargets)
                    {
                        break;
                    }
                }
            }

            // 同时设置两个标记：
            // - m_isEchoBurstInProgress：防止 EchoBurst 递归触发自身
            // - m_isBurstInProgress：防止 Echo 触发的感染再触发普通 Burst（收紧影响范围）
            m_isEchoBurstInProgress = true;
            m_isBurstInProgress = true;
            m_currentInfectionSource = InfectionSource.EchoBurst;
            try
            {
                for (int i = 0; i < m_echoBurstBuffer.Count; i++)
                {
                    TryInfect(m_echoBurstBuffer[i]);
                }
            }
            finally
            {
                m_isBurstInProgress = wasBurstInProgress;
                m_isEchoBurstInProgress = wasEchoBurstInProgress;
                m_echoBurstBuffer.Clear();
            }
        }

        /// <summary>
        /// 用于 ZombieCompanionAI 的惰性委托：每帧按需返回 <see cref="SpawnSystem.ActiveHumans"/>。
        /// 通过方法引用避免每次感染都分配一个新的闭包实例。
        /// </summary>
        private IReadOnlyList<HumanUnit> GetActiveHumansLazy()
        {
            return m_spawnSystem != null ? m_spawnSystem.ActiveHumans : null;
        }

        /// <summary>
        /// 清理 m_activeZombies 中的 null 或已禁用的对象，避免幽灵计数。
        /// 轻量操作：从后向前遍历移除无效项。
        /// </summary>
        private void CleanupInactiveZombies()
        {
            for (int i = m_activeZombies.Count - 1; i >= 0; i--)
            {
                ZombieCompanionUnit zombie = m_activeZombies[i];
                if (zombie == null || !zombie.gameObject.activeInHierarchy)
                {
                    m_activeZombies.RemoveAt(i);
                }
            }
        }

        private void ClampZombieToMap(ZombieCompanionUnit zombie)
        {
            if (zombie == null || m_spawnSystem == null)
            {
                return;
            }

            ApplyZombiePosition(zombie, m_spawnSystem.ClampToMap(zombie.Position));
        }

        private void ApplyZombiePosition(ZombieCompanionUnit zombie, Vector2 position)
        {
            if (zombie == null)
            {
                return;
            }

            Vector2 currentPosition = zombie.Position;
            Vector2 finalPosition = m_spawnSystem != null ? m_spawnSystem.ClampToMap(position) : position;
            MapRuntimeController map = ResolveMapRuntimeController();
            if (map != null)
            {
                float clearance = ResolveZombieBlockerClearance();
                Vector2 clampedCurrent = map.ClampToMap(currentPosition, clearance);
                Vector2 clampedTarget = map.ClampToMap(finalPosition, clearance);

                if (map.IsPointBlocked(clampedTarget, clearance) ||
                    !map.HasDirectPath(clampedCurrent, clampedTarget, clearance))
                {
                    finalPosition = clampedCurrent;
                }
                else
                {
                    finalPosition = clampedTarget;
                }
            }

            Vector3 world = zombie.transform.position;
            zombie.transform.position = new Vector3(finalPosition.x, finalPosition.y, world.z);
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

        private float ResolveZombieBlockerClearance()
        {
            return 0.45f;
        }

        private Vector2 GetDeterministicSplitDirection(ZombieCompanionUnit a, ZombieCompanionUnit b)
        {
            int mixed = a.GetInstanceID() * 486187739 ^ b.GetInstanceID() * 16777619;
            float angle = Mathf.Abs(mixed % 360) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        // ==================== 类型视觉辅助 ====================

        /// <summary>
        /// 为僵尸应用类型对应的视觉效果（颜色和缩放）。
        /// </summary>
        private void ApplyZombieTypeVisual(ZombieCompanionUnit zombie, ZombieType type)
        {
            if (zombie == null || m_config == null) return;

            var typeConfig = m_config.GetZombieTypeConfig(type);

            // 应用缩放
            zombie.transform.localScale = Vector3.one * typeConfig.ScaleMultiplier;

            // 应用颜色
            SpriteRenderer sr = zombie.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = typeConfig.DisplayColor;
            }
        }

        /// <summary>
        /// 获取各类型僵尸的数量统计。
        /// </summary>
        public void GetZombieTypeCounts(out int normalCount, out int runnerCount, out int bruteCount)
        {
            normalCount = 0;
            runnerCount = 0;
            bruteCount = 0;

            for (int i = 0; i < m_activeZombies.Count; i++)
            {
                ZombieCompanionUnit zombie = m_activeZombies[i];
                if (zombie == null || !zombie.gameObject.activeInHierarchy) continue;

                switch (zombie.UnitType)
                {
                    case ZombieType.Normal: normalCount++; break;
                    case ZombieType.Runner: runnerCount++; break;
                    case ZombieType.Brute: bruteCount++; break;
                }
            }
        }
    }
}
