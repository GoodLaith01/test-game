using UnityEngine;

namespace Hollowmark
{
    public struct DamageInfo
    {
        public float Amount;
        public Vector3 From;
        public GameObject Source;
        public bool IsPlayer;      // dealt by the player
        public bool IsMelee;
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(DamageInfo info);
    }

    public static class Layers
    {
        /// <summary>Finds all damageable things in a cone in front of an attacker.</summary>
        public static int HitCone(Vector3 origin, Vector3 forward, float range, float arcDegrees, DamageInfo info, GameObject ignore)
        {
            int hits = 0;
            var cols = Physics.OverlapSphere(origin, range, ~0, QueryTriggerInteraction.Collide);
            var done = new System.Collections.Generic.HashSet<IDamageable>();
            foreach (var c in cols)
            {
                if (ignore != null && c.transform.IsChildOf(ignore.transform)) continue;
                var d = c.GetComponentInParent<IDamageable>();
                if (d == null || !d.IsAlive || done.Contains(d)) continue;
                // Collider.ClosestPoint does not support CharacterControllers (it logs an error), so use bounds for those.
                var closest = c is CharacterController || c is MeshCollider ? c.bounds.ClosestPoint(origin) : c.ClosestPoint(origin);
                var to = closest - origin;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(forward, to) > arcDegrees * 0.5f) continue;
                done.Add(d);
                d.TakeDamage(info);
                hits++;
            }
            return hits;
        }

        /// <summary>
        /// Casts a ray and returns the first thing it really hits: solid geometry, or a damageable hitbox.
        /// Interaction triggers (shops, gates, loot, tents) and the shooter's own colliders are skipped,
        /// so bullets and aim rays never stop on invisible volumes or on the hero.
        /// </summary>
        public static bool RaycastSolid(Vector3 origin, Vector3 dir, float range, Transform ignore, out RaycastHit result)
        {
            result = default(RaycastHit);
            var hits = Physics.RaycastAll(origin, dir, range, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (ignore != null && h.collider.transform.IsChildOf(ignore)) continue;
                if (h.collider.isTrigger && h.collider.GetComponentInParent<IDamageable>() == null) continue;
                result = h;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Finds a spot near <paramref name="center"/> where a body of the given size fits without overlapping walls,
        /// pillars or furniture. Falls back to the center if nothing free is found.
        /// </summary>
        public static Vector3 ClearSpot(Vector3 center, Vector2 halfExtents, float radius, float height, int tries = 16)
        {
            Physics.SyncTransforms(); // freshly built geometry must be visible to the query
            for (int i = 0; i < tries; i++)
            {
                var p = center + new Vector3(Random.Range(-halfExtents.x, halfExtents.x), 0f, Random.Range(-halfExtents.y, halfExtents.y));
                if (Fits(p, radius, height)) return p;
            }
            return center;
        }

        public static bool Fits(Vector3 feet, float radius, float height)
        {
            var bottom = feet + Vector3.up * (radius + 0.15f); // clear of the floor
            var top = feet + Vector3.up * Mathf.Max(radius + 0.15f, height - radius);
            return !Physics.CheckCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);
        }
    }
}
