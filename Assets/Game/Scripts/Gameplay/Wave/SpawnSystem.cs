using System;
using System.Collections.Generic;
using Game.Config;
using Game.Gameplay.Enemy;
using Game.Utility;
using UnityEngine;

namespace Game.Gameplay.Wave
{
    /// <summary>
    /// 人类刷新系统。
    /// 负责单局开始时生成初始批次 Human、按配置间隔持续补充 Human 到配置上限，
    /// 并作为活跃 Human 列表的权威来源，供 InfectionSystem、ZombieCompanionAI 等系统订阅。
    /// 所有从池中取出的 Human 都会被放置在地图范围内、距离玩家至少 <see cref="m_spawnMinDistanceFromPlayer"/> 的位置（超出视野）。
    /// </summary>
    /// <remarks>
    /// 协作约定：
    /// <list type="bullet">
    /// <item>本系统不订阅 Unity 的 Update，所有推进由 GameSystemRunner 调度 <see cref="UpdateSpawn"/> 与 <see cref="UpdateHumanAIs"/>。</item>
    /// <item><see cref="HumanAI.UpdateAI"/> 同样由本系统统一驱动，避免每个 Human 各自订阅 Update 带来的性能与顺序问题。</item>
    /// <item>InfectionSystem 在成功感染一个 Human 后应调用 <see cref="ReturnHuman"/>，保持活跃列表的准确性。</item>
    /// <item><see cref="Initialize"/> 传入的僵尸位置获取委托会转发给每个 HumanAI，使其无需直接引用 Zombie 命名空间。</item>
    /// </list>
    /// </remarks>
    public class SpawnSystem : MonoBehaviour
    {
        // ==================== Inspector 注入 ====================

        [Header("依赖引用")]
        [Tooltip("对象池管理器，提供 GetHuman / ReturnHuman")]
        [SerializeField] private ObjectPoolManager m_poolManager;

        [Tooltip("游戏配置，提供 HumanInitialCount / HumanMaxCount / HumanSpawnInterval 等参数")]
        [SerializeField] private GameConfig m_config;

        [Tooltip("玩家 Transform，用于计算刷新点与玩家的距离以及转发给 HumanAI 作为威胁源")]
        [SerializeField] private Transform m_playerTransform;

        [Header("刷新区域")]
        [Tooltip("刷新地图范围左下角（XY 世界坐标）")]
        [SerializeField] private Vector2 m_mapMin = new Vector2(-20f, -20f);

        [Tooltip("刷新地图范围右上角（XY 世界坐标）")]
        [SerializeField] private Vector2 m_mapMax = new Vector2(20f, 20f);

        [Tooltip("刷新点与玩家的最小距离，确保人类在玩家视野外生成")]
        [SerializeField, Min(0f)] private float m_spawnMinDistanceFromPlayer = 8f;

        [Header("调参")]
        [Tooltip("寻找满足最小距离约束的随机点的最大尝试次数；超过后退化为任意随机位置")]
        [SerializeField, Min(1)] private int m_maxSpawnPositionAttempts = 10;

        // ==================== 运行时状态 ====================

        /// <summary>当前场上活跃的 Human 列表（未归还池）</summary>
        private readonly List<HumanUnit> m_activeHumans = new List<HumanUnit>();

        /// <summary>自上次刷新以来累计的时间（秒）</summary>
        private float m_spawnTimer;

        /// <summary>由上层注入的僵尸位置获取委托，会转发给每个 HumanAI 作为威胁源集合</summary>
        private Func<IReadOnlyList<Vector2>> m_getZombiePositions;

        /// <summary>仅供 <see cref="ActiveHumans"/> 暴露使用，避免每次调用都分配只读包装</summary>
        private IReadOnlyList<HumanUnit> m_activeHumansReadOnly;

        // ==================== 公开属性 ====================

        /// <summary>当前场上活跃的 Human 数量</summary>
        public int ActiveHumanCount => m_activeHumans.Count;

        /// <summary>
        /// 当前场上活跃的 Human 只读列表。
        /// 供 InfectionSystem、ZombieCompanionAI 等系统遍历查询使用；
        /// 调用方不得修改返回列表，所有增删应通过本系统的公开方法进行。
        /// </summary>
        public IReadOnlyList<HumanUnit> ActiveHumans
        {
            get
            {
                if (m_activeHumansReadOnly == null)
                {
                    m_activeHumansReadOnly = m_activeHumans;
                }
                return m_activeHumansReadOnly;
            }
        }

        // ==================== 初始化 ====================

        /// <summary>
        /// 注入运行时依赖。
        /// 通常由 GameSystemRunner 在进入 Playing 状态前调用，将僵尸位置获取委托向下转发给每个 HumanAI。
        /// </summary>
        /// <param name="getZombiePositions">
        /// 获取当前活跃僵尸同伴位置列表的惰性委托；为 null 时表示暂无僵尸威胁源，HumanAI 将只感知玩家。
        /// </param>
        public void Initialize(Func<IReadOnlyList<Vector2>> getZombiePositions)
        {
            m_getZombiePositions = getZombiePositions;
        }

        // ==================== 公开 API ====================

        /// <summary>
        /// 生成单局开始时的初始批次 Human。
        /// 尝试刷新到 <see cref="GameConfig.HumanInitialCount"/> 个单位，若该值超过
        /// <see cref="GameConfig.HumanMaxCount"/>，按上限钳制。
        /// 同一帧内的多次调用会被活跃上限约束，不会重复超发。
        /// </summary>
        public void SpawnInitialBatch()
        {
            if (!ValidateDependencies())
            {
                return;
            }

            int target = Mathf.Min(m_config.HumanInitialCount, m_config.HumanMaxCount);
            while (m_activeHumans.Count < target)
            {
                if (!SpawnOneHuman())
                {
                    // 池异常或取不到有效实例，立即停止避免死循环
                    break;
                }
            }
        }

        /// <summary>
        /// 按 <see cref="GameConfig.HumanSpawnInterval"/> 间隔补充 Human，直到数量到达 <see cref="GameConfig.HumanMaxCount"/>。
        /// 每当计时器跨过间隔阈值时生成一个 Human；若一帧内累计多次跨越（极低帧率），会在同一帧内按需多次补充。
        /// 已达到上限时累计计时器仍然推进但不刷新，允许玩家在清场后立即触发下一次刷新。
        /// </summary>
        /// <param name="deltaTime">本帧时间增量，通常为 <see cref="Time.deltaTime"/></param>
        public void UpdateSpawn(float deltaTime)
        {
            if (!ValidateDependencies() || deltaTime <= 0f)
            {
                return;
            }

            float interval = m_config.HumanSpawnInterval;
            if (interval <= 0f)
            {
                // 非法间隔视为禁用周期性刷新，仅保留初始批次与被动补充
                return;
            }

            m_spawnTimer += deltaTime;

            // 允许在低帧率场景下一帧补充多个，避免长期欠发
            while (m_spawnTimer >= interval)
            {
                m_spawnTimer -= interval;

                if (m_activeHumans.Count >= m_config.HumanMaxCount)
                {
                    // 已满上限：不再消耗更多间隔，避免囤积无效时间
                    m_spawnTimer = 0f;
                    break;
                }

                if (!SpawnOneHuman())
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 统一推进所有活跃 Human 的 AI 行为。
        /// <see cref="HumanAI"/> 不订阅 Unity Update，由本方法在 Playing 状态下每帧调用一次。
        /// </summary>
        /// <param name="deltaTime">本帧时间增量</param>
        public void UpdateHumanAIs(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            // 避免 AI 内部触发感染或其他修改活跃列表的操作时引发枚举异常：使用 for 循环按索引遍历
            for (int i = 0; i < m_activeHumans.Count; i++)
            {
                HumanUnit human = m_activeHumans[i];
                if (human == null)
                {
                    continue;
                }

                HumanAI ai = human.GetComponent<HumanAI>();
                if (ai != null)
                {
                    ai.UpdateAI(deltaTime);
                }
            }
        }

        /// <summary>
        /// 将指定 Human 从活跃列表中移除并归还到对象池。
        /// 主要由 InfectionSystem 在感染成功后调用。
        /// 若传入 null 或未在活跃列表中的实例将被忽略。
        /// </summary>
        /// <param name="human">需要回收的 Human</param>
        public void ReturnHuman(HumanUnit human)
        {
            if (human == null)
            {
                return;
            }

            // 使用 Remove 而不是索引移除：调用方一般不知道索引，且感染事件频率有限，线性移除开销可接受
            bool removed = m_activeHumans.Remove(human);
            if (!removed)
            {
                // 对未在活跃列表中的 Human 仍然尝试归还池，保持池计数健康
                Debug.LogWarning("[SpawnSystem] ReturnHuman 收到不在活跃列表中的 Human，已直接归还池");
            }

            if (m_poolManager != null)
            {
                m_poolManager.ReturnHuman(human);
            }
        }

        /// <summary>
        /// 重置系统状态，通常在单局结束或重新开始时调用。
        /// 将所有活跃 Human 归还池、清空活跃列表、清零计时器。
        /// </summary>
        public void Reset()
        {
            // 复制后遍历，避免 ReturnHuman 中的 Remove 在遍历过程中修改原列表
            if (m_poolManager != null)
            {
                for (int i = 0; i < m_activeHumans.Count; i++)
                {
                    HumanUnit human = m_activeHumans[i];
                    if (human != null)
                    {
                        m_poolManager.ReturnHuman(human);
                    }
                }
            }

            m_activeHumans.Clear();
            m_spawnTimer = 0f;
        }

        // ==================== 内部辅助 ====================

        /// <summary>
        /// 从对象池取出一个 Human 并放置到一个合法的随机位置；成功时加入活跃列表。
        /// </summary>
        /// <returns>成功刷新返回 true；池取不到实例或其他异常返回 false 让调用方中止后续循环</returns>
        private bool SpawnOneHuman()
        {
            HumanUnit human = m_poolManager.GetHuman();
            if (human == null)
            {
                Debug.LogWarning("[SpawnSystem] ObjectPoolManager.GetHuman 返回 null，刷新终止");
                return false;
            }

            Vector2 spawnPos = PickSpawnPosition();
            human.transform.position = new Vector3(spawnPos.x, spawnPos.y, 0f);

            // 为取出的 Human 注入 AI 依赖：若缺少 HumanAI 组件只记录警告，不阻断刷新流程
            HumanAI ai = human.GetComponent<HumanAI>();
            if (ai != null)
            {
                ai.Initialize(m_config, m_playerTransform, m_getZombiePositions);
            }
            else
            {
                Debug.LogWarning("[SpawnSystem] Human 预制体缺少 HumanAI 组件，本次刷新不会获得 AI 行为");
            }

            m_activeHumans.Add(human);
            return true;
        }

        /// <summary>
        /// 选择一个刷新位置：在地图矩形范围内随机取点，并尽量保证与玩家距离 &gt;= <see cref="m_spawnMinDistanceFromPlayer"/>。
        /// 若在 <see cref="m_maxSpawnPositionAttempts"/> 次内找不到满足距离约束的点（例如玩家站在地图中心且地图较小），
        /// 直接返回最后一次随机结果以保证刷新流程不被阻塞。
        /// </summary>
        private Vector2 PickSpawnPosition()
        {
            Vector2 candidate = Vector2.zero;

            // 若未注入玩家 Transform，则不需要距离约束，直接返回一次随机点
            bool hasPlayer = m_playerTransform != null;
            float sqrMinDist = m_spawnMinDistanceFromPlayer * m_spawnMinDistanceFromPlayer;
            Vector2 playerPos = Vector2.zero;
            if (hasPlayer)
            {
                Vector3 playerWorld = m_playerTransform.position;
                playerPos = new Vector2(playerWorld.x, playerWorld.y);
            }

            for (int attempt = 0; attempt < m_maxSpawnPositionAttempts; attempt++)
            {
                float rx = UnityEngine.Random.Range(m_mapMin.x, m_mapMax.x);
                float ry = UnityEngine.Random.Range(m_mapMin.y, m_mapMax.y);
                candidate = new Vector2(rx, ry);

                if (!hasPlayer)
                {
                    return candidate;
                }

                if ((candidate - playerPos).sqrMagnitude >= sqrMinDist)
                {
                    return candidate;
                }
            }

            // 超过尝试次数：退化为任意随机位置（使用最后一次 candidate，避免再调用一次 Random）
            return candidate;
        }

        /// <summary>
        /// 校验关键依赖是否已通过 Inspector 注入，缺失时输出明确错误。
        /// </summary>
        private bool ValidateDependencies()
        {
            if (m_poolManager == null)
            {
                Debug.LogError("[SpawnSystem] ObjectPoolManager 未赋值，无法刷新 Human");
                return false;
            }
            if (m_config == null)
            {
                Debug.LogError("[SpawnSystem] GameConfig 未赋值，无法刷新 Human");
                return false;
            }
            return true;
        }
    }
}
