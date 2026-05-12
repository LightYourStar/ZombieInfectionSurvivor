# Implementation Plan: 体验打磨 (Gameplay Polish)

## Overview

本计划将设计文档中的三个子模块（玩家朝向旋转、视觉区分、浮动摇杆）转化为可增量执行的编码任务。每个任务构建在前一步之上，最终通过 GameSystemRunner 统一调度将所有模块串联。实现语言为 C#（Unity）。

## Tasks

- [x] 1. 基础设施：MeshGeneratorUtility 与 GameConfig 扩展
  - [x] 1.1 创建 MeshGeneratorUtility 静态工具类
    - 创建文件 `Assets/Game/Scripts/Utility/MeshGeneratorUtility.cs`
    - 实现 `CreateTriangleMesh(float width, float height)` 方法，生成尖端指向 +Y 的等腰三角形 Mesh
    - 实现 `CreateCircleMesh(float radius, int segments)` 方法，生成正多边形近似圆形 Mesh
    - 实现 `CreateDiamondMesh(float size)` 方法，生成菱形 Mesh
    - 添加参数校验：radius <= 0 或 segments < 3 时返回空 Mesh 并 Debug.LogWarning
    - _Requirements: 2.3, 3.3, 4.3_

  - [ ]* 1.2 为 MeshGeneratorUtility 编写属性测试
    - **Property 3: 圆形 Mesh 顶点等距**
    - 对任意 segments >= 3 和 radius > 0，验证所有周边顶点到中心距离等于 radius（浮点容差内）
    - **Validates: Requirements 3.1**

  - [x] 1.3 在 GameConfig 中添加 PlayerRotationSpeed 字段
    - 修改文件 `Assets/Game/Scripts/Config/GameConfig.cs`
    - 在玩家属性区域新增 `[Header("玩家旋转")]` 分组
    - 添加 `m_playerRotationSpeed` 字段（float, 默认 720f）及公共属性 `PlayerRotationSpeed`
    - _Requirements: 1.3_

- [x] 2. 玩家朝向旋转
  - [x] 2.1 在 PlayerController 中实现 UpdateRotation 方法
    - 修改文件 `Assets/Game/Scripts/Gameplay/Player/PlayerController.cs`
    - 新增 `public void UpdateRotation(Vector2 direction, float rotationSpeed, float deltaTime)` 方法
    - 方向为零时直接 return，保持当前朝向不变
    - 使用 `Mathf.Atan2` 计算目标角度，`Mathf.MoveTowardsAngle` 实现平滑旋转
    - 旋转应用到 `transform.eulerAngles.z`（2D 俯视角）
    - _Requirements: 1.1, 1.2, 1.4_

  - [ ]* 2.2 为 UpdateRotation 编写属性测试
    - **Property 1: 旋转有界收敛**
    - 对任意非零方向和初始角度，验证每帧旋转量不超过 rotationSpeed * deltaTime，且角度差单调递减
    - **Validates: Requirements 1.1, 1.4**

  - [ ]* 2.3 为零输入保持编写属性测试
    - **Property 2: 零输入旋转保持**
    - 对任意初始角度，当方向为零时验证旋转角度不变
    - **Validates: Requirements 1.2**

  - [x] 2.4 在 GameSystemRunner 中添加旋转调度
    - 修改文件 `Assets/Game/Scripts/Core/GameSystemRunner.cs`
    - 在 `UpdatePlaying` 方法中，`m_playerController.UpdateMovement(deltaTime)` 之后添加旋转调度
    - 读取摇杆方向和键盘方向（取模长较大者），调用 `m_playerController.UpdateRotation(dir, m_gameConfig.PlayerRotationSpeed, deltaTime)`
    - _Requirements: 1.1_

- [x] 3. Checkpoint - 旋转功能验证
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. 视觉组件：PlayerVisual、HumanVisual、ZombieVisual
  - [x] 4.1 创建 PlayerVisual 组件
    - 创建文件 `Assets/Game/Scripts/Gameplay/Player/PlayerVisual.cs`
    - 使用 `[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]`
    - 在 Awake 中调用 `MeshGeneratorUtility.CreateTriangleMesh` 生成绿色三角形
    - 使用 `Sprites/Default` shader 创建 Unlit 材质，颜色为 Color.green
    - 可序列化参数：width (0.8f)、height (1.0f)
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 4.2 创建 HumanVisual 组件
    - 创建文件 `Assets/Game/Scripts/Gameplay/Human/HumanVisual.cs`
    - 使用 `[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]`
    - 在 Awake 中调用 `MeshGeneratorUtility.CreateCircleMesh` 生成蓝色圆形
    - 使用 `Sprites/Default` shader 创建 Unlit 材质，颜色为 Color.blue
    - 可序列化参数：radius (0.4f)、segments (24)
    - _Requirements: 3.1, 3.2, 3.3_

  - [x] 4.3 创建 ZombieVisual 组件
    - 创建文件 `Assets/Game/Scripts/Gameplay/Zombie/ZombieVisual.cs`
    - 使用 `[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]`
    - 在 Awake 中调用 `MeshGeneratorUtility.CreateDiamondMesh` 生成红色菱形
    - 使用 `Sprites/Default` shader 创建 Unlit 材质，颜色为 Color.red
    - 可序列化参数：size (0.5f)
    - _Requirements: 4.1, 4.2, 4.3_

- [x] 5. 浮动摇杆实现
  - [x] 5.1 将 VirtualJoystick 的事件方法标记为 virtual
    - 修改文件 `Assets/Game/Scripts/UI/VirtualJoystick.cs`
    - 将 `OnPointerDown`、`OnDrag`、`OnPointerUp` 方法签名添加 `virtual` 关键字
    - 确保现有功能不受影响
    - _Requirements: 6.1_

  - [x] 5.2 创建 FloatingJoystick 组件
    - 创建文件 `Assets/Game/Scripts/UI/FloatingJoystick.cs`
    - 继承 `VirtualJoystick`，添加 `[RequireComponent(typeof(RectTransform))]`
    - 序列化字段：`m_background` (RectTransform)、`m_backgroundCanvasGroup` (CanvasGroup)
    - Awake 中获取 TouchZone 的 RectTransform，初始隐藏背景
    - Override `OnPointerDown`：将背景移动到手指按下位置（ScreenPointToLocalPointInRectangle 转换），显示背景，调用 base
    - Override `OnPointerUp`：调用 base 归零方向和复位手柄，隐藏背景
    - 实现 `SetBackgroundVisible(bool)` 通过 CanvasGroup 控制 alpha 和 blocksRaycasts
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 6.1, 6.2, 6.3_

  - [ ]* 5.3 为浮动摇杆编写属性测试
    - **Property 5: 方向向量计算语义**
    - 验证偏移在半径内时 Direction == V/R（比例缩放），超出半径时 Direction == normalize(V)，且 |Direction| <= 1
    - **Validates: Requirements 5.3, 6.3**

  - [ ]* 5.4 为摇杆释放状态编写属性测试
    - **Property 6: 摇杆释放状态重置**
    - 验证 OnPointerUp 后 Direction == Vector2.zero，背景隐藏（alpha == 0），Handle 位置归零
    - **Validates: Requirements 5.4**

- [x] 6. Checkpoint - 代码编译验证
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Prefab 与场景集成
  - [x] 7.1 更新 Player Prefab
    - 打开 Player Prefab 进入编辑模式
    - 移除 SpriteRenderer 组件
    - 添加 MeshFilter、MeshRenderer、PlayerVisual 组件
    - 保存 Prefab
    - _Requirements: 2.1, 2.2_

  - [x] 7.2 更新 Human Prefab
    - 打开 Human Prefab 进入编辑模式
    - 移除 SpriteRenderer 组件
    - 添加 MeshFilter、MeshRenderer、HumanVisual 组件
    - 保存 Prefab
    - _Requirements: 3.1, 3.2_

  - [x] 7.3 更新 ZombieCompanion Prefab
    - 打开 ZombieCompanion Prefab 进入编辑模式
    - 移除 SpriteRenderer 组件
    - 添加 MeshFilter、MeshRenderer、ZombieVisual 组件
    - 保存 Prefab
    - _Requirements: 4.1, 4.2_

  - [x] 7.4 在场景中替换 VirtualJoystick 为 FloatingJoystick 设置
    - 在 Canvas 下创建 TouchZone 节点，设置 RectTransform 覆盖屏幕左下 1/3 区域
    - 添加透明 Image 组件（alpha=0）用于接收触摸事件
    - 挂载 FloatingJoystick 组件
    - 创建 JoystickBackground 子节点（带 CanvasGroup，初始 alpha=0）
    - 创建 JoystickHandle 子节点（Background 的子节点）
    - 将 FloatingJoystick 的引用连接到 PlayerController 的 m_joystick 字段
    - 移除或禁用旧的 VirtualJoystick 节点
    - _Requirements: 5.1, 5.2, 5.4, 6.2_

- [x] 8. Final Checkpoint - 全功能集成验证
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- Prefab/场景操作（Task 7）需要在 Unity Editor 中通过 MCP 工具执行

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.3", "5.1"] },
    { "id": 1, "tasks": ["1.2", "2.1", "4.1", "4.2", "4.3"] },
    { "id": 2, "tasks": ["2.2", "2.3", "2.4", "5.2"] },
    { "id": 3, "tasks": ["5.3", "5.4"] },
    { "id": 4, "tasks": ["7.1", "7.2", "7.3"] },
    { "id": 5, "tasks": ["7.4"] }
  ]
}
```
