using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Infection
{
    /// <summary>
    /// 感染视觉反馈对象池。
    /// 监听 GameEvents.OnInfectionSuccess，在感染位置生成扩散圆环效果。
    /// 使用简单的对象池避免频繁 Instantiate/Destroy。
    /// 由 GameSystemRunner 场景中挂载，无需外部调度。
    /// </summary>
    public class InfectionVFXPool : MonoBehaviour
    {
        [Header("配置")]
        [Tooltip("对象池大小，同时最多显示的特效数量")]
        [SerializeField] private int m_poolSize = 20;

        [Tooltip("特效持续时间（秒）")]
        [SerializeField] private float m_effectDuration = 0.35f;

        [Tooltip("特效起始缩放")]
        [SerializeField] private float m_startScale = 0.3f;

        [Tooltip("特效结束缩放")]
        [SerializeField] private float m_endScale = 1.8f;

        [Tooltip("特效颜色")]
        [SerializeField] private Color m_effectColor = new Color(0.2f, 1f, 0.3f, 0.8f);

        // ==================== 运行时状态 ====================

        private readonly List<VFXInstance> m_pool = new List<VFXInstance>();
        private Material m_sharedMaterial;

        private struct VFXInstance
        {
            public GameObject Go;
            public SpriteRenderer Renderer;
            public float Timer;
            public bool Active;
        }

        // ==================== 生命周期 ====================

        private void Awake()
        {
            CreatePool();
        }

        private void OnEnable()
        {
            GameEvents.OnInfectionSuccess += SpawnEffect;
        }

        private void OnDisable()
        {
            GameEvents.OnInfectionSuccess -= SpawnEffect;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < m_pool.Count; i++)
            {
                VFXInstance inst = m_pool[i];
                if (!inst.Active)
                {
                    continue;
                }

                inst.Timer += dt;
                float t = inst.Timer / m_effectDuration;

                if (t >= 1f)
                {
                    inst.Active = false;
                    inst.Go.SetActive(false);
                    m_pool[i] = inst;
                    continue;
                }

                // 扩散 + 淡出
                float scale = Mathf.Lerp(m_startScale, m_endScale, t);
                inst.Go.transform.localScale = new Vector3(scale, scale, 1f);

                Color c = m_effectColor;
                c.a = m_effectColor.a * (1f - t);
                inst.Renderer.color = c;

                m_pool[i] = inst;
            }
        }

        // ==================== 内部方法 ====================

        private void SpawnEffect(Vector2 position)
        {
            // 从池中找一个空闲实例
            for (int i = 0; i < m_pool.Count; i++)
            {
                VFXInstance inst = m_pool[i];
                if (inst.Active)
                {
                    continue;
                }

                inst.Active = true;
                inst.Timer = 0f;
                inst.Go.transform.position = new Vector3(position.x, position.y, 0f);
                inst.Go.transform.localScale = new Vector3(m_startScale, m_startScale, 1f);
                inst.Renderer.color = m_effectColor;
                inst.Go.SetActive(true);
                m_pool[i] = inst;
                return;
            }

            // 池满时复用最老的（timer 最大的）
            int oldestIdx = 0;
            float oldestTime = 0f;
            for (int i = 0; i < m_pool.Count; i++)
            {
                if (m_pool[i].Timer > oldestTime)
                {
                    oldestTime = m_pool[i].Timer;
                    oldestIdx = i;
                }
            }

            VFXInstance oldest = m_pool[oldestIdx];
            oldest.Active = true;
            oldest.Timer = 0f;
            oldest.Go.transform.position = new Vector3(position.x, position.y, 0f);
            oldest.Go.transform.localScale = new Vector3(m_startScale, m_startScale, 1f);
            oldest.Renderer.color = m_effectColor;
            oldest.Go.SetActive(true);
            m_pool[oldestIdx] = oldest;
        }

        private void CreatePool()
        {
            // 创建共享材质（使用 Sprites/Default shader 的圆形）
            m_sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            m_sharedMaterial.color = m_effectColor;

            for (int i = 0; i < m_poolSize; i++)
            {
                GameObject go = new GameObject($"InfectionVFX_{i}");
                go.transform.SetParent(transform, false);

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = CreateCircleSprite();
                sr.material = m_sharedMaterial;
                sr.color = m_effectColor;
                sr.sortingOrder = 100; // 确保在单位上方

                go.SetActive(false);

                m_pool.Add(new VFXInstance
                {
                    Go = go,
                    Renderer = sr,
                    Timer = 0f,
                    Active = false
                });
            }
        }

        /// <summary>
        /// 运行时创建一个简单的圆形 Sprite（白色圆环纹理）。
        /// </summary>
        private Sprite CreateCircleSprite()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            float center = size * 0.5f;
            float outerRadius = size * 0.5f;
            float innerRadius = size * 0.35f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (dist >= innerRadius && dist <= outerRadius)
                    {
                        // 圆环区域，边缘柔化
                        float edgeSoftness = 2f;
                        float outerAlpha = 1f - Mathf.Clamp01((dist - outerRadius + edgeSoftness) / edgeSoftness);
                        float innerAlpha = Mathf.Clamp01((dist - innerRadius) / edgeSoftness);
                        float alpha = outerAlpha * innerAlpha;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
