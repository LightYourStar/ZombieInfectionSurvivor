using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// 浮动摇杆组件。
    /// 继承 <see cref="VirtualJoystick"/> 以保证 PlayerController 通过基类引用无缝兼容。
    /// 
    /// 结构：
    /// - TouchZone（本组件挂载节点）：覆盖屏幕左下 1/3 的透明触摸区域
    /// - JoystickBackground（子节点）：手指按下时移动到按下位置并显示
    /// - JoystickHandle（Background 的子节点）：跟随拖拽偏移
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FloatingJoystick : VirtualJoystick
    {
        [Header("浮动摇杆")]
        [Tooltip("摇杆背景节点，按下时移动到手指位置并显示")]
        [SerializeField] private RectTransform m_background;

        [Tooltip("摇杆背景的 CanvasGroup，用于控制显隐")]
        [SerializeField] private CanvasGroup m_backgroundCanvasGroup;

        [Tooltip("浮动摇杆最大半径（像素）")]
        [SerializeField] private float m_floatingRadius = 100f;

        /// <summary>TouchZone 自身的 RectTransform</summary>
        private RectTransform m_touchZoneRect;

        /// <summary>按下时记录的 Background 中心屏幕坐标，用于后续 Drag 计算偏移</summary>
        private Vector2 m_pointerDownScreenPos;

        protected override void Awake()
        {
            AutoWireReferences();
            base.Awake();
            m_touchZoneRect = GetComponent<RectTransform>();
            SetBackgroundVisible(false);
        }

        private void Reset()
        {
            AutoWireReferences();
        }

        private void OnValidate()
        {
            AutoWireReferences();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            // 将背景移动到手指按下位置
            if (m_background != null && m_touchZoneRect != null)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        m_touchZoneRect,
                        eventData.position,
                        eventData.pressEventCamera,
                        out Vector2 localPoint))
                {
                    m_background.anchoredPosition = localPoint;
                }
            }

            // 记录按下位置作为摇杆中心参考
            m_pointerDownScreenPos = eventData.position;

            SetBackgroundVisible(true);

            // 按下瞬间方向为零（手指还没拖动）
            Direction = Vector2.zero;
            if (m_handle != null)
            {
                m_handle.anchoredPosition = Vector2.zero;
            }
        }

        public override void OnDrag(PointerEventData eventData)
        {
            // 计算手指相对于按下位置的屏幕像素偏移
            Vector2 offset = eventData.position - m_pointerDownScreenPos;

            // 归一化：除以半径，超出时钳制到单位圆
            float radius = m_floatingRadius > 0f ? m_floatingRadius : 100f;
            Vector2 dir = offset / radius;
            if (dir.sqrMagnitude > 1f)
            {
                dir = dir.normalized;
            }

            Direction = dir;

            // Handle 视觉跟随
            if (m_handle != null)
            {
                m_handle.anchoredPosition = dir * radius;
            }
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            Direction = Vector2.zero;
            if (m_handle != null)
            {
                m_handle.anchoredPosition = Vector2.zero;
            }
            SetBackgroundVisible(false);
        }

        private void SetBackgroundVisible(bool visible)
        {
            if (m_backgroundCanvasGroup != null)
            {
                m_backgroundCanvasGroup.alpha = visible ? 1f : 0f;
                m_backgroundCanvasGroup.blocksRaycasts = visible;
            }
        }

        private void AutoWireReferences()
        {
            if (m_background == null)
            {
                Transform background = transform.Find("JoystickBG");
                m_background = background as RectTransform;
            }

            if (m_handle == null && m_background != null)
            {
                Transform handle = m_background.Find("Handle");
                m_handle = handle as RectTransform;
            }

            if (m_backgroundCanvasGroup == null && m_background != null)
            {
                m_backgroundCanvasGroup = m_background.GetComponent<CanvasGroup>();
            }
        }
    }
}
