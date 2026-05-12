using UnityEngine;

namespace Game.Utility
{
    /// <summary>
    /// Mesh 生成工具类，提供三角形、圆形、菱形等基础几何体的纯代码生成。
    /// 所有方法为纯函数，不依赖 MonoBehaviour 生命周期。
    /// </summary>
    public static class MeshGeneratorUtility
    {
        /// <summary>
        /// 生成等腰三角形 Mesh，尖端指向本地 +Y 方向（即朝向方向）。
        /// </summary>
        /// <param name="width">三角形底边宽度</param>
        /// <param name="height">三角形高度（从底边中点到尖端）</param>
        /// <returns>生成的 Mesh 实例</returns>
        public static Mesh CreateTriangleMesh(float width, float height)
        {
            if (width <= 0f || height <= 0f)
            {
                Debug.LogWarning("[MeshGeneratorUtility] CreateTriangleMesh 参数非法，返回空 Mesh");
                return new Mesh();
            }

            Mesh mesh = new Mesh();
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;

            Vector3[] vertices = new Vector3[]
            {
                new Vector3(0f, halfHeight, 0f),          // 尖端（朝向方向）
                new Vector3(-halfWidth, -halfHeight, 0f), // 左下
                new Vector3(halfWidth, -halfHeight, 0f)   // 右下
            };

            int[] triangles = new int[] { 0, 2, 1 };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>
        /// 生成圆形（正多边形近似）Mesh。
        /// </summary>
        /// <param name="radius">圆形半径</param>
        /// <param name="segments">多边形边数，越大越接近圆形，最小为 3</param>
        /// <returns>生成的 Mesh 实例</returns>
        public static Mesh CreateCircleMesh(float radius, int segments)
        {
            if (radius <= 0f || segments < 3)
            {
                Debug.LogWarning("[MeshGeneratorUtility] CreateCircleMesh 参数非法，返回空 Mesh");
                return new Mesh();
            }

            Mesh mesh = new Mesh();
            Vector3[] vertices = new Vector3[segments + 1];
            vertices[0] = Vector3.zero; // 中心点

            float angleStep = 360f / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f);
            }

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
        /// <param name="size">菱形中心到顶点的距离</param>
        /// <returns>生成的 Mesh 实例</returns>
        public static Mesh CreateDiamondMesh(float size)
        {
            if (size <= 0f)
            {
                Debug.LogWarning("[MeshGeneratorUtility] CreateDiamondMesh 参数非法，返回空 Mesh");
                return new Mesh();
            }

            Mesh mesh = new Mesh();
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
