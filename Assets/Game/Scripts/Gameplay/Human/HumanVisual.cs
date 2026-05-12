using Game.Utility;
using UnityEngine;

namespace Game.Gameplay.Human
{
    /// <summary>
    /// 人类视觉组件，在 Awake 时生成蓝色圆形 Mesh。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class HumanVisual : MonoBehaviour
    {
        [Header("圆形参数")]
        [Tooltip("圆形半径")]
        [SerializeField] private float m_radius = 0.4f;

        [Tooltip("多边形边数，越大越接近圆形")]
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
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            mat.color = color;
            return mat;
        }
    }
}
