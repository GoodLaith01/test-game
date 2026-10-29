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
                var to = c.ClosestPoint(origin) - origin;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(forward, to) > arcDegrees * 0.5f) continue;
                done.Add(d);
                d.TakeDamage(info);
                hits++;
            }
            return hits;
        }
    }
}
