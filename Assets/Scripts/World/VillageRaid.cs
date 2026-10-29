using System.Collections.Generic;
using System.Linq;
using Hollowmark.Data;
using Hollowmark.Enemies;
using Hollowmark.Player;
using Hollowmark.UI;
using Hollowmark.Visuals;
using UnityEngine;

namespace Hollowmark.World
{
    /// <summary>A villager who wanders the square. During a raid, one who gives up turns into a Hollowed.</summary>
    public class Villager : MonoBehaviour
    {
        static readonly string[] Names = { "Old Tom", "Martha", "Young Will", "Goodwife Ann", "Silas the Cooper", "Ezra", "Prudence", "Josiah" };
        public string VillagerName;
        public CharacterRig Rig;
        Vector3 home, target;
        float wait;

        public static Villager Spawn(Transform root, Vector3 pos, int seed)
        {
            var go = new GameObject("Villager");
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            Color skin;
            var rig = CharacterBuilder.BuildHuman(go.transform, Random.Range(1.6f, 1.85f), seed, out skin);
            var coat = Prim.Vary(new Color(0.4f, 0.3f, 0.25f), 0.3f);
            Prim.Make(PrimitiveType.Cube, rig.Body, new Vector3(0f, 1.23f, 0f), new Vector3(0.5f, 0.68f, 0.3f) * (rig.Height / 1.8f), coat, "Coat");
            Prim.Make(PrimitiveType.Cube, rig.Body, new Vector3(0f, 0.6f, 0f), new Vector3(0.48f, 0.6f, 0.28f) * (rig.Height / 1.8f), coat * 0.7f, "Breeches");
            var v = go.AddComponent<Villager>();
            v.Rig = rig;
            v.VillagerName = Names[seed % Names.Length];
            v.home = pos;
            v.target = pos;
            return v;
        }

        void Update()
        {
            var to = target - transform.localPosition; to.y = 0f;
            if (to.magnitude < 0.3f)
            {
                Rig.MoveSpeed = 0f;
                wait -= Time.deltaTime;
                if (wait <= 0f)
                {
                    target = home + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
                    wait = Random.Range(2f, 6f);
                }
                return;
            }
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.LookRotation(to), Time.deltaTime * 5f);
            transform.localPosition += to.normalized * 1.2f * Time.deltaTime;
            Rig.MoveSpeed = 1.2f;
        }
    }

    /// <summary>
    /// When the threat meter fills, the bosses' creatures attack the village. Kill them all to push the threat back.
    /// One villager despairs and turns into a monster, as the lore says some humans do.
    /// </summary>
    public class VillageRaid : MonoBehaviour
    {
        readonly List<EnemyAI> raiders = new List<EnemyAI>();
        float startedAt;

        public static VillageRaid Begin(Transform root, int tier)
        {
            var go = new GameObject("VillageRaid");
            go.transform.SetParent(root, false);
            var raid = go.AddComponent<VillageRaid>();
            raid.Spawn(tier);
            return raid;
        }

        void Spawn(int tier)
        {
            startedAt = Time.time;
            var pool = GameDatabase.Dungeons.Where(d => d.tier <= tier && d.enemies != null).SelectMany(d => d.enemies).Distinct()
                .Select(GameDatabase.Enemy).Where(e => e != null).ToList();
            if (pool.Count == 0) return;
            int count = 4 + tier * 2;
            for (int i = 0; i < count; i++)
            {
                var def = pool[Random.Range(0, pool.Count)];
                var pos = new Vector3(Random.Range(-20f, 20f), 0.5f, Random.Range(28f, 38f));
                var e = EnemyAI.Spawn(def, pos, tier, transform);
                e.Reveal();
                raiders.Add(e);
            }

            // Someone gives up.
            var villagers = transform.parent.GetComponentsInChildren<Villager>();
            var hollowDef = GameDatabase.Enemy("hollowed");
            if (villagers.Length > 0 && hollowDef != null)
            {
                var v = villagers[Random.Range(0, villagers.Length)];
                Effects.Puff(v.transform.position + Vector3.up, new Color(0.15f, 0.1f, 0.15f), 2f, 1f);
                var turned = EnemyAI.Spawn(hollowDef, v.transform.position + Vector3.up * 0.2f, tier, transform);
                turned.Reveal();
                raiders.Add(turned);
                Hud.Push(v.VillagerName + " gave up hope and turned into a Hollowed!");
                Destroy(v.gameObject);
            }

            Hud.Instance.Banner("The village is under attack!", "The bosses have sent their creatures. Drive them out!");
        }

        void Update()
        {
            raiders.RemoveAll(r => r == null || !r.IsAlive);
            if (raiders.Count == 0 && Time.time - startedAt > 1f)
            {
                GameManager.Instance.Threat = GameDatabase.Balance.threatAfterRaid;
                Hud.Instance.Banner("The raid is over", "Hollowmark stands, for now. Strike at the bosses to keep it safe.");
                if (Hero.Instance != null) { Hero.Instance.Stats.Inventory.Gold += 50; Hero.Instance.Say("The grateful villagers give you 50 gold."); }
                GameManager.Instance.Raid = null;
                Destroy(gameObject);
            }
        }
    }
}
