# Requirements Document

## Introduction

本文档定义僵尸感染游戏第一版核心单局闭环（session-loop-v1）的需求。目标是让游戏可以在 Unity Editor 中直接跑一局 180 秒短局：玩家从 1 个僵尸开始，通过移动感染人类；游戏实时统计感染人数；达到目标人数算通关；时间结束后进入结算。人类按"人群簇"刷新，取代当前的均匀随机散布方式。

## Glossary

- **GameSessionController**: 单局状态管理控制器，负责管理 Ready / Playing / Result 三种状态以及局内统计数据
- **HumanClusterSpawner**: 人群簇刷新系统，按簇（小人群、中人群）生成人类，取代原有均匀随机刷新
- **SessionState**: 单局状态枚举，包含 Ready（准备）、Playing（进行中）、Result（结算）
- **InfectedCount**: 本局累计感染人数
- **MaxZombieCount**: 本局同时存在的最大僵尸数量
- **MaxChainCount**: 本局最高连锁感染数（预留字段）
- **TargetInfectedCount**: 通关所需的目标感染人数
- **GameDurationSeconds**: 单局游戏总时长（秒）
- **Rating**: 评级，根据最终感染人数计算，分为 C / B / A / S / SS 五档
- **Cluster**: 人群簇，一组在空间上聚集的人类单位
- **SmallCluster**: 小人群簇，包含 3-5 个人类单位
- **MediumCluster**: 中人群簇，包含 8-12 个人类单位
- **HUD**: 游戏内抬头显示界面，实时展示剩余时间、感染人数、评级预览等信息
- **ResultPanel**: 结算面板，时间结束后展示本局成绩与通关状态
- **GameConfig**: ScriptableObject 配置类，所有数值参数的集中来源

## Requirements

### Requirement 1: 单局状态管理

**User Story:** As a 玩家, I want 游戏有明确的单局流程（准备→进行→结算）, so that 我能清楚知道游戏何时开始、何时结束

#### Acceptance Criteria

1. THE GameSessionController SHALL 管理三种 SessionState：Ready、Playing、Result
2. WHEN SessionState 从 Ready 切换到 Playing, THE GameSessionController SHALL 重置 InfectedCount 为 0、重置 MaxZombieCount 为 0、重置 MaxChainCount 为 0、启动倒计时
3. WHILE SessionState 为 Playing, THE GameSessionController SHALL 每帧递减剩余时间
4. WHEN 剩余时间递减到 0, THE GameSessionController SHALL 将 SessionState 切换为 Result
5. WHEN SessionState 切换为 Result, THE GameSessionController SHALL 停止所有游戏逻辑更新并计算最终评级

### Requirement 2: 单局配置参数

**User Story:** As a 开发者, I want 所有单局数值参数集中在 GameConfig 中配置, so that 我能在 Inspector 中快速调参而无需修改代码

#### Acceptance Criteria

1. THE GameConfig SHALL 提供 GameDurationSeconds 字段，默认值为 180
2. THE GameConfig SHALL 提供 TargetInfectedCount 字段，默认值为 80
3. THE GameConfig SHALL 提供 AScoreInfectedCount 字段，默认值为 150
4. THE GameConfig SHALL 提供 SScoreInfectedCount 字段，默认值为 250
5. THE GameConfig SHALL 提供 SSScoreInfectedCount 字段，默认值为 350
6. THE GameConfig SHALL 通过 Inspector 中的 SerializeField 暴露上述所有字段

### Requirement 3: 感染计数与统计

**User Story:** As a 玩家, I want 游戏准确统计我的感染成绩, so that 我能看到自己的表现

#### Acceptance Criteria

1. WHEN GameEvents.OnInfectionSuccess 事件触发, THE GameSessionController SHALL 将 InfectedCount 递增 1
2. WHEN InfectedCount 递增后, THE GameSessionController SHALL 比较当前活跃僵尸数量与 MaxZombieCount，取较大值更新 MaxZombieCount
3. THE GameSessionController SHALL 确保同一个人类单位被感染时仅触发一次 InfectedCount 递增
4. WHEN InfectedCount 达到或超过 TargetInfectedCount, THE GameSessionController SHALL 标记本局为 Victory 状态
5. WHEN Victory 状态被标记, THE GameSessionController SHALL 不立即结束游戏，继续运行直到 GameDurationSeconds 时间耗尽

### Requirement 4: 评级计算

**User Story:** As a 玩家, I want 游戏根据我的最终感染人数给出评级, so that 我有动力追求更高分数

#### Acceptance Criteria

1. WHEN SessionState 切换为 Result, THE GameSessionController SHALL 根据最终 InfectedCount 计算评级
2. WHEN 最终 InfectedCount 小于 TargetInfectedCount, THE GameSessionController SHALL 评定为 C 级
3. WHEN 最终 InfectedCount 大于等于 TargetInfectedCount 且小于 AScoreInfectedCount, THE GameSessionController SHALL 评定为 B 级
4. WHEN 最终 InfectedCount 大于等于 AScoreInfectedCount 且小于 SScoreInfectedCount, THE GameSessionController SHALL 评定为 A 级
5. WHEN 最终 InfectedCount 大于等于 SScoreInfectedCount 且小于 SSScoreInfectedCount, THE GameSessionController SHALL 评定为 S 级
6. WHEN 最终 InfectedCount 大于等于 SSScoreInfectedCount, THE GameSessionController SHALL 评定为 SS 级

### Requirement 5: 人群簇刷新 - 初始生成

**User Story:** As a 玩家, I want 开局附近就有人类可以感染, so that 我能在 10 秒内开始感染体验

#### Acceptance Criteria

1. WHEN SessionState 切换为 Playing, THE HumanClusterSpawner SHALL 在玩家位置附近生成 2 至 3 个 SmallCluster
2. THE HumanClusterSpawner SHALL 将初始 SmallCluster 放置在距离玩家 3 至 8 个单位的范围内
3. THE HumanClusterSpawner SHALL 确保每个 SmallCluster 包含 3 至 5 个人类单位
4. THE HumanClusterSpawner SHALL 确保初始各 Cluster 之间的中心距离大于等于 5 个单位

### Requirement 6: 人群簇刷新 - 持续补充

**User Story:** As a 玩家, I want 游戏持续补充新的人群, so that 我始终有目标可以追逐

#### Acceptance Criteria

1. WHILE SessionState 为 Playing, THE HumanClusterSpawner SHALL 按配置的时间间隔持续生成新的 Cluster
2. THE HumanClusterSpawner SHALL 随机选择生成 SmallCluster（3-5 人）或 MediumCluster（8-12 人）
3. THE HumanClusterSpawner SHALL 将新生成的 Cluster 放置在地图范围内且距离玩家至少 8 个单位的位置
4. THE HumanClusterSpawner SHALL 确保新 Cluster 与已有 Cluster 中心距离大于等于 5 个单位
5. THE HumanClusterSpawner SHALL 在场上人类总数达到 GameConfig.HumanMaxCount 时停止生成新 Cluster
6. THE HumanClusterSpawner SHALL 将刷新间隔、簇大小范围、最小间距等参数暴露在 GameConfig 或组件 Inspector 中

### Requirement 7: 游戏内 HUD 显示

**User Story:** As a 玩家, I want 实时看到剩余时间和感染进度, so that 我能把握节奏

#### Acceptance Criteria

1. WHILE SessionState 为 Playing, THE HUD SHALL 显示剩余时间（格式为 分:秒）
2. WHILE SessionState 为 Playing, THE HUD SHALL 显示当前感染人数与目标感染人数（格式为 当前数/目标数）
3. WHILE SessionState 为 Playing, THE HUD SHALL 根据当前 InfectedCount 实时显示评级预览（C / B / A / S / SS）
4. WHEN InfectedCount 达到或超过 TargetInfectedCount, THE HUD SHALL 显示"目标达成"提示文本
5. THE HUD SHALL 使用 Unity UI Text 组件实现，保持简洁

### Requirement 8: 结算面板

**User Story:** As a 玩家, I want 游戏结束后看到完整的成绩总结, so that 我能了解本局表现

#### Acceptance Criteria

1. WHEN SessionState 切换为 Result, THE ResultPanel SHALL 显示最终感染人数
2. WHEN SessionState 切换为 Result, THE ResultPanel SHALL 显示最大尸群数量（MaxZombieCount）
3. WHEN SessionState 切换为 Result, THE ResultPanel SHALL 显示最终评级（C / B / A / S / SS）
4. WHEN SessionState 切换为 Result, THE ResultPanel SHALL 显示通关状态（Victory 或 Failed）
5. WHEN InfectedCount 大于等于 TargetInfectedCount, THE ResultPanel SHALL 显示 Victory
6. WHEN InfectedCount 小于 TargetInfectedCount, THE ResultPanel SHALL 显示 Failed
7. THE ResultPanel SHALL 提供 Restart 按钮
8. WHEN 玩家点击 Restart 按钮, THE GameSessionController SHALL 将 SessionState 切换为 Ready 并重新开始新一局

### Requirement 9: 系统集成约束

**User Story:** As a 开发者, I want 新系统与现有架构兼容, so that 不破坏已有功能

#### Acceptance Criteria

1. THE GameSessionController SHALL 通过 GameSystemRunner 统一入口进行初始化和调度
2. THE HumanClusterSpawner SHALL 复用现有 ObjectPoolManager 的 GetHuman / ReturnHuman 接口
3. THE GameSessionController SHALL 订阅现有 GameEvents.OnInfectionSuccess 事件进行感染计数
4. THE GameSessionController SHALL 不修改 PlayerController 和 VirtualJoystick 的现有逻辑
5. THE HumanClusterSpawner SHALL 替代现有 SpawnSystem 的初始生成和持续补充逻辑，保留 SpawnSystem 的 ActiveHumans 列表管理和 ReturnHuman 接口
6. THE GameSessionController SHALL 将所有配置参数从 GameConfig ScriptableObject 读取，不硬编码数值
