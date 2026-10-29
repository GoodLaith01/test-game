using System.Linq;
using Hollowmark.Data;
using Hollowmark.Enemies;
using Hollowmark.Player;
using Hollowmark.UI;
using UnityEngine;

namespace Hollowmark.World
{
    /// <summary>Anything the hero can use with E. The hero picks the nearest one in reach.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public virtual bool HoldToUse { get { return false; } }
        public virtual bool CanUse(Hero hero) { return true; }
        public abstract string Prompt(Hero hero);
        public abstract void Interact(Hero hero);
        public virtual void HoldTick(Hero hero, float dt) { }
    }

    /// <summary>The hero's own bed at home: a full night's sleep.</summary>
    public class Bed : Interactable
    {
        public override string Prompt(Hero hero) { return "Sleep until morning (restores stamina, calms the mind)"; }
        public override void Interact(Hero hero)
        {
            // Sleep until 7 in the morning, or a full 8 hours if it is already close to morning.
            float hours = GameManager.Instance.HoursUntil(7f);
            if (hours < 6f) hours = 8f;
            GameManager.Instance.Sleep(hours, true);
        }
    }

    /// <summary>A camp tent inside a dungeon. Sleeping here resets stamina and insanity, but not with monsters nearby.</summary>
    public class Tent : Interactable
    {
        public float Hours = 6f;
        public override string Prompt(Hero hero) { return "Sleep in the tent (" + Hours + " hours)"; }
        public override void Interact(Hero hero)
        {
            var near = EnemyAI.All.Any(e => e.IsAlive && e.Current != EnemyAI.State.Hidden
                && Vector3.Distance(e.transform.position, transform.position) < 15f);
            if (near) { hero.Say("You can't sleep with monsters nearby!"); return; }
            GameManager.Instance.Sleep(Hours, false);
        }
    }

    /// <summary>A campfire next to the tents: warms the hero up quickly.</summary>
    public class Campfire : Interactable
    {
        public override string Prompt(Hero hero) { return "Warm yourself by the fire"; }
        public override void Interact(Hero hero)
        {
            hero.Stats.BodyTemp = Mathf.Max(hero.Stats.BodyTemp, 37f);
            hero.Say("The fire warms your bones.");
        }

        void Update()
        {
            var hero = Hero.Instance;
            if (hero != null && Vector3.Distance(hero.transform.position, transform.position) < 4f && hero.Stats.BodyTemp < 37f)
                hero.Stats.BodyTemp = Mathf.MoveTowards(hero.Stats.BodyTemp, 37f, Time.deltaTime * 0.2f);
        }
    }

    public class ShopStall : Interactable
    {
        public ShopKind Kind;
        public string Keeper;
        public override string Prompt(Hero hero) { return "Trade with " + Keeper; }
        public override void Interact(Hero hero) { Hud.Instance.OpenShop(this); }
    }

    /// <summary>The village well: free water.</summary>
    public class Well : Interactable
    {
        public override string Prompt(Hero hero) { return "Drink from the well / fill a flask (2 gold)"; }
        public override void Interact(Hero hero)
        {
            hero.Stats.Thirst = 100f;
            var inv = hero.Stats.Inventory;
            if (inv.Gold >= 2 && GameDatabase.Item("water_flask") != null)
            {
                inv.Gold -= 2;
                inv.AddItem("water_flask");
                hero.Say("You drink your fill and fill a flask.");
            }
            else hero.Say("You drink your fill.");
        }
    }

    /// <summary>Swing at the dummy to raise Swordsmanship.</summary>
    public class TrainingDummy : Interactable, IDamageable
    {
        Transform body;
        float wobble;

        public bool IsAlive { get { return true; } }
        void Start() { body = transform.childCount > 0 ? transform.GetChild(0) : transform; }

        public override string Prompt(Hero hero)
        {
            return "Training dummy: attack it to train Swordsmanship (" + hero.Stats.Swordsmanship.Level + ")";
        }
        public override void Interact(Hero hero) { hero.Say("Swing at the dummy with left mouse. Each hit trains Swordsmanship."); }

        public void TakeDamage(DamageInfo info)
        {
            wobble = 1f;
            var hero = Hero.Instance;
            if (hero == null || !info.IsPlayer || !info.IsMelee) return;
            // Bigger weapons train strength a little too.
            var w = hero.Combat.Current;
            hero.Stats.TrainSkill(hero.Stats.Swordsmanship, 4f);
            if (w.Weight == WeightClass.Heavy || w.Weight == WeightClass.Great) hero.Stats.TrainSkill(hero.Stats.Strength, 1.5f);
            hero.Stats.TrainSkill(hero.Stats.Endurance, 1f);
            GameManager.Instance.AdvanceTime(3f);
        }

        void Update()
        {
            if (body == null) return;
            wobble = Mathf.MoveTowards(wobble, 0f, Time.deltaTime * 1.5f);
            body.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 20f) * 12f * wobble, 0f, 0f);
        }
    }

    /// <summary>Hold E to lift. Costs stamina and time; builds Strength (for big weapons) and Endurance (stamina).</summary>
    public class WeightRack : Interactable
    {
        public float Weight = 1f;
        public override bool HoldToUse { get { return true; } }
        float reps;

        public override string Prompt(Hero hero)
        {
            return string.Format("Hold E to lift weights (Strength {0}, Endurance {1})", hero.Stats.Strength.Level, hero.Stats.Endurance.Level);
        }

        public override void Interact(Hero hero) { }

        public override void HoldTick(Hero hero, float dt)
        {
            var s = hero.Stats;
            if (s.Stamina < 5f) { if (Random.value < dt) hero.Say("Your muscles are spent. Rest a moment."); return; }
            s.Stamina -= 16f * dt;
            hero.Visual.Rig.MoveSpeed = 0f;
            reps += dt;
            if (reps >= 1f)
            {
                reps = 0f;
                s.TrainSkill(s.Strength, 5f * Weight);
                s.TrainSkill(s.Endurance, 3f);
                GameManager.Instance.AdvanceTime(5f);
                s.Hunger = Mathf.Max(0f, s.Hunger - 0.4f);
                s.Thirst = Mathf.Max(0f, s.Thirst - 0.6f);
            }
        }
    }

    /// <summary>The gate from the village into one of the six dungeons.</summary>
    public class DungeonGate : Interactable
    {
        public DungeonDef Dungeon;

        public override string Prompt(Hero hero)
        {
            var gm = GameManager.Instance;
            if (!gm.IsUnlocked(Dungeon))
                return Dungeon.name + " (sealed: defeat the boss of " + GameDatabase.Dungeon(Dungeon.unlockAfter).name + " first)";
            return "Enter " + Dungeon.name + (gm.BossDefeated(Dungeon) ? " (boss defeated)" : "") + "  [tier " + Dungeon.tier + "]";
        }

        public override void Interact(Hero hero)
        {
            if (!GameManager.Instance.IsUnlocked(Dungeon)) { hero.Say("The gate is sealed."); return; }
            GameManager.Instance.EnterDungeon(Dungeon);
        }
    }

    /// <summary>The way home from a dungeon. The hero can always go back to the village.</summary>
    public class ExitPortal : Interactable
    {
        public override string Prompt(Hero hero) { return "Return to the village"; }
        public override void Interact(Hero hero) { GameManager.Instance.EnterVillage(); }
    }

    /// <summary>A one-time cache of food, water and medicine found in a dungeon.</summary>
    public class SupplyCache : Interactable
    {
        public string[] Items;
        bool opened;
        public override bool CanUse(Hero hero) { return !opened; }
        public override string Prompt(Hero hero) { return "Search the supply crate"; }
        public override void Interact(Hero hero)
        {
            opened = true;
            int count = Random.Range(2, 4);
            for (int i = 0; i < count && Items != null && Items.Length > 0; i++)
            {
                var item = GameDatabase.Item(Items[Random.Range(0, Items.Length)]);
                if (item == null) continue;
                int n = item.Kind == ItemKind.Ammo ? Random.Range(3, 7) : 1;
                hero.Stats.Inventory.AddItem(item.id, n);
                hero.Say("Found " + item.name + (n > 1 ? " x" + n : "") + ".");
            }
            if (transform.childCount > 0) transform.GetChild(0).localScale *= 0.6f;
        }
    }
}
