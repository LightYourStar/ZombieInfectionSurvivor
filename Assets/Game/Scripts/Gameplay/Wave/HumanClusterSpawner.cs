using System;
using System.Collections.Generic;
using Game.Config;
using Game.Gameplay.Enemy;
using Game.Utility;
using UnityEngine;

namespace Game.Gameplay.Wave
{
    /// <summary>
    /// 人群簇刷新系统。
    /// 按簇（SmallCluster 3-5 人、MediumCluster 8-12 人）生成人类，替代 SpawnSystem 的均匀随机刷新逻辑。
    /// 簇内人类使用均匀圆盘分布（uniform disk distribution）放置，使人群看起来自然聚集。
    /// </summary>
    /// <remarks>
    /// 协作约定：
    /// <list type="bullet">
    /// <item>本系统不直接管理 ActiveHumans 列表，而是通过 SpawnSystem 的接口保持列表一致性。</item>
    /// <item>复用 ObjectPoolManager.GetHuman() 取出人类实例，生成后注册到 SpawnSystem.ActiveHumans。</item>
    /// <item>本系统不订阅 Unity 的 Update，所有推进由 GameSystemRunner 调度 <see cref="UpdateSpawn"/>。</item>
    /// <item>保留 SpawnSystem 的 ActiveHumans、ReturnHuman、UpdateHumanAIs、ClampToMap 等接口不变。</item>
    /// </list>
    /// </remarks>
    public class HumanClusterSpawner : MonoBehaviour
    {
        // ==================== Inspector 注入 ====================

        [Header("依赖引用")]
        [Tooltip("对象池管理器，提供 GetHuman 接口")]
        [SerializeField] private ObjectPoolManager m_poolManager;

        [Tooltip("刷新系统，提供 ActiveHumans 列表管理")]
        [SerializeField] private SpawnSystem m_spawnSystem;

        [Tooltip("游戏配置，提供 HumanMaxCount 等参数")]
        [SerializeField] private GameConfig m_config;

        [Tooltip("玩家 Transform，用于计算簇放置距离")]
        [SerializeField] private Transform m_playerTransform;

        // ==================== 簇配置参数 ====================

        [Header("簇大小配置")]
        [Tooltip("小人群簇最少人数")]
        [SerializeField] private int m_smallClusterMin = 3;

        [Tooltip("小人群簇最多人数")]
        [SerializeField] private int m_smallClusterMax = 5;

        [Tooltip("中人群簇最少人数")]
        [SerializeField] private int m_mediumClusterMin = 8;

        [Tooltip("中人群簇最多人数")]
        [SerializeField] private int m_mediumClusterMax = 12;

        [Header("簇刷新配置")]
        [Tooltip("持续补充簇的时间间隔（秒）")]
        [SerializeField] private float m_clusterSpawnInterval = 3f;

        [Tooltip("簇间最小中心距离")]
        [SerializeField] private float m_clusterMinSeparation = 5f;

        [Header("初始生成配置")]
        [Tooltip("初始簇距玩家最小距离")]
        [SerializeField] private float m_initialSpawnMinDist = 3f;

        [Tooltip("初始簇距玩家最大距离")]
        [SerializeField] private float m_initialSpawnMaxDist = 8f;

        [Header("持续补充配置")]
        [Tooltip("持续补充簇距玩家最小距离")]
        [SerializeField] private float m_periodicSpawnMinDistFromPlayer = 8f;

        [Header("调参")]
        [Tooltip("簇内人类分布半径")]
        [SerializeField, Min(0.5f)] private float m_clusterRadius = 2f;

        [Tooltip("寻找满足约束的簇中心位置的最大尝试次数")]
        [SerializeField, Min(1)] private int m_maxPlacementAttempts = 30;

        // ==================== 运行时状态 ====================

        /// <summary>自上次簇刷新以来累计的时间（秒）</summary>
        private float m_spawnTimer;

        /// <summary>已生成的簇中心位置列表，用于簇间距约束检查</summary>
        private readonly List<Vector2> m_clusterCenters = new List<Vector2>();

        /// <summary>由上层注入的僵尸位置获取委托，转发给 HumanAI</summary>
        private Func<IReadOnlyList<Vector2>> m_getZombiePositions;

        /// <summary>是否已进入末日狂潮模式</summary>
        private bool m_isFinalFrenzy;

        // ==================== 公开属性 ====================

        /// <summary>小簇最少人数（供测试访问）</summary>
        public int SmallClusterMin => m_smallClusterMin;

        /// <summary>小簇最多人数（供测试访问）</summary>
        public int SmallClusterMax => m_smallClusterMax;

        /// <summary>中簇最少人数（供测试访问）</summary>
        public int MediumClusterMin => m_mediumClusterMin;

        /// <summary>中簇最多人数（供测试访问）</summary>
        public int MediumClusterMax => m_mediumClusterMax;

        /// <summary>簇刷新间隔（供测试访问）</summary>
        public float ClusterSpawnInterval => m_clusterSpawnInterval;

        /// <summary>簇间最小中心距离（供测试访问）</summary>
        public float ClusterMinSeparation => m_clusterMinSeparation;

        /// <summary>初始簇距玩家最小距离（供测试访问）</summary>
        public float InitialSpawnMinDist => m_initialSpawnMinDist;

        /// <summary>初始簇距玩家最大距离（供测试访问）</summary>
        public float InitialSpawnMaxDist => m_initialSpawnMaxDist;

        /// <summary>持续补充簇距玩家最小距离（供测试访问）</summary>
        public float PeriodicSpawnMinDistFromPlayer => m_periodicSpawnMinDistFromPlayer;

        /// <summary>已生成的簇中心位置只读列表</summary>
        public IReadOnlyList<Vector2> ClusterCenters => m_clusterCenters;

        // ==================== 初始化 ====================

        /// <summary>
        /// 注入运行时依赖。
        /// 由 GameSystemRunner 在进入 Playing 状态前调用。
        /// </summary>
        /// <param name="getZombiePositions">获取当前活跃僵尸位置列表的委托，转发给 HumanAI</param>
        public void Initialize(Func<IReadOnlyList<Vector2>> getZombiePositions)
        {
            m_getZombiePositions = getZombiePositions;

            // 先取消再订阅，防止重复初始化导致重复订阅
            Game.Core.GameEvents.OnFinalFrenzyStarted -= HandleFinalFrenzyStarted;
            Game.Core.GameEvents.OnFinalFrenzyStarted += HandleFinalFrenzyStarted;
        }

        // ==================== 公开 API ====================

        /// <summary>
        /// 生成单局开始时的初始簇。
        /// 在玩家附近生成 2-3 个 SmallCluster，簇中心距玩家 3-8 单位，簇间中心距离 >= 5。
        /// </summary>
        public void SpawnInitialClusters()
        {
            if (!ValidateDependencies() || m_playerTransform == null)
            {
                return;
            }

            // 随机选择生成 2 或 3 个初始簇
            int clusterCount = UnityEngine.Random.Range(2, 4);

            for (int i = 0; i < clusterCount; i++)
            {
                // 选择满足约束的簇中心位置：距玩家 [3, 8] 且与已有簇中心距离 >= 5
                Vector2 center = PickClusterCenterInRange(
                    m_initialSpawnMinDist,
                    m_initialSpawnMaxDist,
                    m_clusterMinSeparation);

                // 随机选择小簇大小 [3, 5]
                int size = UnityEngine.Random.Range(m_smallClusterMin, m_smallClusterMax + 1);

                SpawnCluster(center, size);
            }
        }

        /// <summary>
        /// 按配置间隔持续补充新的簇。
        /// 每帧累积 deltaTime，当累积时间达到 ClusterSpawnInterval 时尝试生成一个新簇。
        /// 随机选择 SmallCluster（3-5 人）或 MediumCluster（8-12 人）。
        /// 末日狂潮期间使用更短间隔和更大簇。
        /// 新簇中心距玩家 >= 8 单位，与已有簇中心距离 >= 5。
        /// 当场上人类总数 >= HumanMaxCount 时停止生成。
        /// </summary>
        /// <param name="deltaTime">本帧时间增量</param>
        public void UpdateSpawn(float deltaTime)
        {
            if (!ValidateDependencies() || m_playerTransform == null)
            {
                return;
            }

            m_spawnTimer += deltaTime;

            // 末日狂潮期间使用更短的刷新间隔
            float currentInterval = m_isFinalFrenzy && m_config.EnableFinalFrenzy
                ? m_config.FinalFrenzyClusterSpawnInterval
                : m_clusterSpawnInterval;

            // 当累积时间达到间隔阈值时尝试生成
            while (m_spawnTimer >= currentInterval)
            {
                // 减去间隔而非归零，保留余数以维持稳定的生成节奏
                m_spawnTimer -= currentInterval;

                // 检查人类数量上限：达到上限时停止生成
                int currentHumanCap = GetCurrentHumanMaxCount();
                if (m_spawnSystem.ActiveHumanCount >= currentHumanCap)
                {
                    break;
                }

                // 根据是否处于末日狂潮选择簇大小
                int clusterSize;
                if (m_isFinalFrenzy && m_config.EnableFinalFrenzy)
                {
                    // 末日狂潮：70% 大簇，30% 中簇
                    if (UnityEngine.Random.value < 0.7f)
                    {
                        clusterSize = UnityEngine.Random.Range(
                            m_config.FinalFrenzyLargeClusterMin,
                            m_config.FinalFrenzyLargeClusterMax + 1);
                    }
                    else
                    {
                        clusterSize = UnityEngine.Random.Range(m_mediumClusterMin, m_mediumClusterMax + 1);
                    }
                }
                else
                {
                    // 正常模式：50% SmallCluster，50% MediumCluster
                    if (UnityEngine.Random.value < 0.5f)
                    {
                        clusterSize = UnityEngine.Random.Range(m_smallClusterMin, m_smallClusterMax + 1);
                    }
                    else
                    {
                        clusterSize = UnityEngine.Random.Range(m_mediumClusterMin, m_mediumClusterMax + 1);
                    }
                }

                // 选择满足约束的簇中心位置：距玩家 >= 8，与已有簇中心距离 >= 5
                Vector2 center = PickClusterCenter(m_periodicSpawnMinDistFromPlayer, m_clusterMinSeparation);

                // 生成簇
                SpawnCluster(center, clusterSize);
            }
        }

        /// <summary>
        /// 重置系统状态，用于新一局开始时清空簇中心记录和计时器。
        /// </summary>
        public void Reset()
        {
            m_clusterCenters.Clear();
            m_spawnTimer = 0f;
            m_isFinalFrenzy = false;
        }

        /// <summary>
        /// 末日狂潮事件处理：切换到狂潮模式，立即刷一波大簇，并重置计时器以开始高频刷新。
        /// </summary>
        private void HandleFinalFrenzyStarted()
        {
            m_isFinalFrenzy = true;
            // 重置计时器，让狂潮立即开始高频生成
            m_spawnTimer = m_config != null ? m_config.FinalFrenzyClusterSpawnInterval : 1f;

            // 立即刷一波大簇，不等下一次自然间隔
            if (ValidateDependencies() && m_playerTransform != null && m_config != null)
            {
                int burstSize = UnityEngine.Random.Range(
                    m_config.FinalFrenzyLargeClusterMin,
                    m_config.FinalFrenzyLargeClusterMax + 1);
                Vector2 center = PickClusterCenter(m_periodicSpawnMinDistFromPlayer, m_clusterMinSeparation);
                SpawnCluster(center, burstSize);
            }
        }

        private void OnDestroy()
        {
            Game.Core.GameEvents.OnFinalFrenzyStarted -= HandleFinalFrenzyStarted;
        }

        // ==================== 内部方法 ====================

        /// <summary>
        /// 在指定中心位置生成一个包含 count 个人类的簇。
        /// 使用均匀圆盘分布（uniform disk distribution）在簇半径内放置人类，
        /// 使人群看起来自然聚集而非规则排列。
        /// </summary>
        /// <param name="center">簇的中心位置（世界坐标 XY）</param>
        /// <param name="count">簇内人类数量</param>
        internal void SpawnCluster(Vector2 center, int count)
        {
            if (!ValidateDependencies())
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                // 检查是否已达到人类上限
                if (m_config != null && m_spawnSystem != null &&
                    m_spawnSystem.ActiveHumanCount >= GetCurrentHumanMaxCount())
                {
                    break;
                }

                HumanUnit human = m_poolManager.GetHuman();
                if (human == null)
                {
                    Debug.LogWarning("[HumanClusterSpawner] ObjectPoolManager.GetHuman 返回 null，停止当前簇的剩余生成");
                    break;
                }

                // 使用均匀圆盘分布计算簇内位置
                Vector2 spawnPos = GetPositionInCluster(center, m_clusterRadius);

                // 将位置钳制到地图范围内
                if (m_spawnSystem != null)
                {
                    spawnPos = m_spawnSystem.ClampToMap(spawnPos);
                }

                human.transform.position = new Vector3(spawnPos.x, spawnPos.y, 0f);

                // 为取出的 Human 注入 AI 依赖
                HumanAI ai = human.GetComponent<HumanAI>();
                if (ai != null)
                {
                    ai.Initialize(m_config, m_playerTransform, m_getZombiePositions);
                }

                // 注册到 SpawnSystem 的活跃列表
                RegisterHumanToSpawnSystem(human);
            }

            // 记录簇中心位置，用于后续簇间距约束检查
            m_clusterCenters.Add(center);
        }

        /// <summary>
        /// 选择簇中心位置，约束距玩家距离在 [minDist, maxDist] 范围内，
        /// 且与已有簇中心距离 >= minSeparation。
        /// 用于初始簇生成（Task 4.2），确保簇在玩家附近但不太近。
        /// </summary>
        /// <param name="minDist">簇中心距玩家的最小距离</param>
        /// <param name="maxDist">簇中心距玩家的最大距离</param>
        /// <param name="minSeparation">簇中心与已有簇中心的最小距离</param>
        /// <returns>满足约束的簇中心位置</returns>
        internal Vector2 PickClusterCenterInRange(float minDist, float maxDist, float minSeparation)
        {
            if (m_spawnSystem == null || m_playerTransform == null)
            {
                return Vector2.zero;
            }

            Vector2 mapMin = m_spawnSystem.MapMin;
            Vector2 mapMax = m_spawnSystem.MapMax;
            Vector2 playerPos = new Vector2(m_playerTransform.position.x, m_playerTransform.position.y);

            float sqrMinDist = minDist * minDist;
            float sqrMaxDist = maxDist * maxDist;
            float sqrMinSeparation = minSeparation * minSeparation;

            Vector2 candidate = Vector2.zero;

            for (int attempt = 0; attempt < m_maxPlacementAttempts; attempt++)
            {
                // 在玩家周围的环形区域 [minDist, maxDist] 内随机选点
                // 使用极坐标方式生成，确保均匀分布在环形区域内
                float angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
                // 在 [minDist^2, maxDist^2] 之间均匀采样后开方，确保面积均匀
                float r = Mathf.Sqrt(UnityEngine.Random.Range(sqrMinDist, sqrMaxDist));

                candidate = new Vector2(
                    playerPos.x + r * Mathf.Cos(angle),
                    playerPos.y + r * Mathf.Sin(angle));

                // 检查是否在地图范围内
                if (candidate.x < mapMin.x || candidate.x > mapMax.x ||
                    candidate.y < mapMin.y || candidate.y > mapMax.y)
                {
                    continue;
                }

                // 检查与已有簇中心的距离
                bool tooClose = false;
                for (int j = 0; j < m_clusterCenters.Count; j++)
                {
                    if ((candidate - m_clusterCenters[j]).sqrMagnitude < sqrMinSeparation)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    return candidate;
                }
            }

            // 超过尝试次数：退化为最后一次候选位置
            Debug.LogWarning("[HumanClusterSpawner] PickClusterCenterInRange 超过最大尝试次数，使用退化位置");
            return candidate;
        }

        /// <summary>
        /// 选择簇中心位置。
        /// 由 Task 4.2 和 4.3 使用，确保满足距离约束。
        /// </summary>
        /// <param name="minDistFromPlayer">簇中心距玩家的最小距离</param>
        /// <param name="minDistFromOtherClusters">簇中心与已有簇中心的最小距离</param>
        /// <returns>满足约束的簇中心位置</returns>
        internal Vector2 PickClusterCenter(float minDistFromPlayer, float minDistFromOtherClusters)
        {
            // Task 4.2 / 4.3 将完善此方法的具体实现
            // 基础实现：在地图范围内随机选点，满足距离约束
            if (m_spawnSystem == null || m_playerTransform == null)
            {
                return Vector2.zero;
            }

            Vector2 mapMin = m_spawnSystem.MapMin;
            Vector2 mapMax = m_spawnSystem.MapMax;
            Vector2 playerPos = new Vector2(m_playerTransform.position.x, m_playerTransform.position.y);

            float sqrMinDistPlayer = minDistFromPlayer * minDistFromPlayer;
            float sqrMinDistCluster = minDistFromOtherClusters * minDistFromOtherClusters;

            Vector2 candidate = Vector2.zero;

            for (int attempt = 0; attempt < m_maxPlacementAttempts; attempt++)
            {
                float rx = UnityEngine.Random.Range(mapMin.x, mapMax.x);
                float ry = UnityEngine.Random.Range(mapMin.y, mapMax.y);
                candidate = new Vector2(rx, ry);

                // 检查与玩家的距离
                if ((candidate - playerPos).sqrMagnitude < sqrMinDistPlayer)
                {
                    continue;
                }

                // 检查与已有簇中心的距离
                bool tooClose = false;
                for (int j = 0; j < m_clusterCenters.Count; j++)
                {
                    if ((candidate - m_clusterCenters[j]).sqrMagnitude < sqrMinDistCluster)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    return candidate;
                }
            }

            // 超过尝试次数：退化为最后一次候选位置
            Debug.LogWarning("[HumanClusterSpawner] 超过最大尝试次数，使用退化位置");
            return candidate;
        }

        /// <summary>
        /// 使用均匀圆盘分布（uniform disk distribution）在指定中心和半径内生成一个随机位置。
        /// 算法：使用 sqrt(random) * radius 确保面积均匀分布，避免中心聚集。
        /// </summary>
        /// <param name="center">圆盘中心位置</param>
        /// <param name="radius">圆盘半径</param>
        /// <returns>圆盘内的随机位置</returns>
        internal Vector2 GetPositionInCluster(Vector2 center, float radius)
        {
            // 均匀圆盘分布：r = sqrt(U) * R, θ = 2π * V
            // 其中 U, V 为 [0, 1) 均匀随机数
            float u = UnityEngine.Random.value;
            float v = UnityEngine.Random.value;

            float r = Mathf.Sqrt(u) * radius;
            float theta = 2f * Mathf.PI * v;

            float offsetX = r * Mathf.Cos(theta);
            float offsetY = r * Mathf.Sin(theta);

            return new Vector2(center.x + offsetX, center.y + offsetY);
        }

        // ==================== 辅助方法 ====================

        /// <summary>
        /// 将生成的 Human 注册到 SpawnSystem 的活跃列表。
        /// 通过反射或内部接口访问 SpawnSystem 的 m_activeHumans 列表。
        /// 这里使用 SpawnSystem 暴露的公开方式来保持列表一致性。
        /// </summary>
        /// <param name="human">需要注册的 Human 实例</param>
        private void RegisterHumanToSpawnSystem(HumanUnit human)
        {
            // SpawnSystem 的 ActiveHumans 是只读列表，我们需要通过内部方式注册
            // 使用 SpawnSystem 提供的内部注册接口
            if (m_spawnSystem != null)
            {
                m_spawnSystem.RegisterHuman(human);
            }
        }

        /// <summary>
        /// 获取当前生效的人类数量上限。
        /// 末日狂潮期间且启用了上限覆盖时使用 FinalFrenzyHumanMaxCount，否则使用 HumanMaxCount。
        /// </summary>
        private int GetCurrentHumanMaxCount()
        {
            if (m_config == null)
            {
                return 50;
            }

            if (m_isFinalFrenzy && m_config.EnableFinalFrenzyHumanCapOverride)
            {
                return m_config.FinalFrenzyHumanMaxCount;
            }

            return m_config.HumanMaxCount;
        }

        /// <summary>
        /// 校验关键依赖是否已通过 Inspector 注入。
        /// </summary>
        private bool ValidateDependencies()
        {
            if (m_poolManager == null)
            {
                Debug.LogError("[HumanClusterSpawner] ObjectPoolManager 未赋值，无法生成人类");
                return false;
            }
            if (m_spawnSystem == null)
            {
                Debug.LogError("[HumanClusterSpawner] SpawnSystem 未赋值，无法注册人类到活跃列表");
                return false;
            }
            if (m_config == null)
            {
                Debug.LogError("[HumanClusterSpawner] GameConfig 未赋值，无法读取配置");
                return false;
            }
            return true;
        }
    }
}
