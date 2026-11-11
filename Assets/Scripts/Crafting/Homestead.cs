using System.Collections.Generic;
using System.Linq;
using Hollowmark.Data;
using Hollowmark.Player;
using UnityEngine;

namespace Hollowmark.Crafting
{
    /// <summary>
    /// What the hero has learned and built, and how the villagers' quests stand. It lives outside the
    /// world objects, so it survives the village being rebuilt every time the hero comes home.
    /// </summary>
    public class Homestead
    {
        static Homestead instance;
        public static Homestead Instance
        {
            get
            {
                if (instance == null) { GameDatabase.Load(); instance = new Homestead(); }
                return instance;
            }
        }

        public readonly HashSet<string> KnownRecipes = new HashSet<string>();
        public readonly HashSet<string> KnownBlueprints = new HashSet<string>();
        public readonly HashSet<string> Built = new HashSet<string>();
        public readonly HashSet<string> AcceptedQuests = new HashSet<string>();
        public readonly HashSet<string> CompletedQuests = new HashSet<string>();
        readonly Dictionary<string, float> lastUsed = new Dictionary<string, float>();   // world minutes
        readonly Dictionary<string, int> lastHarvest = new Dictionary<string, int>();    // day number

        /// <summary>Raised when something is built, so the village can redraw that plot.</summary>
        public System.Action<string> OnBuilt;

        Homestead()
        {
            foreach (var r in CraftingDatabase.Recipes) if (r.known) KnownRecipes.Add(r.id);
            foreach (var b in CraftingDatabase.Blueprints) if (b.known) KnownBlueprints.Add(b.id);
        }

        static Hero H { get { return Hero.Instance; } }
        static Inventory Inv { get { return Hero.Instance.Stats.Inventory; } }
        static void Say(string text) { if (Hero.Instance != null) Hero.Instance.Say(text); }

        public static bool Has(ItemStack[] stacks)
        {
            if (stacks == null) return true;
            foreach (var s in stacks) if (Inv.Count(s.id) < s.count) return false;
            return true;
        }

        static void Take(ItemStack[] stacks)
        {
            if (stacks == null) return;
            foreach (var s in stacks) Inv.TakeItem(s.id, s.count);
        }

        static void Give(ItemStack[] stacks)
        {
            if (stacks == null) return;
            foreach (var s in stacks) if (GameDatabase.Item(s.id) != null) Inv.AddItem(s.id, s.count);
        }

        // ---------- Cooking ----------

        public bool HasStation(RecipeDef r) { return string.IsNullOrEmpty(r.station) || Built.Contains(r.station); }

        public string CookProblem(RecipeDef r)
        {
            if (!KnownRecipes.Contains(r.id)) return "You don't know this recipe.";
            if (!HasStation(r)) return "Needs a " + CraftingDatabase.Blueprint(r.station).name + ".";
            if (!Has(r.ingredients)) return "Missing ingredients.";
            if (r.Result == null) return "Unknown dish.";
            return null;
        }

        public bool Cook(RecipeDef r)
        {
            var problem = CookProblem(r);
            if (problem != null) { Say(problem); return false; }
            Take(r.ingredients);
            Inv.AddItem(r.result, Mathf.Max(1, r.resultCount));
            GameManager.Instance.AdvanceTime(r.minutes);
            Say("You cook " + r.Name + (r.resultCount > 1 ? " x" + r.resultCount : "") + ".");
            return true;
        }

        // ---------- Building ----------

        public string BuildProblem(BlueprintDef b)
        {
            if (Built.Contains(b.id)) return "Already built.";
            if (!KnownBlueprints.Contains(b.id)) return "You need the blueprint first.";
            if (!Has(b.materials)) return "Missing materials.";
            if (Inv.Gold < b.gold) return "Not enough gold to pay the labourers.";
            return null;
        }

        public bool Build(BlueprintDef b)
        {
            var problem = BuildProblem(b);
            if (problem != null) { Say(problem); return false; }
            Take(b.materials);
            Inv.Gold -= b.gold;
            Inv.NotifyChanged();
            Built.Add(b.id);
            GameManager.Instance.AdvanceTime(b.buildHours * 60f);
            // Daily goods can be collected right away, and the first use is ready at once.
            Say("You build the " + b.name + ". It took " + b.buildHours.ToString("0") + " hours.");
            if (b.maxHealth > 0f) H.Stats.Heal(b.maxHealth);
            if (OnBuilt != null) OnBuilt(b.id);
            return true;
        }

        public float HoursUntilReady(BlueprintDef b)
        {
            float last;
            if (!lastUsed.TryGetValue(b.id, out last)) return 0f;
            return Mathf.Max(0f, b.cooldownHours - (GameManager.Instance.WorldMinutes - last) / 60f);
        }

        public bool CanHarvest(BlueprintDef b)
        {
            if (b.dailyItems == null || b.dailyItems.Length == 0) return false;
            int day;
            return !lastHarvest.TryGetValue(b.id, out day) || day < GameManager.Instance.Day;
        }

        /// <summary>Using a finished building: collect its daily goods and/or take its buff.</summary>
        public void Use(BlueprintDef b)
        {
            bool did = false;
            if (CanHarvest(b))
            {
                lastHarvest[b.id] = GameManager.Instance.Day;
                Give(b.dailyItems);
                Say("You collect " + string.Join(", ", b.dailyItems.Select(CraftingDatabase.StackName).ToArray()) + ".");
                did = true;
            }
            var buff = CraftingDatabase.Buff(b.useBuff);
            if (buff != null)
            {
                float wait = HoursUntilReady(b);
                if (wait > 0f) { if (!did) Say("The " + b.name + " will be ready again in " + Mathf.CeilToInt(wait) + " hours."); }
                else
                {
                    lastUsed[b.id] = GameManager.Instance.WorldMinutes;
                    if (!string.IsNullOrEmpty(b.useText)) Say(b.useText);
                    H.Stats.Buffs.Apply(buff, H.Stats);
                    GameManager.Instance.AdvanceTime(30f);
                    did = true;
                }
            }
            if (!did)
            {
                if (b.dailyItems != null && b.dailyItems.Length > 0) Say("Nothing more to collect today. Come back tomorrow.");
                else if (!string.IsNullOrEmpty(b.description)) Say(b.description);
            }
        }

        // Permanent bonuses from what is built.
        public float MaxHealthBonus { get { return BuiltDefs().Sum(b => b.maxHealth); } }
        public float MaxStaminaBonus { get { return BuiltDefs().Sum(b => b.maxStamina); } }
        public float PoisonResist
        {
            get
            {
                float keep = 1f;
                foreach (var b in BuiltDefs()) keep *= 1f - Mathf.Clamp01(b.poisonResist);
                return 1f - keep;
            }
        }

        IEnumerable<BlueprintDef> BuiltDefs()
        {
            foreach (var id in Built)
            {
                var b = CraftingDatabase.Blueprint(id);
                if (b != null) yield return b;
            }
        }

        // ---------- Learning ----------

        public void LearnRecipe(string id)
        {
            var r = CraftingDatabase.Recipe(id);
            if (r == null || !KnownRecipes.Add(id)) return;
            Say("New recipe: " + r.Name + "!");
        }

        public void LearnBlueprint(string id)
        {
            var b = CraftingDatabase.Blueprint(id);
            if (b == null || !KnownBlueprints.Add(id)) return;
            Say("New blueprint: " + b.name + "! Build it on your homestead.");
        }

        // ---------- Quests ----------

        public enum QuestState { None, Available, Active, Ready, Locked, AllDone }

        /// <summary>The quest a villager is offering or waiting on right now, or null when they have nothing.</summary>
        public QuestDef CurrentQuest(string giver, out QuestState state)
        {
            bool any = false;
            foreach (var q in CraftingDatabase.Quests)
            {
                if (q.giver != giver) continue;
                any = true;
                if (CompletedQuests.Contains(q.id)) continue;
                if (!string.IsNullOrEmpty(q.after) && !CompletedQuests.Contains(q.after)) { state = QuestState.Locked; return q; }
                if (!AcceptedQuests.Contains(q.id)) { state = QuestState.Available; return q; }
                state = CanHandIn(q) ? QuestState.Ready : QuestState.Active;
                return q;
            }
            state = any ? QuestState.AllDone : QuestState.None;
            return null;
        }

        public bool CanHandIn(QuestDef q)
        {
            if (!Has(q.deliver)) return false;
            if (!string.IsNullOrEmpty(q.build) && !Built.Contains(q.build)) return false;
            return true;
        }

        public void Accept(QuestDef q)
        {
            if (q == null || CompletedQuests.Contains(q.id) || !AcceptedQuests.Add(q.id)) return;
            Say("Quest accepted: " + q.title + ".");
        }

        public bool HandIn(QuestDef q)
        {
            if (q == null || !AcceptedQuests.Contains(q.id) || !CanHandIn(q)) return false;
            Take(q.deliver);
            AcceptedQuests.Remove(q.id);
            CompletedQuests.Add(q.id);
            Say("Quest complete: " + q.title + "!");
            if (q.rewardGold > 0) { Inv.Gold += q.rewardGold; Say(q.giver + " pays you " + q.rewardGold + " gold."); }
            Give(q.rewardItems);
            if (q.rewardItems != null && q.rewardItems.Length > 0)
                Say("You receive " + string.Join(", ", q.rewardItems.Select(CraftingDatabase.StackName).ToArray()) + ".");
            if (q.rewardRecipes != null) foreach (var id in q.rewardRecipes) LearnRecipe(id);
            if (q.rewardBlueprints != null) foreach (var id in q.rewardBlueprints) LearnBlueprint(id);
            Inv.NotifyChanged();
            return true;
        }

        /// <summary>One line per requirement, coloured green when it is met.</summary>
        public List<string> Progress(QuestDef q)
        {
            var lines = new List<string>();
            if (q.deliver != null)
                foreach (var s in q.deliver)
                {
                    var def = GameDatabase.Item(s.id);
                    int have = Mathf.Min(Inv.Count(s.id), s.count);
                    lines.Add(string.Format("<color={0}>{1} {2}/{3}</color>", have >= s.count ? "#90ff90" : "#ffd080", def != null ? def.name : s.id, have, s.count));
                }
            if (!string.IsNullOrEmpty(q.build))
            {
                var b = CraftingDatabase.Blueprint(q.build);
                bool done = Built.Contains(q.build);
                lines.Add(string.Format("<color={0}>Build {1} {2}</color>", done ? "#90ff90" : "#ffd080", b != null ? b.name : q.build, done ? "(done)" : ""));
            }
            return lines;
        }

        public string Rewards(QuestDef q)
        {
            var parts = new List<string>();
            if (q.rewardGold > 0) parts.Add(q.rewardGold + " gold");
            if (q.rewardItems != null) parts.AddRange(q.rewardItems.Select(CraftingDatabase.StackName));
            if (q.rewardRecipes != null)
                foreach (var id in q.rewardRecipes) { var r = CraftingDatabase.Recipe(id); if (r != null) parts.Add("recipe: " + r.Name); }
            if (q.rewardBlueprints != null)
                foreach (var id in q.rewardBlueprints) { var b = CraftingDatabase.Blueprint(id); if (b != null) parts.Add("blueprint: " + b.name); }
            return string.Join(", ", parts.ToArray());
        }
    }
}
