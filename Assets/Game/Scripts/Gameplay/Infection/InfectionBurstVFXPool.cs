using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Feedback;
using UnityEngine;

namespace Game.Gameplay.Infection
{
    /// <summary>
    /// InfectionBurst 的圆环扩散表现池。
    /// </summary>
    public class InfectionBurstVFXPool : MonoBehaviour
    {
        [Header("Pool")]
        [SerializeField] private int m_poolSize = 24;
        [SerializeField] private int m_maxActiveRings = 12;
        [SerializeField] private float m_duration = 0.42f;

        [Header("Visual")]
        [SerializeField] private float m_startRadiusMultiplier = 0.18f;
        [SerializeField] private float m_overshootMultiplier = 1.08f;
        [SerializeField] private Color m_ringColor = new Color(0.85f, 1f, 0.22f, 0.65f);
        [SerializeField] private int m_sortingOrder = 118;

        private readonly List<RingInstance> m_pool = new List<RingInstance>();
        private Sprite m_ringSprite;
        private int m_peakActiveRings;

        public int PoolSize => m_pool.Count;
        public int PeakActiveRings => m_peakActiveRings;

        private struct RingInstance
        {
            public GameObject Go;
            public SpriteRenderer Renderer;
            public float Timer;
            public float Radius;
            public bool Active;
        }

        private void Awake()
        {
            m_ringSprite = CreateRingSprite();
            CreatePool();
        }

        private void OnEnable()
        {
            GameEvents.OnInfectionBurstVisual += SpawnBurstRing;
        }

        private void OnDisable()
        {
            GameEvents.OnInfectionBurstVisual -= SpawnBurstRing;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < m_pool.Count; i++)
            {
                RingInstance inst = m_pool[i];
                if (!inst.Active)
                {
                    continue;
                }

                inst.Timer += dt;
                float t = Mathf.Clamp01(inst.Timer / Mathf.Max(0.01f, m_duration));

                if (t >= 1f)
                {
                    inst.Active = false;
                    inst.Go.SetActive(false);
                    m_pool[i] = inst;
                    continue;
                }

                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float radius = Mathf.Lerp(
                    inst.Radius * m_startRadiusMultiplier,
                    inst.Radius * m_overshootMultiplier,
                    eased);
                float diameter = Mathf.Max(0.01f, radius * 2f);
                inst.Go.transform.localScale = new Vector3(diameter, diameter, 1f);

                Color color = m_ringColor;
                color.a = m_ringColor.a * (1f - t);
                inst.Renderer.color = color;

                m_pool[i] = inst;
            }
        }

        private void SpawnBurstRing(Vector2 center, float radius, int infectedCount)
        {
            if (radius <= 0f)
            {
                return;
            }

            int activeCount = CountActiveRings();
            if (activeCount >= Mathf.Min(m_maxActiveRings, m_pool.Count))
            {
                return;
            }

            int index = FindAvailableIndex();
            RingInstance inst = m_pool[index];
            inst.Active = true;
            inst.Timer = 0f;
            inst.Radius = radius;
            inst.Go.transform.position = new Vector3(center.x, center.y, -0.15f);
            inst.Go.transform.localScale = Vector3.one * Mathf.Max(0.01f, radius * 2f * m_startRadiusMultiplier);
            inst.Renderer.color = m_ringColor;
            inst.Go.SetActive(true);
            m_pool[index] = inst;
            m_peakActiveRings = Mathf.Max(m_peakActiveRings, activeCount + 1);

            if (GameAudioFeedback.Instance != null)
            {
                GameAudioFeedback.Instance.PlayBurst();
            }
        }

        private int FindAvailableIndex()
        {
            int oldestIndex = 0;
            float oldestTimer = -1f;

            for (int i = 0; i < m_pool.Count; i++)
            {
                if (!m_pool[i].Active)
                {
                    return i;
                }

                if (m_pool[i].Timer > oldestTimer)
                {
                    oldestTimer = m_pool[i].Timer;
                    oldestIndex = i;
                }
            }

            return oldestIndex;
        }

        private void CreatePool()
        {
            int count = Mathf.Max(1, m_poolSize);
            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject($"InfectionBurstRing_{i}");
                go.transform.SetParent(transform, false);

                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = m_ringSprite;
                renderer.color = m_ringColor;
                renderer.sortingOrder = m_sortingOrder;
                go.SetActive(false);

                m_pool.Add(new RingInstance
                {
                    Go = go,
                    Renderer = renderer,
                    Timer = 0f,
                    Radius = 1f,
                    Active = false
                });
            }
        }

        private static Sprite CreateRingSprite()
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "InfectionBurstRing";
            texture.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float outerRadius = size * 0.48f;
            float innerRadius = size * 0.41f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float outerFade = 1f - Mathf.Clamp01((distance - outerRadius + 3f) / 3f);
                    float innerFade = Mathf.Clamp01((distance - innerRadius) / 3f);
                    float alpha = distance >= innerRadius && distance <= outerRadius ? outerFade * innerFade : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private int CountActiveRings()
        {
            int count = 0;
            for (int i = 0; i < m_pool.Count; i++)
            {
                if (m_pool[i].Active)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
