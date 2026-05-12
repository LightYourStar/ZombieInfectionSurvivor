using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// 基于 UGUI 的虚拟摇杆组件。
    /// 通过实现 <see cref="IPointerDownHandler"/>、<see cref="IDragHandler"/>、<see cref="IPointerUpHandler"/>
    /// 三个事件系统接口采集触屏/鼠标拖拽输入，并输出归一化的二维方向向量供玩家移动模块使用。
    ///
    /// 使用约定：
    /// 1. 本组件应挂载在摇杆"背景"节点上，其 <see cref="RectTransform"/> 的 pivot 建议为 (0.5, 0.5)，
    ///    这样拖拽点的本地坐标即为相对中心的偏移，便于归一化计算。
    /// 2. <see cref="m_handle"/> 应为背景节点的子节点，其 anchoredPosition 会被本组件直接覆盖，
    ///    其 anchor 建议也为中心 (0.5, 0.5)，以保证 handle 在不同分辨率下行为一致。
    /// 3. <see cref="m_radius"/> 同时决定 handle 的最大可视偏移以及方向归一化的分母，
    ///    拖拽超出半径时方向向量会被限制到单位圆上，保证模长 <= 1。
    /// 4. 本组件为纯 UGUI 实现，不依赖任何第三方插件。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class VirtualJoystick : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        // ==================== 视觉引用 ====================

        [Header("摇杆视觉")]
        [Tooltip("摇杆可移动的 Handle 节点，通常为背景节点的子节点。按下/拖拽时 anchoredPosition 会被本组件覆盖。")]
        [SerializeField] protected RectTransform m_handle;

        // ==================== 参数 ====================

        [Header("摇杆参数")]
        [Tooltip("Handle 可移动的最大半径（本地像素），同时作为方向归一化的分母。必须为正数。")]
        [SerializeField] private float m_radius = 100f;

        // ==================== 运行时状态 ====================

        /// <summary>
        /// 摇杆所在的背景 <see cref="RectTransform"/>，用于将屏幕坐标转换为本地坐标。
        /// 在 <see cref="Awake"/> 中从自身 GameObject 获取一次。
        /// </summary>
        private RectTransform m_backgroundRect;

        /// <summary>
        /// 当前输出的方向向量，模长始终满足 0 &lt;= |Direction| &lt;= 1。
        /// 未按下或释放时为 <see cref="Vector2.zero"/>。
        /// </summary>
        public Vector2 Direction { get; protected set; }

        // ==================== Unity 生命周期 ====================

        protected virtual void Awake()
        {
            m_backgroundRect = GetComponent<RectTransform>();

            // 初始状态确保 handle 位于中心、方向向量为零
            if (m_handle != null)
            {
                m_handle.anchoredPosition = Vector2.zero;
            }
            Direction = Vector2.zero;
        }

        // ==================== 事件回调 ====================

        /// <summary>
        /// 手指/鼠标按下时调用，立即按当前指针位置刷新一次摇杆方向与 handle 位置。
        /// 这样即使玩家只是点击未拖拽，也能获得一次即时的方向输入。
        /// </summary>
        public virtual void OnPointerDown(PointerEventData eventData)
        {
            UpdateJoystick(eventData);
        }

        /// <summary>
        /// 拖拽过程中持续调用，实时刷新方向向量与 handle 视觉位置。
        /// </summary>
        public virtual void OnDrag(PointerEventData eventData)
        {
            UpdateJoystick(eventData);
        }

        /// <summary>
        /// 手指/鼠标释放时调用，将方向向量归零并把 handle 视觉复位到中心。
        /// </summary>
        public virtual void OnPointerUp(PointerEventData eventData)
        {
            Direction = Vector2.zero;
            if (m_handle != null)
            {
                m_handle.anchoredPosition = Vector2.zero;
            }
        }

        // ==================== 内部逻辑 ====================

        /// <summary>
        /// 根据指针事件的屏幕坐标刷新方向向量与 handle 位置。
        /// 会先通过 <see cref="RectTransformUtility.ScreenPointToLocalPointInRectangle"/>
        /// 将屏幕坐标转换为背景 RectTransform 的本地坐标，再进行归一化。
        /// </summary>
        private void UpdateJoystick(PointerEventData eventData)
        {
            // 将屏幕坐标转为背景 RectTransform 的本地坐标
            // Screen Space - Overlay 模式下 pressEventCamera 为 null，正好与 ScreenPointToLocalPointInRectangle 的要求一致
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    m_backgroundRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            Direction = CalculateDirection(localPoint);

            if (m_handle != null)
            {
                // handle 直接按归一化方向 * 半径放置，保证其不会超出摇杆外圈
                m_handle.anchoredPosition = Direction * m_radius;
            }
        }

        /// <summary>
        /// 将相对摇杆中心的偏移向量转换为模长 &lt;= 1 的归一化方向向量。
        /// 偏移在半径内时按比例缩放（保留推动强度），超出半径时钳制到单位圆上。
        /// </summary>
        /// <param name="inputPosition">指针相对摇杆中心（背景 RectTransform 本地坐标系原点）的偏移</param>
        /// <returns>模长 &lt;= 1 的方向向量</returns>
        private Vector2 CalculateDirection(Vector2 inputPosition)
        {
            // 防御性判断：非法半径时直接返回零向量，避免除零
            if (m_radius <= 0f)
            {
                return Vector2.zero;
            }

            Vector2 normalized = inputPosition / m_radius;

            // 超出单位圆时钳制到单位圆上，保证模长 <= 1
            if (normalized.sqrMagnitude > 1f)
            {
                normalized = normalized.normalized;
            }

            return normalized;
        }
    }
}
