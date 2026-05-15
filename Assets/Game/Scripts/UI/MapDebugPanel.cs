using System.Collections.Generic;
using Game.Gameplay.Infection;
using Game.Gameplay.Wave;
using UnityEngine;

namespace Game.UI
{
    public class MapDebugPanel : MonoBehaviour
    {
        [Header("显示")]
        [SerializeField] private bool m_visible = false;
        [SerializeField] private KeyCode m_toggleKey = KeyCode.F4;
        [SerializeField] private Vector2 m_screenOffset = new Vector2(14f, 14f);
        [SerializeField, Min(360f)] private float m_width = 520f;

        [Header("依赖引用")]
        [SerializeField] private HumanClusterSpawner m_clusterSpawner;
        [SerializeField] private SpawnSystem m_spawnSystem;
        [SerializeField] private InfectionSystem m_infectionSystem;

        private GUIStyle m_labelStyle;
        private GUIStyle m_headerStyle;
        private GUIStyle m_warningStyle;

        public void Initialize(
            HumanClusterSpawner clusterSpawner,
            SpawnSystem spawnSystem,
            InfectionSystem infectionSystem)
        {
            m_clusterSpawner = clusterSpawner;
            m_spawnSystem = spawnSystem;
            m_infectionSystem = infectionSystem;
        }

        private void Update()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return;
#else
            if (m_toggleKey != KeyCode.None && Input.GetKeyDown(m_toggleKey))
            {
                m_visible = !m_visible;
            }
#endif
        }

        private void OnGUI()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return;
#else
            if (!m_visible)
            {
                return;
            }

            EnsureStyles();

            float width = Mathf.Min(m_width, Mathf.Max(360f, Screen.width - 20f));
            float x = Mathf.Max(m_screenOffset.x, Screen.width - width - m_screenOffset.x);
            float y = m_screenOffset.y;
            float lineHeight = 20f;
            int hotspotLineCount = GetHotspotLineCount();
            int totalLineCount = 15 + hotspotLineCount;

            GUI.Box(new Rect(x - 6f, y - 6f, width + 12f, lineHeight * totalLineCount + 16f), string.Empty);

            GUI.Label(new Rect(x, y, width, lineHeight), "=== MAP DEBUG (F4) ===", m_headerStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"启用热点: {GetEnabledHotspotCount()} / {GetTotalHotspotCount()}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"可感染Human: {GetActiveHumanCount()} / {GetHumanCapText()}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"僵尸数量: {GetActiveZombieCount()}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"最近 Hotspot: {GetLastHotspotName()}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"最近来源: {GetLastSpawnSource()}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"下次刷新: {GetSecondsUntilNextSpawnText()}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"距上次感染: {GetSecondsSinceLastInfectionText()}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"距上次成功刷人: {GetSecondsSinceLastSpawnText()}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"最近保底原因: {GetLastGuaranteeReasonText()}", m_labelStyle);
            y += lineHeight;

            int fallbackCount = m_clusterSpawner != null ? m_clusterSpawner.StrictFallbackCount : 0;
            GUI.Label(new Rect(x, y, width, lineHeight), $"本局补流次数: {fallbackCount}", m_labelStyle);
            y += lineHeight;

            int safeFailCount = m_clusterSpawner != null ? m_clusterSpawner.SafeSpawnFailCount : 0;
            GUI.Label(new Rect(x, y, width, lineHeight), $"安全刷怪失败: {safeFailCount}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"Final Frenzy: {GetFinalFrenzyText()}", m_labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"首次感染: {GetFirstInfectionTimeText()}", GetFirstInfectionStyle());
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"最长无感染: {GetMaxNoInfectionText()}", GetNoInfectionStyle());
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), $"断流状态: {GetFlowStateText()}", GetFlowStateStyle());
            y += lineHeight;

            GUI.Label(new Rect(x, y, width, lineHeight), "Hotspot 统计: 刷新次数 / 附近感染", m_headerStyle);
            y += lineHeight;

            DrawHotspotStats(x, ref y, width, lineHeight);
#endif
        }

        private int GetEnabledHotspotCount()
        {
            return m_clusterSpawner != null ? m_clusterSpawner.EnabledHotspotCount : 0;
        }

        private int GetTotalHotspotCount()
        {
            return m_clusterSpawner != null && m_clusterSpawner.Hotspots != null
                ? m_clusterSpawner.Hotspots.Count
                : 0;
        }

        private int GetActiveHumanCount()
        {
            if (m_clusterSpawner != null)
            {
                return m_clusterSpawner.CurrentAliveHumanCount;
            }

            return m_spawnSystem != null ? m_spawnSystem.ActiveHumanCount : 0;
        }

        private string GetHumanCapText()
        {
            return m_clusterSpawner != null ? m_clusterSpawner.CurrentHumanMaxCount.ToString() : "未绑定";
        }

        private int GetActiveZombieCount()
        {
            return m_infectionSystem != null ? m_infectionSystem.ActiveZombieCount : 0;
        }

        private string GetLastHotspotName()
        {
            return m_clusterSpawner != null ? m_clusterSpawner.LastSpawnHotspotName : "未绑定";
        }

        private string GetLastSpawnSource()
        {
            if (m_clusterSpawner == null)
            {
                return "未绑定";
            }

            Vector2 position = m_clusterSpawner.LastSpawnPosition;
            return $"{m_clusterSpawner.LastSpawnSourceText} / {m_clusterSpawner.LastSpawnDetailText} / ({position.x:F1}, {position.y:F1})";
        }

        private string GetSecondsUntilNextSpawnText()
        {
            return m_clusterSpawner != null ? $"{m_clusterSpawner.SecondsUntilNextSpawn:F1}s" : "未绑定";
        }

        private string GetFinalFrenzyText()
        {
            return m_clusterSpawner != null && m_clusterSpawner.IsFinalFrenzyActive ? "是" : "否";
        }

        private string GetFirstInfectionTimeText()
        {
            if (m_clusterSpawner == null)
            {
                return "未绑定";
            }

            return m_clusterSpawner.FirstInfectionTime >= 0f
                ? $"{m_clusterSpawner.FirstInfectionTime:F1}s"
                : "未发生";
        }

        private string GetMaxNoInfectionText()
        {
            return m_clusterSpawner != null
                ? $"{m_clusterSpawner.MaxNoInfectionDuration:F1}s"
                : "未绑定";
        }

        private string GetFlowStateText()
        {
            if (m_clusterSpawner == null)
            {
                return "未绑定";
            }

            switch (m_clusterSpawner.CurrentFlowState)
            {
                case InfectionFlowState.LightBreak:
                    return "轻度断流";
                case InfectionFlowState.SevereBreak:
                    return "严重断流";
                default:
                    return "正常";
            }
        }

        private GUIStyle GetFirstInfectionStyle()
        {
            if (m_clusterSpawner == null || m_clusterSpawner.FirstInfectionTime < 0f)
            {
                return m_warningStyle;
            }

            return m_clusterSpawner.FirstInfectionTime <= 10f ? m_labelStyle : m_warningStyle;
        }

        private GUIStyle GetNoInfectionStyle()
        {
            if (m_clusterSpawner == null)
            {
                return m_warningStyle;
            }

            return m_clusterSpawner.MaxNoInfectionDuration <= 5f ? m_labelStyle : m_warningStyle;
        }

        private GUIStyle GetFlowStateStyle()
        {
            if (m_clusterSpawner == null)
            {
                return m_warningStyle;
            }

            return m_clusterSpawner.CurrentFlowState == InfectionFlowState.Normal ? m_labelStyle : m_warningStyle;
        }

        private string GetSecondsSinceLastInfectionText()
        {
            return m_clusterSpawner != null ? $"{m_clusterSpawner.SecondsSinceLastInfection:F1}s" : "未绑定";
        }

        private string GetSecondsSinceLastSpawnText()
        {
            return m_clusterSpawner != null ? $"{m_clusterSpawner.SecondsSinceLastSuccessfulSpawn:F1}s" : "未绑定";
        }

        private string GetLastGuaranteeReasonText()
        {
            return m_clusterSpawner != null ? m_clusterSpawner.LastGuaranteeSpawnReason : "未绑定";
        }

        private int GetHotspotLineCount()
        {
            IReadOnlyList<HotspotDebugStats> stats = m_clusterSpawner != null ? m_clusterSpawner.HotspotStats : null;
            if (stats == null || stats.Count == 0)
            {
                return 1;
            }

            return Mathf.Min(stats.Count, 8) + (stats.Count > 8 ? 1 : 0);
        }

        private void DrawHotspotStats(float x, ref float y, float width, float lineHeight)
        {
            IReadOnlyList<HotspotDebugStats> stats = m_clusterSpawner != null ? m_clusterSpawner.HotspotStats : null;
            if (stats == null || stats.Count == 0)
            {
                GUI.Label(new Rect(x, y, width, lineHeight), "无 Hotspot 统计", m_labelStyle);
                y += lineHeight;
                return;
            }

            int maxRows = Mathf.Min(stats.Count, 8);
            for (int i = 0; i < maxRows; i++)
            {
                HotspotDebugStats item = stats[i];
                if (item == null)
                {
                    continue;
                }

                GUI.Label(
                    new Rect(x, y, width, lineHeight),
                    $"{item.HotspotName}: {item.SpawnCount} / {item.NearbyInfectionCount}",
                    m_labelStyle);
                y += lineHeight;
            }

            if (stats.Count > maxRows)
            {
                GUI.Label(new Rect(x, y, width, lineHeight), $"... 还有 {stats.Count - maxRows} 个 Hotspot", m_labelStyle);
                y += lineHeight;
            }
        }

        private void EnsureStyles()
        {
            if (m_labelStyle != null)
            {
                return;
            }

            m_labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            m_labelStyle.normal.textColor = Color.white;

            m_headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            m_headerStyle.normal.textColor = new Color(0.65f, 1f, 0.9f, 1f);

            m_warningStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            m_warningStyle.normal.textColor = new Color(1f, 0.72f, 0.28f, 1f);
        }
    }
}
