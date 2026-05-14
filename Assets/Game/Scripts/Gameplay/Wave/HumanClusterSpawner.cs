using System;
using System.Collections;
using System.Collections.Generic;
using Game.Config;
using Game.Gameplay.Enemy;
using Game.Utility;
using UnityEngine;

namespace Game.Gameplay.Wave
{
    public enum InfectionFlowState
    {
        Normal,
        LightBreak,
        SevereBreak
    }

    [Serializable]
    public sealed class HotspotDebugStats
    {
        [SerializeField] private string m_hotspotName;
        [SerializeField] private int m_spawnCount;
        [SerializeField] private int m_nearbyInfectionCount;

        public string HotspotName => m_hotspotName;
        public int SpawnCount => m_spawnCount;
        public int NearbyInfectionCount => m_nearbyInfectionCount;

        internal HotspotDebugStats(string hotspotName)
        {
            m_hotspotName = hotspotName;
        }

        internal void SetHotspotName(string hotspotName)
        {
            m_hotspotName = hotspotName;
        }

        internal void Reset()
        {
            m_spawnCount = 0;
            m_nearbyInfectionCount = 0;
        }

        internal void RecordSpawn()
        {
            m_spawnCount++;
        }

        internal void RecordNearbyInfection()
        {
            m_nearbyInfectionCount++;
        }
    }

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

        [Tooltip("地图运行时控制器，提供白盒阻挡查询")]
        [SerializeField] private MapRuntimeController m_mapRuntimeController;

        [Header("热点配置")]
        [Tooltip("热点根节点，留空时会在场景中自动查找 SpawnHotspot")]
        [SerializeField] private Transform m_hotspotRoot;

        [Tooltip("可用于刷新人群簇的热点列表")]
        [SerializeField] private List<SpawnHotspot> m_hotspots = new List<SpawnHotspot>();

        [Tooltip("末日狂潮期间对标记热点使用的权重倍率")]
        [SerializeField, Min(1f)] private float m_finalFrenzyHotspotWeightMultiplier = 3f;

        [Tooltip("开局优先附近热点的持续时间")]
        [SerializeField, Min(0f)] private float m_openingPriorityDuration = 30f;

        [Tooltip("轻度断流阈值：超过该时间未感染时，下一波优先玩家附近热点")]
        [SerializeField, Min(0f)] private float m_lightBreakThreshold = 3f;

        [Tooltip("严重断流阈值：超过该时间未感染时，立即尝试玩家附近保底小簇")]
        [SerializeField, Min(0f)] private float m_severeBreakThreshold = 5f;

        [Tooltip("严重断流保底冷却，避免连续补流")]
        [SerializeField, Min(0.5f)] private float m_flowTopUpCooldown = 4f;

        [Tooltip("断流保底小簇距离玩家的最小距离")]
        [SerializeField, Min(0f)] private float m_flowFallbackMinDistance = 5f;

        [Tooltip("断流保底小簇距离玩家的最大距离")]
        [SerializeField, Min(0f)] private float m_flowFallbackMaxDistance = 9f;

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

        [Tooltip("可感染 Human 低于该值时立即触发一次保底补充")]
        [SerializeField, Min(0)] private int m_minAliveHumans = 20;

        [Tooltip("可感染 Human 低于该值时提高常规刷新频率")]
        [SerializeField, Min(0)] private int m_comfortAliveHumans = 40;

        [Tooltip("可感染 Human 低于舒适值时，常规刷新间隔倍率")]
        [SerializeField, Range(0.1f, 1f)] private float m_comfortIntervalMultiplier = 0.5f;

        [Tooltip("低 Human 保底补充冷却，避免同一秒内连续补充多波")]
        [SerializeField, Min(0.1f)] private float m_lowHumanTopUpCooldown = 1f;

        [Header("初始生成配置")]
        [Tooltip("初始簇距玩家最小距离")]
        [SerializeField] private float m_initialSpawnMinDist = 3f;

        [Tooltip("初始簇距玩家最大距离")]
        [SerializeField] private float m_initialSpawnMaxDist = 8f;

        [Header("持续补充配置")]
        [Tooltip("持续补充簇距玩家最小距离")]
        [SerializeField] private float m_periodicSpawnMinDistFromPlayer = 8f;

        [Tooltip("玩家附近 Human 检测间隔")]
        [SerializeField, Min(0.1f)] private float m_nearbyHumanCheckInterval = 1f;

        [Tooltip("玩家附近可感染 Human 检测半径")]
        [SerializeField, Min(1f)] private float m_nearbyHumanDetectionRadius = 12f;

        [Tooltip("连续多少秒玩家附近没有 Human 时触发保底补充")]
        [SerializeField, Min(1f)] private float m_noNearbyHumanThreshold = 5f;

        [Tooltip("附近无 Human 保底优先使用的最近热点最大距离")]
        [SerializeField, Min(1f)] private float m_nearbyFallbackHotspotMaxDistance = 18f;

        [Tooltip("附近无 Human 保底冷却，避免连续补充")]
        [SerializeField, Min(0.5f)] private float m_nearbyFallbackCooldown = 4f;

        [Header("调参")]
        [Tooltip("Debug 调参用的人类上限覆盖。0 表示继续使用 GameConfig，不改变默认平衡")]
        [SerializeField, Min(0)] private int m_maxAliveHumansOverride;

        [Tooltip("簇内人类分布半径")]
        [SerializeField, Min(0.5f)] private float m_clusterRadius = 2f;

        [Tooltip("寻找满足约束的簇中心位置的最大尝试次数")]
        [SerializeField, Min(1)] private int m_maxPlacementAttempts = 30;

        // ==================== 运行时状态 ====================

        /// <summary>簇中心历史记录最大保留数量，超过后移除最旧的，避免后期选点越来越重</summary>
        private const int MaxClusterCenterHistory = 16;

        /// <summary>自上次簇刷新以来累计的时间（秒）</summary>
        private float m_spawnTimer;

        /// <summary>已生成的簇中心位置列表，用于簇间距约束检查</summary>
        private readonly List<Vector2> m_clusterCenters = new List<Vector2>();

        /// <summary>由上层注入的僵尸位置获取委托，转发给 HumanAI</summary>
        private Func<IReadOnlyList<Vector2>> m_getZombiePositions;

        /// <summary>是否已进入末日狂潮模式</summary>
        private bool m_isFinalFrenzy;

        /// <summary>当前单局已进行时间，由 UpdateSpawn 推进</summary>
        private float m_elapsedTime;

        /// <summary>最近一次感染发生的单局时间</summary>
        private float m_lastInfectionElapsedTime;

        /// <summary>最近一次严重断流保底触发时间</summary>
        private float m_lastFlowTopUpElapsedTime = -999f;

        private bool m_recentFlowTopUpTriggered;
        private string m_lastSpawnSourceText = "未刷新";
        private string m_lastSpawnDetailText = "无";
        private string m_lastSpawnHotspotName = "无";
        private Vector2 m_lastSpawnPosition;
        private float m_nextPlacementWarningTime;
        private float m_firstInfectionTime = -1f;
        private float m_maxNoInfectionDuration;
        private float m_lastSuccessfulSpawnElapsedTime = -1f;
        private string m_lastGuaranteeSpawnReason = "无";
        private float m_nearbyHumanCheckTimer;
        private float m_noNearbyHumanDuration;
        private float m_lastLowHumanTopUpElapsedTime = -999f;
        private float m_lastNearbyFallbackElapsedTime = -999f;

        /// <summary>严格断流保底：上次触发时间</summary>
        private float m_lastStrictFallbackTime = -999f;

        /// <summary>严格断流保底：本局触发次数</summary>
        private int m_strictFallbackCount;

        [SerializeField] private List<HotspotDebugStats> m_hotspotDebugStats = new List<HotspotDebugStats>();
        private readonly Dictionary<SpawnHotspot, HotspotDebugStats> m_hotspotStatsByHotspot = new Dictionary<SpawnHotspot, HotspotDebugStats>();

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

        /// <summary>当前配置的人群热点列表</summary>
        public IReadOnlyList<SpawnHotspot> Hotspots => m_hotspots;
        public IReadOnlyList<HotspotDebugStats> HotspotStats => m_hotspotDebugStats;

        public float ElapsedTime => m_elapsedTime;
        public bool IsFinalFrenzyActive => m_isFinalFrenzy && m_config != null && m_config.EnableFinalFrenzy;
        public float SecondsUntilNextSpawn => GetSecondsUntilNextSpawn();
        public int EnabledHotspotCount => CountEnabledHotspots();
        public int CurrentAliveHumanCount => CountAliveHumans();
        public int MinAliveHumans => m_minAliveHumans;
        public int ComfortAliveHumans => m_comfortAliveHumans;
        public int CurrentHumanMaxCount => GetCurrentHumanMaxCount();
        public float FirstInfectionTime => m_firstInfectionTime;
        public float MaxNoInfectionDuration => Mathf.Max(m_maxNoInfectionDuration, SecondsSinceLastInfection);
        public float SecondsSinceLastInfection => Mathf.Max(0f, m_elapsedTime - m_lastInfectionElapsedTime);
        public float SecondsSinceLastSuccessfulSpawn => m_lastSuccessfulSpawnElapsedTime >= 0f
            ? Mathf.Max(0f, m_elapsedTime - m_lastSuccessfulSpawnElapsedTime)
            : m_elapsedTime;
        public string LastGuaranteeSpawnReason => m_lastGuaranteeSpawnReason;
        public float NoNearbyHumanDuration => m_noNearbyHumanDuration;

        /// <summary>本局严格断流保底触发次数</summary>
        public int StrictFallbackCount => m_strictFallbackCount;
        public InfectionFlowState CurrentFlowState => GetCurrentFlowState();
        public bool RecentFlowTopUpTriggered => m_recentFlowTopUpTriggered;
        public string LastSpawnSourceText => m_lastSpawnSourceText;
        public string LastSpawnDetailText => m_lastSpawnDetailText;
        public string LastSpawnHotspotName => m_lastSpawnHotspotName;
        public Vector2 LastSpawnPosition => m_lastSpawnPosition;
        public float FlowTopUpCooldownRemaining => Mathf.Max(0f, m_flowTopUpCooldown - (m_elapsedTime - m_lastFlowTopUpElapsedTime));

        // ==================== 初始化 ====================

        /// <summary>
        /// 注入运行时依赖。
        /// 由 GameSystemRunner 在进入 Playing 状态前调用。
        /// </summary>
        /// <param name="getZombiePositions">获取当前活跃僵尸位置列表的委托，转发给 HumanAI</param>
        public void Initialize(Func<IReadOnlyList<Vector2>> getZombiePositions)
        {
            m_getZombiePositions = getZombiePositions;
            if (m_mapRuntimeController == null)
            {
                m_mapRuntimeController = FindObjectOfType<MapRuntimeController>();
            }
            RefreshHotspots();

            // 先取消再订阅，防止重复初始化导致重复订阅
            Game.Core.GameEvents.OnInfectionSuccess -= HandleInfectionSuccess;
            Game.Core.GameEvents.OnInfectionSuccess += HandleInfectionSuccess;
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
                if (TrySpawnClusterFromHotspot(false, true))
                {
                    continue;
                }

                // 选择满足约束的簇中心位置：距玩家 [3, 8] 且与已有簇中心距离 >= 5
                Vector2 center = PickClusterCenterInRange(
                    m_initialSpawnMinDist,
                    m_initialSpawnMaxDist,
                    m_clusterMinSeparation);

                // 随机选择小簇大小 [3, 5]
                int size = UnityEngine.Random.Range(m_smallClusterMin, m_smallClusterMax + 1);

                int spawnedCount = SpawnCluster(center, size);
                if (spawnedCount > 0)
                {
                    RecordSpawn("随机回退", $"开局 {FormatPosition(center)}", center, null, "NormalInterval");
                }
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
            if (!ValidateDependencies() || m_playerTransform == null || deltaTime <= 0f)
            {
                return;
            }

            m_elapsedTime += deltaTime;
            m_spawnTimer += deltaTime;
            UpdateNearbyHumanMonitor(deltaTime);

            // 唯一的断流保底：距上次感染 >= 5s 且附近无 Human 时才触发
            TryStrictFlowFallback();

            // 末日狂潮期间使用更短的刷新间隔
            float currentInterval = GetCurrentSpawnInterval();
            if (currentInterval <= 0f)
            {
                return;
            }

            // 当累积时间达到间隔阈值时尝试生成
            while (m_spawnTimer >= currentInterval)
            {
                // 减去间隔而非归零，保留余数以维持稳定的生成节奏
                m_spawnTimer -= currentInterval;

                // 检查人类数量上限：达到上限时停止生成
                int currentHumanCap = GetCurrentHumanMaxCount();
                if (CountAliveHumans() >= currentHumanCap)
                {
                    break;
                }

                if (TrySpawnClusterFromHotspot(false, IsFlowSupportPreferred(), "NormalInterval"))
                {
                    continue;
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
                Vector2 center = PickClusterCenter(GetCurrentSpawnMinDistanceFromPlayer(), m_clusterMinSeparation);

                // 生成簇
                int spawnedCount = SpawnCluster(center, clusterSize);
                if (spawnedCount > 0)
                {
                    RecordSpawn("随机回退", $"位置 {FormatPosition(center)}", center, null, "NormalInterval");
                }
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
            m_elapsedTime = 0f;
            m_lastInfectionElapsedTime = 0f;
            m_lastFlowTopUpElapsedTime = -999f;
            m_recentFlowTopUpTriggered = false;
            m_lastSpawnSourceText = "未刷新";
            m_lastSpawnDetailText = "无";
            m_lastSpawnHotspotName = "无";
            m_lastSpawnPosition = Vector2.zero;
            m_nextPlacementWarningTime = 0f;
            m_firstInfectionTime = -1f;
            m_maxNoInfectionDuration = 0f;
            m_lastSuccessfulSpawnElapsedTime = -1f;
            m_lastGuaranteeSpawnReason = "无";
            m_nearbyHumanCheckTimer = 0f;
            m_noNearbyHumanDuration = 0f;
            m_lastLowHumanTopUpElapsedTime = -999f;
            m_lastNearbyFallbackElapsedTime = -999f;
            m_lastStrictFallbackTime = -999f;
            m_strictFallbackCount = 0;
            ResetHotspotDebugStats();
            RefreshHotspots();
        }

        /// <summary>
        /// 末日狂潮事件处理：切换到狂潮模式，分批刷出首波大簇（避免同帧尖峰），并重置计时器。
        /// </summary>
        private void HandleFinalFrenzyStarted()
        {
            m_isFinalFrenzy = true;
            // 重置计时器，让狂潮立即开始高频生成
            m_spawnTimer = m_config != null ? m_config.FinalFrenzyClusterSpawnInterval : 1f;

            // 分批刷出首波大簇，而不是同一帧全部创建
            if (ValidateDependencies() && m_playerTransform != null && m_config != null)
            {
                if (!TrySpawnClusterFromHotspot(true, true))
                {
                    int burstSize = UnityEngine.Random.Range(
                        m_config.FinalFrenzyLargeClusterMin,
                        m_config.FinalFrenzyLargeClusterMax + 1);
                    Vector2 center = PickClusterCenter(GetCurrentSpawnMinDistanceFromPlayer(), m_clusterMinSeparation);
                    StartCoroutine(StaggeredSpawnCluster(center, burstSize, m_clusterRadius));
                    RecordSpawn("随机回退", $"狂潮首波 {FormatPosition(center)}", center, null, "NormalInterval");
                }
            }
        }

        /// <summary>
        /// 分批生成簇内人类，将一个大簇拆成 3~4 批在 0.25 秒内刷完，避免同帧尖峰。
        /// </summary>
        /// <param name="center">簇中心位置</param>
        /// <param name="totalCount">总人数</param>
        private IEnumerator StaggeredSpawnCluster(Vector2 center, int totalCount, float radius)
        {
            const int batchCount = 4;
            const float totalDuration = 0.25f;
            float batchInterval = totalDuration / batchCount;

            int spawned = 0;
            for (int batch = 0; batch < batchCount; batch++)
            {
                // 计算本批数量：均匀分配，最后一批补余数
                int batchSize = (totalCount - spawned) / (batchCount - batch);
                if (batchSize <= 0)
                {
                    break;
                }

                // 使用现有 SpawnCluster 生成本批
                SpawnCluster(center, batchSize, radius);
                spawned += batchSize;

                if (batch < batchCount - 1)
                {
                    yield return new WaitForSeconds(batchInterval);
                }
            }
        }

        private void OnDestroy()
        {
            Game.Core.GameEvents.OnInfectionSuccess -= HandleInfectionSuccess;
            Game.Core.GameEvents.OnFinalFrenzyStarted -= HandleFinalFrenzyStarted;
        }

        // ==================== 内部方法 ====================

        private bool TrySpawnClusterFromHotspot(bool staggered, bool preferPlayerReachable, string reason = "NormalInterval")
        {
            if (!TryPickHotspot(preferPlayerReachable, out SpawnHotspot hotspot))
            {
                return false;
            }

            int count = hotspot.GetRandomCount();
            int currentHumanCap = GetCurrentHumanMaxCount();
            int remainingCapacity = Mathf.Max(0, currentHumanCap - CountAliveHumans());
            if (remainingCapacity <= 0)
            {
                return true;
            }

            count = Mathf.Min(count, remainingCapacity);
            Vector2 center = hotspot.Position;
            float radius = hotspot.SpawnRadius;
            if (m_mapRuntimeController != null)
            {
                center = m_mapRuntimeController.ClampToMap(center, 0.5f);
            }

            if (staggered)
            {
                StartCoroutine(StaggeredSpawnCluster(center, count, radius));
            }
            else
            {
                SpawnCluster(center, count, radius);
            }

            RecordHotspotSpawn(hotspot);
            RecordSpawn("普通热点", hotspot.HotspotName, center, hotspot.HotspotName, reason);
            return true;
        }

        private bool TryPickHotspot(bool preferPlayerReachable, out SpawnHotspot hotspot)
        {
            RefreshHotspots();

            hotspot = null;
            if (m_playerTransform == null)
            {
                return false;
            }

            bool isFinalFrenzy = m_isFinalFrenzy && m_config != null && m_config.EnableFinalFrenzy;
            bool strictDistance = preferPlayerReachable || m_elapsedTime <= m_openingPriorityDuration;
            if (TryPickHotspotInternal(isFinalFrenzy, strictDistance, out hotspot))
            {
                return true;
            }

            return TryPickHotspotInternal(isFinalFrenzy, false, out hotspot);
        }

        private bool TryPickHotspotInternal(bool isFinalFrenzy, bool strictDistance, out SpawnHotspot hotspot)
        {
            hotspot = null;
            float totalWeight = 0f;

            for (int i = 0; i < m_hotspots.Count; i++)
            {
                SpawnHotspot candidate = m_hotspots[i];
                if (candidate == null || !candidate.IsEnabledAt(m_elapsedTime))
                {
                    continue;
                }

                totalWeight += GetHotspotWeight(candidate, isFinalFrenzy, strictDistance);
            }

            if (totalWeight <= 0f)
            {
                return false;
            }

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cursor = 0f;

            for (int i = 0; i < m_hotspots.Count; i++)
            {
                SpawnHotspot candidate = m_hotspots[i];
                if (candidate == null || !candidate.IsEnabledAt(m_elapsedTime))
                {
                    continue;
                }

                cursor += GetHotspotWeight(candidate, isFinalFrenzy, strictDistance);
                if (roll <= cursor)
                {
                    hotspot = candidate;
                    return true;
                }
            }

            return false;
        }

        private float GetHotspotWeight(SpawnHotspot hotspot, bool isFinalFrenzy, bool strictDistance)
        {
            Vector2 playerPos = GetPlayerPosition();
            Vector2 hotspotPos = hotspot.Position;
            float distance = Vector2.Distance(playerPos, hotspotPos);

            float weight = hotspot.GetEffectiveWeight(m_elapsedTime, isFinalFrenzy, m_finalFrenzyHotspotWeightMultiplier);
            if (weight <= 0f)
            {
                return 0f;
            }

            float maxDistance = hotspot.RecommendedMaxPlayerDistance;
            if (strictDistance && distance > maxDistance)
            {
                return 0f;
            }

            if (distance < hotspot.RecommendedMinPlayerDistance)
            {
                weight *= 0.45f;
            }
            else if (distance >= hotspot.RecommendedIdealMinPlayerDistance &&
                     distance <= hotspot.RecommendedIdealMaxPlayerDistance)
            {
                weight *= 1.5f;
            }
            else if (distance > maxDistance)
            {
                weight *= isFinalFrenzy ? 0.35f : 0.12f;
            }

            if (m_elapsedTime <= m_openingPriorityDuration)
            {
                weight *= hotspot.IsOpeningPriority ? 3f : 0.45f;
            }

            InfectionFlowState flowState = GetCurrentFlowState();
            if (flowState != InfectionFlowState.Normal)
            {
                weight *= hotspot.IsFlowFallback ? 3f : 0.55f;
            }

            if (m_mapRuntimeController != null)
            {
                bool centerBlocked = m_mapRuntimeController.IsPointBlocked(hotspotPos, 0.45f);
                if (centerBlocked)
                {
                    return 0f;
                }

                bool hasDirectPath = m_mapRuntimeController.HasDirectPath(playerPos, hotspotPos, 0.25f);
                if (!hasDirectPath)
                {
                    if (strictDistance)
                    {
                        return 0f;
                    }

                    weight *= 0.18f;
                }
            }

            return Mathf.Max(0f, weight);
        }

        private void TryHandleLowHumanCount()
        {
            // [已禁用] 低人类存量补充逻辑已移除，不再因人类数量少而主动灌怪
        }

        private void TryHandleNoNearbyHumanFallback()
        {
            // [已禁用] 由 TryStrictFlowFallback 统一替代
        }

        private void TryHandleSevereFlowBreak()
        {
            // [已禁用] 由 TryStrictFlowFallback 统一替代
        }

        /// <summary>
        /// 严格断流保底：仅在距上次感染 >= 5 秒且玩家附近无可感染 Human 时触发。
        /// 冷却 8 秒，每次只补 3-5 人小簇。不会把人类数量拉回高位。
        /// </summary>
        private void TryStrictFlowFallback()
        {
            // 条件 1：距上次感染 >= 5 秒
            if (m_noNearbyHumanDuration < m_noNearbyHumanThreshold)
            {
                return;
            }

            // 条件 2：冷却 8 秒
            const float strictCooldown = 8f;
            if (m_elapsedTime - m_lastStrictFallbackTime < strictCooldown)
            {
                return;
            }

            // 条件 3：玩家附近确实没有可感染 Human
            if (CountNearbyAliveHumans(m_nearbyHumanDetectionRadius) > 0)
            {
                return;
            }

            // 补充 3-5 人小簇
            int currentHumanCap = GetCurrentHumanMaxCount();
            int remainingCapacity = Mathf.Max(0, currentHumanCap - CountAliveHumans());
            if (remainingCapacity <= 0)
            {
                return;
            }

            int count = Mathf.Min(UnityEngine.Random.Range(3, 6), remainingCapacity);
            bool handled = TrySpawnFromNearestEnabledHotspot("StrictFallback", count, m_nearbyFallbackHotspotMaxDistance);
            if (!handled)
            {
                handled = TrySpawnNearPlayerEdge(count, "StrictFallback");
            }

            if (handled)
            {
                m_lastStrictFallbackTime = m_elapsedTime;
                m_noNearbyHumanDuration = 0f;
                m_strictFallbackCount++;
                m_lastGuaranteeSpawnReason = "StrictFallback";
            }
        }

        private bool TrySpawnFlowFallbackCluster(int count, string reason)
        {
            if (count <= 0)
            {
                return false;
            }

            if (!TryPickReachablePointNearPlayer(m_flowFallbackMinDistance, m_flowFallbackMaxDistance, out Vector2 center))
            {
                WarnPlacementFallback("[HumanClusterSpawner] 严重断流保底未找到直线可达位置，退回玩家附近随机点");
                center = PickClusterCenterInRange(m_flowFallbackMinDistance, m_flowFallbackMaxDistance, 1f);
            }

            return SpawnClusterWithRecord(center, count, Mathf.Min(m_clusterRadius, 1.7f), "断流保底", $"小簇 {count} @ {FormatPosition(center)}", null, reason) > 0;
        }

        private bool TryRelocateExistingHumansForFlow()
        {
            IReadOnlyList<HumanUnit> humans = m_spawnSystem.ActiveHumans;
            if (humans == null || humans.Count == 0)
            {
                return false;
            }

            if (!TryPickReachablePointNearPlayer(m_flowFallbackMinDistance, m_flowFallbackMaxDistance, out Vector2 center))
            {
                WarnPlacementFallback("[HumanClusterSpawner] 人类已满且未找到可搬运保底点，跳过本次断流保底");
                return false;
            }

            int targetCount = Mathf.Min(UnityEngine.Random.Range(m_smallClusterMin, m_smallClusterMax + 1), humans.Count);
            int movedCount = 0;

            for (int i = 0; i < targetCount; i++)
            {
                HumanUnit human = FindFarthestHumanFromPlayer(humans);
                if (human == null)
                {
                    break;
                }

                Vector2 spawnPos = GetPositionInCluster(center, Mathf.Min(m_clusterRadius, 1.7f));
                if (m_mapRuntimeController != null)
                {
                    spawnPos = m_mapRuntimeController.ClampToMap(spawnPos, 0.45f);
                }
                else if (m_spawnSystem != null)
                {
                    spawnPos = m_spawnSystem.ClampToMap(spawnPos);
                }

                human.transform.position = new Vector3(spawnPos.x, spawnPos.y, human.transform.position.z);
                movedCount++;
            }

            if (movedCount <= 0)
            {
                return false;
            }

            RecordSpawn("断流保底", $"搬运现有人类 {movedCount} @ {FormatPosition(center)}", center, null, "NoNearbyHuman");
            return true;
        }

        private HumanUnit FindFarthestHumanFromPlayer(IReadOnlyList<HumanUnit> humans)
        {
            Vector2 playerPos = GetPlayerPosition();
            HumanUnit best = null;
            float bestSqrDistance = -1f;

            for (int i = 0; i < humans.Count; i++)
            {
                HumanUnit human = humans[i];
                if (human == null || human.IsInfected)
                {
                    continue;
                }

                float sqrDistance = (human.Position - playerPos).sqrMagnitude;
                if (sqrDistance > bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    best = human;
                }
            }

            return best;
        }

        private bool TryPickReachablePointNearPlayer(float minDistance, float maxDistance, out Vector2 point)
        {
            point = Vector2.zero;
            Vector2 playerPos = GetPlayerPosition();
            float minSqrDistance = minDistance * minDistance;
            float maxSqrDistance = maxDistance * maxDistance;

            for (int attempt = 0; attempt < m_maxPlacementAttempts; attempt++)
            {
                float angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
                float radius = Mathf.Sqrt(UnityEngine.Random.Range(minSqrDistance, maxSqrDistance));
                Vector2 candidate = playerPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                if (m_mapRuntimeController != null)
                {
                    candidate = m_mapRuntimeController.ClampToMap(candidate, 0.45f);
                    if (m_mapRuntimeController.IsPointBlocked(candidate, 0.45f))
                    {
                        continue;
                    }
                    if (!m_mapRuntimeController.HasDirectPath(playerPos, candidate, 0.25f))
                    {
                        continue;
                    }
                }
                else if (m_spawnSystem != null)
                {
                    candidate = m_spawnSystem.ClampToMap(candidate);
                }

                bool tooCloseToCluster = false;
                float minClusterSqrDistance = m_clusterMinSeparation * m_clusterMinSeparation * 0.5f;
                for (int i = 0; i < m_clusterCenters.Count; i++)
                {
                    if ((candidate - m_clusterCenters[i]).sqrMagnitude < minClusterSqrDistance)
                    {
                        tooCloseToCluster = true;
                        break;
                    }
                }

                if (tooCloseToCluster)
                {
                    continue;
                }

                point = candidate;
                return true;
            }

            return false;
        }

        private bool TrySpawnFromNearestEnabledHotspot(string reason, int count, float maxDistance)
        {
            RefreshHotspots();

            SpawnHotspot best = null;
            float bestSqrDistance = float.MaxValue;
            Vector2 playerPos = GetPlayerPosition();
            float maxSqrDistance = maxDistance * maxDistance;

            for (int i = 0; i < m_hotspots.Count; i++)
            {
                SpawnHotspot hotspot = m_hotspots[i];
                if (hotspot == null || !hotspot.IsEnabledAt(m_elapsedTime))
                {
                    continue;
                }

                Vector2 hotspotPos = hotspot.Position;
                float sqrDistance = (hotspotPos - playerPos).sqrMagnitude;
                if (sqrDistance > maxSqrDistance || sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                if (m_mapRuntimeController != null)
                {
                    if (m_mapRuntimeController.IsPointBlocked(hotspotPos, 0.45f))
                    {
                        continue;
                    }
                    if (!m_mapRuntimeController.HasDirectPath(playerPos, hotspotPos, 0.25f) &&
                        reason == "NoNearbyHuman")
                    {
                        continue;
                    }
                }

                best = hotspot;
                bestSqrDistance = sqrDistance;
            }

            if (best == null)
            {
                return false;
            }

            int spawned = SpawnClusterWithRecord(
                best.Position,
                count,
                best.SpawnRadius,
                "保底热点",
                $"{best.HotspotName} x{count}",
                best,
                reason);
            return spawned > 0;
        }

        private bool TrySpawnNearPlayerEdge(int count, string reason)
        {
            Vector2 center;
            if (!TryPickForwardReachablePoint(out center) &&
                !TryPickReachablePointNearPlayer(m_flowFallbackMinDistance, m_flowFallbackMaxDistance, out center))
            {
                center = PickClusterCenterInRange(m_flowFallbackMinDistance, m_flowFallbackMaxDistance, 1f);
            }

            int spawned = SpawnClusterWithRecord(
                center,
                count,
                Mathf.Min(m_clusterRadius, 1.7f),
                "保底近点",
                $"玩家附近 {FormatPosition(center)}",
                null,
                reason);
            return spawned > 0;
        }

        private bool TryPickForwardReachablePoint(out Vector2 point)
        {
            point = Vector2.zero;
            if (m_playerTransform == null)
            {
                return false;
            }

            Vector2 playerPos = GetPlayerPosition();
            Vector2 forward = m_playerTransform.up;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector2.up;
            }
            forward.Normalize();

            for (int attempt = 0; attempt < m_maxPlacementAttempts; attempt++)
            {
                float angleOffset = UnityEngine.Random.Range(-45f, 45f);
                float radians = angleOffset * Mathf.Deg2Rad;
                float cos = Mathf.Cos(radians);
                float sin = Mathf.Sin(radians);
                Vector2 direction = new Vector2(
                    forward.x * cos - forward.y * sin,
                    forward.x * sin + forward.y * cos);
                float distance = UnityEngine.Random.Range(m_flowFallbackMinDistance, m_flowFallbackMaxDistance);
                Vector2 candidate = playerPos + direction * distance;

                if (m_mapRuntimeController != null)
                {
                    candidate = m_mapRuntimeController.ClampToMap(candidate, 0.45f);
                    if (m_mapRuntimeController.IsPointBlocked(candidate, 0.45f))
                    {
                        continue;
                    }
                    if (!m_mapRuntimeController.HasDirectPath(playerPos, candidate, 0.25f))
                    {
                        continue;
                    }
                }
                else if (m_spawnSystem != null)
                {
                    candidate = m_spawnSystem.ClampToMap(candidate);
                }

                point = candidate;
                return true;
            }

            return false;
        }

        private int SpawnClusterWithRecord(
            Vector2 center,
            int count,
            float radius,
            string source,
            string detail,
            SpawnHotspot hotspot,
            string reason)
        {
            int remainingCapacity = Mathf.Max(0, GetCurrentHumanMaxCount() - CountAliveHumans());
            int finalCount = Mathf.Min(count, remainingCapacity);
            if (finalCount <= 0)
            {
                return 0;
            }

            int spawned = SpawnCluster(center, finalCount, radius);
            if (spawned <= 0)
            {
                return 0;
            }

            if (hotspot != null)
            {
                RecordHotspotSpawn(hotspot);
            }
            RecordSpawn(source, detail, center, hotspot != null ? hotspot.HotspotName : null, reason);
            return spawned;
        }

        private void RefreshHotspots()
        {
            if (m_hotspots == null)
            {
                m_hotspots = new List<SpawnHotspot>();
            }

            for (int i = m_hotspots.Count - 1; i >= 0; i--)
            {
                if (m_hotspots[i] == null)
                {
                    m_hotspots.RemoveAt(i);
                }
            }

            if (m_hotspotRoot != null)
            {
                SpawnHotspot[] childHotspots = m_hotspotRoot.GetComponentsInChildren<SpawnHotspot>(true);
                for (int i = 0; i < childHotspots.Length; i++)
                {
                    if (!m_hotspots.Contains(childHotspots[i]))
                    {
                        m_hotspots.Add(childHotspots[i]);
                    }
                }
            }

            if (m_hotspots.Count <= 0)
            {
                SpawnHotspot[] sceneHotspots = FindObjectsOfType<SpawnHotspot>(true);
                for (int i = 0; i < sceneHotspots.Length; i++)
                {
                    if (!m_hotspots.Contains(sceneHotspots[i]))
                    {
                        m_hotspots.Add(sceneHotspots[i]);
                    }
                }
            }

            EnsureHotspotDebugStats();
        }

        /// <summary>
        /// 在指定中心位置生成一个包含 count 个人类的簇。
        /// 使用均匀圆盘分布（uniform disk distribution）在簇半径内放置人类，
        /// 使人群看起来自然聚集而非规则排列。
        /// </summary>
        /// <param name="center">簇的中心位置（世界坐标 XY）</param>
        /// <param name="count">簇内人类数量</param>
        internal int SpawnCluster(Vector2 center, int count)
        {
            return SpawnCluster(center, count, m_clusterRadius);
        }

        /// <summary>
        /// 在指定中心位置和半径内生成一个人群簇。
        /// </summary>
        internal int SpawnCluster(Vector2 center, int count, float radius)
        {
            if (!ValidateDependencies())
            {
                return 0;
            }

            int spawnedCount = 0;
            radius = Mathf.Max(0.1f, radius);

            for (int i = 0; i < count; i++)
            {
                // 检查是否已达到人类上限
                if (m_config != null && m_spawnSystem != null &&
                    CountAliveHumans() >= GetCurrentHumanMaxCount())
                {
                    break;
                }

                HumanUnit human = m_poolManager.GetHuman();
                if (human == null)
                {
                    Debug.LogWarning("[HumanClusterSpawner] ObjectPoolManager.GetHuman 返回 null，停止当前簇的剩余生成");
                    break;
                }

                Vector2 spawnPos = GetSafePositionInCluster(center, radius);

                human.transform.position = new Vector3(spawnPos.x, spawnPos.y, 0f);

                // 为取出的 Human 注入 AI 依赖
                HumanAI ai = human.GetComponent<HumanAI>();
                if (ai != null)
                {
                    ai.Initialize(m_config, m_playerTransform, m_getZombiePositions);
                }

                // 注册到 SpawnSystem 的活跃列表
                RegisterHumanToSpawnSystem(human);
                spawnedCount++;
            }

            if (spawnedCount <= 0)
            {
                return 0;
            }

            // 记录簇中心位置，用于后续簇间距约束检查。
            // 只保留最近 MaxClusterCenterHistory 个，避免后期选点越来越重。
            m_clusterCenters.Add(center);
            if (m_clusterCenters.Count > MaxClusterCenterHistory)
            {
                m_clusterCenters.RemoveAt(0);
            }

            return spawnedCount;
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

        private Vector2 GetSafePositionInCluster(Vector2 center, float radius)
        {
            Vector2 spawnPos = center;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                spawnPos = GetPositionInCluster(center, radius);
                if (m_mapRuntimeController != null)
                {
                    spawnPos = m_mapRuntimeController.ClampToMap(spawnPos, 0.35f);
                    if (m_mapRuntimeController.IsPointBlocked(spawnPos, 0.35f))
                    {
                        continue;
                    }
                }
                else if (m_spawnSystem != null)
                {
                    spawnPos = m_spawnSystem.ClampToMap(spawnPos);
                }

                return spawnPos;
            }

            WarnPlacementFallback("[HumanClusterSpawner] 簇内位置多次落入阻挡，使用中心点附近退化位置");
            if (m_mapRuntimeController != null)
            {
                return m_mapRuntimeController.ClampToMap(center, 0.35f);
            }
            if (m_spawnSystem != null)
            {
                return m_spawnSystem.ClampToMap(center);
            }

            return spawnPos;
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
            int configuredCap;
            if (m_config == null)
            {
                configuredCap = 50;
            }
            else if (m_isFinalFrenzy && m_config.EnableFinalFrenzyHumanCapOverride)
            {
                configuredCap = m_config.FinalFrenzyHumanMaxCount;
            }
            else
            {
                configuredCap = m_config.HumanMaxCount;
            }

            return m_maxAliveHumansOverride > 0 ? m_maxAliveHumansOverride : configuredCap;
        }

        private float GetCurrentSpawnInterval()
        {
            float interval;
            if (m_isFinalFrenzy && m_config != null && m_config.EnableFinalFrenzy)
            {
                interval = m_config.FinalFrenzyClusterSpawnInterval;
            }
            else
            {
                interval = m_clusterSpawnInterval;
            }

            // 不再因人类数量少而加速刷怪
            return Mathf.Max(0f, interval);
        }

        private float GetSecondsUntilNextSpawn()
        {
            float currentInterval = GetCurrentSpawnInterval();
            if (currentInterval <= 0f)
            {
                return 0f;
            }

            return Mathf.Max(0f, currentInterval - m_spawnTimer);
        }

        private int CountEnabledHotspots()
        {
            int count = 0;
            if (m_hotspots == null)
            {
                return count;
            }

            for (int i = 0; i < m_hotspots.Count; i++)
            {
                SpawnHotspot hotspot = m_hotspots[i];
                if (hotspot != null && hotspot.IsEnabledAt(m_elapsedTime))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountAliveHumans()
        {
            if (m_spawnSystem == null || m_spawnSystem.ActiveHumans == null)
            {
                return 0;
            }

            IReadOnlyList<HumanUnit> humans = m_spawnSystem.ActiveHumans;
            int count = 0;
            for (int i = 0; i < humans.Count; i++)
            {
                HumanUnit human = humans[i];
                if (human != null && !human.IsInfected)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountNearbyAliveHumans(float radius)
        {
            if (m_spawnSystem == null || m_spawnSystem.ActiveHumans == null || m_playerTransform == null)
            {
                return 0;
            }

            Vector2 playerPos = GetPlayerPosition();
            float sqrRadius = radius * radius;
            IReadOnlyList<HumanUnit> humans = m_spawnSystem.ActiveHumans;
            int count = 0;

            for (int i = 0; i < humans.Count; i++)
            {
                HumanUnit human = humans[i];
                if (human == null || human.IsInfected)
                {
                    continue;
                }

                if ((human.Position - playerPos).sqrMagnitude <= sqrRadius)
                {
                    count++;
                }
            }

            return count;
        }

        private void UpdateNearbyHumanMonitor(float deltaTime)
        {
            m_nearbyHumanCheckTimer += deltaTime;
            while (m_nearbyHumanCheckTimer >= m_nearbyHumanCheckInterval)
            {
                m_nearbyHumanCheckTimer -= m_nearbyHumanCheckInterval;
                if (CountNearbyAliveHumans(m_nearbyHumanDetectionRadius) > 0)
                {
                    m_noNearbyHumanDuration = 0f;
                }
                else
                {
                    m_noNearbyHumanDuration += m_nearbyHumanCheckInterval;
                }
            }
        }

        private float GetCurrentSpawnMinDistanceFromPlayer()
        {
            if (!m_isFinalFrenzy)
            {
                return m_periodicSpawnMinDistFromPlayer;
            }

            return Mathf.Max(6f, m_periodicSpawnMinDistFromPlayer * 0.85f);
        }

        private bool IsFlowSupportPreferred()
        {
            return m_elapsedTime <= m_openingPriorityDuration || GetCurrentFlowState() != InfectionFlowState.Normal;
        }

        private InfectionFlowState GetCurrentFlowState()
        {
            float seconds = SecondsSinceLastInfection;
            if (seconds >= m_severeBreakThreshold)
            {
                return InfectionFlowState.SevereBreak;
            }
            if (seconds >= m_lightBreakThreshold)
            {
                return InfectionFlowState.LightBreak;
            }

            return InfectionFlowState.Normal;
        }

        private Vector2 GetPlayerPosition()
        {
            if (m_playerTransform == null)
            {
                return Vector2.zero;
            }

            Vector3 world = m_playerTransform.position;
            return new Vector2(world.x, world.y);
        }

        private void HandleInfectionSuccess(Vector2 position)
        {
            float noInfectionDuration = Mathf.Max(0f, m_elapsedTime - m_lastInfectionElapsedTime);
            if (noInfectionDuration > m_maxNoInfectionDuration)
            {
                m_maxNoInfectionDuration = noInfectionDuration;
            }

            if (m_firstInfectionTime < 0f)
            {
                m_firstInfectionTime = m_elapsedTime;
            }

            m_lastInfectionElapsedTime = m_elapsedTime;
            RecordNearbyHotspotInfection(position);
        }

        private void RecordSpawn(string source, string detail, Vector2 position, string hotspotName = null, string reason = "NormalInterval")
        {
            m_lastSpawnSourceText = source;
            m_lastSpawnDetailText = detail;
            m_lastSpawnHotspotName = string.IsNullOrWhiteSpace(hotspotName) ? "无" : hotspotName;
            m_lastSpawnPosition = position;
            m_lastSuccessfulSpawnElapsedTime = m_elapsedTime;
            m_lastGuaranteeSpawnReason = reason;
        }

        private void EnsureHotspotDebugStats()
        {
            if (m_hotspotDebugStats == null)
            {
                m_hotspotDebugStats = new List<HotspotDebugStats>();
            }

            m_hotspotStatsByHotspot.Clear();

            for (int i = 0; i < m_hotspots.Count; i++)
            {
                SpawnHotspot hotspot = m_hotspots[i];
                if (hotspot == null)
                {
                    continue;
                }

                HotspotDebugStats stats = FindHotspotStats(hotspot.HotspotName);
                if (stats == null)
                {
                    stats = new HotspotDebugStats(hotspot.HotspotName);
                    m_hotspotDebugStats.Add(stats);
                }

                stats.SetHotspotName(hotspot.HotspotName);
                m_hotspotStatsByHotspot[hotspot] = stats;
            }
        }

        private HotspotDebugStats FindHotspotStats(string hotspotName)
        {
            for (int i = 0; i < m_hotspotDebugStats.Count; i++)
            {
                HotspotDebugStats stats = m_hotspotDebugStats[i];
                if (stats != null && stats.HotspotName == hotspotName)
                {
                    return stats;
                }
            }

            return null;
        }

        private void ResetHotspotDebugStats()
        {
            if (m_hotspotDebugStats == null)
            {
                m_hotspotDebugStats = new List<HotspotDebugStats>();
            }

            for (int i = 0; i < m_hotspotDebugStats.Count; i++)
            {
                if (m_hotspotDebugStats[i] != null)
                {
                    m_hotspotDebugStats[i].Reset();
                }
            }
        }

        private void RecordHotspotSpawn(SpawnHotspot hotspot)
        {
            if (hotspot == null)
            {
                return;
            }

            if (!m_hotspotStatsByHotspot.TryGetValue(hotspot, out HotspotDebugStats stats))
            {
                EnsureHotspotDebugStats();
                m_hotspotStatsByHotspot.TryGetValue(hotspot, out stats);
            }

            if (stats != null)
            {
                stats.RecordSpawn();
            }
        }

        private void RecordNearbyHotspotInfection(Vector2 infectionPosition)
        {
            if (m_hotspots == null)
            {
                return;
            }

            SpawnHotspot nearestHotspot = null;
            float nearestSqrDistance = float.MaxValue;

            for (int i = 0; i < m_hotspots.Count; i++)
            {
                SpawnHotspot hotspot = m_hotspots[i];
                if (hotspot == null)
                {
                    continue;
                }

                float allowedDistance = hotspot.SpawnRadius + 1.5f;
                float sqrDistance = (hotspot.Position - infectionPosition).sqrMagnitude;
                if (sqrDistance <= allowedDistance * allowedDistance && sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearestHotspot = hotspot;
                }
            }

            if (nearestHotspot == null)
            {
                return;
            }

            if (!m_hotspotStatsByHotspot.TryGetValue(nearestHotspot, out HotspotDebugStats stats))
            {
                EnsureHotspotDebugStats();
                m_hotspotStatsByHotspot.TryGetValue(nearestHotspot, out stats);
            }

            if (stats != null)
            {
                stats.RecordNearbyInfection();
            }
        }

        private static string FormatPosition(Vector2 position)
        {
            return $"({position.x:F1}, {position.y:F1})";
        }

        private void WarnPlacementFallback(string message)
        {
            if (m_elapsedTime < m_nextPlacementWarningTime)
            {
                return;
            }

            Debug.LogWarning(message);
            m_nextPlacementWarningTime = m_elapsedTime + 5f;
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
