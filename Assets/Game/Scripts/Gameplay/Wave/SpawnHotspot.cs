using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Gameplay.Wave
{
    public enum HumanClusterType
    {
        Small,
        Medium,
        Large
    }

    [DisallowMultipleComponent]
    public class SpawnHotspot : MonoBehaviour
    {
        [Header("Hotspot")]
        [SerializeField] private string m_hotspotName = "Hotspot";
        [SerializeField] private HumanClusterType m_clusterType = HumanClusterType.Small;

        [Header("Cluster")]
        [SerializeField, Min(1)] private int m_minCount = 3;
        [SerializeField, Min(1)] private int m_maxCount = 5;
        [SerializeField, Min(0.1f)] private float m_spawnRadius = 2.5f;

        [Header("Schedule")]
        [SerializeField, Min(0f)] private float m_weight = 1f;
        [SerializeField, Min(0f)] private float m_enableTime;
        [SerializeField] private float m_weightRampTime = -1f;
        [SerializeField] private float m_weightAfterRamp = -1f;
        [SerializeField] private bool m_isFinalFrenzyBoosted;

        [Header("Flow Support")]
        [SerializeField] private bool m_isOpeningPriority;
        [SerializeField] private bool m_isFlowFallback;
        [SerializeField, Min(0f)] private float m_recommendedMinPlayerDistance = 4f;
        [SerializeField, Min(0f)] private float m_recommendedIdealMinPlayerDistance = 6f;
        [SerializeField, Min(0f)] private float m_recommendedIdealMaxPlayerDistance = 14f;
        [SerializeField, Min(0f)] private float m_recommendedMaxPlayerDistance = 18f;

        public string HotspotName => string.IsNullOrWhiteSpace(m_hotspotName) ? name : m_hotspotName;
        public HumanClusterType ClusterType => m_clusterType;
        public int MinCount => m_minCount;
        public int MaxCount => m_maxCount;
        public float SpawnRadius => m_spawnRadius;
        public float Weight => m_weight;
        public float EnableTime => m_enableTime;
        public float WeightRampTime => m_weightRampTime;
        public float WeightAfterRamp => m_weightAfterRamp;
        public bool IsFinalFrenzyBoosted => m_isFinalFrenzyBoosted;
        public bool IsOpeningPriority => m_isOpeningPriority;
        public bool IsFlowFallback => m_isFlowFallback;
        public float RecommendedMinPlayerDistance => m_recommendedMinPlayerDistance;
        public float RecommendedIdealMinPlayerDistance => m_recommendedIdealMinPlayerDistance;
        public float RecommendedIdealMaxPlayerDistance => m_recommendedIdealMaxPlayerDistance;
        public float RecommendedMaxPlayerDistance => m_recommendedMaxPlayerDistance;
        public Vector2 Position => transform.position;

        public bool IsEnabledAt(float elapsedTime)
        {
            return isActiveAndEnabled
                && elapsedTime >= m_enableTime
                && m_weight > 0f
                && m_maxCount > 0;
        }

        public float GetEffectiveWeight(float elapsedTime, bool isFinalFrenzy, float finalFrenzyMultiplier)
        {
            float result = GetBaseWeightAt(elapsedTime);
            if (isFinalFrenzy && m_isFinalFrenzyBoosted)
            {
                result *= Mathf.Max(1f, finalFrenzyMultiplier);
            }

            return Mathf.Max(0f, result);
        }

        public int GetRandomCount()
        {
            int min = Mathf.Max(1, Mathf.Min(m_minCount, m_maxCount));
            int max = Mathf.Max(min, m_maxCount);
            return Random.Range(min, max + 1);
        }

        public void Configure(
            string hotspotName,
            HumanClusterType clusterType,
            int minCount,
            int maxCount,
            float spawnRadius,
            float weight,
            float enableTime,
            bool isFinalFrenzyBoosted,
            bool isOpeningPriority = false,
            bool isFlowFallback = false,
            float weightRampTime = -1f,
            float weightAfterRamp = -1f)
        {
            m_hotspotName = hotspotName;
            m_clusterType = clusterType;
            m_minCount = Mathf.Max(1, minCount);
            m_maxCount = Mathf.Max(m_minCount, maxCount);
            m_spawnRadius = Mathf.Max(0.1f, spawnRadius);
            m_weight = Mathf.Max(0f, weight);
            m_enableTime = Mathf.Max(0f, enableTime);
            m_isFinalFrenzyBoosted = isFinalFrenzyBoosted;
            m_isOpeningPriority = isOpeningPriority;
            m_isFlowFallback = isFlowFallback;
            m_weightRampTime = weightRampTime;
            m_weightAfterRamp = weightAfterRamp;
        }

        private float GetBaseWeightAt(float elapsedTime)
        {
            if (m_weightRampTime >= 0f && elapsedTime >= m_weightRampTime && m_weightAfterRamp >= 0f)
            {
                return m_weightAfterRamp;
            }

            return m_weight;
        }

        private void OnValidate()
        {
            m_minCount = Mathf.Max(1, m_minCount);
            m_maxCount = Mathf.Max(m_minCount, m_maxCount);
            m_spawnRadius = Mathf.Max(0.1f, m_spawnRadius);
            m_weight = Mathf.Max(0f, m_weight);
            m_enableTime = Mathf.Max(0f, m_enableTime);
            if (m_weightRampTime >= 0f)
            {
                m_weightRampTime = Mathf.Max(m_enableTime, m_weightRampTime);
            }
            if (m_weightAfterRamp >= 0f)
            {
                m_weightAfterRamp = Mathf.Max(0f, m_weightAfterRamp);
            }
            m_recommendedMinPlayerDistance = Mathf.Max(0f, m_recommendedMinPlayerDistance);
            m_recommendedIdealMinPlayerDistance = Mathf.Max(m_recommendedMinPlayerDistance, m_recommendedIdealMinPlayerDistance);
            m_recommendedIdealMaxPlayerDistance = Mathf.Max(m_recommendedIdealMinPlayerDistance, m_recommendedIdealMaxPlayerDistance);
            m_recommendedMaxPlayerDistance = Mathf.Max(m_recommendedIdealMaxPlayerDistance, m_recommendedMaxPlayerDistance);
        }

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            float elapsedTime = 0f;
            bool isFinalFrenzy = false;
            if (Application.isPlaying)
            {
                HumanClusterSpawner spawner = FindObjectOfType<HumanClusterSpawner>();
                if (spawner != null)
                {
                    elapsedTime = spawner.ElapsedTime;
                    isFinalFrenzy = spawner.IsFinalFrenzyActive;
                }
            }

            bool isEnabled = IsEnabledAt(elapsedTime);
            Color color = GetGizmoColor(isEnabled, isFinalFrenzy);
            Vector3 center = transform.position;

            Gizmos.color = new Color(color.r, color.g, color.b, 0.12f);
            Gizmos.DrawSphere(center, m_spawnRadius);
            Gizmos.color = new Color(color.r, color.g, color.b, 0.95f);
            Gizmos.DrawWireSphere(center, m_spawnRadius);

            Handles.color = color;
            Handles.Label(
                center + Vector3.up * (m_spawnRadius + 0.35f),
                $"{HotspotName}\n{GetGizmoStateText(isEnabled, isFinalFrenzy)}",
                GetGizmoLabelStyle(color));
#endif
        }

#if UNITY_EDITOR
        private Color GetGizmoColor(bool isEnabled, bool isFinalFrenzy)
        {
            if (!isEnabled)
            {
                return new Color(0.55f, 0.55f, 0.55f, 0.85f);
            }

            if (isFinalFrenzy && m_isFinalFrenzyBoosted)
            {
                return new Color(1f, 0.55f, 0.15f, 0.95f);
            }

            return new Color(0.15f, 0.95f, 0.65f, 0.95f);
        }

        private string GetGizmoStateText(bool isEnabled, bool isFinalFrenzy)
        {
            if (!isEnabled)
            {
                return $"未启用 ({m_enableTime:F0}s)";
            }

            if (isFinalFrenzy && m_isFinalFrenzyBoosted)
            {
                return "Final Frenzy Boosted";
            }

            return "已启用";
        }

        private static GUIStyle GetGizmoLabelStyle(Color color)
        {
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = color;
            style.alignment = TextAnchor.MiddleCenter;
            return style;
        }
#endif
    }
}
