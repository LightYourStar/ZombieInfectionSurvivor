using UnityEngine;
using Game.Config;
using Game.Gameplay.Enemy;
using Game.Gameplay.Zombie;

namespace Game.Utility
{
    /// <summary>
    /// 对象池统一管理器。
    /// 同时管理 Human 与 ZombieCompanion 两个对象池，向上层系统（SpawnSystem、InfectionSystem 等）提供便捷的取出/归还接口。
    /// 在游戏初始化阶段根据 <see cref="GameConfig"/> 预热池容量，运行期避免频繁 Instantiate/Destroy 导致的 GC 与卡顿。
    /// </summary>
    public class ObjectPoolManager : MonoBehaviour
    {
        // ==================== Inspector 注入 ====================

        [Header("预制体引用")]
        [Tooltip("Human 预制体；根节点需挂载 HumanUnit 组件")]
        [SerializeField] private HumanUnit m_humanPrefab;

        [Tooltip("ZombieCompanion 预制体；根节点需挂载 ZombieCompanionUnit 组件")]
        [SerializeField] private ZombieCompanionUnit m_zombiePrefab;

        [Header("配置引用")]
        [Tooltip("游戏配置，用于读取各池的预热数量")]
        [SerializeField] private GameConfig m_gameConfig;

        [Header("层级组织（可选）")]
        [Tooltip("Human 池实例统一挂载的父节点，便于 Hierarchy 观察；为空则挂到场景根")]
        [SerializeField] private Transform m_humanPoolRoot;

        [Tooltip("ZombieCompanion 池实例统一挂载的父节点，便于 Hierarchy 观察；为空则挂到场景根")]
        [SerializeField] private Transform m_zombiePoolRoot;

        // ==================== 运行时状态 ====================

        /// <summary>Human 对象池</summary>
        private ObjectPool<HumanUnit> m_humanPool;

        /// <summary>ZombieCompanion 对象池</summary>
        private ObjectPool<ZombieCompanionUnit> m_zombiePool;

        // ==================== 公开属性 ====================

        /// <summary>当前场上活跃的 Human 数量（未归还）</summary>
        public int ActiveHumanCount => m_humanPool != null ? m_humanPool.ActiveCount : 0;

        /// <summary>当前场上活跃的 ZombieCompanion 数量（未归还）</summary>
        public int ActiveZombieCount => m_zombiePool != null ? m_zombiePool.ActiveCount : 0;

        /// <summary>Human 池空闲实例数量</summary>
        public int FreeHumanCount => m_humanPool != null ? m_humanPool.FreeCount : 0;

        /// <summary>ZombieCompanion 池空闲实例数量</summary>
        public int FreeZombieCount => m_zombiePool != null ? m_zombiePool.FreeCount : 0;

        // ==================== 初始化 ====================

        /// <summary>
        /// 初始化两个对象池并根据 <see cref="GameConfig"/> 预热。
        /// 应由 GameSystemRunner 在进入 Playing 状态前显式调用，确保首次 Get 时无 Instantiate 开销。
        /// </summary>
        public void Initialize()
        {
            // 参数校验：任一引用缺失都无法正常工作，给出明确错误便于 Inspector 定位
            if (m_gameConfig == null)
            {
                Debug.LogError("[ObjectPoolManager] GameConfig 未赋值，无法初始化对象池");
                return;
            }
            if (m_humanPrefab == null)
            {
                Debug.LogError("[ObjectPoolManager] Human 预制体未赋值，无法初始化对象池");
                return;
            }
            if (m_zombiePrefab == null)
            {
                Debug.LogError("[ObjectPoolManager] ZombieCompanion 预制体未赋值，无法初始化对象池");
                return;
            }

            // 创建并预热两个池
            m_humanPool = new ObjectPool<HumanUnit>(m_humanPrefab, m_humanPoolRoot);
            m_zombiePool = new ObjectPool<ZombieCompanionUnit>(m_zombiePrefab, m_zombiePoolRoot);

            m_humanPool.Prewarm(m_gameConfig.HumanPoolInitialSize);
            m_zombiePool.Prewarm(m_gameConfig.ZombiePoolInitialSize);
        }

        // ==================== Human 便捷方法 ====================

        /// <summary>
        /// 从 Human 池取出一个实例并激活。
        /// 池未初始化时返回 null 并输出错误日志。
        /// </summary>
        /// <returns>已激活的 Human 实例；池未初始化时返回 null</returns>
        public HumanUnit GetHuman()
        {
            if (m_humanPool == null)
            {
                Debug.LogError("[ObjectPoolManager] Human 池尚未初始化，请先调用 Initialize()");
                return null;
            }
            return m_humanPool.Get();
        }

        /// <summary>
        /// 将 Human 实例归还池中并禁用。
        /// 池未初始化或实例为 null 时输出日志并忽略调用。
        /// </summary>
        /// <param name="human">需要归还的 Human 实例</param>
        public void ReturnHuman(HumanUnit human)
        {
            if (m_humanPool == null)
            {
                Debug.LogError("[ObjectPoolManager] Human 池尚未初始化，请先调用 Initialize()");
                return;
            }
            m_humanPool.Return(human);
        }

        // ==================== ZombieCompanion 便捷方法 ====================

        /// <summary>
        /// 从 ZombieCompanion 池取出一个实例并激活。
        /// 池未初始化时返回 null 并输出错误日志。
        /// </summary>
        /// <returns>已激活的 ZombieCompanion 实例；池未初始化时返回 null</returns>
        public ZombieCompanionUnit GetZombie()
        {
            if (m_zombiePool == null)
            {
                Debug.LogError("[ObjectPoolManager] ZombieCompanion 池尚未初始化，请先调用 Initialize()");
                return null;
            }
            return m_zombiePool.Get();
        }

        /// <summary>
        /// 将 ZombieCompanion 实例归还池中并禁用。
        /// 池未初始化或实例为 null 时输出日志并忽略调用。
        /// </summary>
        /// <param name="zombie">需要归还的 ZombieCompanion 实例</param>
        public void ReturnZombie(ZombieCompanionUnit zombie)
        {
            if (m_zombiePool == null)
            {
                Debug.LogError("[ObjectPoolManager] ZombieCompanion 池尚未初始化，请先调用 Initialize()");
                return;
            }
            m_zombiePool.Return(zombie);
        }
    }
}
