# Design Document: 体验打磨 (Gameplay Polish)

## Architecture Overview

本功能包含三个独立子模块，均遵循现有项目架构模式：MonoBehaviour 组件 + ScriptableObject 配置 + GameSystemRunner 统一调度。

```
┌─────────────────────────────────────────────────────────┐
│                   GameSystemRunner                        │
│  (调度 PlayerController.UpdateMovement + UpdateRotation) │
└────────────┬────────────────────────────────────────────┘
             │
    ┌────────┴────────┐
    │ PlayerController │──── reads ──── FloatingJoystick.Direction
    │  + 旋转逻辑      │                      │
    └────────┬────────┘               ┌───────┴───────┐
             │                        │ TouchZone      │
    ┌────────┴────────┐               │ Background     │
    │  PlayerVisual   │               │ Handle         │
    │  (绿色三角形)    │               └───────────────┘
    └─────────────────┘
    
    ┌─────────────────┐    ┌─────────────────┐
    │  HumanVisual    │    │  ZombieVisual   │
    │  (蓝色圆形)      │    │  (红色菱形)      │
    └─────────────────┘    └─────────────────┘
```

**设计原则：**
- 所有新增参数通过 GameConfig 配置，不硬编码数值
- FloatingJoystick 继承 VirtualJoystick，保证 PlayerController 零修改兼容
- Visual 组件为独立 MonoBehaviour，挂载在各单位 GameObject 上，Awake 时生成 Mesh
- 旋转逻辑内聚在 PlayerController 中，由 GameSystemRunner 统一调度

## Components

### 1. PlayerController 旋转扩展

在现有 PlayerController 中新增旋转逻辑，不拆分为独立组件以保持内聚性。

```csharp
namespace Game.Gameplay.Player
{
    public class PlayerController : MonoBehaviour
    {
        // ... 现有字段保持不变 ...

        /// <summary>
        /// 根据输入方向平滑旋转玩家朝向。
        /// 方向为零时保持当前朝向不变。
        /// </summary>
        /// <param name="direction">当前帧的输入方向向量</param>
        /// <param name="rotationSpeed">旋转速度（度/秒），来自 GameConfig</param>
        /// <param name="deltaTime">本帧时间增量</param>
        public void UpdateRotation(Vector2 direction, float rotationSpeed, float deltaTime)
        {
            // 方向为零时不旋转，保持当前朝向
            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            // 计算目标角度（2D 俯视角，Z 轴旋转）
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

            // 当前角度
            float currentAngle = transform.eulerAngles.z;

            // 使用 MoveTowardsAngle 实现平滑旋转，自动处理 360° 环绕
            float newAngle = Mathf.MoveTowardsAngle(
                currentAngle,
                targetAngle,
                rotationSpeed * deltaTime
            );

            transform.eulerAngles = new Vector3(0f, 0f, newAngle);
        }
    }
}
```

### 2. GameConfig 扩展

```csharp
namespace Game.Config
{
    public class GameConfig : ScriptableObject
    {
        // ... 现有字段保持不变 ...

        // ==================== 玩家旋转参数 ====================

        [Header("玩家旋转")]
        [Tooltip("玩家朝向旋转速度（度/秒），值越大转向越快")]
        [SerializeField] private float m_playerRotationSpeed = 720f;

        /// <summary>玩家朝向旋转速度（度/秒）。</summary>
        public float PlayerRotationSpeed => m_playerRotationSpeed;
    }
}
```

### 3. MeshGeneratorUtility（静态工具类）

集中管理所有几何体 Mesh 的生成逻辑，便于复用和测试。

```csharp
namespace Game.Utility
{
    /// <summary>
    /// Mesh 生成工具类，提供三角形、圆形、菱形等基础几何体的纯代码生成。
    /// 所有方法为纯函数，不依赖 MonoBehaviour 生命周期。
    /// </summary>
    public static class MeshGeneratorUtility
    {
        /// <summary>
        /// 生成等腰三角形 Mesh，尖端指向本地 +Y 方向。
        /// </summary>
        /// <param name="width">三角形底边宽度</param>
        /// <param name="height">三角形高度（从底边中点到尖端）</param>
        /// <returns>生成的 Mesh 实例</returns>
        public static Mesh CreateTriangleMesh(float width, float height)
        {
            Mesh mesh = new Mesh();

            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;

            // 顶点：尖端在 +Y，底边两端在 -Y
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(0f, halfHeight, 0f),        // 尖端（朝向方向）
                new Vector3(-halfWidth, -halfHeight, 0f), // 左下
                new Vector3(halfWidth, -halfHeight, 0f)   // 右下
            };

            int[] triangles = new int[] { 0, 2, 1 }; // 顺时针（面向摄像机）

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            return mesh;
        }

        /// <summary>
        /// 生成圆形（正多边形近似）Mesh。
        /// </summary>
        /// <param name="radius">圆形半径</param>
        /// <param name="segments">多边形边数，越大越接近圆形</param>
        /// <returns>生成的 Mesh 实例</returns>
        public static Mesh CreateCircleMesh(float radius, int segments)
        {
            Mesh mesh = new Mesh();

            // segments + 1 个顶点：中心 + 周边
            Vector3[] vertices = new Vector3[segments + 1];
            vertices[0] = Vector3.zero; // 中心点

            float angleStep = 360f / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f
                );
            }

            // 三角形：每个扇形由中心 + 相邻两个周边顶点组成
            int[] triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            return mesh;
        }

        /// <summary>
        /// 生成菱形（旋转 45° 的正方形）Mesh。
        /// </summary>
        /// <param name="size">菱形对角线长度的一半（中心到顶点距离）</param>
        /// <returns>生成的 Mesh 实例</returns>
        public static Mesh CreateDiamondMesh(float size)
        {
            Mesh mesh = new Mesh();

            // 4 个顶点 + 中心点，菱形顶点在上下左右
            Vector3[] vertices = new Vector3[]
            {
                Vector3.zero,                    // 中心
                new Vector3(0f, size, 0f),       // 上
                new Vector3(size, 0f, 0f),       // 右
                new Vector3(0f, -size, 0f),      // 下
                new Vector3(-size, 0f, 0f)       // 左
            };

            int[] triangles = new int[]
            {
                0, 1, 2, // 右上
                0, 2, 3, // 右下
                0, 3, 4, // 左下
                0, 4, 1  // 左上
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            return mesh;
        }
    }
}
```

### 4. PlayerVisual（绿色三角形）

```csharp
namespace Game.Gameplay.Player
{
    /// <summary>
    /// 玩家视觉组件，在 Awake 时生成绿色等腰三角形 Mesh。
    /// 挂载在 Player GameObject 上，三角形尖端指向本地 +Y（即朝向方向）。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PlayerVisual : MonoBehaviour
    {
        [Header("三角形参数")]
        [SerializeField] private float m_width = 0.8f;
        [SerializeField] private float m_height = 1.0f;

        private void Awake()
        {
            var meshFilter = GetComponent<MeshFilter>();
            meshFilter.mesh = MeshGeneratorUtility.CreateTriangleMesh(m_width, m_height);

            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.material = CreateUnlitMaterial(Color.green);
        }

        private Material CreateUnlitMaterial(Color color)
        {
            // 使用 Sprites/Default shader，适合 2D 渲染
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = color;
            return mat;
        }
    }
}
```

### 5. HumanVisual（蓝色圆形）

```csharp
namespace Game.Gameplay.Human
{
    /// <summary>
    /// 人类视觉组件，在 Awake 时生成蓝色圆形 Mesh。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class HumanVisual : MonoBehaviour
    {
        [Header("圆形参数")]
        [SerializeField] private float m_radius = 0.4f;
        [SerializeField] private int m_segments = 24;

        private void Awake()
        {
            var meshFilter = GetComponent<MeshFilter>();
            meshFilter.mesh = MeshGeneratorUtility.CreateCircleMesh(m_radius, m_segments);

            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.material = CreateUnlitMaterial(Color.blue);
        }

        private Material CreateUnlitMaterial(Color color)
        {
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = color;
            return mat;
        }
    }
}
```

### 6. ZombieVisual（红色菱形）

```csharp
namespace Game.Gameplay.Zombie
{
    /// <summary>
    /// 僵尸同伴视觉组件，在 Awake 时生成红色菱形 Mesh。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ZombieVisual : MonoBehaviour
    {
        [Header("菱形参数")]
        [SerializeField] private float m_size = 0.5f;

        private void Awake()
        {
            var meshFilter = GetComponent<MeshFilter>();
            meshFilter.mesh = MeshGeneratorUtility.CreateDiamondMesh(m_size);

            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.material = CreateUnlitMaterial(Color.red);
        }

        private Material CreateUnlitMaterial(Color color)
        {
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = color;
            return mat;
        }
    }
}
```

### 7. FloatingJoystick（浮动摇杆）

继承现有 VirtualJoystick，重写触摸行为以实现浮动效果。

```csharp
namespace Game.UI
{
    /// <summary>
    /// 浮动摇杆组件。
    /// 继承 VirtualJoystick 以保证 PlayerController 通过基类引用无缝兼容。
    /// 
    /// 结构：
    /// - TouchZone（本组件挂载节点）：覆盖屏幕左下 1/3 的透明触摸区域
    /// - JoystickBackground（子节点）：手指按下时移动到按下位置并显示
    /// - JoystickHandle（Background 的子节点）：跟随拖拽偏移
    /// 
    /// 与基类的区别：
    /// - 基类的背景固定在屏幕某位置，本类的背景跟随手指按下位置
    /// - 松手后背景和手柄隐藏，基类松手后仅手柄复位
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FloatingJoystick : VirtualJoystick
    {
        [Header("浮动摇杆")]
        [Tooltip("摇杆背景节点，按下时移动到手指位置并显示")]
        [SerializeField] private RectTransform m_background;

        [Tooltip("摇杆背景的 CanvasGroup，用于控制显隐")]
        [SerializeField] private CanvasGroup m_backgroundCanvasGroup;

        private RectTransform m_touchZoneRect;

        private void Awake()
        {
            m_touchZoneRect = GetComponent<RectTransform>();
            // 初始状态隐藏背景
            SetBackgroundVisible(false);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            // 将背景移动到手指按下位置
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    m_touchZoneRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                m_background.anchoredPosition = localPoint;
            }

            // 显示背景
            SetBackgroundVisible(true);

            // 调用基类处理方向计算
            base.OnPointerDown(eventData);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            // 调用基类归零方向和复位手柄
            base.OnPointerUp(eventData);

            // 隐藏背景
            SetBackgroundVisible(false);
        }

        /// <summary>
        /// 控制摇杆背景的显隐状态。
        /// </summary>
        private void SetBackgroundVisible(bool visible)
        {
            if (m_backgroundCanvasGroup != null)
            {
                m_backgroundCanvasGroup.alpha = visible ? 1f : 0f;
                m_backgroundCanvasGroup.blocksRaycasts = visible;
            }
        }
    }
}
```

## Interfaces

### VirtualJoystick 基类接口调整

为支持 FloatingJoystick 继承，需将 VirtualJoystick 的事件方法标记为 `virtual`：

```csharp
// VirtualJoystick.cs 中需要修改的方法签名
public virtual void OnPointerDown(PointerEventData eventData) { ... }
public virtual void OnDrag(PointerEventData eventData) { ... }
public virtual void OnPointerUp(PointerEventData eventData) { ... }
```

### PlayerController 输入接口

PlayerController 中 `m_joystick` 字段类型保持为 `VirtualJoystick`，通过多态自动兼容 FloatingJoystick：

```csharp
[SerializeField] private VirtualJoystick m_joystick;
// FloatingJoystick 继承自 VirtualJoystick，可直接赋值
```

### GameSystemRunner 调度扩展

在 UpdatePlaying 中新增旋转调度：

```csharp
private void UpdatePlaying(float deltaTime)
{
    // 1. 玩家移动
    if (m_playerController != null)
    {
        m_playerController.UpdateMovement(deltaTime);
        // 新增：玩家朝向旋转
        Vector2 dir = (m_joystick != null) ? m_joystick.Direction : Vector2.zero;
        m_playerController.UpdateRotation(dir, m_gameConfig.PlayerRotationSpeed, deltaTime);
    }
    // ... 其余调度不变 ...
}
```

## Data Models

### GameConfig 新增字段

| 字段名 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| m_playerRotationSpeed | float | 720f | 玩家朝向旋转速度（度/秒） |

### MeshGeneratorUtility 参数

| 方法 | 参数 | 默认值 | 说明 |
|------|------|--------|------|
| CreateTriangleMesh | width, height | 0.8f, 1.0f | 等腰三角形底宽和高 |
| CreateCircleMesh | radius, segments | 0.4f, 24 | 圆形半径和多边形边数 |
| CreateDiamondMesh | size | 0.5f | 菱形中心到顶点距离 |

### FloatingJoystick UI 层级结构

```
Canvas
└── TouchZone (FloatingJoystick 组件)
    │   RectTransform: 左下 1/3 屏幕区域
    │   Image: alpha=0 (透明但接收触摸)
    │
    └── JoystickBackground (CanvasGroup)
        │   RectTransform: 摇杆背景圆形
        │   初始状态: alpha=0, blocksRaycasts=false
        │
        └── JoystickHandle
                RectTransform: 摇杆手柄
```

## Error Handling

| 场景 | 处理策略 |
|------|----------|
| GameConfig 为 null | PlayerRotationSpeed 返回默认值 720，UpdateRotation 使用 0 作为 fallback |
| m_joystick 为 null | PlayerController 已有保护：方向向量为 Vector2.zero，不旋转 |
| MeshGeneratorUtility 参数非法（radius <= 0, segments < 3） | 返回空 Mesh 或最小有效 Mesh，Debug.LogWarning 提示 |
| FloatingJoystick 的 m_background 未赋值 | SetBackgroundVisible 中 null check 保护，摇杆仍可正常输出方向 |
| ScreenPointToLocalPointInRectangle 转换失败 | 与基类一致，直接 return 不更新方向 |
| Shader.Find 返回 null（Shader 未包含在构建中） | 使用 fallback：`Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color")` |

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: 旋转有界收敛

*For any* non-zero direction vector D and any initial rotation angle A, after calling UpdateRotation with rotationSpeed R and deltaTime T, the angular difference between the new rotation and the target angle shall decrease or remain zero, and the rotation change per frame shall not exceed R * T degrees.

**Validates: Requirements 1.1, 1.4**

### Property 2: 零输入旋转保持

*For any* initial rotation angle A, when UpdateRotation is called with a zero direction vector, the resulting rotation angle shall equal A (unchanged).

**Validates: Requirements 1.2**

### Property 3: 圆形 Mesh 顶点等距

*For any* segment count N >= 3 and radius R > 0, all perimeter vertices generated by CreateCircleMesh shall be equidistant from the center vertex (distance == R within floating-point tolerance).

**Validates: Requirements 3.1**

### Property 4: 浮动摇杆背景跟随触摸位置

*For any* touch position P within the TouchZone bounds, after OnPointerDown is called, the JoystickBackground's anchoredPosition shall equal the local-space equivalent of P, and the background shall be in visible state (alpha == 1).

**Validates: Requirements 5.2**

### Property 5: 方向向量计算语义

*For any* 2D offset vector V relative to the joystick center with max radius R: if |V| <= R, then Direction == V / R (proportional scaling); if |V| > R, then Direction == normalize(V). In all cases, |Direction| <= 1.

**Validates: Requirements 5.3, 6.3**

### Property 6: 摇杆释放状态重置

*For any* prior drag state (any direction vector and handle position), after OnPointerUp is called, Direction shall equal Vector2.zero, the JoystickBackground shall be hidden (alpha == 0), and the JoystickHandle's anchoredPosition shall equal Vector2.zero.

**Validates: Requirements 5.4**
