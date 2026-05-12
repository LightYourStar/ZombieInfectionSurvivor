# Requirements Document

## Introduction

本文档定义 2D 俯视角僵尸感染割草小游戏第一版核心玩法闭环的需求。目标是实现单局 3~5 分钟的可玩 Demo，验证"感染扩张 + 割草爽感"的核心体验。玩家控制初始僵尸通过虚拟摇杆移动，靠近人类后自动感染，被感染人类转化为僵尸同伴跟随玩家继续感染，形成滚雪球式扩张。单局包含经验升级系统和倒计时，结算后金币可用于局外数据层升级。

## Glossary

- **GameSystem**: 游戏核心系统的统称，由 GameSystemRunner 统一管理
- **Player**: 玩家控制的初始僵尸角色
- **Human**: 地图中刷新的人类单位，可被感染
- **ZombieCompanion**: 被感染后转化的僵尸同伴单位
- **VirtualJoystick**: 基于 UGUI 实现的轻量虚拟摇杆输入组件
- **InfectionRadius**: 感染判定的有效距离范围
- **ObjectPool**: 管理 Human 和 ZombieCompanion 实例复用的对象池
- **UpgradePanel**: 升级时弹出的三选一技能选择界面
- **SettlementPanel**: 单局结束后的结算界面
- **GameConfig**: 使用 ScriptableObject 存储的游戏配置数据
- **MetaUpgrade**: 局外永久升级数据，存储于 PlayerPrefs

## Requirements

### Requirement 1: 虚拟摇杆输入

**User Story:** As a 玩家, I want 通过虚拟摇杆控制角色移动方向, so that 我可以在触屏设备上流畅操控角色。

#### Acceptance Criteria

1. THE VirtualJoystick SHALL 基于 UGUI EventSystem 实现拖拽输入，输出归一化的二维方向向量
2. WHEN 玩家触摸并拖拽摇杆区域, THE VirtualJoystick SHALL 实时更新方向向量并传递给 Player 移动模块
3. WHEN 玩家释放触摸, THE VirtualJoystick SHALL 将方向向量归零并将摇杆视觉复位到中心
4. THE VirtualJoystick SHALL 不依赖任何第三方插件，仅使用 Unity UGUI 组件实现

### Requirement 2: 玩家移动

**User Story:** As a 玩家, I want 角色根据摇杆方向平滑移动, so that 我能自由探索地图并接近人类目标。

#### Acceptance Criteria

1. WHILE VirtualJoystick 输出方向向量不为零, THE Player SHALL 沿该方向以当前移动速度持续移动
2. THE Player SHALL 从 GameConfig 中读取基础移动速度值
3. WHEN MetaUpgrade 或局内升级修改了移动速度属性, THE Player SHALL 使用修改后的速度值进行移动

### Requirement 3: 人类单位刷新

**User Story:** As a 玩家, I want 地图中持续出现人类单位, so that 我始终有感染目标可以追逐。

#### Acceptance Criteria

1. WHEN 单局开始, THE GameSystem SHALL 在地图范围内生成初始批次的 Human 单位
2. WHILE 单局进行中且场上 Human 数量低于配置上限, THE GameSystem SHALL 按配置间隔从 ObjectPool 中取出 Human 并放置在地图边缘或玩家视野外
3. THE GameSystem SHALL 从 GameConfig 中读取刷新间隔、初始数量和场上上限等参数
4. THE ObjectPool SHALL 管理 Human 实例的创建和回收，避免频繁调用 Instantiate 和 Destroy

### Requirement 4: 人类逃跑行为

**User Story:** As a 玩家, I want 人类在感知到危险时逃跑, so that 追逐过程具有游戏性和挑战感。

#### Acceptance Criteria

1. WHILE Human 与 Player 的距离小于配置的感知半径, THE Human SHALL 沿远离 Player 的方向移动
2. WHILE Human 与任意 ZombieCompanion 的距离小于配置的感知半径, THE Human SHALL 沿远离该 ZombieCompanion 的方向移动
3. WHILE Human 未感知到任何威胁, THE Human SHALL 执行随机漫游行为
4. THE Human SHALL 从 GameConfig 中读取感知半径和移动速度参数

### Requirement 5: 感染判定与转化

**User Story:** As a 玩家, I want 靠近人类后自动感染他们, so that 我的僵尸军团不断壮大。

#### Acceptance Criteria

1. WHEN Player 与 Human 的距离小于当前 InfectionRadius, THE GameSystem SHALL 将该 Human 标记为被感染状态
2. WHEN ZombieCompanion 与 Human 的距离小于当前 InfectionRadius, THE GameSystem SHALL 将该 Human 标记为被感染状态
3. WHEN Human 被标记为感染状态, THE GameSystem SHALL 将该 Human 回收至 ObjectPool 并从 ObjectPool 中取出一个 ZombieCompanion 放置在相同位置
4. IF 当前 ZombieCompanion 数量已达到配置上限, THEN THE GameSystem SHALL 不执行新的感染转化
5. THE GameSystem SHALL 从 GameConfig 中读取基础 InfectionRadius 值

### Requirement 6: 僵尸同伴行为

**User Story:** As a 玩家, I want 僵尸同伴自动跟随我并追击附近人类, so that 我的军团能自主扩张感染范围。

#### Acceptance Criteria

1. WHILE ZombieCompanion 感知范围内无 Human, THE ZombieCompanion SHALL 朝 Player 位置移动以保持跟随
2. WHEN ZombieCompanion 感知范围内存在 Human, THE ZombieCompanion SHALL 朝最近的 Human 移动进行追击
3. THE ZombieCompanion SHALL 从 GameConfig 中读取移动速度和感知范围参数

### Requirement 7: 经验与金币奖励

**User Story:** As a 玩家, I want 每次感染人类获得经验和金币, so that 我有成长反馈和资源积累。

#### Acceptance Criteria

1. WHEN Human 被成功感染转化, THE GameSystem SHALL 给予玩家配置数量的经验值
2. WHEN Human 被成功感染转化, THE GameSystem SHALL 给予玩家配置数量的金币
3. THE GameSystem SHALL 从 GameConfig 中读取单次感染奖励的经验值和金币数量

### Requirement 8: 局内升级系统

**User Story:** As a 玩家, I want 经验满后选择升级强化属性, so that 每局游戏都有成长决策的乐趣。

#### Acceptance Criteria

1. WHEN 玩家累计经验值达到当前等级所需经验阈值, THE GameSystem SHALL 暂停游戏逻辑并弹出 UpgradePanel
2. THE UpgradePanel SHALL 从可用升级池中随机抽取三个不同的升级选项展示给玩家
3. WHEN 玩家选择一个升级选项, THE GameSystem SHALL 应用该升级效果并关闭 UpgradePanel 恢复游戏
4. THE GameSystem SHALL 支持以下升级类型：InfectionRadius 增加、移动速度增加、ZombieCompanion 上限增加、经验倍率增加
5. WHEN 升级被应用, THE GameSystem SHALL 立即更新对应属性的运行时数值

### Requirement 9: 单局倒计时

**User Story:** As a 玩家, I want 单局有明确的时间限制, so that 每局游戏节奏紧凑且有紧迫感。

#### Acceptance Criteria

1. WHEN 单局开始, THE GameSystem SHALL 从 GameConfig 读取单局总时长并启动倒计时
2. WHILE 单局进行中, THE GameSystem SHALL 在 HUD 上实时显示剩余时间
3. WHEN 倒计时归零, THE GameSystem SHALL 结束当前局并触发结算流程
4. THE GameConfig SHALL 配置单局时长在 180 秒至 300 秒范围内

### Requirement 10: 结算流程

**User Story:** As a 玩家, I want 单局结束后看到本局成果, so that 我有明确的成就感和继续游玩的动力。

#### Acceptance Criteria

1. WHEN 单局结束, THE GameSystem SHALL 显示 SettlementPanel 展示本局感染总数、获得金币和获得经验
2. WHEN SettlementPanel 显示, THE GameSystem SHALL 将本局获得的金币累加到 MetaUpgrade 的持久化金币总量中
3. WHEN 玩家在 SettlementPanel 点击继续按钮, THE GameSystem SHALL 重置游戏状态并开始新一局

### Requirement 11: 局外升级数据层

**User Story:** As a 玩家, I want 金币可以永久提升角色属性, so that 多次游玩有长期成长感。

#### Acceptance Criteria

1. THE MetaUpgrade SHALL 使用 PlayerPrefs 持久化存储局外升级等级数据
2. THE MetaUpgrade SHALL 支持以下升级项：基础移动速度加成、基础 InfectionRadius 加成、基础 ZombieCompanion 上限加成、基础经验倍率加成
3. WHEN 单局开始, THE GameSystem SHALL 读取 MetaUpgrade 数据并将加成应用到对应的运行时属性上
4. THE MetaUpgrade SHALL 从 GameConfig 中读取每级升级所需金币和每级加成数值

### Requirement 12: 场景流程管理

**User Story:** As a 玩家, I want 游戏流程简洁流畅, so that 我可以快速进入游戏并循环游玩。

#### Acceptance Criteria

1. THE GameSystem SHALL 在单场景内管理开始、游戏中、结算三个状态的切换
2. WHEN 游戏启动, THE GameSystem SHALL 显示开始界面并等待玩家点击开始按钮
3. WHEN 玩家点击开始按钮, THE GameSystem SHALL 初始化所有游戏子系统并进入游戏中状态
4. THE GameSystemRunner SHALL 作为唯一入口管理所有子系统的初始化和更新顺序

### Requirement 13: 性能与对象池

**User Story:** As a 开发者, I want 游戏在 100~200 个单位时保持流畅, so that 玩家体验不受卡顿影响。

#### Acceptance Criteria

1. THE ObjectPool SHALL 管理 Human 和 ZombieCompanion 的实例复用
2. THE ObjectPool SHALL 在游戏初始化时预创建配置数量的实例
3. IF ObjectPool 中无可用实例且需要新单位, THEN THE ObjectPool SHALL 动态扩展池容量
4. WHEN 单位不再需要时, THE GameSystem SHALL 将单位归还 ObjectPool 而非销毁

### Requirement 14: 配置数据管理

**User Story:** As a 开发者, I want 所有数值参数通过 ScriptableObject 配置, so that 调参不需要修改代码。

#### Acceptance Criteria

1. THE GameConfig SHALL 使用 ScriptableObject 存储所有游戏数值参数
2. THE GameConfig SHALL 包含以下配置分类：玩家属性、人类属性、僵尸同伴属性、感染参数、刷怪参数、升级参数、单局时长
3. THE GameSystem SHALL 通过 Inspector 注入方式引用 GameConfig 实例
4. THE GameConfig SHALL 不包含硬编码在逻辑代码中的数值

### Requirement 15: 代码规范与占位表现

**User Story:** As a 开发者, I want 代码遵循统一规范且使用占位图, so that 后续扩展和美术替换方便。

#### Acceptance Criteria

1. THE GameSystem SHALL 使用 m_ 前缀命名所有私有字段
2. THE GameSystem SHALL 对关键逻辑添加中文注释
3. THE GameSystem SHALL 使用 [SerializeField] private 标记需要 Inspector 赋值的字段
4. THE GameSystem SHALL 使用纯色方块或简单几何图形作为所有单位和 UI 的占位视觉表现
