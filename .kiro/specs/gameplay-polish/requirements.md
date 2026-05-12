# Requirements Document

## Introduction

本文档定义"体验打磨"功能的需求，涵盖三个独立子模块：
1. **玩家朝向旋转**——使玩家角色在移动时平滑旋转至移动方向，停止时保持最后朝向。
2. **视觉区分**——通过几何形状与颜色区分玩家、人类、僵尸同伴三类单位，无需美术资源。
3. **浮动摇杆**——将现有固定摇杆改为左下 1/3 屏幕区域内的浮动摇杆，手指按下时摇杆背景跟随出现，松手后隐藏/复位。

本次迭代不涉及数值平衡调整，所有新增参数通过 GameConfig ScriptableObject 配置。

## Glossary

- **PlayerController**: 玩家移动控制器组件，负责读取输入方向并推动玩家位置
- **VirtualJoystick**: 虚拟摇杆 UI 组件，采集触屏/鼠标拖拽输入并输出归一化方向向量
- **FloatingJoystick**: 浮动摇杆系统，包含触摸检测区域、摇杆背景和摇杆手柄三层结构
- **GameConfig**: 游戏全局配置 ScriptableObject，所有运行时可调数值参数的唯一来源
- **RotationSpeed**: 玩家朝向旋转速度参数（度/秒），存储在 GameConfig 中
- **PlayerVisual**: 玩家视觉表现组件，负责渲染绿色三角形几何体并指示朝向
- **HumanVisual**: 人类视觉表现组件，负责渲染蓝色圆形几何体
- **ZombieVisual**: 僵尸同伴视觉表现组件，负责渲染红色菱形几何体
- **TouchZone**: 浮动摇杆的触摸检测区域，覆盖屏幕左下 1/3 范围
- **JoystickBackground**: 摇杆背景节点，在手指按下时移动到按下位置并显示
- **JoystickHandle**: 摇杆手柄节点，跟随拖拽偏移并限制在最大半径内

## Requirements

### Requirement 1: 玩家朝向平滑旋转

**User Story:** As a 玩家, I want 角色在移动时平滑旋转至移动方向, so that 角色运动看起来自然流畅而非瞬间跳变。

#### Acceptance Criteria

1.1 WHILE PlayerController 接收到非零方向输入, THE PlayerController SHALL 使用平滑插值（Quaternion.RotateTowards 或等效方法）将玩家 Transform 的 Z 轴旋转朝向移动方向，旋转速度由 GameConfig 中的 RotationSpeed 参数决定。

1.2 WHEN 方向输入从非零变为零, THE PlayerController SHALL 保持玩家 Transform 的当前旋转角度不变。

1.3 THE GameConfig SHALL 提供一个名为 PlayerRotationSpeed 的 float 类型配置字段，单位为度/秒，默认值为 720。

1.4 WHEN RotationSpeed 配置为极大值（如 99999）, THE PlayerController SHALL 在单帧内完成旋转，表现为即时转向。

### Requirement 2: 玩家视觉表现——绿色三角形

**User Story:** As a 玩家, I want 自己的角色显示为绿色三角形, so that 能一眼识别自己的位置和朝向。

#### Acceptance Criteria

2.1 THE PlayerVisual SHALL 使用纯代码生成的等腰三角形 Mesh 作为玩家的视觉表现，三角形尖端指向本地坐标系的正 Y 方向（即朝向方向）。

2.2 THE PlayerVisual SHALL 使用绿色（Color.green 或近似值）作为三角形的渲染颜色。

2.3 THE PlayerVisual SHALL 在运行时通过代码生成 Mesh，不依赖任何外部美术资源文件。

### Requirement 3: 人类视觉表现——蓝色圆形

**User Story:** As a 玩家, I want 人类单位显示为蓝色圆形, so that 能快速区分可感染的目标。

#### Acceptance Criteria

3.1 THE HumanVisual SHALL 使用纯代码生成的圆形（多边形近似）Mesh 作为人类单位的视觉表现。

3.2 THE HumanVisual SHALL 使用蓝色（Color.blue 或近似值）作为圆形的渲染颜色。

3.3 THE HumanVisual SHALL 在运行时通过代码生成 Mesh，不依赖任何外部美术资源文件。

### Requirement 4: 僵尸同伴视觉表现——红色菱形

**User Story:** As a 玩家, I want 僵尸同伴显示为红色菱形, so that 能快速区分己方单位。

#### Acceptance Criteria

4.1 THE ZombieVisual SHALL 使用纯代码生成的菱形（旋转 45° 的正方形）Mesh 作为僵尸同伴的视觉表现。

4.2 THE ZombieVisual SHALL 使用红色（Color.red 或近似值）作为菱形的渲染颜色。

4.3 THE ZombieVisual SHALL 在运行时通过代码生成 Mesh，不依赖任何外部美术资源文件。

### Requirement 5: 浮动摇杆——触摸区域与出现逻辑

**User Story:** As a 玩家, I want 在屏幕左下区域任意位置按下即可开始操控摇杆, so that 不需要精确触碰固定位置就能操作。

#### Acceptance Criteria

5.1 THE FloatingJoystick SHALL 提供一个覆盖屏幕左下 1/3 区域的透明 TouchZone 作为触摸检测区域。

5.2 WHEN 手指在 TouchZone 内按下, THE FloatingJoystick SHALL 将 JoystickBackground 移动到手指按下的屏幕位置并设置为可见状态。

5.3 WHILE 手指保持按下并拖拽, THE FloatingJoystick SHALL 根据手指相对 JoystickBackground 中心的偏移计算归一化方向向量（模长小于等于 1），并将 JoystickHandle 的位置限制在最大半径内。

5.4 WHEN 手指从屏幕抬起, THE FloatingJoystick SHALL 将方向向量归零、隐藏 JoystickBackground 和 JoystickHandle、并将 JoystickHandle 位置复位到 JoystickBackground 中心。

5.5 THE FloatingJoystick SHALL 输出与现有 VirtualJoystick 相同签名的 Direction 属性（Vector2，模长小于等于 1），保证 PlayerController 无需修改即可兼容。

### Requirement 6: 浮动摇杆——与现有系统兼容

**User Story:** As a 开发者, I want 浮动摇杆与现有 PlayerController 输入接口完全兼容, so that 切换摇杆实现时不需要修改移动逻辑代码。

#### Acceptance Criteria

6.1 THE FloatingJoystick SHALL 继承或实现与 VirtualJoystick 相同的公共接口（至少包含 Direction 属性），使 PlayerController 可通过相同引用类型读取方向输入。

6.2 WHEN FloatingJoystick 替换 VirtualJoystick 后, THE PlayerController SHALL 无需代码修改即可正常读取移动方向。

6.3 THE FloatingJoystick SHALL 保持与现有 VirtualJoystick 相同的方向计算语义：偏移在半径内时按比例缩放保留推动强度，超出半径时钳制到单位圆上。
