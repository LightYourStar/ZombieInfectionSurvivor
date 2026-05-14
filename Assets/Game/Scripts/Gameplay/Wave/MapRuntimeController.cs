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

        public Vector2 ClampToMap(Vector2 position, float padding = 0f)
        {
            return new Vector2(
                Mathf.Clamp(position.x, m_mapMin.x + padding, m_mapMax.x - padding),
                Mathf.Clamp(position.y, m_mapMin.y + padding, m_mapMax.y - padding));
        }

        public bool IsPointBlocked(Vector2 worldPoint, float radius = 0f)
        {
            if (m_blockers == null)
            {
                return false;
            }

            for (int i = 0; i < m_blockers.Length; i++)
            {
                if (m_blockers[i] is BoxCollider2D box && box.enabled &&
                    IsPointInsideExpandedBox(worldPoint, radius, box))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasDirectPath(Vector2 from, Vector2 to, float clearance = 0f)
        {
            if (m_blockers == null)
            {
                return true;
            }

            for (int i = 0; i < m_blockers.Length; i++)
            {
                if (m_blockers[i] is BoxCollider2D box && box.enabled &&
                    SegmentIntersectsExpandedBox(from, to, clearance, box))
                {
                    return false;
                }
            }

            return true;
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

            position = ClampToMap(position, radius);

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

        private static bool IsPointInsideExpandedBox(Vector2 worldPoint, float radius, BoxCollider2D box)
        {
            Transform boxTransform = box.transform;
            Vector2 localPoint = boxTransform.InverseTransformPoint(worldPoint);
            Vector2 center = box.offset;
            Vector2 halfSize = box.size * 0.5f;
            Vector3 scale = boxTransform.lossyScale;

            float localRadiusX = radius / Mathf.Max(0.001f, Mathf.Abs(scale.x));
            float localRadiusY = radius / Mathf.Max(0.001f, Mathf.Abs(scale.y));

            return localPoint.x >= center.x - halfSize.x - localRadiusX
                && localPoint.x <= center.x + halfSize.x + localRadiusX
                && localPoint.y >= center.y - halfSize.y - localRadiusY
                && localPoint.y <= center.y + halfSize.y + localRadiusY;
        }

        private static bool SegmentIntersectsExpandedBox(Vector2 from, Vector2 to, float clearance, BoxCollider2D box)
        {
            Transform boxTransform = box.transform;
            Vector2 localFrom = boxTransform.InverseTransformPoint(from);
            Vector2 localTo = boxTransform.InverseTransformPoint(to);
            Vector2 direction = localTo - localFrom;

            Vector2 center = box.offset;
            Vector2 halfSize = box.size * 0.5f;
            Vector3 scale = boxTransform.lossyScale;

            float localClearanceX = clearance / Mathf.Max(0.001f, Mathf.Abs(scale.x));
            float localClearanceY = clearance / Mathf.Max(0.001f, Mathf.Abs(scale.y));
            Vector2 min = center - halfSize - new Vector2(localClearanceX, localClearanceY);
            Vector2 max = center + halfSize + new Vector2(localClearanceX, localClearanceY);

            if (localFrom.x >= min.x && localFrom.x <= max.x && localFrom.y >= min.y && localFrom.y <= max.y)
            {
                return true;
            }
            if (localTo.x >= min.x && localTo.x <= max.x && localTo.y >= min.y && localTo.y <= max.y)
            {
                return true;
            }

            float tMin = 0f;
            float tMax = 1f;
            if (!ClipSegmentAxis(localFrom.x, direction.x, min.x, max.x, ref tMin, ref tMax))
            {
                return false;
            }
            if (!ClipSegmentAxis(localFrom.y, direction.y, min.y, max.y, ref tMin, ref tMax))
            {
                return false;
            }

            return true;
        }

        private static bool ClipSegmentAxis(float start, float direction, float min, float max, ref float tMin, ref float tMax)
        {
            if (Mathf.Abs(direction) < 0.0001f)
            {
                return start >= min && start <= max;
            }

            float inv = 1f / direction;
            float t1 = (min - start) * inv;
            float t2 = (max - start) * inv;
            if (t1 > t2)
            {
                float temp = t1;
                t1 = t2;
                t2 = temp;
            }

            tMin = Mathf.Max(tMin, t1);
            tMax = Mathf.Min(tMax, t2);
            return tMin <= tMax;
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
