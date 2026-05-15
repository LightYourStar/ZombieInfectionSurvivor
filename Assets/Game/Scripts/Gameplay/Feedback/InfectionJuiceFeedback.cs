using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Feedback
{
    /// <summary>
    /// 感染瞬间轻量反馈：+1 浮字与白闪冲击。
    /// </summary>
    public class InfectionJuiceFeedback : MonoBehaviour
    {
        [Header("Float Text")]
        [SerializeField] private int m_floatTextPoolSize = 48;
        [SerializeField] private float m_floatTextDuration = 0.55f;
        [SerializeField] private float m_floatTextRiseSpeed = 1.65f;
        [SerializeField] private float m_floatTextStartScale = 0.72f;
        [SerializeField] private float m_floatTextPeakScale = 1.2f;

        [Header("Impact Flash")]
        [SerializeField] private int m_impactPoolSize = 48;
        [SerializeField] private float m_impactDuration = 0.22f;
        [SerializeField] private float m_impactStartScale = 0.72f;
        [SerializeField] private float m_impactPeakScale = 1.05f;
        [SerializeField] private Color m_impactColor = new Color(1f, 1f, 1f, 0.75f);

        private readonly List<FloatTextInstance> m_floatTexts = new List<FloatTextInstance>();
        private readonly List<ImpactInstance> m_impacts = new List<ImpactInstance>();
        private Sprite m_flashSprite;
        private Font m_defaultFont;

        private struct FloatTextInstance
        {
            public GameObject Go;
            public TextMesh Text;
            public MeshRenderer Renderer;
            public float Timer;
            public bool Active;
            public Vector2 StartPos;
        }

        private struct ImpactInstance
        {
            public GameObject Go;
            public SpriteRenderer Renderer;
            public float Timer;
            public bool Active;
        }

        private void Awake()
        {
            m_defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m_flashSprite = CreateFlashSprite();
            CreateFloatTextPool();
            CreateImpactPool();
        }

        private void OnEnable()
        {
            GameEvents.OnInfectionSuccess += HandleInfection;
        }

        private void OnDisable()
        {
            GameEvents.OnInfectionSuccess -= HandleInfection;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            UpdateFloatTexts(dt);
            UpdateImpacts(dt);
        }

        private void HandleInfection(Vector2 position)
        {
            SpawnFloatText(position);
            SpawnImpact(position);

            if (GameAudioFeedback.Instance != null)
            {
                GameAudioFeedback.Instance.PlayInfection();
            }
        }

        private void UpdateFloatTexts(float dt)
        {
            float duration = Mathf.Max(0.01f, m_floatTextDuration);
            for (int i = 0; i < m_floatTexts.Count; i++)
            {
                FloatTextInstance inst = m_floatTexts[i];
                if (!inst.Active)
                {
                    continue;
                }

                inst.Timer += dt;
                float t = Mathf.Clamp01(inst.Timer / duration);

                if (t >= 1f)
                {
                    inst.Active = false;
                    inst.Go.SetActive(false);
                    m_floatTexts[i] = inst;
                    continue;
                }

                float yOffset = m_floatTextRiseSpeed * inst.Timer;
                inst.Go.transform.position = new Vector3(inst.StartPos.x, inst.StartPos.y + yOffset, -1f);

                float pop = t < 0.18f
                    ? Mathf.Lerp(m_floatTextStartScale, m_floatTextPeakScale, t / 0.18f)
                    : Mathf.Lerp(m_floatTextPeakScale, 0.65f, (t - 0.18f) / 0.82f);
                inst.Go.transform.localScale = new Vector3(pop, pop, 1f);

                Color color = inst.Text.color;
                color.a = 1f - t;
                inst.Text.color = color;

                m_floatTexts[i] = inst;
            }
        }

        private void UpdateImpacts(float dt)
        {
            float duration = Mathf.Max(0.01f, m_impactDuration);
            for (int i = 0; i < m_impacts.Count; i++)
            {
                ImpactInstance inst = m_impacts[i];
                if (!inst.Active)
                {
                    continue;
                }

                inst.Timer += dt;
                float t = Mathf.Clamp01(inst.Timer / duration);

                if (t >= 1f)
                {
                    inst.Active = false;
                    inst.Go.SetActive(false);
                    m_impacts[i] = inst;
                    continue;
                }

                float scale = t < 0.45f
                    ? Mathf.Lerp(m_impactStartScale, m_impactPeakScale, t / 0.45f)
                    : Mathf.Lerp(m_impactPeakScale, m_impactStartScale * 0.92f, (t - 0.45f) / 0.55f);
                inst.Go.transform.localScale = new Vector3(scale, scale, 1f);

                Color color = m_impactColor;
                color.a = m_impactColor.a * (1f - t);
                inst.Renderer.color = color;

                m_impacts[i] = inst;
            }
        }

        private void SpawnFloatText(Vector2 position)
        {
            Vector2 offset = Random.insideUnitCircle * 0.18f;
            Vector2 spawnPos = position + offset + Vector2.up * 0.56f;
            int index = FindAvailableFloatTextIndex();

            FloatTextInstance inst = m_floatTexts[index];
            inst.Active = true;
            inst.Timer = 0f;
            inst.StartPos = spawnPos;
            inst.Go.transform.position = new Vector3(spawnPos.x, spawnPos.y, -1f);
            inst.Go.transform.localScale = Vector3.one * m_floatTextStartScale;
            inst.Text.text = "+1";
            inst.Text.color = new Color(0.55f, 1f, 0.38f, 1f);
            inst.Go.SetActive(true);
            m_floatTexts[index] = inst;
        }

        private void SpawnImpact(Vector2 position)
        {
            int index = FindAvailableImpactIndex();
            ImpactInstance inst = m_impacts[index];
            inst.Active = true;
            inst.Timer = 0f;
            inst.Go.transform.position = new Vector3(position.x, position.y, -0.2f);
            inst.Go.transform.localScale = Vector3.one * m_impactStartScale;
            inst.Renderer.color = m_impactColor;
            inst.Go.SetActive(true);
            m_impacts[index] = inst;
        }

        private int FindAvailableFloatTextIndex()
        {
            int oldestIndex = 0;
            float oldestTimer = -1f;

            for (int i = 0; i < m_floatTexts.Count; i++)
            {
                if (!m_floatTexts[i].Active)
                {
                    return i;
                }

                if (m_floatTexts[i].Timer > oldestTimer)
                {
                    oldestTimer = m_floatTexts[i].Timer;
                    oldestIndex = i;
                }
            }

            return oldestIndex;
        }

        private int FindAvailableImpactIndex()
        {
            int oldestIndex = 0;
            float oldestTimer = -1f;

            for (int i = 0; i < m_impacts.Count; i++)
            {
                if (!m_impacts[i].Active)
                {
                    return i;
                }

                if (m_impacts[i].Timer > oldestTimer)
                {
                    oldestTimer = m_impacts[i].Timer;
                    oldestIndex = i;
                }
            }

            return oldestIndex;
        }

        private void CreateFloatTextPool()
        {
            int count = Mathf.Max(1, m_floatTextPoolSize);
            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject($"InfectionFloatText_{i}", typeof(TextMesh), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);

                TextMesh text = go.GetComponent<TextMesh>();
                text.text = "+1";
                text.font = m_defaultFont;
                text.fontSize = 34;
                text.characterSize = 0.08f;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.color = new Color(0.55f, 1f, 0.38f, 1f);

                MeshRenderer renderer = go.GetComponent<MeshRenderer>();
                renderer.sortingOrder = 130;
                if (m_defaultFont != null)
                {
                    renderer.sharedMaterial = m_defaultFont.material;
                }

                go.SetActive(false);

                m_floatTexts.Add(new FloatTextInstance
                {
                    Go = go,
                    Text = text,
                    Renderer = renderer,
                    Timer = 0f,
                    Active = false,
                    StartPos = Vector2.zero
                });
            }
        }

        private void CreateImpactPool()
        {
            int count = Mathf.Max(1, m_impactPoolSize);
            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject($"InfectionImpact_{i}");
                go.transform.SetParent(transform, false);

                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = m_flashSprite;
                renderer.color = m_impactColor;
                renderer.sortingOrder = 124;

                go.SetActive(false);

                m_impacts.Add(new ImpactInstance
                {
                    Go = go,
                    Renderer = renderer,
                    Timer = 0f,
                    Active = false
                });
            }
        }

        private static Sprite CreateFlashSprite()
        {
            const int size = 48;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "InfectionImpactFlash";
            texture.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.45f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(1f - distance / radius);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
