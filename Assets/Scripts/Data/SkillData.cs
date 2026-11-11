using System;
using System.Collections.Generic;
using UnityEngine;

// The RPG layer: character levels, skill trees with perks, and active abilities.
// Content lives in Resources/Data/skills.json, so every number can be tuned without touching code.
namespace Hollowmark.Data
{
    [Serializable]
    public class SkillTreeDef
    {
        public string id;
        public string name;
        public string description;
        public string skill;                 // practice skill that gates the upper tiers (Swordsmanship, Marksmanship, Endurance...)
        public string color = "#FFFFFF";

        [NonSerialized] public Color Tint;
        public void Init() { Tint = EnumUtil.ParseColor(color, Color.white); }
    }

    /// <summary>
    /// One perk in a skill tree. Every effect field is the bonus for ONE rank; ranks add up.
    /// Fields left out of the JSON are 0 and do nothing.
    /// </summary>
    [Serializable]
    public class PerkDef
    {
        public string id;
        public string tree;
        public string name;
        public string description;
        public int tier;                     // 0..3; needs SkillConfig.tierPoints[tier] points in the tree
        public int maxRank = 1;
        public string ability;               // unlocks an active ability

        // Offence
        public float meleeDamage;            // +share of melee damage
        public float firearmDamage;          // +share of firearm damage
        public float critChance;             // +chance, every weapon
        public float meleeCrit;              // +chance, melee only
        public float critDamage;             // +critical multiplier
        public float attackSpeed;            // share faster swings
        public float lifeSteal;              // share of melee damage healed
        public float killStamina;            // stamina back on each kill
        public float executeDamage;          // +share against enemies below 30% health
        // Firearms
        public float spread;                 // share less spread
        public float reloadSpeed;            // share faster reloads
        public float ammoSave;               // chance a reload is free
        // Defence and survival
        public float blockReduction;         // extra share of blocked damage removed
        public float blockStamina;           // share less stamina per block
        public float maxHealth;
        public float maxStamina;
        public float staminaRegen;           // +share regeneration
        public float hungerDrain;            // share slower
        public float thirstDrain;            // share slower
        public float temperatureResist;      // share less cold and heat exposure
        public float insanityResist;         // share slower insanity
        public float poisonResist;           // share of poison ignored
        public float armorWear;              // share slower armor wear
        public float lastStand;              // > 0: survive one killing blow per sleep

        [NonSerialized] public SkillTreeDef Tree;
    }

    [Serializable]
    public class AbilityDef
    {
        public string id;
        public string name;
        public string description;
        public float stamina;
        public float cooldown = 10f;
        public float power = 1f;             // damage multiplier, flat damage or heal share, depending on the ability
        public float guard;                  // share less damage taken while the effect lasts
        public float radius;
        public float seconds;                // how long the effect lasts
        public float stun;                   // seconds creatures are stunned
        public string needs;                 // "Melee", "Firearm" or empty for any weapon
    }

    [Serializable]
    public class SkillConfig
    {
        public int maxLevel = 30;
        public float baseXp = 80f;
        public float xpExponent = 1.45f;
        public float healthPerLevel = 5f;
        public float staminaPerLevel = 3f;
        public int bonusPointEvery = 5;
        public int[] tierPoints = { 0, 3, 6, 10 };
        public int[] tierSkillLevels = { 1, 1, 3, 6 };
        public float baseCritChance = 0.03f;
        public float baseCritDamage = 1.5f;
        public float killXpBase = 8f;
        public float killXpPerHealth = 0.45f;
        public float bossXpPerTier = 250f;
        public float raidXp = 200f;
        public float questXpBase = 60f;
        public float questXpPerGold = 0.5f;
        public float skillLevelXp = 15f;
        public int respecGoldPerLevel = 40;

        public SkillTreeDef[] trees;
        public PerkDef[] perks;
        public AbilityDef[] abilities;
    }

    /// <summary>Loads skills.json on first use.</summary>
    public static class SkillDatabase
    {
        static SkillConfig config;
        static readonly Dictionary<string, PerkDef> perks = new Dictionary<string, PerkDef>();
        static readonly Dictionary<string, AbilityDef> abilities = new Dictionary<string, AbilityDef>();
        static readonly List<SkillTreeDef> trees = new List<SkillTreeDef>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { config = null; perks.Clear(); abilities.Clear(); trees.Clear(); }

        public static SkillConfig Config { get { Load(); return config; } }
        public static List<SkillTreeDef> Trees { get { Load(); return trees; } }
        public static IEnumerable<PerkDef> Perks { get { Load(); return perks.Values; } }

        public static PerkDef Perk(string id) { Load(); PerkDef p; return id != null && perks.TryGetValue(id, out p) ? p : null; }
        public static AbilityDef Ability(string id) { Load(); AbilityDef a; return id != null && abilities.TryGetValue(id, out a) ? a : null; }
        public static SkillTreeDef Tree(string id) { Load(); return trees.Find(t => t.id == id); }

        static void Load()
        {
            if (config != null) return;
            var asset = Resources.Load<TextAsset>("Data/skills");
            if (asset == null) Debug.LogError("[Hollowmark] Missing data file Resources/Data/skills.json");
            config = (asset != null ? JsonUtility.FromJson<SkillConfig>(asset.text) : null) ?? new SkillConfig();
            if (config.trees != null) foreach (var t in config.trees) { t.Init(); trees.Add(t); }
            if (config.abilities != null) foreach (var a in config.abilities) if (!string.IsNullOrEmpty(a.id)) abilities[a.id] = a;
            if (config.perks != null)
                foreach (var p in config.perks)
                {
                    if (string.IsNullOrEmpty(p.id)) continue;
                    if (perks.ContainsKey(p.id)) Debug.LogWarning("[Hollowmark] Duplicate perk " + p.id);
                    p.Tree = trees.Find(t => t.id == p.tree);
                    if (p.Tree == null) Debug.LogWarning("[Hollowmark] Perk " + p.id + " is in unknown tree " + p.tree);
                    if (!string.IsNullOrEmpty(p.ability) && !abilities.ContainsKey(p.ability)) Debug.LogWarning("[Hollowmark] Perk " + p.id + " unlocks unknown ability " + p.ability);
                    p.maxRank = Mathf.Max(1, p.maxRank);
                    p.tier = Mathf.Clamp(p.tier, 0, config.tierPoints.Length - 1);
                    perks[p.id] = p;
                }
        }
    }
}
