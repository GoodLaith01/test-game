using Hollowmark.Data;
using Hollowmark.Visuals;
using UnityEngine;

namespace Hollowmark.Player
{
    /// <summary>
    /// What a sleepless mind sees: whispers, and phantom creatures that rush the hero and vanish.
    /// Screen tint and camera sway are handled by the HUD and the camera.
    /// </summary>
    public class InsanityEffects : MonoBehaviour
    {
        static readonly string[] Whispers =
        {
            "...give up... join us...",
            "...they are behind you...",
            "...your village burns while you wander...",
            "...sleep... you need to sleep...",
            "...the Hollow King remembers your name...",
        };

        PlayerStats stats;
        float nextWhisper;

        void Awake() { stats = GetComponent<PlayerStats>(); }

        void Update()
        {
            if (stats.Dead || stats.Insanity < 45f) return;
            if (Time.time > nextWhisper)
            {
                nextWhisper = Time.time + Random.Range(20f, 45f) * (1.5f - stats.MindPenalty);
                if (stats.OnMessage != null) stats.OnMessage("<i>" + Whispers[Random.Range(0, Whispers.Length)] + "</i>");
            }
        }

        public void SpawnPhantom()
        {
            if (GameDatabase.Enemies.Count == 0) return;
            var list = new System.Collections.Generic.List<EnemyDef>(GameDatabase.Enemies.Values);
            var def = list[Random.Range(0, list.Count)];
            var go = new GameObject("Phantom");
            var offset = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * 9f;
            go.transform.position = transform.position + offset;
            var rig = CharacterBuilder.BuildCreature(go.transform, def.Shape, def.height, def.width,
                new Color(0.1f, 0.05f, 0.08f), new Color(1f, 0.1f, 0.1f), Random.Range(0, 99999));
            var p = go.AddComponent<Phantom>();
            p.Rig = rig;
            p.Target = transform;
        }
    }

    public class Phantom : MonoBehaviour
    {
        public CharacterRig Rig;
        public Transform Target;
        float life = 7f;

        void Update()
        {
            life -= Time.deltaTime;
            if (Target == null || life <= 0f) { Destroy(gameObject); return; }
            var to = Target.position - transform.position; to.y = 0f;
            if (to.magnitude < 1.6f)
            {
                Effects.Puff(transform.position + Vector3.up, new Color(0.1f, 0f, 0f), 1.2f, 0.6f);
                Destroy(gameObject);
                return;
            }
            transform.rotation = Quaternion.LookRotation(to);
            transform.position += to.normalized * 7f * Time.deltaTime;
            if (Rig != null) Rig.MoveSpeed = 7f;
        }
    }
}
