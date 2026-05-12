using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 摄像机跟随玩家组件。
    /// 挂载在 Main Camera 上，每帧平滑跟随玩家位置，保持 Z 轴不变。
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("跟随目标")]
        [Tooltip("玩家 Transform")]
        [SerializeField] private Transform m_target;

        [Header("跟随参数")]
        [Tooltip("跟随平滑速度，值越大跟随越紧")]
        [SerializeField] private float m_smoothSpeed = 8f;

        private void LateUpdate()
        {
            if (m_target == null)
            {
                return;
            }

            Vector3 targetPos = m_target.position;
            Vector3 currentPos = transform.position;

            // 平滑插值跟随，保持摄像机 Z 轴不变
            float newX = Mathf.Lerp(currentPos.x, targetPos.x, m_smoothSpeed * Time.deltaTime);
            float newY = Mathf.Lerp(currentPos.y, targetPos.y, m_smoothSpeed * Time.deltaTime);

            transform.position = new Vector3(newX, newY, currentPos.z);
        }
    }
}
