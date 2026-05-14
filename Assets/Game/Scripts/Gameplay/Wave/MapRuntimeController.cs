using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Enemy;
using UnityEngine;

namespace Game.Gameplay.Wave
{
    public class MapRuntimeController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpawnSystem m_spawnSystem;
        [SerializeField] private Transform m_playerTransform;
        [SerializeField] private Transform m_playerSpawnPoint;
        [SerializeField] private Collider2D[] m_blockers;

        [Header("Runtime")]
        [SerializeField] private bool m_resetPlayerOnSessionStart = true;
        [SerializeField] private bool m_resolveActorsAgainstBlockers = true;
        [SerializeField, Min(0.1f)] private float m_actorRadius = 0.45f;
        [SerializeField] private Vector2 m_mapMin = new Vector2(-20f, -20f);
        [SerializeField] private Vector2 m_mapMax = new Vector2(20f, 20f);

        public void Configure(
            SpawnSystem spawnSystem,
            Transform playerTransform,
            Transform playerSpawnPoint,
            Collider2D[] blockers,
            Vector2 mapMin,
            Vector2 mapMax)
        {
            m_spawnSystem = spawnSystem;
            m_playerTransform = playerTransform;
            m_playerSpawnPoint = playerSpawnPoint;
            m_blockers = blockers;
            m_mapMin = mapMin;
            m_mapMax = mapMax;
        }

        private void OnEnable()
        {
            GameEvents.OnSessionStateChanged -= HandleSessionStateChanged;
            GameEvents.OnSessionStateChanged += HandleSessionStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnSessionStateChanged -= HandleSessionStateChanged;
        }

        private void LateUpdate()
        {
            if (!m_resolveActorsAgainstBlockers)
            {
                return;
            }

            ResolveTransform(m_playerTransform, m_actorRadius);

            if (m_spawnSystem == null)
            {
                return;
            }

            IReadOnlyList<HumanUnit> humans = m_spawnSystem.ActiveHumans;
            if (humans == null)
            {
                return;
            }

            for (int i = 0; i < humans.Count; i++)
            {
                HumanUnit human = humans[i];
                if (human != null)
                {
                    ResolveTransform(human.transform, m_actorRadius);
                }
            }
        }

        private void HandleSessionStateChanged(SessionState state)
        {
            if (state == SessionState.Playing && m_resetPlayerOnSessionStart)
            {
                ResetPlayerToSpawnPoint();
            }
        }

        private void ResetPlayerToSpawnPoint()
        {
            if (m_playerTransform == null || m_playerSpawnPoint == null)
            {
                return;
            }

            Vector3 spawnPosition = m_playerSpawnPoint.position;
            m_playerTransform.position = new Vector3(spawnPosition.x, spawnPosition.y, m_playerTransform.position.z);
        }

        private void ResolveTransform(Transform target, float radius)
        {
            if (target == null)
            {
                return;
            }

            Vector3 world = target.position;
            Vector2 position = new Vector2(world.x, world.y);

            position.x = Mathf.Clamp(position.x, m_mapMin.x + radius, m_mapMax.x - radius);
            position.y = Mathf.Clamp(position.y, m_mapMin.y + radius, m_mapMax.y - radius);

            if (m_blockers != null)
            {
                for (int i = 0; i < m_blockers.Length; i++)
                {
                    if (m_blockers[i] is BoxCollider2D box && box.enabled)
                    {
                        position = ResolvePointFromBox(position, radius, box);
                    }
                }
            }

            target.position = new Vector3(position.x, position.y, world.z);
        }

        private static Vector2 ResolvePointFromBox(Vector2 worldPoint, float radius, BoxCollider2D box)
        {
            Transform boxTransform = box.transform;
            Vector2 localPoint = boxTransform.InverseTransformPoint(worldPoint);
            Vector2 center = box.offset;
            Vector2 halfSize = box.size * 0.5f;
            Vector3 scale = boxTransform.lossyScale;

            float localRadiusX = radius / Mathf.Max(0.001f, Mathf.Abs(scale.x));
            float localRadiusY = radius / Mathf.Max(0.001f, Mathf.Abs(scale.y));

            float minX = center.x - halfSize.x - localRadiusX;
            float maxX = center.x + halfSize.x + localRadiusX;
            float minY = center.y - halfSize.y - localRadiusY;
            float maxY = center.y + halfSize.y + localRadiusY;

            if (localPoint.x < minX || localPoint.x > maxX || localPoint.y < minY || localPoint.y > maxY)
            {
                return worldPoint;
            }

            float pushLeft = Mathf.Abs(localPoint.x - minX);
            float pushRight = Mathf.Abs(maxX - localPoint.x);
            float pushDown = Mathf.Abs(localPoint.y - minY);
            float pushUp = Mathf.Abs(maxY - localPoint.y);

            float smallest = Mathf.Min(pushLeft, pushRight, pushDown, pushUp);
            if (Mathf.Approximately(smallest, pushLeft))
            {
                localPoint.x = minX;
            }
            else if (Mathf.Approximately(smallest, pushRight))
            {
                localPoint.x = maxX;
            }
            else if (Mathf.Approximately(smallest, pushDown))
            {
                localPoint.y = minY;
            }
            else
            {
                localPoint.y = maxY;
            }

            return boxTransform.TransformPoint(localPoint);
        }
    }
}
