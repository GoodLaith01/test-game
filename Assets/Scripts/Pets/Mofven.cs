using System.Collections.Generic;
using Hollowmark.Data;
using Hollowmark.Player;
using Hollowmark.UI;
using Hollowmark.Visuals;
using Hollowmark.World;
using UnityEngine;

namespace Hollowmark.Pets
{
    /// <summary>
    /// Mofven, a grown eagle-owl. There is a 20% chance he is perched somewhere in a dungeon, lonely and hungry.
    /// Feed him meat and he becomes your pet for the rest of the run: he flies after you between the village and
    /// the dungeons, and lands beside you when you stand still.
    ///
    /// He looks after his own belly by hunting. When you run out of food he also hunts for you, bringing back
    /// the kind of prey an owl can carry: field voles, mice, cave rats and now and then a young rabbit.
    /// </summary>
    public class Mofven : Interactable
    {
        public const float DungeonChance = 0.2f;      // chance he is in a dungeon on any visit, until he is tamed
        const float HungerPerGameHour = 2.5f;          // an owl eats a few small animals a day
        const float HuntCooldownMinutes = 90f;          // game minutes between hunts
        const float FollowSpeed = 10f;                  // a little faster than the hero's sprint

        /// <summary>The tamed Mofven, or null while he is still wild (or not met yet).</summary>
        public static Mofven Tamed { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Tamed = null; }

        static readonly string[] Meats = { "raw_meat", "salt_pork", "owl_rabbit", "owl_rat", "owl_vole", "owl_mouse" };

        enum Mode { Wild, Follow, Leaving, Hunting, Returning }

        public float Hunger = 30f; // 0..100, 100 = full. He is hungry when you find him.

        Mode mode = Mode.Wild;
        Transform model, head, wingL, wingR;
        TextMesh label;
        Collider trigger;
        float lastWorldMinutes = -1f, lastHuntEnd = -1000f;
        float stillFor, flap, headTurn, headTimer, modeTimer;
        bool huntingForHero;
        Vector3 lastHeroPos, velocity;

        // ---------------------------------------------------------------------------------------------
        // Spawning
        // ---------------------------------------------------------------------------------------------

        /// <summary>Called by the dungeon builder with the ordinary rooms of a fresh dungeon.</summary>
        public static void MaybeSpawnWild(List<KeyValuePair<Transform, float>> rooms)
        {
            if (Tamed != null || rooms == null || rooms.Count == 0 || Random.value >= DungeonChance) return;
            var pick = rooms[Random.Range(0, rooms.Count)];
            var room = pick.Key;
            float size = pick.Value;

            // A broken pillar in a corner of the room makes his perch.
            var local = new Vector3(-size * 0.36f, 0f, size * 0.36f);
            Prim.Box(room, local + Vector3.up * 0.5f, new Vector3(0.9f, 1f, 0.9f), new Color(0.42f, 0.4f, 0.37f), true, "OwlPerch");

            var go = new GameObject("Mofven");
            go.transform.SetParent(room, false);
            go.transform.localPosition = local + Vector3.up * 1f;
            go.transform.localRotation = Quaternion.Euler(0f, 135f, 0f); // facing into the room
            var owl = go.AddComponent<Mofven>();
            owl.Build();
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1.8f;
            owl.trigger = col;
            owl.label = Prim.Label(go.transform, new Vector3(0f, 1.1f, 0f), "A lone owl", new Color(0.95f, 0.85f, 0.6f), 0.06f);
            Hud.Push("Somewhere in the dark an owl is hooting.");
        }

        // ---------------------------------------------------------------------------------------------
        // Taming
        // ---------------------------------------------------------------------------------------------

        public override bool CanUse(Hero hero) { return mode == Mode.Wild; }

        public override string Prompt(Hero hero)
        {
            var meat = BestMeat(hero.Stats.Inventory);
            if (meat != null) return "Feed the hungry owl your " + meat.name;
            return "A hungry owl watches you. He wants meat (raw mutton, salted pork, small game)";
        }

        public override void Interact(Hero hero)
        {
            if (mode != Mode.Wild) return;
            var inv = hero.Stats.Inventory;
            var meat = BestMeat(inv);
            if (meat == null)
            {
                hero.Say(HasAnyFood(inv)
                    ? "The owl turns his head away. Owls only eat meat. Buy raw mutton at the larder."
                    : "The owl clacks his beak. He is hungry, and you have nothing to give him.");
                return;
            }
            inv.TakeItem(meat.id);
            Hunger = 100f;
            Tame(hero);
            hero.Say("The owl gulps down the " + meat.name + " and hops onto your arm.");
            Hud.Instance.Banner("Mofven is your pet!", "He will follow you, hunt for himself, and bring you food when you have none.");
        }

        void Tame(Hero hero)
        {
            Tamed = this;
            mode = Mode.Follow;
            transform.SetParent(null, true); // he is not part of the dungeon any more, so he survives leaving it
            if (trigger != null) Destroy(trigger); // never steal the [E] prompt from shops and doors
            if (label != null) Destroy(label.gameObject);
            lastWorldMinutes = GameManager.Instance.WorldMinutes;
            lastHuntEnd = lastWorldMinutes;
            lastHeroPos = hero.transform.position;
        }

        static ItemDef BestMeat(Inventory inv)
        {
            foreach (var id in Meats) if (inv.Count(id) > 0) return GameDatabase.Item(id);
            return null;
        }

        static bool HasAnyFood(Inventory inv)
        {
            foreach (var kv in inv.Items)
            {
                var d = GameDatabase.Item(kv.Key);
                if (d != null && d.Usable && d.hunger > 0f && (d.Kind == ItemKind.Food || d.Kind == ItemKind.Meal)) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------------------------------------
        // Behaviour
        // ---------------------------------------------------------------------------------------------

        void Update()
        {
            float dt = Time.deltaTime;
            AnimateHead(dt);
            if (mode == Mode.Wild) { Fold(dt); return; }

            var hero = Hero.Instance;
            var gm = GameManager.Instance;
            if (hero == null || gm == null) return;

            // Hunger follows the game clock, so sleeping and training make him hungry too.
            if (lastWorldMinutes < 0f) lastWorldMinutes = gm.WorldMinutes;
            float minutes = Mathf.Max(0f, gm.WorldMinutes - lastWorldMinutes);
            lastWorldMinutes = gm.WorldMinutes;
            Hunger = Mathf.Max(0f, Hunger - HungerPerGameHour * minutes / 60f);
            if (gm.Paused) return;

            switch (mode)
            {
                case Mode.Follow: Follow(hero, dt); MaybeHunt(hero, gm); break;
                case Mode.Leaving: Leave(dt); break;
                case Mode.Hunting:
                    modeTimer -= dt;
                    if (modeTimer <= 0f) ComeBack(hero, gm);
                    break;
                case Mode.Returning: Return(hero, dt); break;
            }
        }

        void Follow(Hero hero, float dt)
        {
            var hp = hero.transform.position;
            // Hero changed world (dungeon, village, respawn): fly in from close by instead of crossing the map.
            if ((transform.position - hp).sqrMagnitude > 30f * 30f) transform.position = hp + Vector3.up * 3f - hero.transform.forward * 2f;

            bool moving = (hp - lastHeroPos).sqrMagnitude > 0.0004f;
            lastHeroPos = hp;
            stillFor = moving ? 0f : stillFor + dt;

            Vector3 target;
            bool landing = stillFor > 1.5f;
            if (landing) target = hp + hero.transform.right * 1.1f + hero.transform.forward * 0.3f; // on the ground at your side
            else target = hp + hero.transform.rotation * new Vector3(0.9f, 2.1f, -0.8f) + Vector3.up * Mathf.Sin(Time.time * 2.3f) * 0.12f;

            MoveTo(target, dt, FollowSpeed);
            bool onGround = landing && (transform.position - target).sqrMagnitude < 0.05f;
            if (onGround) { Fold(dt); Face(hero.transform.forward, dt); }
            else { Flap(dt); Face(velocity.sqrMagnitude > 0.5f ? velocity : hero.transform.forward, dt); }
        }

        void MaybeHunt(Hero hero, GameManager gm)
        {
            if (hero.Stats.Dead || gm.Raid != null) return;
            if (gm.WorldMinutes - lastHuntEnd < HuntCooldownMinutes) return;
            bool heroHungry = !HasAnyFood(hero.Stats.Inventory);
            if (!heroHungry && Hunger > 30f) return;

            huntingForHero = heroHungry;
            mode = Mode.Leaving;
            modeTimer = 2.5f;
            velocity = (-hero.transform.forward + Vector3.up * 0.8f).normalized * 6f;
            hero.Say(heroHungry
                ? "Mofven sees your empty pack. He spreads his wings and glides off to hunt for you both."
                : "Mofven's stomach growls. He glides off to hunt.");
        }

        void Leave(float dt)
        {
            Flap(dt);
            transform.position += velocity * dt;
            Face(velocity, dt);
            modeTimer -= dt;
            if (modeTimer > 0f) return;
            mode = Mode.Hunting;
            modeTimer = Random.Range(20f, 35f);
            if (model != null) model.gameObject.SetActive(false);
        }

        void ComeBack(Hero hero, GameManager gm)
        {
            mode = Mode.Returning;
            lastHuntEnd = gm.WorldMinutes;
            var away = Random.insideUnitCircle.normalized * 12f;
            transform.position = hero.transform.position + new Vector3(away.x, 8f, away.y);
            if (model != null) model.gameObject.SetActive(true);

            bool success = Random.value < (gm.InVillage ? 0.85f : 0.7f);
            if (!success)
            {
                lastHuntEnd -= HuntCooldownMinutes * 0.5f; // he will try again sooner
                hero.Say("Mofven comes back with empty talons. The prey got away this time.");
                return;
            }
            // He always eats his own catch first.
            Hunger = Mathf.Min(100f, Hunger + 55f);
            if (!huntingForHero) { hero.Say("Mofven comes back licking his beak. He has eaten."); return; }

            var prey = PickPrey(gm.InVillage);
            if (prey == null) return;
            hero.Stats.Inventory.AddItem(prey.id);
            hero.Say("Mofven ate his share and drops a " + prey.name + " at your feet. (Press Q to eat.)");
        }

        /// <summary>What a grown owl can catch and carry: small animals only.</summary>
        static ItemDef PickPrey(bool outdoors)
        {
            string id;
            float r = Random.value;
            if (outdoors) id = r < 0.25f ? "owl_rabbit" : r < 0.65f ? "owl_vole" : "owl_mouse";
            else id = r < 0.55f ? "owl_rat" : r < 0.85f ? "owl_vole" : "owl_mouse"; // tunnels are full of rats
            return GameDatabase.Item(id) ?? GameDatabase.Item("owl_vole");
        }

        void Return(Hero hero, float dt)
        {
            var target = hero.transform.position + hero.transform.rotation * new Vector3(0.9f, 2.1f, -0.8f);
            MoveTo(target, dt, FollowSpeed * 1.2f);
            Flap(dt);
            Face(velocity, dt);
            if ((transform.position - target).sqrMagnitude < 0.5f) { mode = Mode.Follow; stillFor = 0f; }
        }

        void MoveTo(Vector3 target, float dt, float maxSpeed)
        {
            var to = target - transform.position;
            var desired = Vector3.ClampMagnitude(to * 3f, maxSpeed);
            velocity = Vector3.MoveTowards(velocity, desired, dt * 25f);
            transform.position += velocity * dt;
        }

        void Face(Vector3 dir, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), dt * 6f);
        }

        // ---------------------------------------------------------------------------------------------
        // Looks: a grown eagle-owl, about 65 cm tall with a 1.6 m wingspan
        // ---------------------------------------------------------------------------------------------

        static readonly Color Feathers = new Color(0.42f, 0.29f, 0.18f);
        static readonly Color Breast = new Color(0.79f, 0.66f, 0.47f);
        static readonly Color Disc = new Color(0.62f, 0.48f, 0.32f);

        void Build()
        {
            model = new GameObject("Model").transform;
            model.SetParent(transform, false);

            Prim.Make(PrimitiveType.Sphere, model, new Vector3(0f, 0.25f, 0f), new Vector3(0.32f, 0.42f, 0.3f), Feathers, "Feathers");
            Prim.Make(PrimitiveType.Sphere, model, new Vector3(0f, 0.23f, 0.09f), new Vector3(0.24f, 0.32f, 0.14f), Breast, "BreastFeathers");
            var tail = Prim.Make(PrimitiveType.Cube, model, new Vector3(0f, 0.1f, -0.15f), new Vector3(0.14f, 0.03f, 0.16f), Feathers * 0.85f, "Tail");
            tail.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            for (int s = -1; s <= 1; s += 2)
                Prim.Make(PrimitiveType.Cube, model, new Vector3(s * 0.05f, 0.02f, 0.05f), new Vector3(0.05f, 0.04f, 0.07f), new Color(0.35f, 0.33f, 0.3f), "Talons");

            head = new GameObject("Head").transform;
            head.SetParent(model, false);
            head.localPosition = new Vector3(0f, 0.5f, 0f);
            Prim.Make(PrimitiveType.Sphere, head, Vector3.zero, new Vector3(0.26f, 0.22f, 0.24f), Feathers, "Feathers");
            Prim.Make(PrimitiveType.Sphere, head, new Vector3(0f, -0.01f, 0.09f), new Vector3(0.22f, 0.18f, 0.07f), Disc, "FacialDisc");
            for (int s = -1; s <= 1; s += 2)
            {
                Prim.Make(PrimitiveType.Sphere, head, new Vector3(s * 0.05f, 0.01f, 0.12f), Vector3.one * 0.06f, new Color(1f, 0.55f, 0.08f), "Eye", false, true);
                Prim.Make(PrimitiveType.Sphere, head, new Vector3(s * 0.05f, 0.01f, 0.145f), Vector3.one * 0.03f, Color.black, "Pupil");
                var tuft = Prim.Make(PrimitiveType.Cube, head, new Vector3(s * 0.08f, 0.13f, 0.02f), new Vector3(0.04f, 0.1f, 0.03f), Feathers * 0.8f, "EarTuft");
                tuft.transform.localRotation = Quaternion.Euler(0f, 0f, -s * 20f);
            }
            var beak = Prim.Make(PrimitiveType.Cube, head, new Vector3(0f, -0.03f, 0.13f), new Vector3(0.03f, 0.03f, 0.05f), new Color(0.2f, 0.2f, 0.2f), "Beak");
            beak.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);

            wingL = Wing(-1);
            wingR = Wing(1);
        }

        Transform Wing(int side)
        {
            var pivot = new GameObject(side < 0 ? "WingL" : "WingR").transform;
            pivot.SetParent(model, false);
            pivot.localPosition = new Vector3(side * 0.15f, 0.4f, -0.02f);
            Prim.Make(PrimitiveType.Sphere, pivot, new Vector3(0f, -0.5f, 0f), new Vector3(0.1f, 1f, 0.26f), Feathers * 0.9f, "WingFeathers");
            return pivot;
        }

        /// <summary>Spread is 0 for folded wings and 1 for wide open. Folded wings are short and hug the body.</summary>
        void SetWings(float spread, float flapAngle)
        {
            if (wingL == null) return;
            float angle = Mathf.Lerp(8f, 85f, spread) + flapAngle;
            float len = Mathf.Lerp(0.34f, 0.72f, spread);
            wingL.localRotation = Quaternion.Euler(0f, 0f, -angle);
            wingR.localRotation = Quaternion.Euler(0f, 0f, angle);
            wingL.localScale = wingR.localScale = new Vector3(1f, len, 1f);
        }

        void Flap(float dt)
        {
            flap += dt * 9f;
            SetWings(1f, Mathf.Sin(flap) * 35f);
        }

        void Fold(float dt) { SetWings(0f, 0f); }

        /// <summary>Owls swivel their heads, so now and then he looks far over his shoulder.</summary>
        void AnimateHead(float dt)
        {
            if (head == null) return;
            headTimer -= dt;
            if (headTimer <= 0f)
            {
                headTimer = Random.Range(1.5f, 4f);
                headTurn = Random.value < 0.35f ? Random.Range(-160f, 160f) : Random.Range(-30f, 30f);
            }
            head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(0f, headTurn, 0f), dt * 5f);
        }
    }
}
