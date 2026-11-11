using System.Collections.Generic;
using Hollowmark.Enemies;
using Hollowmark.Player;
using Hollowmark.Visuals;
using Hollowmark.World;
using UnityEngine;

namespace Hollowmark.Family
{
    /// <summary>
    /// A village child. The children play tag around the square: whoever is "it" chases the nearest other child,
    /// and a tag passes it on. They run indoors to the church while the village is raided, and go home at night.
    /// Talk to one with E.
    /// </summary>
    public class Kid : Interactable
    {
        public static readonly List<Kid> All = new List<Kid>();

        public string KidName;
        public CharacterRig Rig;
        public bool It;
        Vector3 playCentre, target, shelter;
        float wait, blockedFor, tagCooldown, sayCooldown, hideTimer;
        bool hidden;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static Kid Spawn(Transform root, KidDef def, Vector3 pos, Vector3 playCentre, Vector3 shelter)
        {
            var go = new GameObject("Kid_" + def.name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            Color skin;
            var rig = CharacterBuilder.BuildHuman(go.transform, def.height, def.seed, out skin, def.feminine, 0f);
            CharacterBuilder.DressVillager(rig, Prim.Vary(new Color(0.45f, 0.35f, 0.3f), 0.35f), def.feminine);
            ChildProportions(rig);
            var col = go.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.radius = 0.4f;
            col.height = def.height;
            col.center = Vector3.up * def.height * 0.5f;
            var kid = go.AddComponent<Kid>();
            kid.KidName = def.name;
            kid.Rig = rig;
            kid.playCentre = playCentre;
            kid.shelter = shelter;
            kid.target = pos;
            kid.wait = Random.Range(0f, 2f);
            return kid;
        }

        /// <summary>A child's head is big for the body.</summary>
        public static void ChildProportions(CharacterRig rig)
        {
            if (rig.Head != null) rig.Head.localScale *= 1.12f;
        }

        public override bool CanUse(Hero hero) { return !hidden; }
        public override string Prompt(Hero hero) { return "Talk to " + KidName; }

        public override void Interact(Hero hero)
        {
            var cfg = Family.Config;
            var lines = Family.Instance.Adopted && cfg.kidLinesOlmarHome.Length > 0 && Random.value < 0.4f ? cfg.kidLinesOlmarHome : cfg.kidLines;
            if (lines.Length == 0) return;
            hero.Say(KidName + ": \"" + lines[Random.Range(0, lines.Length)] + "\"");
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || Rig == null) return;
            tagCooldown -= Time.deltaTime;
            sayCooldown -= Time.deltaTime;

            // Hide in the church during raids and at night.
            bool shouldHide = gm.Raid != null || gm.Hour >= 21f || gm.Hour < 6.5f;
            if (shouldHide)
            {
                if (hidden) return;
                hideTimer += Time.deltaTime;
                // Home, or ducked into the nearest doorway if the way is blocked.
                if (Walk(shelter, gm.Raid != null ? 4.5f : 2f, 1.2f) || hideTimer > 12f) SetHidden(true);
                return;
            }
            hideTimer = 0f;
            if (hidden)
            {
                SetHidden(false);
                transform.localPosition = shelter;
                target = playCentre + Offset(4f);
            }

            if (It)
            {
                var prey = Nearest();
                if (prey != null)
                {
                    if (tagCooldown > 0f) { Walk(transform.localPosition + (transform.localPosition - prey.transform.localPosition).normalized, 0f, 0.3f); return; }
                    if (Walk(prey.transform.localPosition, 3.4f, 0.7f))
                    {
                        It = false;
                        prey.It = true;
                        prey.tagCooldown = 2.5f; // count to three before chasing
                        if (sayCooldown <= 0f && Near(Hero.Instance, 10f)) { UI.Hud.Push(KidName + ": \"Tag! You're it, " + prey.KidName + "!\""); sayCooldown = 20f; }
                    }
                    return;
                }
            }
            else
            {
                // Run from whoever is "it" when they get close, otherwise skip about the square.
                var chaser = All.Find(k => k != this && k.It && !k.hidden);
                if (chaser != null && chaser.tagCooldown <= 0f)
                {
                    var away = transform.localPosition - chaser.transform.localPosition; away.y = 0f;
                    if (away.magnitude < 6f)
                    {
                        var flee = transform.localPosition + away.normalized * 3f;
                        if ((flee - playCentre).magnitude > 9f) flee = playCentre + Offset(5f); // stay near the square
                        target = flee;
                        Walk(target, 3.1f, 0.3f);
                        return;
                    }
                }
            }

            if (Walk(target, 2.2f, 0.3f))
            {
                wait -= Time.deltaTime;
                if (wait <= 0f) { target = playCentre + Offset(7f); wait = Random.Range(0.5f, 3f); }
            }
        }

        static Vector3 Offset(float r) { return new Vector3(Random.Range(-r, r), 0f, Random.Range(-r, r)); }

        bool Near(Hero h, float d) { return h != null && Vector3.Distance(h.transform.position, transform.position) < d; }

        Kid Nearest()
        {
            Kid best = null;
            float bestD = float.MaxValue;
            foreach (var k in All)
            {
                if (k == this || k.hidden) continue;
                float d = (k.transform.localPosition - transform.localPosition).sqrMagnitude;
                if (d < bestD) { bestD = d; best = k; }
            }
            return best;
        }

        /// <summary>Steps toward a point (local to the village). Returns true when there.</summary>
        bool Walk(Vector3 to, float speed, float arrive)
        {
            var d = to - transform.localPosition; d.y = 0f;
            if (d.magnitude < arrive || speed <= 0f) { Rig.MoveSpeed = 0f; return d.magnitude < arrive; }
            var dir = d.normalized;
            // Children have no physics body either: look ahead and turn back instead of running through walls.
            if (Physics.SphereCast(transform.position + Vector3.up * 0.6f, 0.25f, dir, out var hit, 0.5f, ~0, QueryTriggerInteraction.Ignore)
                && !(hit.collider is CharacterController))
            {
                blockedFor += Time.deltaTime;
                Rig.MoveSpeed = 0f;
                if (blockedFor > 0.3f)
                {
                    blockedFor = 0f;
                    // Sidestep, then try again; a stuck child just pops round the obstacle after a while.
                    transform.localPosition += Vector3.Cross(Vector3.up, dir) * (Random.value < 0.5f ? -0.6f : 0.6f);
                }
                return false;
            }
            blockedFor = 0f;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
            transform.localPosition += dir * speed * Time.deltaTime;
            Rig.MoveSpeed = speed;
            return false;
        }

        void SetHidden(bool h)
        {
            hidden = h;
            if (Rig == null) return;
            Rig.MoveSpeed = 0f;
            Rig.gameObject.SetActive(!h);
        }
    }
}
