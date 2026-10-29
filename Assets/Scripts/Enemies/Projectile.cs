using Hollowmark.Player;
using Hollowmark.Visuals;
using UnityEngine;

namespace Hollowmark.Enemies
{
    /// <summary>A thrown ember. Moves in a straight line and hurts the hero on contact.</summary>
    public class Projectile : MonoBehaviour
    {
        Vector3 velocity;
        float damage;
        GameObject owner;
        float life = 4f;

        public static void Launch(Vector3 from, Vector3 target, float speed, float damage, GameObject owner)
        {
            var go = Prim.Make(PrimitiveType.Sphere, null, from, Vector3.one * 0.25f, new Color(1f, 0.5f, 0.1f), "Ember", false, true);
            var light = go.AddComponent<Light>();
            light.color = new Color(1f, 0.5f, 0.1f);
            light.range = 4f;
            light.intensity = 2f;
            var p = go.AddComponent<Projectile>();
            p.velocity = (target - from).normalized * speed;
            p.damage = damage;
            p.owner = owner;
        }

        void Update()
        {
            life -= Time.deltaTime;
            var step = velocity * Time.deltaTime;
            RaycastHit hit;
            if (Physics.SphereCast(transform.position, 0.12f, velocity.normalized, out hit, step.magnitude, ~0, QueryTriggerInteraction.Ignore)
                && (owner == null || !hit.transform.IsChildOf(owner.transform)))
            {
                var hero = hit.collider.GetComponentInParent<Hero>();
                if (hero != null) hero.Stats.TakeDamage(new DamageInfo { Amount = damage, From = transform.position, Source = owner });
                Effects.Puff(hit.point, new Color(1f, 0.6f, 0.2f), 0.5f, 0.3f);
                Destroy(gameObject);
                return;
            }
            transform.position += step;
            if (life <= 0f) Destroy(gameObject);
        }
    }
}
