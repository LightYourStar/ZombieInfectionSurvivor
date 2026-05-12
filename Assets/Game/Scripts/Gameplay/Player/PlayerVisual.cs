using Game.Utility;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 玩家视觉组件，在 Awake 时生成绿色等腰三角形 Mesh。
    /// 三角形尖端指向本地 +Y（即朝向方向），配合 PlayerController 的旋转逻辑指示移动方向。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PlayerVisual : MonoBehaviour
    {
        [Header("三角形参数")]
        [Tooltip("三角形底边宽度")]
        [SerializeField] private float m_width = 0.8f;

        [Tooltip("三角形高度（底边中点到尖端）")]
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
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            mat.color = color;
            return mat;
        }
    }
}
