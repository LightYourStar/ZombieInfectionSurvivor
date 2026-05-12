using Game.UI;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 玩家移动控制器。
    /// 支持两种输入方式：虚拟摇杆（移动端）和 WASD 键盘（PC 端），两者取模长较大者作为最终方向。
    /// 每帧读取归一化方向向量，结合 <see cref="PlayerStats.MoveSpeed"/> 与
    /// deltaTime 计算本帧位移，并将最终位置钳制到配置的地图范围内。
    ///
    /// 位移公式：<c>displacement = direction * stats.MoveSpeed * deltaTime</c>
    /// 方向向量模长保证 &lt;= 1，故符合设计文档 Property 2
    /// 「位移 == normalize(D) * S * T（D 模长 &gt; 1 时归一化，否则保持原值）」。
    ///
    /// 注入约定：
    /// 1. <see cref="m_joystick"/> 为 MonoBehaviour，通过 Inspector 注入。
    /// 2. <see cref="PlayerStats"/> 为纯 C# 对象（非 MonoBehaviour 且未标记 [Serializable]），
    ///    无法在 Inspector 中赋值，需由 <see cref="GameSystemRunner"/> 在 InitializeSystems 阶段
    ///    通过 <see cref="Initialize"/> 方法注入；未注入时 <see cref="UpdateMovement"/> 会安全跳过。
    /// 3. 本组件不订阅 Unity 的 Update，主循环由 <see cref="GameSystemRunner.UpdatePlaying"/> 统一调度，
    ///    外部每帧调用 <see cref="UpdateMovement"/> 推动位移。
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        // ==================== Inspector 注入 ====================

        [Header("输入")]
        [Tooltip("用于读取移动方向的虚拟摇杆组件，Inspector 中注入")]
        [SerializeField] private VirtualJoystick m_joystick;

        [Header("地图边界")]
        [Tooltip("地图在世界坐标系下的最小边界（x、y），玩家位置会被钳制到此范围内")]
        [SerializeField] private Vector2 m_mapMin = new Vector2(-20f, -20f);

        [Tooltip("地图在世界坐标系下的最大边界（x、y），玩家位置会被钳制到此范围内")]
        [SerializeField] private Vector2 m_mapMax = new Vector2(20f, 20f);

        // ==================== 运行时依赖 ====================

        /// <summary>
        /// 玩家运行时属性，由 <see cref="Initialize"/> 注入。为 null 时 <see cref="UpdateMovement"/> 跳过处理。
        /// </summary>
        private PlayerStats m_stats;

        /// <summary>
        /// 当前挂载的 <see cref="PlayerStats"/> 实例，供外部（如 InfectionSystem）读取共享属性。
        /// 未调用 <see cref="Initialize"/> 时为 null。
        /// </summary>
        public PlayerStats Stats => m_stats;

        // ==================== 初始化 ====================

        /// <summary>
        /// 注入玩家运行时属性。通常由 <see cref="Game.Core.GameSystemRunner"/> 在系统初始化阶段调用。
        /// </summary>
        /// <param name="stats">已通过 <c>PlayerStats.Initialize</c> 完成基础值设置的实例，不应为 null</param>
        public void Initialize(PlayerStats stats)
        {
            if (stats == null)
            {
                Debug.LogError("[PlayerController] Initialize 收到空的 PlayerStats，玩家将无法移动");
            }

            m_stats = stats;
        }

        // ==================== 每帧更新 ====================

        /// <summary>
        /// 根据输入方向和玩家当前移动速度推动玩家位置。
        /// 支持两种输入方式：虚拟摇杆和 WASD 键盘输入，两者取模长较大者作为最终方向。
        /// 计算步骤：
        /// 1. 分别读取摇杆方向和 WASD 键盘方向，取模长较大者。
        /// 2. 位移 = direction * <see cref="PlayerStats.MoveSpeed"/> * deltaTime。
        /// 3. 叠加到当前 XY 坐标，Z 保持不变（2D 俯视角）。
        /// 4. 使用 <see cref="m_mapMin"/> / <see cref="m_mapMax"/> 钳制最终位置。
        ///
        /// 当 PlayerStats 未注入或 deltaTime 非正数时，方法直接返回以保证安全。
        /// </summary>
        /// <param name="deltaTime">本帧时间增量，通常为 <see cref="Time.deltaTime"/></param>
        public void UpdateMovement(float deltaTime)
        {
            if (m_stats == null)
            {
                return;
            }

            // 非正数 deltaTime 直接忽略，避免暂停帧产生反向位移
            if (deltaTime <= 0f)
            {
                return;
            }

            // 读取摇杆方向（模长 <= 1）
            Vector2 joystickDir = (m_joystick != null) ? m_joystick.Direction : Vector2.zero;

            // 读取 WASD 键盘方向
            Vector2 keyboardDir = GetKeyboardDirection();

            // 取模长较大者作为最终方向，确保两种输入方式互不干扰
            Vector2 direction = (keyboardDir.sqrMagnitude > joystickDir.sqrMagnitude)
                ? keyboardDir
                : joystickDir;

            // 方向向量为零时仍执行钳制，确保玩家位置始终在地图范围内
            Vector2 displacement = direction * (m_stats.MoveSpeed * deltaTime);

            Vector3 currentPosition = transform.position;
            float nextX = currentPosition.x + displacement.x;
            float nextY = currentPosition.y + displacement.y;

            // 钳制位置到地图范围内（要求 m_mapMin.x <= m_mapMax.x 且 m_mapMin.y <= m_mapMax.y，
            // Inspector 错误配置时由 Mathf.Clamp 返回 min 值，玩家会停留在边界上而非异常穿越）
            nextX = Mathf.Clamp(nextX, m_mapMin.x, m_mapMax.x);
            nextY = Mathf.Clamp(nextY, m_mapMin.y, m_mapMax.y);

            transform.position = new Vector3(nextX, nextY, currentPosition.z);
        }

        /// <summary>
        /// 读取 WASD 键盘输入并返回归一化方向向量（模长 &lt;= 1）。
        /// W/S 控制 Y 轴，A/D 控制 X 轴。对角线移动时自动归一化，防止速度超标。
        /// </summary>
        private Vector2 GetKeyboardDirection()
        {
            float x = 0f;
            float y = 0f;

            if (Input.GetKey(KeyCode.W)) y += 1f;
            if (Input.GetKey(KeyCode.S)) y -= 1f;
            if (Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.D)) x += 1f;

            Vector2 dir = new Vector2(x, y);

            // 对角线移动时归一化，保证模长 <= 1，与摇杆行为一致
            if (dir.sqrMagnitude > 1f)
            {
                dir.Normalize();
            }

            return dir;
        }
    }
}
