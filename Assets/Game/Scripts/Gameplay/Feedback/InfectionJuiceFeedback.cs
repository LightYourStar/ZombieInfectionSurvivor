using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Feedback
{
    /// <summary>
    /// 感染瞬间表现反馈：浮字 +1、缩放冲击。
    /// 监听 GameEvents.OnInfectionSuccess，使用对象池管理浮字。
    /// 独立组件，挂载在场景中即可工作。
    /// </summary>
    public class InfectionJuiceFeedback : MonoBehaviour
    {
        [Header("浮字配置")]
        [SerializeField] private int m_floatTextPoolSize = 30;
        [SerializeField] private float m_floatTextDuration = 0.6f;
        [SerializeField] private float m_floatTextRiseSpeed = 2f;
        [SerializeField] private float m_floatTextStartScale = 0.8f;
        [SerializeField] private float m_floatTextPeakScale = 1.2f;

        private readonly List<FloatTextInstance> m_floatTexts = new List<FloatTextInstance>();
        private Sprite m_dotSprite;

        private struct FloatTextInstance
        {
            public GameObject Go;
            public SpriteRenderer Renderer;
            public float Timer;
            public bool Active;
            public Vector2 StartPos;
        }

        private void Awake()
        {
            CreateFloatTextPool();
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
            for (int i = 0; i < m_floatTexts.Count; i++)
            {
                FloatTextInstance inst = m_floatTexts[i];
                if (!inst.Active) continue;

                inst.Timer += dt;
                float t = inst.Timer / m_floatTextDuration;

                if (t >= 1f)
                {
                    inst.Active = false;
                    inst.Go.SetActive(false);
                    m_floatTexts[i] = inst;
                    continue;
                }

                // 上升
                float yOffset = m_floatTextRiseSpeed * inst.Timer;
                inst.Go.transform.position = new Vector3(inst.StartPos.x, inst.StartPos.y + yOffset, -1f);

                // 缩放：快速放大再缩小
                float scale;
                if (t < 0.2f)
                {
                    scale = Mathf.Lerp(m_floatTextStartScale, m_floatTextPeakScale, t / 0.2f);
                }
                else
                {
                    scale = Mathf.Lerp(m_floatTextPeakScale, 0.4f, (t - 0.2f) / 0.8f);
                }
                inst.Go.transform.localScale = new Vector3(scale, scale, 1f);

                // 淡出
                Color c = inst.Renderer.color;
                c.a = 1f - t;
                inst.Renderer.color = c;

                m_floatTexts[i] = inst;
            }
        }

        private void HandleInfection(Vector2 position)
        {
            SpawnFloatText(position);

            // 音效
            if (GameAudioFeedback.Instance != null)
            {
                GameAudioFeedback.Instance.PlayInfection();
            }
        }

        private void SpawnFloatText(Vector2 position)
        {
            // 添加随机偏移避免重叠
            Vector2 offset = Random.insideUnitCircle * 0.3f;
            Vector2 spawnPos = position + offset + Vector2.up * 0.5f;

            for (int i = 0; i < m_floatTexts.Count; i++)
            {
                FloatTextInstance inst = m_floatTexts[i];
                if (inst.Active) continue;

                inst.Active = true;
                inst.Timer = 0f;
                inst.StartPos = spawnPos;
                inst.Go.transform.position = new Vector3(spawnPos.x, spawnPos.y, -1f);
                inst.Go.transform.localScale = new Vector3(m_floatTextStartScale, m_floatTextStartScale, 1f);
                inst.Renderer.color = new Color(0.3f, 1f, 0.4f, 1f);
                inst.Go.SetActive(true);
                m_floatTexts[i] = inst;
                return;
            }

            // 池满：复用最老的
            int oldestIdx = 0;
            float oldestTime = 0f;
            for (int i = 0; i < m_floatTexts.Count; i++)
            {
                if (m_floatTexts[i].Timer > oldestTime)
                {
                    oldestTime = m_floatTexts[i].Timer;
                    oldestIdx = i;
                }
            }

            FloatTextInstance oldest = m_floatTexts[oldestIdx];
            oldest.Active = true;
            oldest.Timer = 0f;
            oldest.StartPos = spawnPos;
            oldest.Go.transform.position = new Vector3(spawnPos.x, spawnPos.y, -1f);
            oldest.Go.transform.localScale = new Vector3(m_floatTextStartScale, m_floatTextStartScale, 1f);
            oldest.Renderer.color = new Color(0.3f, 1f, 0.4f, 1f);
            oldest.Go.SetActive(true);
            m_floatTexts[oldestIdx] = oldest;
        }

        private void CreateFloatTextPool()
        {
            m_dotSprite = CreateDotSprite();

            for (int i = 0; i < m_floatTextPoolSize; i++)
            {
                GameObject go = new GameObject($"FloatText_{i}");
                go.transform.SetParent(transform, false);

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = m_dotSprite;
                sr.color = new Color(0.3f, 1f, 0.4f, 1f);
                sr.sortingOrder = 110;
                go.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                go.SetActive(false);

                m_floatTexts.Add(new FloatTextInstance
                {
                    Go = go,
                    Renderer = sr,
                    Timer = 0f,
                    Active = false,
                    StartPos = Vector2.zero
                });
            }
        }

        private Sprite CreateDotSprite()
        {
            int size = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = size * 0.5f;
            float radius = size * 0.4f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(1f - (dist / radius));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
