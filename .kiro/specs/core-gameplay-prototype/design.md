# Design Document

## Introduction

本文档描述 2D 俯视角僵尸感染割草小游戏核心玩法原型的技术设计。基于 Unity 2022.3 LTS，采用单场景状态机架构，GameSystemRunner 作为唯一入口统一管理各子系统的初始化和更新。所有数值配置通过 ScriptableObject 管理，Human 和 ZombieCompanion 通过对象池复用，目标在 100~200 个单位时保持流畅运行。

---

## High-Level Architecture

### 系统组件图

```
┌─────────────────────────────────────────────────────────────┐
│                     GameSystemRunner                          │
│  (MonoBehaviour - 唯一入口, Inspector 注入所有子系统引用)      │
├─────────────────────────────────────────────────────────────┤
│  GameStateManager        │  管理 Start/Playing/Settlement    │
├──────────────────────────┼──────────────────────────────────┤
│  PlayerSystem            │  玩家移动、属性管理               │
│  InputSystem             │  VirtualJoystick 输入采集         │
│  SpawnSystem             │  Human 刷新逻辑                   │
│  InfectionSystem         │  感染判定与转化                   │
│  ZombieCompanionSystem   │  僵尸同伴 AI 行为                 │
│  HumanAISystem           │  人类逃跑/漫游 AI                 │
│  ExperienceSystem        │  经验/金币/升级触发               │
│  UpgradeSystem           │  局内升级选择与应用               │
│  TimerSystem             │  单局倒计时                       │
│  MetaUpgradeSystem       │  局外升级数据读写                 │
│  ObjectPoolManager       │  Human/ZombieCompanion 对象池     │
├──────────────────────────┼──────────────────────────────────┤
│  UIManager               │  管理所有 UI 面板显示/隐藏         │
└─────────────────────────────────────────────────────────────┘
         │                          │
         ▼                          ▼
┌─────────────────┐      ┌─────────────────────┐
│   GameConfig    │      │   MetaUpgradeData   │
│ (ScriptableObj) │      │   (PlayerPrefs)     │
└─────────────────┘      └─────────────────────┘
```

### 数据流

```
[VirtualJoystick] ──direction──▶ [PlayerSystem] ──position──▶ [InfectionSystem]
                                                                     │
[SpawnSystem] ──spawn──▶ [Human] ◀──AI──▶ [HumanAISystem]           │
                                                                     │
[InfectionSystem] ──infect──▶ [ObjectPoolManager] ──recycle/spawn──▶ │
                  ──reward──▶ [ExperienceSystem] ──levelUp──▶ [UpgradeSystem]
                                                                     │
[ZombieCompanion] ◀──AI──▶ [ZombieCompanionSystem]                   │
                                                                     │
[TimerSystem] ──timeUp──▶ [GameStateManager] ──settle──▶ [UIManager]
                                                    │
                                              [MetaUpgradeSystem]
```

### 游戏状态流转

```
[Start] ──点击开始──▶ [Playing] ──倒计时归零──▶ [Settlement] ──点击继续──▶ [Playing]
```

---

## Low-Level Design

### 目录结构与文件映射

```
Assets/Game/Scripts/
├── Core/
│   ├── GameSystemRunner.cs        // 唯一入口，管理所有子系统
│   ├── GameStateManager.cs        // 游戏状态机 (Start/Playing/Settlement)
│   └── GameEvents.cs              // 全局事件定义 (C# event/Action)
├── Config/
│   ├── GameConfig.cs              // ScriptableObject 配置类
│   └── MetaUpgradeConfig.cs       // 局外升级配置 (每级花费/加成)
├── Gameplay/
│   ├── Player/
│   │   ├── PlayerController.cs    // 玩家移动逻辑
│   │   └── PlayerStats.cs         // 玩家运行时属性 (速度/感染半径等)
│   ├── Enemy/
│   │   ├── HumanUnit.cs           // Human 单位组件
│   │   └── HumanAI.cs             // 人类 AI (逃跑/漫游)
│   ├── Zombie/
│   │   ├── ZombieCompanionUnit.cs // 僵尸同伴单位组件
│   │   └── ZombieCompanionAI.cs   // 僵尸同伴 AI (跟随/追击)
│   ├── Infection/
│   │   └── InfectionSystem.cs     // 感染判定与转化逻辑
│   ├── Wave/
│   │   └── SpawnSystem.cs         // Human 刷新管理
│   └── Skill/
│       ├── UpgradeSystem.cs       // 局内升级逻辑
│       ├── UpgradeOption.cs       // 升级选项数据结构
│       └── MetaUpgradeSystem.cs   // 局外升级读写
├── UI/
│   ├── UIManager.cs               // UI 面板管理
│   ├── VirtualJoystick.cs         // 虚拟摇杆输入
│   ├── HUDPanel.cs                // HUD (时间/经验/金币)
│   ├── UpgradePanel.cs            // 升级三选一面板
│   ├── SettlementPanel.cs         // 结算面板
│   └── StartPanel.cs              // 开始界面
└── Utility/
    ├── ObjectPool.cs              // 泛型对象池
    ├── ObjectPoolManager.cs       // 对象池统一管理
    └── MathUtils.cs               // 数学工具 (距离判定等)
```

---

### 关键类接口设计

#### GameSystemRunner (Core/GameSystemRunner.cs)

```csharp
/// <summary>
/// 游戏系统统一入口，管理所有子系统的初始化和更新
/// </summary>
public class GameSystemRunner : MonoBehaviour
{
    [SerializeField] private GameConfig m_gameConfig;
    [SerializeField] private GameStateManager m_stateManager;
    [SerializeField] private PlayerController m_playerController;
    [SerializeField] private SpawnSystem m_spawnSystem;
    [SerializeField] private InfectionSystem m_infectionSystem;
    [SerializeField] private ExperienceSystem m_experienceSystem;
    [SerializeField] private UpgradeSystem m_upgradeSystem;
    [SerializeField] private TimerSystem m_timerSystem;
    [SerializeField] private MetaUpgradeSystem m_metaUpgradeSystem;
    [SerializeField] private ObjectPoolManager m_poolManager;
    [SerializeField] private UIManager m_uiManager;

    // 初始化所有子系统
    public void InitializeSystems();
    // 重置所有子系统状态（新局开始）
    public void ResetSystems();
}
```

#### GameStateManager (Core/GameStateManager.cs)

```csharp
/// <summary>
/// 游戏状态机，管理 Start/Playing/Settlement 三个状态
/// </summary>
public class GameStateManager : MonoBehaviour
{
    public enum GameState { Start, Playing, Settlement }

    public GameState CurrentState { get; }
    public event Action<GameState> OnStateChanged;

    // 切换到指定状态
    public void ChangeState(GameState newState);
}
```

#### GameConfig (Config/GameConfig.cs)

```csharp
/// <summary>
/// 游戏全局配置，所有数值参数集中管理
/// </summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "Game/GameConfig")]
public class GameConfig : ScriptableObject
{
    // 玩家属性
    public float PlayerBaseSpeed;
    public float PlayerBaseInfectionRadius;

    // 人类属性
    public float HumanMoveSpeed;
    public float HumanPerceptionRadius;
    public int HumanMaxCount;
    public int HumanInitialCount;
    public float HumanSpawnInterval;

    // 僵尸同伴属性
    public float ZombieCompanionSpeed;
    public float ZombieCompanionPerceptionRadius;
    public int ZombieCompanionMaxCount;

    // 感染参数
    public float BaseInfectionRadius;

    // 奖励参数
    public int ExpPerInfection;
    public int GoldPerInfection;

    // 升级参数
    public int[] ExpThresholds; // 每级所需经验
    public float InfectionRadiusPerUpgrade;
    public float SpeedPerUpgrade;
    public int ZombieCapPerUpgrade;
    public float ExpMultiplierPerUpgrade;

    // 单局时长
    public float MatchDuration; // 180~300 秒

    // 对象池
    public int HumanPoolInitialSize;
    public int ZombiePoolInitialSize;
}
```

#### VirtualJoystick (UI/VirtualJoystick.cs)

```csharp
/// <summary>
/// 基于 UGUI 的虚拟摇杆，输出归一化方向向量
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform m_handle;
    [SerializeField] private float m_radius;

    /// <summary>
    /// 当前方向向量，模长 <= 1
    /// </summary>
    public Vector2 Direction { get; }

    public void OnPointerDown(PointerEventData eventData);
    public void OnDrag(PointerEventData eventData);
    public void OnPointerUp(PointerEventData eventData);

    // 计算归一化方向，确保模长不超过 1
    private Vector2 CalculateDirection(Vector2 inputPosition);
}
```

#### PlayerController (Gameplay/Player/PlayerController.cs)

```csharp
/// <summary>
/// 玩家移动控制器
/// </summary>
public class PlayerController : MonoBehaviour
{
    [SerializeField] private VirtualJoystick m_joystick;
    [SerializeField] private PlayerStats m_stats;

    /// <summary>
    /// 根据摇杆方向和当前速度移动玩家
    /// 位移 = direction * speed * deltaTime
    /// </summary>
    public void UpdateMovement(float deltaTime);
}
```

#### PlayerStats (Gameplay/Player/PlayerStats.cs)

```csharp
/// <summary>
/// 玩家运行时属性，支持基础值 + 加成值
/// </summary>
public class PlayerStats
{
    public float MoveSpeed { get; }
    public float InfectionRadius { get; }
    public int ZombieCompanionCap { get; }
    public float ExpMultiplier { get; }

    // 从 GameConfig + MetaUpgrade 初始化基础值
    public void Initialize(GameConfig config, MetaUpgradeData metaData);
    // 应用局内升级加成
    public void ApplyUpgrade(UpgradeType type, float value);
    // 重置为基础值（新局开始）
    public void Reset();
}
```

#### ObjectPool<T> (Utility/ObjectPool.cs)

```csharp
/// <summary>
/// 泛型对象池，管理 MonoBehaviour 实例的复用
/// </summary>
public class ObjectPool<T> where T : MonoBehaviour
{
    // 从池中取出一个实例（池空时动态扩展）
    public T Get();
    // 将实例归还池中
    public void Return(T instance);
    // 预创建指定数量实例
    public void Prewarm(int count);
    // 当前活跃实例数
    public int ActiveCount { get; }
    // 当前池中空闲实例数
    public int FreeCount { get; }
}
```

#### InfectionSystem (Gameplay/Infection/InfectionSystem.cs)

```csharp
/// <summary>
/// 感染判定与转化系统
/// </summary>
public class InfectionSystem : MonoBehaviour
{
    [SerializeField] private ObjectPoolManager m_poolManager;
    [SerializeField] private PlayerStats m_playerStats;

    /// <summary>
    /// 判定指定位置是否在感染范围内
    /// </summary>
    public bool IsInInfectionRange(Vector2 sourcePos, Vector2 targetPos, float radius);

    /// <summary>
    /// 执行感染转化：回收 Human，生成 ZombieCompanion
    /// 返回是否成功（受上限约束）
    /// </summary>
    public bool TryInfect(HumanUnit human);

    /// <summary>
    /// 每帧检测所有 Human 与 Player/ZombieCompanion 的距离
    /// </summary>
    public void UpdateInfectionCheck();
}
```

#### HumanAI (Gameplay/Enemy/HumanAI.cs)

```csharp
/// <summary>
/// 人类 AI：感知威胁时逃跑，无威胁时随机漫游
/// </summary>
public class HumanAI : MonoBehaviour
{
    /// <summary>
    /// 计算逃跑方向（远离最近威胁源）
    /// </summary>
    public Vector2 CalculateFleeDirection(Vector2 humanPos, Vector2 threatPos);

    /// <summary>
    /// 计算随机漫游方向
    /// </summary>
    public Vector2 CalculateWanderDirection();

    /// <summary>
    /// 更新 AI 行为
    /// </summary>
    public void UpdateAI(float deltaTime);
}
```

#### ZombieCompanionAI (Gameplay/Zombie/ZombieCompanionAI.cs)

```csharp
/// <summary>
/// 僵尸同伴 AI：无目标时跟随玩家，有目标时追击最近 Human
/// </summary>
public class ZombieCompanionAI : MonoBehaviour
{
    /// <summary>
    /// 从候选目标中选择最近的 Human
    /// </summary>
    public HumanUnit FindNearestTarget(Vector2 selfPos, List<HumanUnit> candidates);

    /// <summary>
    /// 计算朝向目标的移动方向
    /// </summary>
    public Vector2 CalculateMoveDirection(Vector2 selfPos, Vector2 targetPos);

    /// <summary>
    /// 更新 AI 行为
    /// </summary>
    public void UpdateAI(float deltaTime);
}
```

#### ExperienceSystem (Gameplay/Skill/ExperienceSystem.cs — 逻辑上属于 Skill 目录)

```csharp
/// <summary>
/// 经验与金币管理，处理升级触发
/// </summary>
public class ExperienceSystem : MonoBehaviour
{
    public int CurrentExp { get; }
    public int CurrentGold { get; }
    public int CurrentLevel { get; }
    public event Action OnLevelUp;

    /// <summary>
    /// 增加经验值，检查是否触发升级
    /// </summary>
    public void AddExp(int amount);

    /// <summary>
    /// 增加金币
    /// </summary>
    public void AddGold(int amount);

    /// <summary>
    /// 判断当前经验是否达到升级阈值
    /// </summary>
    public bool HasReachedThreshold();
}
```

#### UpgradeSystem (Gameplay/Skill/UpgradeSystem.cs)

```csharp
/// <summary>
/// 局内升级系统
/// </summary>
public enum UpgradeType
{
    InfectionRadius,
    MoveSpeed,
    ZombieCompanionCap,
    ExpMultiplier
}

public class UpgradeOption
{
    public UpgradeType Type;
    public float Value;
    public string DisplayName;
}

public class UpgradeSystem : MonoBehaviour
{
    /// <summary>
    /// 从可用池中随机抽取 N 个不重复选项
    /// </summary>
    public List<UpgradeOption> DrawOptions(int count);

    /// <summary>
    /// 应用选中的升级效果到 PlayerStats
    /// </summary>
    public void ApplyUpgrade(UpgradeOption option);
}
```

#### MetaUpgradeSystem (Gameplay/Skill/MetaUpgradeSystem.cs)

```csharp
/// <summary>
/// 局外升级数据管理，使用 PlayerPrefs 持久化
/// </summary>
public class MetaUpgradeSystem : MonoBehaviour
{
    /// <summary>
    /// 保存升级数据到 PlayerPrefs
    /// </summary>
    public void Save(MetaUpgradeData data);

    /// <summary>
    /// 从 PlayerPrefs 加载升级数据
    /// </summary>
    public MetaUpgradeData Load();

    /// <summary>
    /// 累加金币到持久化数据
    /// </summary>
    public void AddGold(int amount);

    /// <summary>
    /// 获取指定升级项的当前加成值
    /// </summary>
    public float GetBonus(UpgradeType type);
}

public class MetaUpgradeData
{
    public int TotalGold;
    public int SpeedLevel;
    public int InfectionRadiusLevel;
    public int ZombieCapLevel;
    public int ExpMultiplierLevel;
}
```

#### SpawnSystem (Gameplay/Wave/SpawnSystem.cs)

```csharp
/// <summary>
/// Human 刷新管理系统
/// </summary>
public class SpawnSystem : MonoBehaviour
{
    [SerializeField] private ObjectPoolManager m_poolManager;
    [SerializeField] private GameConfig m_config;

    /// <summary>
    /// 生成初始批次 Human
    /// </summary>
    public void SpawnInitialBatch();

    /// <summary>
    /// 按间隔检查并补充 Human（不超过上限）
    /// </summary>
    public void UpdateSpawn(float deltaTime);

    /// <summary>
    /// 当前场上活跃 Human 数量
    /// </summary>
    public int ActiveHumanCount { get; }
}
```

---

## Error Handling

| 场景 | 处理方式 |
|------|----------|
| ObjectPool 耗尽 | 动态扩展池容量，Debug.LogWarning 提示 |
| GameConfig 未赋值 | Awake 中检查并 Debug.LogError，禁止进入 Playing 状态 |
| 升级池选项不足 3 个 | 返回所有可用选项（可能少于 3） |
| PlayerPrefs 数据损坏 | 返回默认初始值，不中断游戏 |
| 单位移动超出地图边界 | 钳制位置到地图范围内 |

---

## Performance Considerations

1. **对象池预热**: 游戏初始化时预创建 Human 和 ZombieCompanion 实例，避免运行时 GC
2. **距离判定优化**: InfectionSystem 使用 sqrMagnitude 替代 Vector2.Distance 避免开方运算
3. **分帧检测**: 当单位数量较多时，感染判定可分帧处理（每帧检测部分单位）
4. **AI 更新频率**: HumanAI 和 ZombieCompanionAI 可降低更新频率（如每 0.1 秒一次）而非每帧

---

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*


### Property 1: 摇杆输出向量归一化

*For any* 触摸拖拽输入位置，VirtualJoystick 输出的方向向量模长应始终 >= 0 且 <= 1。

**Validates: Requirements 1.1**

### Property 2: 玩家移动位移正确性

*For any* 非零方向向量 D 和正数速度 S 以及正数 deltaTime T，PlayerController 计算的位移应等于 normalize(D) * S * T（当 D 模长 > 1 时归一化，否则保持原值）。

**Validates: Requirements 2.1**

### Property 3: 属性修改立即生效

*For any* PlayerStats 实例，当通过 ApplyUpgrade 修改某属性后，立即读取该属性应返回包含修改后的值。对于 MetaUpgrade 加成，Initialize 后属性值应等于基础值 + 对应加成值。

**Validates: Requirements 2.3, 8.5, 11.3**

### Property 4: Human 场上数量不超过上限

*For any* 刷新时刻，SpawnSystem 维护的活跃 Human 数量应始终 <= GameConfig.HumanMaxCount。

**Validates: Requirements 3.2**

### Property 5: 对象池取出/回收 round-trip

*For any* ObjectPool<T> 实例，执行任意序列的 Get/Return 操作后，ActiveCount + FreeCount 应等于池中曾创建的总实例数，且 Return 后再次 Get 应返回有效（非 null）实例。

**Validates: Requirements 3.4, 13.1**

### Property 6: Human 逃跑方向远离威胁

*For any* Human 位置 H 和威胁源位置 T（Player 或 ZombieCompanion），当 distance(H, T) < 感知半径时，HumanAI.CalculateFleeDirection(H, T) 返回的方向向量与 (H - T) 的点积应 > 0（即方向远离威胁）。

**Validates: Requirements 4.1, 4.2**

### Property 7: 感染距离判定正确性

*For any* 两个位置 A 和 B 以及正数半径 R，InfectionSystem.IsInInfectionRange(A, B, R) 返回 true 当且仅当 distance(A, B) < R。

**Validates: Requirements 5.1, 5.2**

### Property 8: 感染转化守恒

*For any* 成功的感染转化事件，执行 TryInfect 后活跃 Human 数量应减少 1，活跃 ZombieCompanion 数量应增加 1，且新生成的 ZombieCompanion 位置应等于被感染 Human 的原始位置。

**Validates: Requirements 5.3**

### Property 9: ZombieCompanion 数量不超过上限

*For any* 感染尝试，当当前 ZombieCompanion 数量已等于 PlayerStats.ZombieCompanionCap 时，TryInfect 应返回 false 且不改变任何单位数量。

**Validates: Requirements 5.4**

### Property 10: ZombieCompanion 追击最近目标

*For any* ZombieCompanion 位置 Z 和多个 Human 候选位置集合，FindNearestTarget 返回的 Human 应是集合中与 Z 距离最小的那个。

**Validates: Requirements 6.2**

### Property 11: 感染奖励累加正确性

*For any* 正整数 N 次成功感染，ExperienceSystem 的 CurrentExp 应增加 N * ExpPerInfection（乘以当前倍率），CurrentGold 应增加 N * GoldPerInfection。

**Validates: Requirements 7.1, 7.2**

### Property 12: 经验阈值触发升级

*For any* 经验值 E 和当前等级阈值 T，当 E >= T 时 HasReachedThreshold() 应返回 true，当 E < T 时应返回 false。

**Validates: Requirements 8.1**

### Property 13: 升级抽取不重复

*For any* 包含 >= 3 个选项的升级池，DrawOptions(3) 返回的列表长度应为 3 且所有元素的 UpgradeType 互不相同。

**Validates: Requirements 8.2**

### Property 14: MetaUpgrade 持久化 round-trip

*For any* MetaUpgradeData 实例，执行 Save 后再执行 Load 应返回与原始数据等价的 MetaUpgradeData（所有字段值相同）。

**Validates: Requirements 11.1**

### Property 15: 游戏状态机单一状态

*For any* 状态转换操作序列，GameStateManager.CurrentState 在任意时刻应恰好等于 Start、Playing 或 Settlement 中的一个，且 ChangeState 后 CurrentState 应立即等于目标状态。

**Validates: Requirements 12.1**

### Property 16: 结算金币持久化累加

*For any* 正整数金币数量 G，调用 MetaUpgradeSystem.AddGold(G) 后，Load() 返回的 TotalGold 应等于调用前的 TotalGold + G。

**Validates: Requirements 10.2**
