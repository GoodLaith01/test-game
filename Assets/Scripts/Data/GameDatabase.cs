using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hollowmark.Data
{
    /// <summary>
    /// Loads every JSON file in Resources/Data once and exposes lookups.
    /// Content is validated on load so a typo in an id shows up as a console warning.
    /// </summary>
    public static class GameDatabase
    {
        public static BalanceConfig Balance { get; private set; }
        public static readonly Dictionary<string, WeaponDef> Weapons = new Dictionary<string, WeaponDef>();
        public static readonly Dictionary<string, ArmorDef> Armor = new Dictionary<string, ArmorDef>();
        public static readonly Dictionary<string, ItemDef> Items = new Dictionary<string, ItemDef>();
        public static readonly Dictionary<string, EnemyDef> Enemies = new Dictionary<string, EnemyDef>();
        public static readonly Dictionary<string, BossDef> Bosses = new Dictionary<string, BossDef>();
        public static readonly Dictionary<string, WeatherDef> Weathers = new Dictionary<string, WeatherDef>();
        public static readonly List<DungeonDef> Dungeons = new List<DungeonDef>();

        static bool loaded;

        public static void Load()
        {
            if (loaded) return;
            loaded = true;

            Balance = Read<BalanceConfig>("balance") ?? new BalanceConfig();
            Fill(Read<WeaponList>("weapons")?.items, Weapons, w => { w.Init(); return w.id; });
            Fill(Read<ArmorList>("armor")?.items, Armor, a => { a.Init(); return a.id; });
            Fill(Read<ItemList>("items")?.items, Items, i => { i.Init(); return i.id; });
            Fill(Read<EnemyList>("enemies")?.items, Enemies, e => { e.Init(); return e.id; });
            Fill(Read<BossList>("bosses")?.items, Bosses, b => { b.Init(); return b.id; });
            Fill(Read<WeatherList>("weather")?.items, Weathers, w => { w.Init(); return w.id; });

            var dungeons = Read<DungeonList>("dungeons");
            if (dungeons != null && dungeons.items != null)
            {
                foreach (var d in dungeons.items) { d.Init(); Dungeons.Add(d); }
            }

            Validate();
            Debug.Log(string.Format("[Hollowmark] Loaded {0} weapons, {1} armor, {2} items, {3} enemies, {4} bosses, {5} weathers, {6} dungeons",
                Weapons.Count, Armor.Count, Items.Count, Enemies.Count, Bosses.Count, Weathers.Count, Dungeons.Count));
        }

        static T Read<T>(string file) where T : class
        {
            var asset = Resources.Load<TextAsset>("Data/" + file);
            if (asset == null)
            {
                Debug.LogError("[Hollowmark] Missing data file Resources/Data/" + file + ".json");
                return null;
            }
            return JsonUtility.FromJson<T>(asset.text);
        }

        static void Fill<T>(T[] source, Dictionary<string, T> target, System.Func<T, string> init)
        {
            if (source == null) return;
            foreach (var entry in source)
            {
                var id = init(entry);
                if (string.IsNullOrEmpty(id)) { Debug.LogWarning("[Hollowmark] Entry without id in " + typeof(T).Name); continue; }
                if (target.ContainsKey(id)) Debug.LogWarning("[Hollowmark] Duplicate id " + id);
                target[id] = entry;
            }
        }

        static void Validate()
        {
            foreach (var d in Dungeons)
            {
                Check(d.enemies, Enemies, d.id + ".enemies");
                Check(d.weathers, Weathers, d.id + ".weathers");
                Check(d.supplyItems, Items, d.id + ".supplyItems");
                if (!string.IsNullOrEmpty(d.bossId) && !Bosses.ContainsKey(d.bossId))
                    Debug.LogWarning("[Hollowmark] Dungeon " + d.id + " has unknown boss " + d.bossId);
            }
            foreach (var e in Enemies.Values)
            {
                Check(e.weaponDrops, Weapons, e.id + ".weaponDrops");
                Check(e.armorDrops, Armor, e.id + ".armorDrops");
                Check(e.itemDrops, Items, e.id + ".itemDrops");
            }
        }

        static void Check<T>(string[] ids, Dictionary<string, T> table, string where)
        {
            if (ids == null) return;
            foreach (var id in ids)
                if (!table.ContainsKey(id)) Debug.LogWarning("[Hollowmark] Unknown id '" + id + "' in " + where);
        }

        public static WeaponDef Weapon(string id) { WeaponDef d; return id != null && Weapons.TryGetValue(id, out d) ? d : null; }
        public static ArmorDef ArmorPiece(string id) { ArmorDef d; return id != null && Armor.TryGetValue(id, out d) ? d : null; }
        public static ItemDef Item(string id) { ItemDef d; return id != null && Items.TryGetValue(id, out d) ? d : null; }
        public static EnemyDef Enemy(string id) { EnemyDef d; return id != null && Enemies.TryGetValue(id, out d) ? d : null; }
        public static BossDef Boss(string id) { BossDef d; return id != null && Bosses.TryGetValue(id, out d) ? d : null; }
        public static WeatherDef Weather(string id) { WeatherDef d; return id != null && Weathers.TryGetValue(id, out d) ? d : null; }
        public static DungeonDef Dungeon(string id) { return Dungeons.FirstOrDefault(d => d.id == id); }

        /// <summary>Weighted random pick, used for loot and weather.</summary>
        public static T PickWeighted<T>(IList<T> options, System.Func<T, float> weight)
        {
            if (options == null || options.Count == 0) return default(T);
            float total = 0f;
            foreach (var o in options) total += Mathf.Max(0f, weight(o));
            float roll = Random.value * total;
            foreach (var o in options)
            {
                roll -= Mathf.Max(0f, weight(o));
                if (roll <= 0f) return o;
            }
            return options[options.Count - 1];
        }

        public static WeaponDef RandomWeaponForTier(int tier)
        {
            var pool = Weapons.Values.Where(w => w.tier <= tier && w.tier >= tier - 2).ToList();
            return PickWeighted(pool, w => w.dropWeight * (w.tier == tier ? 1.5f : 1f));
        }

        public static ArmorDef RandomArmorForTier(int tier)
        {
            var pool = Armor.Values.Where(a => a.tier <= tier && a.tier >= tier - 2).ToList();
            return PickWeighted(pool, a => a.dropWeight);
        }
    }
}
