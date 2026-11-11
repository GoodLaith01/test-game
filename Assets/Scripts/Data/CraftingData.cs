using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Cooking, building and village quests. Content lives in Resources/Data:
//   crafting_items.json  ingredients, building materials, dungeon trophies and cooked meals (merged into the item table)
//   crafting.json        buffs, cooking recipes and building blueprints
//   quests.json          the villagers' mini quests
namespace Hollowmark.Data
{
    [Serializable]
    public class ItemStack
    {
        public string id;
        public int count = 1;
    }

    /// <summary>A timed effect from a meal or a building: healing, stamina, damage, protection or poison resistance.</summary>
    [Serializable]
    public class BuffDef
    {
        public string id;
        public string name;
        public string description;
        public float seconds = 180f;                 // real seconds of play
        public float healthPerSecond;
        public float sanityPerSecond;                // lowers insanity
        public float staminaRegenMultiplier = 1f;
        public float damageMultiplier = 1f;          // weapon damage dealt
        public float damageTakenMultiplier = 1f;     // below 1 = protection
        public float poisonResist;                   // 0..1, share of incoming poison ignored
        public bool curesPoison;
        public string color = "#FFFFFF";

        [NonSerialized] public Color Tint;
        public void Init() { Tint = EnumUtil.ParseColor(color, Color.white); }
    }

    [Serializable]
    public class RecipeDef
    {
        public string id;
        public string result;                        // item id
        public int resultCount = 1;
        public ItemStack[] ingredients;
        public float minutes = 30f;                  // game minutes spent cooking
        public string station;                       // blueprint id that must be built (empty = the basic hearth)
        public bool known;                           // known from the start
        public int price;                            // what Master Crowe asks for the recipe

        public ItemDef Result { get { return GameDatabase.Item(result); } }
        public string Name { get { var r = Result; return r != null ? r.name : result; } }
    }

    [Serializable]
    public class BlueprintDef
    {
        public string id;
        public string name;
        public string description;
        public ItemStack[] materials;
        public int gold;                             // labour cost when building
        public float buildHours = 2f;
        public bool known;
        public int price;                            // what Master Crowe asks for the plans
        // Permanent bonuses while built.
        public float maxHealth;
        public float maxStamina;
        public float poisonResist;
        // Using the finished building.
        public string useBuff;
        public float cooldownHours = 8f;
        public string useText;
        public ItemStack[] dailyItems;               // collected once per day
    }

    [Serializable]
    public class QuestDef
    {
        public string id;
        public string giver;
        public string title;
        public string text;
        public string after;                         // quest id that must be finished first
        public ItemStack[] deliver;                  // items handed over
        public string build;                         // blueprint that must be built
        public int rewardGold;
        public string[] rewardRecipes;
        public string[] rewardBlueprints;
        public ItemStack[] rewardItems;
    }

    [Serializable]
    public class CraftingConfig
    {
        public float peddlerMarkup = 6f;
        public string peddlerName = "Master Crowe";
        public string larderName = "Goody Pell";
        public BuffDef[] buffs;
        public RecipeDef[] recipes;
        public BlueprintDef[] blueprints;
    }

    [Serializable] public class QuestList { public QuestDef[] items; }

    public static class CraftingDatabase
    {
        public static CraftingConfig Config { get; private set; }
        public static readonly Dictionary<string, BuffDef> Buffs = new Dictionary<string, BuffDef>();
        public static readonly List<RecipeDef> Recipes = new List<RecipeDef>();
        public static readonly List<BlueprintDef> Blueprints = new List<BlueprintDef>();
        public static readonly List<QuestDef> Quests = new List<QuestDef>();

        /// <summary>Called by GameDatabase.Load after the base items exist.</summary>
        public static void Load()
        {
            var extra = Read<ItemList>("crafting_items");
            if (extra != null && extra.items != null)
                foreach (var i in extra.items)
                {
                    if (string.IsNullOrEmpty(i.id)) continue;
                    i.Init();
                    // These are sold by Goody Pell and Master Crowe, not by Widow Marsh.
                    if (!string.IsNullOrEmpty(i.vendor)) i.sold = false;
                    if (GameDatabase.Items.ContainsKey(i.id)) Debug.LogWarning("[Hollowmark] Duplicate item id " + i.id);
                    GameDatabase.Items[i.id] = i;
                }

            Config = Read<CraftingConfig>("crafting") ?? new CraftingConfig();
            if (Config.buffs != null) foreach (var b in Config.buffs) if (!string.IsNullOrEmpty(b.id)) { b.Init(); Buffs[b.id] = b; }
            if (Config.recipes != null) Recipes.AddRange(Config.recipes.Where(r => r != null && !string.IsNullOrEmpty(r.id)));
            if (Config.blueprints != null) Blueprints.AddRange(Config.blueprints.Where(b => b != null && !string.IsNullOrEmpty(b.id)));
            var quests = Read<QuestList>("quests");
            if (quests != null && quests.items != null) Quests.AddRange(quests.items.Where(q => q != null && !string.IsNullOrEmpty(q.id)));
            Validate();
        }

        static T Read<T>(string file) where T : class
        {
            var asset = Resources.Load<TextAsset>("Data/" + file);
            if (asset == null) { Debug.LogWarning("[Hollowmark] Missing data file Resources/Data/" + file + ".json"); return null; }
            try { return JsonUtility.FromJson<T>(asset.text); }
            catch (Exception e) { Debug.LogError("[Hollowmark] Could not read " + file + ".json: " + e.Message); return null; }
        }

        static void Validate()
        {
            foreach (var i in GameDatabase.Items.Values)
                if (!string.IsNullOrEmpty(i.buff) && !Buffs.ContainsKey(i.buff)) Warn("item " + i.id + " has unknown buff " + i.buff);
            foreach (var r in Recipes)
            {
                if (r.Result == null) Warn("recipe " + r.id + " makes unknown item " + r.result);
                CheckStacks(r.ingredients, "recipe " + r.id);
                if (!string.IsNullOrEmpty(r.station) && Blueprint(r.station) == null) Warn("recipe " + r.id + " needs unknown station " + r.station);
            }
            foreach (var b in Blueprints)
            {
                CheckStacks(b.materials, "blueprint " + b.id);
                CheckStacks(b.dailyItems, "blueprint " + b.id);
                if (!string.IsNullOrEmpty(b.useBuff) && !Buffs.ContainsKey(b.useBuff)) Warn("blueprint " + b.id + " has unknown buff " + b.useBuff);
            }
            foreach (var q in Quests)
            {
                CheckStacks(q.deliver, "quest " + q.id);
                CheckStacks(q.rewardItems, "quest " + q.id);
                if (!string.IsNullOrEmpty(q.build) && Blueprint(q.build) == null) Warn("quest " + q.id + " needs unknown building " + q.build);
                if (!string.IsNullOrEmpty(q.after) && Quest(q.after) == null) Warn("quest " + q.id + " comes after unknown quest " + q.after);
                if (q.rewardRecipes != null) foreach (var id in q.rewardRecipes) if (Recipe(id) == null) Warn("quest " + q.id + " rewards unknown recipe " + id);
                if (q.rewardBlueprints != null) foreach (var id in q.rewardBlueprints) if (Blueprint(id) == null) Warn("quest " + q.id + " rewards unknown blueprint " + id);
            }
        }

        static void CheckStacks(ItemStack[] stacks, string where)
        {
            if (stacks == null) return;
            foreach (var s in stacks) if (s == null || GameDatabase.Item(s.id) == null) Warn(where + " uses unknown item " + (s == null ? "null" : s.id));
        }

        static void Warn(string text) { Debug.LogWarning("[Hollowmark] " + text); }

        public static BuffDef Buff(string id) { BuffDef b; return id != null && Buffs.TryGetValue(id, out b) ? b : null; }
        public static RecipeDef Recipe(string id) { return Recipes.FirstOrDefault(r => r.id == id); }
        public static BlueprintDef Blueprint(string id) { return Blueprints.FirstOrDefault(b => b.id == id); }
        public static QuestDef Quest(string id) { return Quests.FirstOrDefault(q => q.id == id); }

        public static string StackName(ItemStack s)
        {
            var def = GameDatabase.Item(s.id);
            return (def != null ? def.name : s.id) + (s.count > 1 ? " x" + s.count : "");
        }
    }
}
