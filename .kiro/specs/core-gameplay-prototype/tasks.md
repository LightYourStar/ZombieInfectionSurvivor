# Implementation Plan: Core Gameplay Prototype

## Overview

基于 Unity 2022.3 LTS 实现 2D 俯视角僵尸感染割草小游戏核心玩法闭环。采用 GameSystemRunner 统一入口 + Inspector 注入架构，所有数值通过 ScriptableObject 配置，Human/ZombieCompanion 通过对象池管理。代码遵循 m_ 前缀、[SerializeField] private、中文注释规范。

## Tasks

- [x] 1. 基础框架与配置层
  - [x] 1.1 创建 GameConfig ScriptableObject 配置类
    - 创建 `Assets/Game/Scripts/Config/GameConfig.cs`
    - 定义所有数值参数字段：玩家属性、人类属性、僵尸同伴属性、感染参数、奖励参数、升级参数、单局时长、对象池参数
    - 使用 `[CreateAssetMenu]` 特性方便创建实例
    - 所有字段使用 `[SerializeField] private` + public 属性访问器
    - 添加中文注释说明每个参数含义
    - _Requirements: 14.1, 14.2, 14.4_

  - [x] 1.2 创建 MetaUpgradeConfig 局外升级配置
    - 创建 `Assets/Game/Scripts/Config/MetaUpgradeConfig.cs`
    - 定义每级升级花费和每级加成数值配置
    - _Requirements: 11.4_

  - [x] 1.3 创建 GameEvents 全局事件定义
    - 创建 `Assets/Game/Scripts/Core/GameEvents.cs`
    - 定义 C# event/Action 事件：OnInfectionSuccess、OnLevelUp、OnStateChanged、OnTimerEnd 等
    - _Requirements: 12.1_

  - [x] 1.4 创建 GameStateManager 游戏状态机
    - 创建 `Assets/Game/Scripts/Core/GameStateManager.cs`
    - 实现 Start/Playing/Settlement 三状态枚举和切换逻辑
    - 暴露 CurrentState 属性和 OnStateChanged 事件
    - _Requirements: 12.1, 12.2_

  - [x] 1.5 创建 GameSystemRunner 统一入口
    - 创建 `Assets/Game/Scripts/Core/GameSystemRunner.cs`
    - 使用 `[SerializeField] private` 引用所有子系统
    - 实现 InitializeSystems() 和 ResetSystems() 方法
    - 在 Update 中根据当前状态调度各子系统更新
    - _Requirements: 12.3, 12.4, 14.3_

- [x] 2. 对象池系统
  - [x] 2.1 实现泛型对象池 ObjectPool<T>
    - 创建 `Assets/Game/Scripts/Utility/ObjectPool.cs`
    - 实现 Get()、Return()、Prewarm() 方法
    - 池空时动态扩展并输出 Debug.LogWarning
    - 维护 ActiveCount 和 FreeCount 属性
    - _Requirements: 13.1, 13.2, 13.3, 13.4_

  - [x] 2.2 实现 ObjectPoolManager 统一管理
    - 创建 `Assets/Game/Scripts/Utility/ObjectPoolManager.cs`
    - 管理 Human 池和 ZombieCompanion 池
    - 提供 GetHuman()、ReturnHuman()、GetZombie()、ReturnZombie() 便捷方法
    - 初始化时根据 GameConfig 预热池
    - _Requirements: 3.4, 13.1, 13.2_

  - [ ]* 2.3 编写对象池属性测试
    - **Property 5: 对象池取出/回收 round-trip**
    - 验证任意 Get/Return 序列后 ActiveCount + FreeCount == 总实例数
    - 验证 Return 后再 Get 返回有效非 null 实例
    - **Validates: Requirements 3.4, 13.1**

- [x] 3. Checkpoint - 确保基础框架编译通过
  - 确保所有脚本编译无错误，ask the user if questions arise.

- [x] 4. 输入与玩家移动
  - [x] 4.1 实现 VirtualJoystick 虚拟摇杆
    - 创建 `Assets/Game/Scripts/UI/VirtualJoystick.cs`
    - 实现 IPointerDownHandler、IDragHandler、IPointerUpHandler 接口
    - 计算归一化方向向量（模长 <= 1）
    - 释放时方向归零、Handle 视觉复位
    - 不依赖第三方插件，纯 UGUI 实现
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [ ]* 4.2 编写摇杆输出属性测试
    - **Property 1: 摇杆输出向量归一化**
    - 验证任意输入位置下输出方向向量模长 >= 0 且 <= 1
    - **Validates: Requirements 1.1**

  - [x] 4.3 实现 PlayerStats 玩家运行时属性
    - 创建 `Assets/Game/Scripts/Gameplay/Player/PlayerStats.cs`
    - 实现基础值 + 加成值的属性计算
    - 实现 Initialize()、ApplyUpgrade()、Reset() 方法
    - _Requirements: 2.2, 2.3, 8.5_

  - [ ]* 4.4 编写属性修改属性测试
    - **Property 3: 属性修改立即生效**
    - 验证 ApplyUpgrade 后立即读取返回修改后的值
    - 验证 Initialize 后属性值 == 基础值 + 加成值
    - **Validates: Requirements 2.3, 8.5, 11.3**

  - [x] 4.5 实现 PlayerController 玩家移动
    - 创建 `Assets/Game/Scripts/Gameplay/Player/PlayerController.cs`
    - 从 VirtualJoystick 读取方向，乘以速度和 deltaTime 计算位移
    - 钳制位置到地图范围内
    - _Requirements: 2.1, 2.2_

  - [ ]* 4.6 编写玩家移动属性测试
    - **Property 2: 玩家移动位移正确性**
    - 验证位移 == direction * speed * deltaTime
    - **Validates: Requirements 2.1**

- [x] 5. 人类单位与 AI
  - [x] 5.1 实现 HumanUnit 人类单位组件
    - 创建 `Assets/Game/Scripts/Gameplay/Enemy/HumanUnit.cs`
    - 定义 Human 的基础属性和状态（活跃/被感染）
    - 实现激活/回收时的状态重置逻辑
    - _Requirements: 3.1, 4.4_

  - [x] 5.2 实现 HumanAI 人类 AI 行为
    - 创建 `Assets/Game/Scripts/Gameplay/Enemy/HumanAI.cs`
    - 实现感知威胁时逃跑（远离 Player 和 ZombieCompanion）
    - 实现无威胁时随机漫游
    - 从 GameConfig 读取感知半径和移动速度
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [ ]* 5.3 编写人类逃跑方向属性测试
    - **Property 6: Human 逃跑方向远离威胁**
    - 验证 CalculateFleeDirection 返回方向与 (H-T) 点积 > 0
    - **Validates: Requirements 4.1, 4.2**

  - [x] 5.4 实现 SpawnSystem 人类刷新系统
    - 创建 `Assets/Game/Scripts/Gameplay/Wave/SpawnSystem.cs`
    - 实现初始批次生成和按间隔补充逻辑
    - 从 ObjectPool 取出 Human 放置在地图边缘/玩家视野外
    - 维护 ActiveHumanCount 不超过配置上限
    - _Requirements: 3.1, 3.2, 3.3_

  - [ ]* 5.5 编写刷新上限属性测试
    - **Property 4: Human 场上数量不超过上限**
    - 验证任意刷新时刻 ActiveHumanCount <= HumanMaxCount
    - **Validates: Requirements 3.2**

- [x] 6. 感染系统
  - [x] 6.1 实现 MathUtils 数学工具
    - 创建 `Assets/Game/Scripts/Utility/MathUtils.cs`
    - 实现基于 sqrMagnitude 的距离判定方法
    - _Requirements: 5.1, 5.2_

  - [x] 6.2 实现 InfectionSystem 感染判定与转化
    - 创建 `Assets/Game/Scripts/Gameplay/Infection/InfectionSystem.cs`
    - 实现 IsInInfectionRange() 距离判定
    - 实现 TryInfect() 转化逻辑：回收 Human → 生成 ZombieCompanion
    - 实现 UpdateInfectionCheck() 每帧检测
    - 受 ZombieCompanion 上限约束
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

  - [ ]* 6.3 编写感染距离判定属性测试
    - **Property 7: 感染距离判定正确性**
    - 验证 IsInInfectionRange 返回 true 当且仅当 distance < R
    - **Validates: Requirements 5.1, 5.2**

  - [ ]* 6.4 编写感染转化守恒属性测试
    - **Property 8: 感染转化守恒**
    - 验证 TryInfect 后 Human 数量 -1，ZombieCompanion 数量 +1
    - 验证新 ZombieCompanion 位置 == 被感染 Human 原始位置
    - **Validates: Requirements 5.3**

  - [ ]* 6.5 编写 ZombieCompanion 上限属性测试
    - **Property 9: ZombieCompanion 数量不超过上限**
    - 验证达到上限时 TryInfect 返回 false 且不改变数量
    - **Validates: Requirements 5.4**

- [x] 7. 僵尸同伴系统
  - [x] 7.1 实现 ZombieCompanionUnit 僵尸同伴组件
    - 创建 `Assets/Game/Scripts/Gameplay/Zombie/ZombieCompanionUnit.cs`
    - 定义僵尸同伴基础属性和状态
    - 实现激活/回收时的状态重置
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 7.2 实现 ZombieCompanionAI 僵尸同伴 AI
    - 创建 `Assets/Game/Scripts/Gameplay/Zombie/ZombieCompanionAI.cs`
    - 实现无目标时跟随 Player
    - 实现有目标时追击最近 Human
    - 从 GameConfig 读取移动速度和感知范围
    - _Requirements: 6.1, 6.2, 6.3_

  - [ ]* 7.3 编写追击最近目标属性测试
    - **Property 10: ZombieCompanion 追击最近目标**
    - 验证 FindNearestTarget 返回距离最小的 Human
    - **Validates: Requirements 6.2**

- [x] 8. Checkpoint - 确保核心玩法逻辑编译通过
  - 确保所有脚本编译无错误，ask the user if questions arise.

- [x] 9. 经验与升级系统
  - [x] 9.1 实现 ExperienceSystem 经验金币管理
    - 创建 `Assets/Game/Scripts/Gameplay/Skill/ExperienceSystem.cs`
    - 实现 AddExp()、AddGold()、HasReachedThreshold() 方法
    - 暴露 OnLevelUp 事件
    - 支持经验倍率
    - _Requirements: 7.1, 7.2, 7.3, 8.1_

  - [ ]* 9.2 编写感染奖励累加属性测试
    - **Property 11: 感染奖励累加正确性**
    - 验证 N 次感染后 CurrentExp == N * ExpPerInfection * 倍率
    - 验证 CurrentGold == N * GoldPerInfection
    - **Validates: Requirements 7.1, 7.2**

  - [ ]* 9.3 编写经验阈值触发属性测试
    - **Property 12: 经验阈值触发升级**
    - 验证 E >= T 时 HasReachedThreshold() == true
    - 验证 E < T 时 HasReachedThreshold() == false
    - **Validates: Requirements 8.1**

  - [x] 9.4 实现 UpgradeOption 升级选项数据结构
    - 创建 `Assets/Game/Scripts/Gameplay/Skill/UpgradeOption.cs`
    - 定义 UpgradeType 枚举和 UpgradeOption 类
    - _Requirements: 8.4_

  - [x] 9.5 实现 UpgradeSystem 局内升级逻辑
    - 创建 `Assets/Game/Scripts/Gameplay/Skill/UpgradeSystem.cs`
    - 实现 DrawOptions(3) 随机抽取不重复选项
    - 实现 ApplyUpgrade() 应用升级效果到 PlayerStats
    - _Requirements: 8.2, 8.3, 8.4, 8.5_

  - [ ]* 9.6 编写升级抽取不重复属性测试
    - **Property 13: 升级抽取不重复**
    - 验证 DrawOptions(3) 返回 3 个不同 UpgradeType 的选项
    - **Validates: Requirements 8.2**

  - [x] 9.7 实现 MetaUpgradeSystem 局外升级数据
    - 创建 `Assets/Game/Scripts/Gameplay/Skill/MetaUpgradeSystem.cs`
    - 实现 Save()、Load()、AddGold()、GetBonus() 方法
    - 使用 PlayerPrefs 持久化
    - 数据损坏时返回默认值
    - _Requirements: 11.1, 11.2, 11.3, 11.4_

  - [ ]* 9.8 编写 MetaUpgrade 持久化属性测试
    - **Property 14: MetaUpgrade 持久化 round-trip**
    - 验证 Save 后 Load 返回等价数据
    - **Validates: Requirements 11.1**

  - [ ]* 9.9 编写结算金币累加属性测试
    - **Property 16: 结算金币持久化累加**
    - 验证 AddGold(G) 后 Load().TotalGold == 原值 + G
    - **Validates: Requirements 10.2**

- [x] 10. 计时与结算
  - [x] 10.1 实现 TimerSystem 单局倒计时
    - 创建 `Assets/Game/Scripts/Core/TimerSystem.cs`（逻辑属于 Core 层）
    - 从 GameConfig 读取单局时长并启动倒计时
    - 倒计时归零时触发结算事件
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

- [x] 11. UI 系统
  - [x] 11.1 实现 UIManager UI 面板管理
    - 创建 `Assets/Game/Scripts/UI/UIManager.cs`
    - 管理所有面板的显示/隐藏
    - 通过 Inspector 注入各面板引用
    - _Requirements: 12.1_

  - [x] 11.2 实现 StartPanel 开始界面
    - 创建 `Assets/Game/Scripts/UI/StartPanel.cs`
    - 显示开始按钮，点击后通知 GameStateManager 切换到 Playing 状态
    - 使用占位 UI 元素
    - _Requirements: 12.2, 12.3_

  - [x] 11.3 实现 HUDPanel 游戏内 HUD
    - 创建 `Assets/Game/Scripts/UI/HUDPanel.cs`
    - 实时显示剩余时间、当前经验/等级、金币数量
    - _Requirements: 9.2_

  - [x] 11.4 实现 UpgradePanel 升级选择面板
    - 创建 `Assets/Game/Scripts/UI/UpgradePanel.cs`
    - 显示三个升级选项按钮
    - 选择后通知 UpgradeSystem 应用并关闭面板
    - 暂停/恢复游戏逻辑
    - _Requirements: 8.1, 8.2, 8.3_

  - [x] 11.5 实现 SettlementPanel 结算面板
    - 创建 `Assets/Game/Scripts/UI/SettlementPanel.cs`
    - 显示本局感染总数、获得金币、获得经验
    - 点击继续按钮重置游戏开始新局
    - _Requirements: 10.1, 10.2, 10.3_

- [x] 12. Checkpoint - 确保所有系统编译通过
  - 确保所有脚本编译无错误，ask the user if questions arise.

- [x] 13. 系统集成与串联
  - [x] 13.1 集成 GameSystemRunner 串联所有子系统
    - 在 GameSystemRunner 中按正确顺序初始化所有子系统
    - 实现 Playing 状态下的 Update 调度：Input → Player → SpawnSystem → HumanAI → ZombieAI → InfectionSystem → Timer
    - 连接事件：感染成功 → 经验/金币奖励 → 升级检查
    - 连接事件：倒计时结束 → 结算 → MetaUpgrade 保存
    - _Requirements: 12.3, 12.4_

  - [x] 13.2 实现游戏流程完整闭环
    - Start 状态：显示 StartPanel，等待点击
    - Playing 状态：初始化系统、开始倒计时、刷新 Human
    - Settlement 状态：显示结算、保存金币、等待继续
    - 继续后重置所有系统进入新一局
    - _Requirements: 12.1, 12.2, 12.3, 10.3_

  - [ ]* 13.3 编写游戏状态机属性测试
    - **Property 15: 游戏状态机单一状态**
    - 验证任意时刻 CurrentState 恰好为三个状态之一
    - 验证 ChangeState 后 CurrentState 立即等于目标状态
    - **Validates: Requirements 12.1**

- [x] 14. 占位视觉与场景搭建
  - [x] 14.1 创建占位 Prefab 和场景
    - 创建 Player 占位 Prefab（绿色方块 + SpriteRenderer）
    - 创建 Human 占位 Prefab（蓝色方块 + SpriteRenderer）
    - 创建 ZombieCompanion 占位 Prefab（红色方块 + SpriteRenderer）
    - 搭建游戏场景：Camera、Canvas、GameSystemRunner GameObject
    - _Requirements: 15.4_

  - [x] 14.2 创建 GameConfig ScriptableObject 实例并赋值
    - 在 Assets/Game/Config/ 目录创建 GameConfig 实例
    - 填入合理的默认数值参数
    - 在 Inspector 中将 GameConfig 注入到 GameSystemRunner
    - _Requirements: 14.3_

- [x] 15. Final Checkpoint - 确保完整闭环可运行
  - 确保所有脚本编译无错误，游戏流程可完整运行，ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- 所有代码遵循 m_ 前缀、[SerializeField] private、中文注释规范
- 每个任务引用具体需求条目以确保可追溯性
- Property tests 验证设计文档中定义的正确性属性
- Checkpoints 确保增量验证，避免大量代码积累后才发现问题
- 占位视觉使用纯色方块，后续可替换为正式美术资源

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["1.4", "1.5", "2.1"] },
    { "id": 2, "tasks": ["2.2", "2.3", "4.1"] },
    { "id": 3, "tasks": ["4.2", "4.3", "5.1", "6.1"] },
    { "id": 4, "tasks": ["4.4", "4.5", "5.2", "5.4", "7.1"] },
    { "id": 5, "tasks": ["4.6", "5.3", "5.5", "6.2", "7.2"] },
    { "id": 6, "tasks": ["6.3", "6.4", "6.5", "7.3", "9.1", "9.4"] },
    { "id": 7, "tasks": ["9.2", "9.3", "9.5", "9.7", "10.1"] },
    { "id": 8, "tasks": ["9.6", "9.8", "9.9", "11.1"] },
    { "id": 9, "tasks": ["11.2", "11.3", "11.4", "11.5"] },
    { "id": 10, "tasks": ["13.1", "13.2"] },
    { "id": 11, "tasks": ["13.3", "14.1", "14.2"] }
  ]
}
```
