using System.Collections.Generic;
using Game.Config;
using Game.Core;
using Game.Gameplay.Enemy;
using Game.Gameplay.Player;
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

        [Header("僵尸站位")]
        [Tooltip("僵尸同伴之间希望保持的最小间距，用于运行时解重叠")]
        [SerializeField, Min(0f)] private float m_zombieSeparationRadius = 0.8f;

        [Tooltip("每帧解重叠迭代次数；值越大分离越稳定，但开销也略高")]
        [SerializeField, Min(1)] private int m_zombieSeparationIterations = 2;

        // ==================== 运行时状态 ====================

        /// <summary>玩家运行时属性，提供感染半径与僵尸同伴上限；由 <see cref="Initialize"/> 注入</summary>
        private PlayerStats m_playerStats;

        /// <summary>当前场上活跃的 ZombieCompanion 列表</summary>
        private readonly List<ZombieCompanionUnit> m_activeZombies = new List<ZombieCompanionUnit>();

        /// <summary>供 <see cref="GetActiveZombiePositions"/> 复用的位置缓存，避免每帧分配</summary>
        private readonly List<Vector2> m_zombiePositionsCache = new List<Vector2>();

        /// <summary>每帧感染判定时用于暂存本帧将被感染的 Human，避免遍历活跃列表时发生修改</summary>
        private readonly List<HumanUnit> m_pendingInfectionBuffer = new List<HumanUnit>();

        /// <summary>感染爆发时用于暂存二次感染目标，避免递归和 GC 分配</summary>
        private readonly List<HumanUnit> m_burstBuffer = new List<HumanUnit>();

        /// <summary>标记当前帧是否正在执行爆发感染，防止递归无限传播</summary>
        private bool m_isBurstInProgress;

        // ==================== 公开属性 ====================

        /// <summary>当前活跃 ZombieCompanion 数量</summary>
        public int ActiveZombieCount => m_activeZombies.Count;

        /// <summary>
        /// 活跃 ZombieCompanion 的只读列表。
        /// 供调试、结算统计等外部系统查询使用；调用方不得修改返回集合。
        /// </summary>
        public IReadOnlyList<ZombieCompanionUnit> ActiveZombies => m_activeZombies;

        // ==================== 初始化 ====================

        /// <summary>
        /// 注入运行时依赖 <see cref="PlayerStats"/>。
        /// 由 GameSystemRunner 在进入 Playing 状态前调用。
        /// </summary>
        /// <param name="playerStats">玩家运行时属性；为 null 时感染检测将被跳过</param>
        public void Initialize(PlayerStats playerStats)
        {
            if (playerStats == null)
            {
                Debug.LogError("[InfectionSystem] Initialize 收到空的 PlayerStats，感染检测将被跳过");
            }
            m_playerStats = playerStats;
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
            if (m_playerStats == null)
            {
                Debug.LogError("[InfectionSystem] TryInfect 时 PlayerStats 尚未注入，感染失败");
                return false;
            }

            // 先缓存位置，标记后 Human 即将被回收，位置可能被重置
            Vector2 pos = human.Position;

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

            // 感染爆发：以感染点为中心做一次小范围二次感染检测（仅一层，不递归）
            if (!m_isBurstInProgress && m_config != null && m_config.EnableInfectionBurst)
            {
                TryInfectionBurst(pos);
            }

            // 上限约束（Requirement 5.4）：达到上限时不生成新 ZombieCompanion，但感染本身仍然成功
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
            m_activeZombies.Add(zombie);

            // 为新生成的 ZombieCompanion 注入 AI 依赖
            ZombieCompanionAI ai = zombie.GetComponent<ZombieCompanionAI>();
            if (ai != null)
            {
                ai.Initialize(m_config, m_playerTransform, GetActiveHumansLazy);

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

            IReadOnlyList<HumanUnit> activeHumans = m_spawnSystem.ActiveHumans;
            for (int i = 0; i < activeHumans.Count; i++)
            {
                HumanUnit human = activeHumans[i];
                if (human == null || human.IsInfected)
                {
                    continue;
                }

                // 玩家作为感染源（Requirement 5.1）
                if (hasPlayer && IsInInfectionRange(human.Position, playerPos, radius))
                {
                    m_pendingInfectionBuffer.Add(human);
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
                    if (IsInInfectionRange(human.Position, zombie.Position, radius))
                    {
                        m_pendingInfectionBuffer.Add(human);
                        break;
                    }
                }
            }

            // 统一执行转化：TryInfect 内部自带上限校验，超限时会自动停止增加新单位
            for (int i = 0; i < m_pendingInfectionBuffer.Count; i++)
            {
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
            m_isBurstInProgress = true;

            for (int i = 0; i < m_burstBuffer.Count; i++)
            {
                TryInfect(m_burstBuffer[i]);
            }

            m_isBurstInProgress = false;
            m_burstBuffer.Clear();
        }

        /// <summary>
        /// 用于 ZombieCompanionAI 的惰性委托：每帧按需返回 <see cref="SpawnSystem.ActiveHumans"/>。
        /// 通过方法引用避免每次感染都分配一个新的闭包实例。
        /// </summary>
        private IReadOnlyList<HumanUnit> GetActiveHumansLazy()
        {
            return m_spawnSystem != null ? m_spawnSystem.ActiveHumans : null;
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

            Vector2 finalPosition = m_spawnSystem != null ? m_spawnSystem.ClampToMap(position) : position;
            Vector3 world = zombie.transform.position;
            zombie.transform.position = new Vector3(finalPosition.x, finalPosition.y, world.z);
        }

        private Vector2 GetDeterministicSplitDirection(ZombieCompanionUnit a, ZombieCompanionUnit b)
        {
            int mixed = a.GetInstanceID() * 486187739 ^ b.GetInstanceID() * 16777619;
            float angle = Mathf.Abs(mixed % 360) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }
}
