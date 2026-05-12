# 实现计划: session-loop-v1

## 概述

本实现计划将单局闭环功能分解为增量式编码任务。从数据模型和配置扩展开始，逐步构建核心控制器、生成系统、UI 面板，最后集成到 GameSystemRunner 中。每个任务都建立在前一步的基础上，确保无孤立代码。

## Tasks

- [ ] 1. 数据模型与配置扩展
  - [-] 1.1 创建 SessionState 枚举、SessionRating 枚举和 SessionResult 数据类
    - 在 `Assets/Game/Scripts/Core/` 下创建 `SessionState.cs`，定义 Ready/Playing/Result 枚举
    - 在同目录下创建 `SessionRating.cs`，定义 C/B/A/S/SS 枚举
    - 在同目录下创建 `SessionResult.cs`，定义包含 InfectedCount、MaxZombieCount、MaxChainCount、Rating、IsVictory、ElapsedTime 的数据类
    - _Requirements: 1.1, 4.1, 8.1, 8.2, 8.3, 8.4_

  - [-] 1.2 扩展 GameConfig 添加单局目标与评级配置字段
    - 在现有 GameConfig ScriptableObject 中添加 `[Header("单局目标与评级")]` 区域
    - 添加 SerializeField 字段：TargetInfectedCount(80)、AScoreInfectedCount(150)、SScoreInfectedCount(250)、SSScoreInfectedCount(350)
    - 为每个字段添加公开只读属性
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

  - [-] 1.3 扩展 GameEvents 添加单局状态事件
    - 在现有 `GameEvents.cs` 中添加 `OnSessionStateChanged` 事件（Action\<SessionState\>）
    - 添加 `OnInfectionCountChanged` 事件（Action\<int, int\>，参数为 current 和 target）
    - 添加对应的 Raise 方法
    - _Requirements: 9.3_

- [ ] 2. GameSessionController 核心实现
  - [~] 2.1 实现 GameSessionController 状态管理与感染计数
    - 在 `Assets/Game/Scripts/Core/` 下创建 `GameSessionController.cs`
    - 实现 SessionState 状态机：Ready → Playing → Result → Ready 循环
    - 实现 Initialize()、StartSession()、EndSession()、RestartSession() 方法
    - 订阅 GameEvents.OnInfectionSuccess 事件，在 Playing 状态下递增 InfectedCount
    - 每次感染时比较并更新 MaxZombieCount
    - StartSession() 重置所有计数为 0 并启动倒计时
    - 当 InfectedCount >= TargetInfectedCount 时标记 IsVictory
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 3.1, 3.2, 3.3, 3.4, 3.5, 9.3, 9.6_

  - [~] 2.2 实现 CalculateRating 纯函数评级计算
    - 在 GameSessionController 中实现 `CalculateRating(int infectedCount)` 方法
    - 根据 GameConfig 中的阈值配置返回对应 SessionRating
    - 确保评级逻辑为纯函数，不依赖外部状态（便于测试和 HUD 实时预览复用）
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_

  - [~] 2.3 编写评级计算属性测试
    - **Property 1: 评级计算正确性**
    - 创建 `Assets/Game/Tests/EditMode/Core/RatingCalculationPropertyTests.cs`
    - 使用随机 int [0, 1000] 和随机有效阈值组合验证评级结果与阈值规则一致
    - **Validates: Requirements 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 7.3**

  - [~] 2.4 编写胜利判定属性测试
    - **Property 2: 胜利判定与评级一致性**
    - 创建 `Assets/Game/Tests/EditMode/Core/VictoryDeterminationPropertyTests.cs`
    - 使用随机 int [0, 500] 和随机 target [1, 200] 验证 IsVictory 与评级 C 的互斥关系
    - **Validates: Requirements 3.4, 8.5, 8.6**

  - [~] 2.5 编写会话重置属性测试
    - **Property 3: 会话重置完整性**
    - 创建 `Assets/Game/Tests/EditMode/Core/SessionResetPropertyTests.cs`
    - 使用随机先前状态值验证 StartSession() 后所有计数归零
    - **Validates: Requirements 1.2**

  - [~] 2.6 编写感染计数属性测试
    - **Property 4: 感染计数单调递增与最大值追踪**
    - 创建 `Assets/Game/Tests/EditMode/Core/InfectionCountingPropertyTests.cs`
    - 使用随机长度 [0, 200] 的事件序列验证 InfectedCount 等于事件数、MaxZombieCount 等于最大活跃僵尸数
    - **Validates: Requirements 3.1, 3.2**

- [~] 3. 检查点 - 核心控制器验证
  - 确保所有测试通过，ask the user if questions arise.

- [ ] 4. HumanClusterSpawner 实现
  - [~] 4.1 实现 HumanClusterSpawner 基础框架与簇生成逻辑
    - 在 `Assets/Game/Scripts/Gameplay/Wave/` 下创建 `HumanClusterSpawner.cs`
    - 实现 Inspector 注入字段：ObjectPoolManager、SpawnSystem、GameConfig、PlayerTransform
    - 实现簇配置参数（SmallCluster 3-5、MediumCluster 8-12、间隔 3s、最小间距 5 等）
    - 实现 `SpawnCluster(Vector2 center, int count)` 方法，使用均匀圆盘分布生成簇内人类
    - 实现 `GetPositionInCluster(Vector2 center, float radius)` 方法
    - 复用 ObjectPoolManager.GetHuman() 接口，注册到 SpawnSystem.ActiveHumans
    - _Requirements: 5.3, 6.2, 9.2, 9.5_

  - [~] 4.2 实现初始簇生成逻辑
    - 实现 `SpawnInitialClusters()` 方法
    - 在玩家附近生成 2-3 个 SmallCluster
    - 实现 `PickClusterCenter(float minDist, float maxDist, float minSeparation)` 方法
    - 确保簇中心距玩家 3-8 单位，簇间中心距离 >= 5
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [~] 4.3 实现持续补充簇生成逻辑
    - 实现 `UpdateSpawn(float deltaTime)` 方法
    - 按 ClusterSpawnInterval 间隔随机生成 SmallCluster 或 MediumCluster
    - 新簇距玩家 >= 8 单位，与已有簇中心距离 >= 5
    - 当场上人类总数 >= HumanMaxCount 时停止生成
    - 实现 `Reset()` 方法用于新一局重置
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_

  - [~] 4.4 编写簇大小属性测试
    - **Property 5: 簇大小有效性**
    - 创建 `Assets/Game/Tests/EditMode/Gameplay/ClusterSizePropertyTests.cs`
    - 验证生成的簇大小在 SmallCluster [3,5] 或 MediumCluster [8,12] 范围内
    - **Validates: Requirements 5.3, 6.2**

  - [~] 4.5 编写初始簇放置属性测试
    - **Property 6: 初始簇放置约束**
    - 创建 `Assets/Game/Tests/EditMode/Gameplay/InitialClusterPlacementPropertyTests.cs`
    - 使用随机玩家位置和地图范围验证簇数量 [2,3]、距玩家 [3,8]、簇间距 >= 5
    - **Validates: Requirements 5.1, 5.2, 5.4**

  - [~] 4.6 编写持续补充簇放置属性测试
    - **Property 7: 持续补充簇放置约束**
    - 创建 `Assets/Game/Tests/EditMode/Gameplay/PeriodicClusterPlacementPropertyTests.cs`
    - 使用随机玩家位置和已有簇集合验证新簇距玩家 >= 8、与已有簇距离 >= 5
    - **Validates: Requirements 6.3, 6.4**

  - [~] 4.7 编写人类数量上限属性测试
    - **Property 8: 人类数量上限约束**
    - 创建 `Assets/Game/Tests/EditMode/Gameplay/HumanCapPropertyTests.cs`
    - 验证当活跃人类 >= HumanMaxCount 时不生成新簇
    - **Validates: Requirements 6.5**

- [~] 5. 检查点 - 生成系统验证
  - 确保所有测试通过，ask the user if questions arise.

- [ ] 6. HUD 与 ResultPanel UI 实现
  - [~] 6.1 扩展 HUDPanel 添加感染进度与评级预览显示
    - 在现有 HUDPanel 中添加 UI 元素引用：m_infectionCountText、m_ratingPreviewText、m_victoryIndicator
    - 实现 `UpdateInfectionCount(int current, int target)` 方法，格式为 "当前数/目标数"
    - 实现 `UpdateRatingPreview(SessionRating rating)` 方法
    - 实现 `ShowVictoryIndicator()` / `HideVictoryIndicator()` 方法
    - 订阅 GameSessionController 的 OnInfectedCountChanged、OnRatingChanged、OnVictoryAchieved 事件
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [~] 6.2 编写时间格式化属性测试
    - **Property 9: 时间格式化正确性**
    - 创建 `Assets/Game/Tests/EditMode/UI/TimeFormattingPropertyTests.cs`
    - 使用随机 float [0, 9999] 验证 "MM:SS" 格式化结果正确
    - **Validates: Requirements 7.1**

  - [~] 6.3 实现 ResultPanel 结算面板
    - 在 `Assets/Game/Scripts/UI/` 下创建 `ResultPanel.cs`
    - 实现 UI 元素引用：感染人数文本、最大尸群文本、评级文本、通关状态文本、Restart 按钮
    - 实现 `ShowResult(SessionResult result, Action onRestart)` 方法
    - 根据 IsVictory 显示 "Victory" 或 "Failed"
    - Restart 按钮绑定回调，触发 GameSessionController.RestartSession()
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8_

- [ ] 7. GameSystemRunner 集成与完整流程串联
  - [~] 7.1 修改 GameSystemRunner 集成新组件
    - 在 GameSystemRunner 中添加 Inspector 引用：GameSessionController、HumanClusterSpawner、ResultPanel
    - 在 `InitializeSystems()` 中调用 m_sessionController.Initialize() 和 m_clusterSpawner.Initialize()
    - 在 `HandleStateChanged()` 的 Playing 状态中调用 m_sessionController.StartSession()
    - 在 `HandleStateChanged()` 的 Settlement 状态中调用 m_sessionController.EndSession()
    - 在 `UpdatePlaying()` 中替换 m_spawnSystem.UpdateSpawn() 为 m_clusterSpawner.UpdateSpawn()
    - _Requirements: 9.1, 9.4, 9.5_

  - [~] 7.2 连接 GameSessionController 与 UI 面板事件
    - 将 GameSessionController.OnSessionEnd 事件连接到 ResultPanel.ShowResult()
    - 将 ResultPanel 的 Restart 按钮回调连接到 GameSessionController.RestartSession()
    - 确保 HUDPanel 在 Playing 状态显示、Result 状态隐藏
    - 确保 ResultPanel 在 Result 状态显示、其他状态隐藏
    - _Requirements: 8.7, 8.8, 7.1, 7.2_

  - [~] 7.3 编写集成单元测试
    - 测试完整状态循环：Ready → Playing → Result → Ready
    - 测试 GameSessionController 与 GameEvents.OnInfectionSuccess 的事件订阅
    - 测试 Restart 按钮触发新一局的完整流程
    - 测试边界值：InfectedCount 恰好等于各阈值时的评级
    - _Requirements: 1.1, 1.2, 1.4, 8.8_

- [~] 8. 最终检查点 - 全部测试通过
  - 确保所有测试通过，ask the user if questions arise.

## Notes

- 标记 `*` 的任务为可选任务，可跳过以加速 MVP 开发
- 每个任务引用具体需求条目以确保可追溯性
- 属性测试验证设计文档中定义的正确性属性
- 单元测试验证具体示例和边界条件
- 检查点确保增量验证，避免问题累积
- 项目使用 NUnit + FsCheck.NUnit 作为属性测试框架
- 所有新组件通过 GameSystemRunner 统一初始化，遵循现有架构模式

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "4.1"] },
    { "id": 2, "tasks": ["2.2", "4.2", "4.3"] },
    { "id": 3, "tasks": ["2.3", "2.4", "2.5", "2.6", "4.4", "4.5", "4.6", "4.7"] },
    { "id": 4, "tasks": ["6.1", "6.3"] },
    { "id": 5, "tasks": ["6.2", "7.1"] },
    { "id": 6, "tasks": ["7.2"] },
    { "id": 7, "tasks": ["7.3"] }
  ]
}
```
