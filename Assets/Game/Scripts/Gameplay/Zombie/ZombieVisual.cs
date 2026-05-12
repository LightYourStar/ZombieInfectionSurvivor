using Game.Utility;
using UnityEngine;

namespace Game.Gameplay.Zombie
{
    /// <summary>
    /// 僵尸同伴视觉组件，在 Awake 时生成红色菱形 Mesh。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ZombieVisual : MonoBehaviour
    {
        [Header("菱形参数")]
        [Tooltip("菱形中心到顶点的距离")]
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
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            mat.color = color;
            return mat;
        }
    }
}
