using UnityEngine;

namespace Hollowmark.Visuals
{
    /// <summary>Short-lived placeholder effects: gun smoke, tracers, hit sparks, slam warnings.</summary>
    public static class Effects
    {
        public static void Tracer(Vector3 from, Vector3 to)
        {
            var go = new GameObject("Tracer");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = 0.03f;
            lr.endWidth = 0.01f;
            lr.sharedMaterial = Prim.Mat(new Color(1f, 0.9f, 0.6f), true);
            Object.Destroy(go, 0.06f);
        }

        public static void MuzzleFlash(Vector3 at)
        {
            var go = new GameObject("MuzzleFlash");
            go.transform.position = at;
            var l = go.AddComponent<Light>();
            l.color = new Color(1f, 0.75f, 0.4f);
            l.range = 8f;
            l.intensity = 4f;
            Object.Destroy(go, 0.07f);
            Puff(at, new Color(0.85f, 0.85f, 0.85f), 0.5f, 1.2f);
        }

        public static void Puff(Vector3 at, Color c, float size, float life)
        {
            var go = Prim.Make(PrimitiveType.Sphere, null, at, Vector3.one * size, c, "Puff");
            go.AddComponent<Fader>().Life = life;
        }

        public static void HitSpark(Vector3 at, Color c)
        {
            for (int i = 0; i < 4; i++)
            {
                var go = Prim.Make(PrimitiveType.Cube, null, at + Random.insideUnitSphere * 0.2f, Vector3.one * 0.08f, c, "Spark", false, true);
                var f = go.AddComponent<Fader>();
                f.Life = 0.35f;
                f.Velocity = Random.insideUnitSphere * 3f + Vector3.up * 2f;
            }
        }

        /// <summary>A flat red disc on the ground showing where a boss slam will land.</summary>
        public static GameObject Warning(Vector3 at, float radius, float life)
        {
            var go = Prim.Make(PrimitiveType.Cylinder, null, at + Vector3.up * 0.03f, new Vector3(radius * 2f, 0.02f, radius * 2f), new Color(0.9f, 0.1f, 0.05f), "SlamWarning", false, true);
            Object.Destroy(go, life);
            return go;
        }
    }

    /// <summary>Grows, drifts and removes a temporary effect object.</summary>
    public class Fader : MonoBehaviour
    {
        public float Life = 1f;
        public Vector3 Velocity = Vector3.up * 0.5f;
        float age;
        Vector3 startScale;

        void Start() { startScale = transform.localScale; }

        void Update()
        {
            age += Time.deltaTime;
            transform.position += Velocity * Time.deltaTime;
            transform.localScale = startScale * (1f + age / Life);
            if (age >= Life) Destroy(gameObject);
        }
    }
}
