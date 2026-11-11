using System.Collections.Generic;
using System.Linq;
using Hollowmark.Data;
using Hollowmark.Enemies;
using Hollowmark.UI;
using UnityEngine;

namespace Hollowmark.Player
{
    /// <summary>
    /// The hero's RPG progress. Experience from kills, bosses, raids, quests and practice raises the character level.
    /// Each level gives a skill point (plus a bonus point every few levels) to spend on perks in the three skill trees.
    /// Upper tiers need points in that tree and a trained practice skill, so you have to both fight and train.
    /// </summary>
    public class Progression
    {
        public int Level = 1;
        public float Xp;
        public int Points;
        public readonly Dictionary<string, int> Ranks = new Dictionary<string, int>();

        // Timed effects of the active abilities (see Abilities.cs).
        public float WarCryUntil;
        public float DeadEyeUntil;
        public float UntouchableUntil;
        public bool LastStandReady = true;

        PlayerStats stats;
        public void Bind(PlayerStats s) { stats = s; }

        static SkillConfig C { get { return SkillDatabase.Config; } }

        public bool MaxLevel { get { return Level >= C.maxLevel; } }
        public float XpToNext { get { return XpFor(Level); } }
        public static float XpFor(int level) { return Mathf.Round(C.baseXp * Mathf.Pow(level, C.xpExponent)); }

        // ---------- Experience ----------

        public void AddXp(float amount, string reason)
        {
            if (amount <= 0f || MaxLevel || stats == null || stats.Dead) return;
            amount = Mathf.Round(amount);
            Xp += amount;
            if (!string.IsNullOrEmpty(reason)) stats.Say("<color=#c8a0ff>+" + amount + " XP</color> " + reason);
            int gained = 0;
            while (!MaxLevel && Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                gained++;
                Points++;
                if (C.bonusPointEvery > 0 && Level % C.bonusPointEvery == 0) Points++;
            }
            if (MaxLevel) Xp = 0f;
            if (gained == 0) return;
            stats.Heal(stats.MaxHealth);
            stats.Stamina = stats.MaxStamina;
            string sub = "You have " + Points + " skill point" + (Points == 1 ? "" : "s") + ". Press K to open your skills.";
            if (Hud.Instance != null) Hud.Instance.Banner("Level " + Level + "!", sub);
            stats.Say("<color=#ffd040>You reached level " + Level + "!</color> Health and stamina restored.");
        }

        public void OnKill(EnemyAI enemy)
        {
            if (enemy == null) return;
            float xp = C.killXpBase + enemy.MaxHealth * C.killXpPerHealth;
            var boss = enemy as BossAI;
            if (boss != null)
            {
                int tier = 1;
                var d = GameDatabase.Dungeons.Find(x => x.id == boss.DungeonId);
                if (d != null) tier = d.tier;
                xp += C.bossXpPerTier * tier;
            }
            string name = boss != null && boss.Boss != null ? boss.Boss.name : enemy.Def != null ? enemy.Def.name : "a creature";
            AddXp(xp, "for slaying " + name);
            float back = Sum(p => p.killStamina);
            if (back > 0f && stats != null) stats.Stamina = Mathf.Min(stats.MaxStamina, stats.Stamina + back);
        }

        /// <summary>A practice skill (Strength, Swordsmanship...) went up: a little character XP too.</summary>
        public void OnSkillLevel(Skill skill) { AddXp(C.skillLevelXp * skill.Level, "for training " + skill.Name); }

        // ---------- Perks ----------

        public int Rank(string perkId) { int r; return perkId != null && Ranks.TryGetValue(perkId, out r) ? r : 0; }
        public int PointsIn(string treeId) { return Ranks.Where(kv => { var p = SkillDatabase.Perk(kv.Key); return p != null && p.tree == treeId; }).Sum(kv => kv.Value); }
        public int Spent { get { return Ranks.Values.Sum(); } }

        public static int TierPoints(int tier) { var t = C.tierPoints; return t != null && tier < t.Length ? t[tier] : 0; }
        public static int TierSkill(int tier) { var t = C.tierSkillLevels; return t != null && tier < t.Length ? t[tier] : 1; }

        /// <summary>Returns null when the perk can be learned, otherwise the reason it can't.</summary>
        public string Blocker(PerkDef perk)
        {
            if (perk == null) return "unknown";
            if (Rank(perk.id) >= perk.maxRank) return "Mastered";
            int need = TierPoints(perk.tier);
            if (PointsIn(perk.tree) < need) return "Needs " + need + " points in " + (perk.Tree != null ? perk.Tree.name : perk.tree);
            int skillNeed = TierSkill(perk.tier);
            var skill = stats != null && perk.Tree != null ? stats.SkillByName(perk.Tree.skill) : null;
            if (skill != null && skill.Level < skillNeed) return "Needs " + skill.Name + " " + skillNeed;
            if (Points <= 0) return "No skill points";
            return null;
        }

        public bool Learn(PerkDef perk)
        {
            if (Blocker(perk) != null) return false;
            Points--;
            Ranks[perk.id] = Rank(perk.id) + 1;
            stats.Say("Learned " + perk.name + (perk.maxRank > 1 ? " (rank " + Rank(perk.id) + ")" : "") + ".");
            if (!string.IsNullOrEmpty(perk.ability))
            {
                var a = SkillDatabase.Ability(perk.ability);
                var abilities = stats.GetComponent<Abilities>();
                if (a != null && abilities != null) abilities.OnLearned(a);
            }
            return true;
        }

        public int RespecCost { get { return C.respecGoldPerLevel * Level; } }

        /// <summary>Forget every perk and get the points back.</summary>
        public void Respec()
        {
            Points += Spent;
            Ranks.Clear();
            var abilities = stats.GetComponent<Abilities>();
            if (abilities != null) abilities.ClearSlots();
            stats.Health = Mathf.Min(stats.Health, stats.MaxHealth);
            stats.Stamina = Mathf.Min(stats.Stamina, stats.MaxStamina);
        }

        public IEnumerable<AbilityDef> KnownAbilities
        {
            get
            {
                foreach (var kv in Ranks)
                {
                    var p = SkillDatabase.Perk(kv.Key);
                    var a = p != null ? SkillDatabase.Ability(p.ability) : null;
                    if (a != null && kv.Value > 0) yield return a;
                }
            }
        }

        public float Sum(System.Func<PerkDef, float> field)
        {
            float total = 0f;
            foreach (var kv in Ranks)
            {
                var p = SkillDatabase.Perk(kv.Key);
                if (p != null) total += field(p) * kv.Value;
            }
            return total;
        }

        // ---------- What the perks, levels and practice skills add up to ----------

        bool WarCry { get { return Time.time < WarCryUntil; } }
        float WarCryPower { get { var a = SkillDatabase.Ability("battle_cry"); return a != null ? a.power : 0.2f; } }
        float WarCryGuard { get { var a = SkillDatabase.Ability("battle_cry"); return a != null ? a.guard : 0.25f; } }
        int Marks { get { return stats != null ? stats.Marksmanship.Level - 1 : 0; } }
        int Athletics { get { return stats != null ? stats.Athletics.Level - 1 : 0; } }

        public float MeleeDamageMult { get { return (1f + Sum(p => p.meleeDamage)) * (WarCry ? 1f + WarCryPower : 1f); } }
        public float FirearmDamageMult { get { return (1f + Sum(p => p.firearmDamage) + 0.05f * Marks) * (WarCry ? 1f + WarCryPower : 1f); } }
        public float CritChance(bool melee) { return Mathf.Clamp01(C.baseCritChance + Sum(p => p.critChance) + (melee ? Sum(p => p.meleeCrit) : 0f)); }
        public float CritDamage { get { return C.baseCritDamage + Sum(p => p.critDamage); } }
        public float AttackSpeed { get { return 1f + Sum(p => p.attackSpeed); } }
        public float LifeSteal { get { return Sum(p => p.lifeSteal); } }
        public float ExecuteBonus { get { return Sum(p => p.executeDamage); } }
        public float SpreadMult { get { return Mathf.Max(0.15f, (1f - Sum(p => p.spread)) * (1f - 0.03f * Marks)); } }
        public float ReloadMult { get { return Mathf.Max(0.3f, (1f - Sum(p => p.reloadSpeed)) * (1f - 0.03f * Marks)); } }
        public float AmmoSave { get { return Mathf.Clamp01(Sum(p => p.ammoSave)); } }
        /// <summary>Share of a blocked hit that still gets through: 25%, minus Iron Guard.</summary>
        public float BlockFactor { get { return Mathf.Max(0.02f, 0.25f - Sum(p => p.blockReduction)); } }
        public float BlockStaminaMult { get { return Mathf.Max(0.2f, 1f - Sum(p => p.blockStamina)); } }
        public float MaxHealthBonus { get { return (Level - 1) * C.healthPerLevel + Sum(p => p.maxHealth); } }
        public float MaxStaminaBonus { get { return (Level - 1) * C.staminaPerLevel + Sum(p => p.maxStamina); } }
        public float StaminaRegenMult { get { return 1f + Sum(p => p.staminaRegen); } }
        public float HungerMult { get { return Mathf.Max(0.2f, 1f - Sum(p => p.hungerDrain)); } }
        public float ThirstMult { get { return Mathf.Max(0.2f, 1f - Sum(p => p.thirstDrain)); } }
        public float TemperatureMult { get { return Mathf.Max(0.1f, 1f - Sum(p => p.temperatureResist)); } }
        public float InsanityMult { get { return Mathf.Max(0.1f, 1f - Sum(p => p.insanityResist)); } }
        public float PoisonResist { get { return Mathf.Clamp01(Sum(p => p.poisonResist)); } }
        public float ArmorWearMult { get { return Mathf.Max(0.2f, 1f - Sum(p => p.armorWear)); } }
        public float DamageTakenMult { get { return WarCry ? 1f - WarCryGuard : 1f; } }
        public bool HasLastStand { get { return Sum(p => p.lastStand) > 0f; } }
        /// <summary>Athletics: cheaper sprinting, rolling and jumping, and a little more speed.</summary>
        public float MoveStaminaMult { get { return Mathf.Max(0.55f, 1f - 0.03f * Athletics); } }
        public float MoveSpeedMult { get { return 1f + Mathf.Min(0.15f, 0.01f * Athletics); } }
    }
}
