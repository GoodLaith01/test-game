using System.Collections.Generic;
using Hollowmark.Data;
using Hollowmark.Enemies;
using Hollowmark.Player;
using Hollowmark.UI;
using Hollowmark.Visuals;
using Hollowmark.World;
using UnityEngine;

namespace Hollowmark.Family
{
    /// <summary>
    /// Olmar, the orphan. Before you adopt him he sits alone beside the church. Once adopted he practises on his
    /// own dummy by the training yard during the day and sleeps in your house at night. At 16 he can follow you
    /// into the dungeons and fight, or stay home and defend the village during raids. His Strength, Swordplay and
    /// Smarts (see Family) decide how hard he hits, how fast and surely he swings, and how well he reads a fight.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class Olmar : Interactable
    {
        /// <summary>The Olmar in the current world, if any.</summary>
        public static Olmar Active { get; private set; }

        // Village spots (the village root sits at the world origin).
        public static readonly Vector3 LonelySpot = new Vector3(36.2f, 0f, 13f);   // beside the church
        public static readonly Vector3 DummySpot = new Vector3(-25f, 0f, -16.8f);  // facing his dummy
        public static readonly Vector3 DummyPos = new Vector3(-25f, 0f, -18.2f);
        public static readonly Vector3 CotSpot = new Vector3(-17.5f, 0f, 2.6f);    // inside your house

        enum Job { Sit, Practice, Home, Follow, Fight, Retreat }

        public CharacterRig Rig;
        Job job;
        CharacterController cc;
        float verticalSpeed, nextSwing, flurryUntil, retreatUntil, lastWorldMinutes = -1f, nextScan, stuckFor, nextChat;
        Vector3 lastPos;
        EnemyAI foe;
        bool inDungeon;
        readonly HashSet<EnemyAI> warned = new HashSet<EnemyAI>();

        static Family F { get { return Family.Instance; } }
        static OlmarConfig Cfg { get { return Family.Cfg; } }
        static GameManager GM { get { return GameManager.Instance; } }
        static Hero H { get { return Hero.Instance; } }

        void OnEnable() { Active = this; }
        void OnDisable() { if (Active == this) Active = null; }

        /// <summary>True while he is in the fight and creatures may go for him instead of the hero.</summary>
        public bool CanBeTargeted { get { return (job == Job.Fight || job == Job.Follow) && F.Grown && !F.Wounded && isActiveAndEnabled; } }

        // ---------- Spawning ----------

        public static Olmar SpawnInVillage(Transform root)
        {
            var f = F;
            Vector3 pos = !f.Adopted ? LonelySpot : IsDaytime() ? DummySpot : CotSpot;
            return Spawn(root, pos, false);
        }

        /// <summary>Brings Olmar along into a dungeon when he is old enough, set to come, and not hurt.</summary>
        public static Olmar SpawnInDungeon(Transform root, Vector3 heroSpawn, float yaw)
        {
            var f = F;
            if (!f.Adopted || !f.Grown || f.Mode != Family.Stay.Along || f.Wounded) return null;
            var back = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
            var pos = Layers.ClearSpot(heroSpawn + back * 1.6f, new Vector2(1.5f, 1.5f), 0.3f, 1.7f);
            var o = Spawn(root, pos, true);
            Hud.Push(Cfg.name + " draws his sword and follows you in.");
            return o;
        }

        static Olmar Spawn(Transform root, Vector3 worldPos, bool dungeon)
        {
            var f = F;
            float grow = Mathf.Clamp01((f.Age - Cfg.startAge) / (float)Mathf.Max(1, Cfg.joinAge - Cfg.startAge));
            float height = Mathf.Lerp(1.48f, 1.74f, grow);
            var go = new GameObject(Cfg.name);
            go.transform.SetParent(root, true);
            go.transform.position = worldPos;
            if (!f.Adopted) go.transform.rotation = Quaternion.Euler(0f, 250f, 0f);

            var cc = go.AddComponent<CharacterController>();
            cc.height = height;
            cc.radius = 0.3f;
            cc.center = Vector3.up * height * 0.5f;
            cc.stepOffset = 0.35f;
            var reach = go.AddComponent<SphereCollider>();
            reach.isTrigger = true;
            reach.radius = 0.7f;
            reach.center = Vector3.up * height * 0.5f;

            Color skin;
            float muscle = Mathf.Clamp(0.05f + 0.09f * (f.Strength.Level - 1), 0f, 1.1f);
            var rig = CharacterBuilder.BuildHuman(go.transform, height, 513, out skin, false, muscle);
            // Patched, too-big clothes before you take him in; a proper coat after.
            CharacterBuilder.DressVillager(rig, f.Adopted ? new Color(0.22f, 0.3f, 0.45f) : new Color(0.36f, 0.32f, 0.27f), false);
            if (!f.Grown) Kid.ChildProportions(rig);
            if (rig.Hand != null)
            {
                var w = f.Grown ? GameDatabase.Weapon(Cfg.grownWeapon) : null;
                CharacterBuilder.BuildWeapon(w ?? WoodenSword(), rig.Hand);
            }
            Prim.Label(go.transform, Vector3.up * (height + 0.35f), Cfg.name, new Color(0.9f, 0.9f, 1f), 0.05f);

            var o = go.AddComponent<Olmar>();
            o.Rig = rig;
            o.cc = cc;
            o.inDungeon = dungeon;
            o.lastPos = worldPos;
            if (f.Health < 0f) f.HealFull();
            return o;
        }

        static WeaponDef woodenSword;
        /// <summary>His father's wooden practice sword. Not a real weapon: never sold, never dropped.</summary>
        static WeaponDef WoodenSword()
        {
            if (woodenSword != null) return woodenSword;
            woodenSword = new WeaponDef { id = "wooden_sword", name = "Wooden Sword", kind = "Melee", weightClass = "Light", length = 0.6f, color = "#8A6A40", sold = false };
            woodenSword.Init();
            return woodenSword;
        }

        static bool IsDaytime()
        {
            if (GM == null) return true;
            float h = GM.Hour;
            return h >= Cfg.trainFromHour && h < Cfg.trainToHour;
        }

        // ---------- Interaction ----------

        public override string Prompt(Hero hero)
        {
            if (!F.Met) return "Talk to the lonely boy";
            if (!F.Adopted) return "Talk to " + Cfg.name + " (" + F.Age + ")";
            return "Talk to " + Cfg.name + " (" + F.Age + ", " + F.MoodWord + ")";
        }

        public override void Interact(Hero hero)
        {
            if (FamilyUi.Instance != null) FamilyUi.Instance.Open(this);
            if (nextChat <= Time.time) { hero.Say(Cfg.name + ": \"" + F.RandomLine() + "\""); nextChat = Time.time + 8f; }
        }

        /// <summary>Called by the panel after you adopt him.</summary>
        public void TakeHome()
        {
            F.Adopt();
            Hud.Instance.Fade(Cfg.name + " takes your hand. You walk him home.");
            Hud.Instance.Banner(Cfg.name + " is part of your family", "Teach him at his dummy beside the training yard. At " + Cfg.joinAge + " he can fight beside you.");
            if (GM != null) GM.AdvanceTime(20f);
            // Swap his ragged clothes for the new coat.
            if (transform.parent != null)
            {
                var root = transform.parent;
                Destroy(gameObject);
                SpawnInVillage(root);
            }
        }

        /// <summary>Called by the panel after a lesson.</summary>
        public void ShowLesson()
        {
            Teleport(DummySpot);
            flurryUntil = Time.time + 5f;
        }

        /// <summary>Sends him back to the village from a dungeon.</summary>
        public void SendHome()
        {
            F.Mode = Family.Stay.Home;
            Effects.Puff(transform.position + Vector3.up, new Color(0.6f, 0.6f, 0.6f), 1f, 0.6f);
            Hud.Push(Cfg.name + " heads back to guard the house.");
            Destroy(gameObject);
        }

        public void Teleport(Vector3 worldPos)
        {
            if (cc == null) cc = GetComponent<CharacterController>();
            cc.enabled = false;
            transform.position = worldPos + Vector3.up * 0.05f;
            cc.enabled = true;
            verticalSpeed = 0f;
            lastPos = transform.position;
            stuckFor = 0f;
        }

        // ---------- Taking hits ----------

        /// <summary>A creature's blow or ember. Smarts lets him duck some of them.</summary>
        public void Hurt(float amount, Vector3 from)
        {
            var f = F;
            if (!CanBeTargeted) return;
            if (Random.value < f.DodgeChance)
            {
                if (Random.value < 0.3f) Hud.Push(Cfg.name + " ducks under the blow!");
                return;
            }
            f.Health -= amount;
            Rig.PlayHurt();
            Effects.HitSpark(transform.position + Vector3.up * 1.2f, new Color(0.6f, 0.05f, 0.05f));
            if (f.Health > 0f)
            {
                if (f.KnowsToRetreat && f.Health < f.MaxHealth * 0.3f && retreatUntil < Time.time)
                {
                    retreatUntil = Time.time + 4f;
                    Hud.Push(Cfg.name + ": \"I need a breath!\" He steps back behind you.");
                }
                return;
            }
            f.Wound();
            if (inDungeon)
            {
                Hud.Instance.Banner(Cfg.name + " is badly hurt!", "He limps back to the village to rest. He can come with you again after " + Cfg.woundedHours.ToString("0") + " hours.");
                Effects.Puff(transform.position + Vector3.up, new Color(0.5f, 0.5f, 0.5f), 1.2f, 0.7f);
                Destroy(gameObject);
            }
            else
            {
                Hud.Push(Cfg.name + " is hurt and runs into the house.");
                job = Job.Home;
            }
        }

        // ---------- Behaviour ----------

        void Update()
        {
            var gm = GM;
            var hero = H;
            if (gm == null || hero == null || Rig == null) return;
            var f = F;
            f.Tick();
            if (transform.position.y < -30f) Teleport(inDungeon ? hero.transform.position : (f.Adopted ? CotSpot : LonelySpot));

            // Out of a fight he gets his breath back.
            if (!f.Wounded && foe == null) f.Health = Mathf.Min(f.MaxHealth, f.Health + f.MaxHealth * 0.02f * Time.deltaTime);
            else if (!f.Wounded && retreatUntil > Time.time) f.Health = Mathf.Min(f.MaxHealth, f.Health + f.MaxHealth * 0.04f * Time.deltaTime);

            PassTime(gm, f);
            if (Time.time >= nextScan) { nextScan = Time.time + 0.4f; Scan(f, hero); }
            job = ChooseJob(gm, f, hero);

            switch (job)
            {
                case Job.Sit: Gravity(Vector3.zero); break;
                case Job.Practice: Practice(); break;
                case Job.Home: GoTo(CotSpot, 3f, 0.5f); break;
                case Job.Follow: Follow(hero); break;
                case Job.Retreat: Retreat(hero); break;
                case Job.Fight: Fight(f); break;
            }
        }

        Job ChooseJob(GameManager gm, Family f, Hero hero)
        {
            if (!f.Adopted) return Job.Sit;
            bool fighter = f.Grown && !f.Wounded;
            if (inDungeon)
            {
                if (retreatUntil > Time.time) return Job.Retreat;
                return foe != null ? Job.Fight : Job.Follow;
            }
            if (gm.Raid != null)
            {
                if (!fighter) return Job.Home; // too young or hurt: hide inside
                if (retreatUntil > Time.time) return Job.Retreat;
                return foe != null ? Job.Fight : Job.Follow; // stays by you while the village is defended
            }
            if (f.Wounded) return Job.Home;
            if (flurryUntil > Time.time) return Job.Practice;
            return IsDaytime() ? Job.Practice : Job.Home;
        }

        /// <summary>Practice on his own while the hero is away from the yard; feeling at home cheers him up.</summary>
        void PassTime(GameManager gm, Family f)
        {
            float now = gm.WorldMinutes;
            if (lastWorldMinutes < 0f) { lastWorldMinutes = now; return; }
            float minutes = Mathf.Clamp(now - lastWorldMinutes, 0f, 180f);
            lastWorldMinutes = now;
            if (!f.Adopted || minutes <= 0f) return;
            if (job == Job.Practice && !f.Wounded)
            {
                float xp = Cfg.selfPracticeXpPerHour * minutes / 60f;
                f.Train(f.Strength, xp * 0.6f);
                f.Train(f.Swordplay, xp * 0.4f);
            }
            if (f.Mood < 60f) f.Mood = Mathf.Min(60f, f.Mood + 2f * minutes / 60f);
        }

        /// <summary>Looks for a creature to fight, and for any hiding nearby that he can warn you about.</summary>
        void Scan(Family f, Hero hero)
        {
            if (foe != null && (!foe.IsAlive || foe.Current == EnemyAI.State.Hidden)) foe = null;
            if (!f.Adopted || !f.Grown || f.Wounded) { foe = null; return; }
            bool raid = GM.Raid != null;
            if (!inDungeon && !raid) { foe = null; return; }

            EnemyAI best = null;
            float bestScore = float.MaxValue;
            foreach (var e in EnemyAI.All)
            {
                if (e == null || !e.IsAlive) continue;
                float toMe = Vector3.Distance(e.transform.position, transform.position);
                if (e.Current == EnemyAI.State.Hidden)
                {
                    if (toMe < f.WarnRange && !warned.Contains(e))
                    {
                        warned.Add(e);
                        if (f.PointsOutAmbushes)
                        {
                            Hud.Push(Cfg.name + ": \"There! In the " + (e.Def.Stealth == StealthKind.Burrow ? "ground" : "shadows") + "!\" He drives it out.");
                            e.Reveal();
                        }
                        else Hud.Push(Cfg.name + ": \"Careful... something is hiding near here.\"");
                    }
                    continue;
                }
                float toHero = Vector3.Distance(e.transform.position, hero.transform.position);
                if (toHero > (raid ? 30f : 13f) && toMe > 6f) continue;
                // Smart: go for whatever is closest to the hero first, protect them.
                float score = Mathf.Lerp(toMe, toHero, Mathf.Clamp01(f.Smarts.Level / 8f));
                if (score < bestScore) { bestScore = score; best = e; }
            }
            foe = best;
        }

        void Practice()
        {
            if (!GoTo(DummySpot, 2.6f, 0.4f)) return;
            Face(DummyPos - transform.position);
            float gap = flurryUntil > Time.time ? 0.45f : 1.6f;
            if (Time.time >= nextSwing)
            {
                nextSwing = Time.time + gap * Random.Range(0.85f, 1.2f);
                Rig.PlayAttack(0.4f);
                Invoke("HitDummy", 0.2f);
            }
        }

        void HitDummy()
        {
            foreach (var c in Physics.OverlapSphere(DummyPos + Vector3.up, 0.8f, ~0, QueryTriggerInteraction.Collide))
            {
                var d = c.GetComponentInParent<OlmarDummy>();
                if (d != null) { d.Wobble(); break; }
            }
        }

        void Follow(Hero hero)
        {
            var hp = hero.transform.position;
            var spot = hp - hero.transform.forward * 1.8f + hero.transform.right * 1.1f;
            float dist = Vector3.Distance(transform.position, hp);
            if (dist > (inDungeon ? 18f : 35f)) { Teleport(Layers.ClearSpot(spot, new Vector2(1f, 1f), 0.3f, 1.7f)); return; }
            float speed = Mathf.Clamp((dist - 2f) * 2f, 0f, 6.5f);
            if (dist < 3f) speed = 0f;
            GoTo(spot, speed, 0.6f);
            if (speed == 0f) Face(hero.transform.forward);
        }

        void Retreat(Hero hero)
        {
            var hp = hero.transform.position;
            Vector3 away = foe != null ? (hp - foe.transform.position) : -hero.transform.forward;
            away.y = 0f;
            GoTo(hp + away.normalized * 2.5f, 5.5f, 0.6f);
        }

        void Fight(Family f)
        {
            var to = foe.transform.position - transform.position; to.y = 0f;
            float reach = 1.5f + foe.Def.width * 0.5f;
            if (to.magnitude > reach) { GoTo(foe.transform.position, 5.8f, reach * 0.9f); return; }
            Gravity(Vector3.zero);
            Face(to);
            if (Time.time < nextSwing) return;
            nextSwing = Time.time + f.AttackInterval;
            Rig.PlayAttack(Mathf.Min(0.45f, f.AttackInterval * 0.6f));
            Invoke("Strike", Mathf.Min(0.22f, f.AttackInterval * 0.3f));
        }

        void Strike()
        {
            var f = F;
            if (foe == null || !foe.IsAlive) return;
            var to = foe.transform.position - transform.position; to.y = 0f;
            if (to.magnitude > 2.2f + foe.Def.width * 0.5f) return;
            if (Random.value > f.HitChance) return; // missed
            var target = foe;
            target.TakeDamage(new DamageInfo { Amount = f.Damage, From = transform.position, Source = gameObject, IsMelee = true });
            f.Train(f.Swordplay, 2f);
            if (!target.IsAlive)
            {
                f.Kills++;
                f.Train(f.Strength, 6f);
                f.Train(f.Smarts, 3f);
                if (Random.value < 0.35f) Hud.Push(Cfg.name + ": \"Got one!\"");
                foe = null;
            }
        }

        // ---------- Movement ----------

        /// <summary>Walks toward a world point. Returns true when there.</summary>
        bool GoTo(Vector3 to, float speed, float arrive)
        {
            var d = to - transform.position; d.y = 0f;
            if (d.magnitude <= arrive || speed <= 0f) { Gravity(Vector3.zero); stuckFor = 0f; return d.magnitude <= arrive; }
            Face(d);
            Gravity(d.normalized * speed);
            // No pathfinding: if he cannot get round something, he slips round it when you are not looking.
            float moved = (transform.position - lastPos).magnitude;
            lastPos = transform.position;
            stuckFor = moved < speed * Time.deltaTime * 0.25f ? stuckFor + Time.deltaTime : 0f;
            if (stuckFor > 2.5f) Teleport(job == Job.Follow || job == Job.Fight || job == Job.Retreat ? Layers.ClearSpot(to, new Vector2(1f, 1f), 0.3f, 1.7f) : to);
            return false;
        }

        void Gravity(Vector3 horizontal)
        {
            if (cc == null) cc = GetComponent<CharacterController>();
            if (!cc.enabled) return;
            verticalSpeed = cc.isGrounded ? -2f : verticalSpeed - 18f * Time.deltaTime;
            cc.Move((horizontal + Vector3.up * verticalSpeed) * Time.deltaTime);
            var flat = horizontal; flat.y = 0f;
            Rig.MoveSpeed = flat.magnitude;
        }

        void Face(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
        }

        /// <summary>Before he has a home, he sits hunched with his head down.</summary>
        void LateUpdate()
        {
            if (Rig == null || job != Job.Sit) return;
            Rig.transform.localPosition = Vector3.down * Rig.Height * 0.27f;
            foreach (var l in Rig.Legs) if (l != null) l.localRotation = Quaternion.Euler(-80f, 0f, 0f);
            foreach (var k in Rig.Knees) if (k != null) k.localRotation = Quaternion.Euler(85f, 0f, 0f);
            if (Rig.Head != null) Rig.Head.localRotation = Quaternion.Euler(28f, 0f, 0f);
            if (Rig.Torso != null) Rig.Torso.localRotation *= Quaternion.Euler(14f, 0f, 0f);
        }

        void OnDestroy() { CancelInvoke(); }
    }

    /// <summary>Olmar's own small dummy beside the training yard. It wobbles when he hits it.</summary>
    public class OlmarDummy : MonoBehaviour
    {
        Transform body;
        float wobble;
        void Start() { body = transform.childCount > 0 ? transform.GetChild(0) : transform; }
        public void Wobble() { wobble = 1f; }
        void Update()
        {
            if (body == null) return;
            wobble = Mathf.MoveTowards(wobble, 0f, Time.deltaTime * 1.5f);
            body.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 20f) * 10f * wobble, 0f, 0f);
        }
    }
}
