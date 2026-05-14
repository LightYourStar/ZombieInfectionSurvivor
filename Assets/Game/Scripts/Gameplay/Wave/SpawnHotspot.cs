using UnityEngine;

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
        [SerializeField] private bool m_isFinalFrenzyBoosted;

        public string HotspotName => string.IsNullOrWhiteSpace(m_hotspotName) ? name : m_hotspotName;
        public HumanClusterType ClusterType => m_clusterType;
        public int MinCount => m_minCount;
        public int MaxCount => m_maxCount;
        public float SpawnRadius => m_spawnRadius;
        public float Weight => m_weight;
        public float EnableTime => m_enableTime;
        public bool IsFinalFrenzyBoosted => m_isFinalFrenzyBoosted;
        public Vector2 Position => transform.position;

        public bool IsEnabledAt(float elapsedTime)
        {
            return isActiveAndEnabled
                && elapsedTime >= m_enableTime
                && m_weight > 0f
                && m_maxCount > 0;
        }

        public float GetEffectiveWeight(bool isFinalFrenzy, float finalFrenzyMultiplier)
        {
            float result = m_weight;
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
            bool isFinalFrenzyBoosted)
        {
            m_hotspotName = hotspotName;
            m_clusterType = clusterType;
            m_minCount = Mathf.Max(1, minCount);
            m_maxCount = Mathf.Max(m_minCount, maxCount);
            m_spawnRadius = Mathf.Max(0.1f, spawnRadius);
            m_weight = Mathf.Max(0f, weight);
            m_enableTime = Mathf.Max(0f, enableTime);
            m_isFinalFrenzyBoosted = isFinalFrenzyBoosted;
        }

        private void OnValidate()
        {
            m_minCount = Mathf.Max(1, m_minCount);
            m_maxCount = Mathf.Max(m_minCount, m_maxCount);
            m_spawnRadius = Mathf.Max(0.1f, m_spawnRadius);
            m_weight = Mathf.Max(0f, m_weight);
            m_enableTime = Mathf.Max(0f, m_enableTime);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = m_isFinalFrenzyBoosted
                ? new Color(1f, 0.55f, 0.15f, 0.35f)
                : new Color(0.2f, 0.75f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, m_spawnRadius);
        }
    }
}
