using System.Collections.Generic;
using UnityEngine;
using Game.Config;
using Game.Gameplay.Infection;
using Game.Gameplay.Player;
using Game.Gameplay.Skill;
using Game.Gameplay.Wave;
using Game.UI;
using Game.Utility;

namespace Game.Core
{
    /// <summary>
    /// 游戏系统统一入口。
    /// 作为整个游戏的唯一 MonoBehaviour 入口，挂载在场景中的 GameSystemRunner GameObject 上。
    /// 负责：
    /// 1. 通过 Inspector 注入方式持有所有子系统的引用
    /// 2. 在 Awake 阶段订阅状态变化事件，Start 阶段初始化所有子系统
    /// 3. 在 Playing 状态下按数据流顺序调度各子系统的每帧更新
    /// 4. 连接事件：感染成功 → 经验/金币奖励 → 升级检查
    /// 5. 连接事件：倒计时结束 → 结算 → MetaUpgrade 保存
    /// 6. 管理 Start / Playing / Settlement 三状态的完整游戏流程闭环
    /// </summary>
    public class GameSystemRunner : MonoBehaviour
    {
        // ==================== 配置引用 ====================

        [Header("配置")]
        [Tooltip("游戏全局配置，所有子系统的数值来源")]
        [SerializeField] private GameConfig m_gameConfig;

        [Tooltip("局外升级配置")]
        [SerializeField] private MetaUpgradeConfig m_metaUpgradeConfig;

        /// <summary>游戏全局配置。</summary>
        public GameConfig GameConfig => m_gameConfig;

        // ==================== 子系统引用 ====================

        [Header("核心系统")]
        [Tooltip("游戏状态机")]
        [SerializeField] private GameStateManager m_stateManager;

        [Tooltip("单局倒计时系统")]
        [SerializeField] private TimerSystem m_timerSystem;

        [Tooltip("对象池管理器")]
        [SerializeField] private ObjectPoolManager m_poolManager;

        [Header("玩家系统")]
        [Tooltip("玩家控制器")]
        [SerializeField] private PlayerController m_playerController;

        [Tooltip("虚拟摇杆（用于读取旋转方向）")]
        [SerializeField] private VirtualJoystick m_joystick;

        [Header("游戏逻辑系统")]
        [Tooltip("人类刷新系统")]
        [SerializeField] private SpawnSystem m_spawnSystem;

        [Tooltip("感染判定系统")]
        [SerializeField] private InfectionSystem m_infectionSystem;

        [Tooltip("经验金币管理系统")]
        [SerializeField] private ExperienceSystem m_experienceSystem;

        [Tooltip("局内升级系统")]
        [SerializeField] private UpgradeSystem m_upgradeSystem;

        [Tooltip("局外永久升级系统")]
        [SerializeField] private MetaUpgradeSystem m_metaUpgradeSystem;

        [Header("UI 系统")]
        [Tooltip("UI 面板管理器")]
        [SerializeField] private UIManager m_uiManager;

        [Tooltip("游戏内 HUD 面板")]
        [SerializeField] private HUDPanel m_hudPanel;

        [Tooltip("升级选择面板")]
        [SerializeField] private UpgradePanel m_upgradePanel;

        [Tooltip("结算面板")]
        [SerializeField] private SettlementPanel m_settlementPanel;

        [Header("单局管理")]
        [SerializeField] private GameSessionController m_sessionController;
        [SerializeField] private HumanClusterSpawner m_clusterSpawner;
        [SerializeField] private ResultPanel m_resultPanel;

        // ==================== 内部运行时状态 ====================

        /// <summary>玩家运行时属性实例，每局初始化时创建</summary>
        private PlayerStats m_playerStats;

        /// <summary>本局感染成功次数，用于结算面板展示</summary>
        private int m_infectionCount;

        /// <summary>
        /// 当前游戏状态缓存，由 GameStateManager.OnStateChanged 同步更新。
        /// 用于 Update 中快速判断当前状态，避免每帧访问 GameStateManager。
        /// </summary>
        private GameState m_currentState = GameState.Start;

        /// <summary>升级面板弹出时暂停游戏逻辑</summary>
        private bool m_isPaused;

        // ==================== Unity 生命周期 ====================

        private void Awake()
        {
            // 关键配置缺失时提前报错
            if (m_gameConfig == null)
            {
                Debug.LogError("[GameSystemRunner] GameConfig 未在 Inspector 中赋值，系统无法正确初始化");
            }
            if (m_stateManager == null)
            {
                Debug.LogError("[GameSystemRunner] GameStateManager 未在 Inspector 中赋值");
            }

            // 订阅状态机实例事件（优先于全局事件，保证 Runner 最先响应状态变化）
            if (m_stateManager != null)
            {
                m_stateManager.OnStateChanged += HandleStateChanged;
            }
        }

        private void Start()
        {
            InitializeSystems();
        }

        private void Update()
        {
            // 仅在 Playing 状态下且未暂停时调度子系统更新
            if (m_currentState == GameState.Playing && !m_isPaused)
            {
                UpdatePlaying(Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            // 取消订阅状态机事件
            if (m_stateManager != null)
            {
                m_stateManager.OnStateChanged -= HandleStateChanged;
            }

            // 取消订阅全局事件
            GameEvents.OnInfectionSuccess -= HandleInfection;
            GameEvents.OnTimerEnd -= HandleTimerEnd;

            // 取消订阅 ExperienceSystem 实例事件
            if (m_experienceSystem != null)
            {
                m_experienceSystem.OnLevelUp -= HandleLevelUp;
            }

            // 取消订阅 GameSessionController 实例事件
            if (m_sessionController != null)
            {
                m_sessionController.OnSessionEnd -= HandleSessionEnd;
            }

            // 清空所有全局事件订阅，防止场景卸载后残留引用
            GameEvents.ClearAllSubscriptions();
        }

        // ==================== 初始化 ====================

        /// <summary>
        /// 初始化所有子系统，按依赖顺序执行。
        /// 被 Start 调用，完成后触发初始状态（Start）的 UI 显示。
        /// </summary>
        public void InitializeSystems()
        {
            // 1. 加载局外升级数据
            MetaUpgradeData metaData = null;
            if (m_metaUpgradeSystem != null)
            {
                metaData = m_metaUpgradeSystem.Load();
            }

            // 2. 创建并初始化玩家运行时属性
            m_playerStats = new PlayerStats();
            m_playerStats.Initialize(m_gameConfig, metaData);

            // 3. 初始化玩家控制器
            if (m_playerController != null)
            {
                m_playerController.Initialize(m_playerStats);
            }

            // 4. 初始化对象池
            if (m_poolManager != null)
            {
                m_poolManager.Initialize();
            }

            // 5. 初始化刷新系统（注入僵尸位置获取委托）
            if (m_spawnSystem != null)
            {
                m_spawnSystem.Initialize(() => m_infectionSystem != null
                    ? m_infectionSystem.GetActiveZombiePositions()
                    : null);
            }

            // 6. 初始化感染系统
            if (m_infectionSystem != null)
            {
                m_infectionSystem.Initialize(m_playerStats);
            }

            // 7. 初始化经验系统
            if (m_experienceSystem != null)
            {
                m_experienceSystem.Initialize(m_playerStats);
            }

            // 8. 初始化升级系统
            if (m_upgradeSystem != null)
            {
                m_upgradeSystem.Initialize(m_playerStats);
            }

            // 初始化单局控制器
            if (m_sessionController != null)
            {
                m_sessionController.Initialize();
            }
            // 初始化簇刷新系统
            if (m_clusterSpawner != null)
            {
                m_clusterSpawner.Initialize(() => m_infectionSystem != null
                    ? m_infectionSystem.GetActiveZombiePositions()
                    : null);
            }

            // 9. 初始化 HUD（注入 TimerSystem 和 SessionController 以订阅时间变化和单局事件）
            if (m_hudPanel != null)
            {
                m_hudPanel.Initialize(m_timerSystem, m_sessionController);
            }

            // 10. 初始化 UI 管理器（会隐藏所有面板并显示 StartPanel）
            if (m_uiManager != null)
            {
                m_uiManager.Initialize();
            }

            // 11. 订阅游戏事件
            SubscribeEvents();

            // 12. 确保状态机处于 Start 状态
            if (m_stateManager != null)
            {
                m_stateManager.ChangeState(GameState.Start);
            }
        }

        // ==================== 事件订阅 ====================

        /// <summary>
        /// 订阅所有需要的游戏事件。
        /// </summary>
        private void SubscribeEvents()
        {
            // 感染成功 → 经验/金币奖励
            GameEvents.OnInfectionSuccess += HandleInfection;

            // 倒计时结束 → 进入结算
            GameEvents.OnTimerEnd += HandleTimerEnd;

            // 升级事件 → 暂停并弹出升级面板
            if (m_experienceSystem != null)
            {
                m_experienceSystem.OnLevelUp += HandleLevelUp;
            }

            // 单局结束 → 显示结算面板
            if (m_sessionController != null && m_resultPanel != null)
            {
                m_sessionController.OnSessionEnd += HandleSessionEnd;
            }
        }

        // ==================== Playing 状态每帧调度 ====================

        /// <summary>
        /// Playing 状态下的每帧更新。
        /// 按照设计文档中定义的数据流顺序调度各子系统：
        /// Input → Player → SpawnSystem → HumanAI → ZombieAI → InfectionSystem → Timer
        /// </summary>
        /// <param name="deltaTime">本帧时间增量</param>
        private void UpdatePlaying(float deltaTime)
        {
            // 1. 玩家移动（Input 由 VirtualJoystick 内部处理，PlayerController 读取方向）
            if (m_playerController != null)
            {
                m_playerController.UpdateMovement(deltaTime);

                // 玩家朝向旋转：读取当前移动方向，平滑旋转到该方向
                Vector2 rotDir = GetCurrentInputDirection();
                float rotSpeed = m_gameConfig != null ? m_gameConfig.PlayerRotationSpeed : 720f;
                m_playerController.UpdateRotation(rotDir, rotSpeed, deltaTime);
            }

            // 2. 人类刷新（按簇间隔补充新 Human）
            if (m_clusterSpawner != null)
            {
                m_clusterSpawner.UpdateSpawn(deltaTime);
            }

            // 3. 人类 AI 行为（逃跑/漫游）
            if (m_spawnSystem != null)
            {
                m_spawnSystem.UpdateHumanAIs(deltaTime);
            }

            // 4. 僵尸同伴 AI 行为（跟随/追击）
            if (m_infectionSystem != null)
            {
                m_infectionSystem.UpdateZombieAIs(deltaTime);
            }

            // 5. 感染判定（检测距离并执行转化）
            if (m_infectionSystem != null)
            {
                m_infectionSystem.UpdateInfectionCheck();
            }

            // 6. 倒计时推进
            if (m_timerSystem != null)
            {
                m_timerSystem.UpdateTimer(deltaTime);
            }
        }

        // ==================== 状态变化处理 ====================

        /// <summary>
        /// 响应 GameStateManager 的状态变化事件。
        /// 根据新状态执行对应的流程逻辑。
        /// </summary>
        /// <param name="newState">切换后的新状态</param>
        private void HandleStateChanged(GameState newState)
        {
            m_currentState = newState;

            switch (newState)
            {
                case GameState.Start:
                    OnEnterStart();
                    break;
                case GameState.Playing:
                    OnEnterPlaying();
                    break;
                case GameState.Settlement:
                    OnEnterSettlement();
                    break;
            }
        }

        /// <summary>
        /// 进入 Start 状态：显示开始界面，等待玩家点击开始。
        /// </summary>
        private void OnEnterStart()
        {
            if (m_uiManager != null)
            {
                m_uiManager.ShowStartPanel();
            }
        }

        /// <summary>
        /// 进入 Playing 状态：重置系统、生成初始批次、启动倒计时、显示 HUD。
        /// </summary>
        private void OnEnterPlaying()
        {
            StartNewMatch();
        }

        /// <summary>
        /// 进入 Settlement 状态：保存金币、显示结算面板。
        /// </summary>
        private void OnEnterSettlement()
        {
            HandleSettlement();
        }

        // ==================== 游戏流程方法 ====================

        /// <summary>
        /// 开始新一局：重置所有子系统状态、生成初始 Human、启动倒计时、显示 HUD。
        /// </summary>
        private void StartNewMatch()
        {
            // 重置本局统计
            m_infectionCount = 0;

            // 重置玩家属性（清除局内升级加成，保留基础值+局外加成）
            if (m_playerStats != null)
            {
                m_playerStats.Reset();
            }

            // 重置各子系统
            if (m_spawnSystem != null)
            {
                m_spawnSystem.Reset();
            }
            if (m_infectionSystem != null)
            {
                m_infectionSystem.Reset();
            }
            if (m_experienceSystem != null)
            {
                m_experienceSystem.Reset();
            }
            if (m_upgradeSystem != null)
            {
                m_upgradeSystem.Reset();
            }
            if (m_timerSystem != null)
            {
                m_timerSystem.Reset();
            }

            // 启动单局控制器
            if (m_sessionController != null)
            {
                m_sessionController.StartSession();
            }

            // 生成初始批次 Human（使用簇刷新系统替代）
            if (m_clusterSpawner != null)
            {
                m_clusterSpawner.Reset();
                m_clusterSpawner.SpawnInitialClusters();
            }
            else if (m_spawnSystem != null)
            {
                m_spawnSystem.SpawnInitialBatch();
            }

            // 启动倒计时
            if (m_timerSystem != null)
            {
                m_timerSystem.StartTimer();
            }

            // 显示游戏内 HUD
            if (m_uiManager != null)
            {
                m_uiManager.ShowHUD();
            }

            // 隐藏结算面板（确保 ResultPanel 在 Playing 状态隐藏）
            if (m_resultPanel != null)
            {
                m_resultPanel.HideResult();
            }

            // 隐藏 HUD 上的目标达成提示（新一局开始时重置）
            if (m_hudPanel != null)
            {
                m_hudPanel.HideVictoryIndicator();
            }
        }

        /// <summary>
        /// 处理结算流程：将本局金币存入局外系统、显示结算面板。
        /// </summary>
        private void HandleSettlement()
        {
            // 结束单局控制器
            if (m_sessionController != null)
            {
                m_sessionController.EndSession();
            }

            // 停止倒计时（防止残余帧继续递减）
            if (m_timerSystem != null)
            {
                m_timerSystem.StopTimer();
            }

            // 将本局获得的金币累加到局外持久化数据
            if (m_metaUpgradeSystem != null && m_experienceSystem != null)
            {
                int goldEarned = m_experienceSystem.CurrentGold;
                if (goldEarned > 0)
                {
                    m_metaUpgradeSystem.AddGold(goldEarned);
                }
            }

            // 新的 ResultPanel 已接管结算展示时，这里只做停表与持久化，
            // 面板显示由 HandleSessionEnd 统一处理，避免与旧 SettlementPanel 重叠。
            if (m_resultPanel != null)
            {
                if (m_uiManager != null)
                {
                    m_uiManager.HideAll();
                }
                return;
            }

            // 兼容旧流程：未配置 ResultPanel 时回退到 SettlementPanel
            if (m_uiManager != null)
            {
                m_uiManager.ShowSettlementPanel();
            }

            // 填充结算面板数据并注册继续回调
            if (m_settlementPanel != null)
            {
                int gold = m_experienceSystem != null ? m_experienceSystem.CurrentGold : 0;
                int exp = m_experienceSystem != null ? m_experienceSystem.CurrentExp : 0;

                m_settlementPanel.ShowSettlement(m_infectionCount, gold, exp, OnContinue);
            }
        }

        /// <summary>
        /// 结算面板点击继续后的回调：切换到 Playing 状态开始新一局。
        /// </summary>
        private void OnContinue()
        {
            if (m_stateManager != null)
            {
                m_stateManager.ChangeState(GameState.Playing);
            }
        }

        // ==================== 事件处理 ====================

        /// <summary>
        /// 处理感染成功事件：增加经验和金币、累计感染次数。
        /// </summary>
        /// <param name="position">被感染单位的世界坐标（当前未使用，预留给 VFX）</param>
        private void HandleInfection(Vector2 position)
        {
            m_infectionCount++;

            if (m_experienceSystem != null && m_gameConfig != null)
            {
                m_experienceSystem.AddExp(m_gameConfig.ExpPerInfection);
                m_experienceSystem.AddGold(m_gameConfig.GoldPerInfection);
            }
        }

        /// <summary>
        /// 处理升级事件：暂停倒计时、显示升级面板供玩家选择。
        /// </summary>
        /// <param name="newLevel">升级后的新等级</param>
        private void HandleLevelUp(int newLevel)
        {
            // 暂停游戏逻辑
            m_isPaused = true;
            if (m_timerSystem != null)
            {
                m_timerSystem.StopTimer();
            }

            // 显示升级面板
            if (m_uiManager != null)
            {
                m_uiManager.ShowUpgradePanel();
            }

            // 抽取升级选项并展示
            if (m_upgradePanel != null && m_upgradeSystem != null)
            {
                List<UpgradeOption> options = m_upgradeSystem.DrawOptions(3);
                m_upgradePanel.ShowOptions(options, OnUpgradeSelected);
            }
        }

        /// <summary>
        /// 玩家选择升级选项后的回调：应用升级、关闭面板、恢复倒计时。
        /// </summary>
        /// <param name="option">玩家选择的升级选项</param>
        private void OnUpgradeSelected(UpgradeOption option)
        {
            // 应用升级效果
            if (m_upgradeSystem != null)
            {
                m_upgradeSystem.ApplyUpgrade(option);
            }

            // 隐藏升级面板
            if (m_uiManager != null)
            {
                m_uiManager.HideUpgradePanel();
            }

            // 恢复游戏
            m_isPaused = false;

            // 恢复倒计时
            if (m_timerSystem != null)
            {
                m_timerSystem.ResumeTimer();
            }
        }

        /// <summary>
        /// 处理倒计时结束事件：切换到结算状态。
        /// </summary>
        private void HandleTimerEnd()
        {
            if (m_stateManager != null)
            {
                m_stateManager.ChangeState(GameState.Settlement);
            }
        }

        /// <summary>
        /// 处理单局结束事件：隐藏 HUD，显示 ResultPanel 并绑定 Restart 回调。
        /// 由 GameSessionController.OnSessionEnd 事件触发。
        /// </summary>
        /// <param name="result">本局结算数据</param>
        private void HandleSessionEnd(SessionResult result)
        {
            if (m_uiManager != null)
            {
                m_uiManager.HideAll();
            }

            // 显示 ResultPanel，绑定 Restart 回调
            if (m_resultPanel != null)
            {
                m_resultPanel.ShowResult(result, HandleRestart);
            }
        }

        /// <summary>
        /// 处理 ResultPanel 的 Restart 按钮回调：隐藏 ResultPanel，重新开始新一局。
        /// </summary>
        private void HandleRestart()
        {
            // 隐藏 ResultPanel
            if (m_resultPanel != null)
            {
                m_resultPanel.HideResult();
            }

            // 切换到 Playing 状态开始新一局
            if (m_stateManager != null)
            {
                m_stateManager.ChangeState(GameState.Playing);
            }
        }

        // ==================== 公共方法（兼容旧接口） ====================

        /// <summary>
        /// 重置所有子系统状态。
        /// 在一局结束、玩家点击继续开启新一局时由 StartNewMatch 内部调用。
        /// 保留此公共方法以兼容外部调用场景。
        /// </summary>
        public void ResetSystems()
        {
            StartNewMatch();
        }

        // ==================== 输入辅助 ====================

        /// <summary>
        /// 获取当前帧的输入方向（摇杆 + 键盘取模长较大者），用于旋转调度。
        /// </summary>
        private Vector2 GetCurrentInputDirection()
        {
            Vector2 joystickDir = (m_joystick != null) ? m_joystick.Direction : Vector2.zero;

            float kx = 0f, ky = 0f;
            if (Input.GetKey(KeyCode.W)) ky += 1f;
            if (Input.GetKey(KeyCode.S)) ky -= 1f;
            if (Input.GetKey(KeyCode.A)) kx -= 1f;
            if (Input.GetKey(KeyCode.D)) kx += 1f;
            Vector2 keyDir = new Vector2(kx, ky);
            if (keyDir.sqrMagnitude > 1f) keyDir.Normalize();

            return (keyDir.sqrMagnitude > joystickDir.sqrMagnitude) ? keyDir : joystickDir;
        }
    }
}
