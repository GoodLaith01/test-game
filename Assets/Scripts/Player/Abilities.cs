using System.Collections;
using System.Collections.Generic;
using Hollowmark.Data;
using Hollowmark.Enemies;
using Hollowmark.Visuals;
using UnityEngine;

namespace Hollowmark.Player
{
    /// <summary>
    /// Active abilities unlocked by perks in the skill trees. Up to four sit on the 1-4 keys; the skills screen (K)
    /// lets you choose which. Each costs stamina and has a cooldown.
    /// </summary>
    public class Abilities : MonoBehaviour
    {
        public const int SlotCount = 4;
        public readonly AbilityDef[] Slots = new AbilityDef[SlotCount];
        readonly Dictionary<string, float> readyAt = new Dictionary<string, float>();
        Hero hero;

        void Awake() { hero = GetComponent<Hero>(); }

        PlayerStats S { get { return hero.Stats; } }
        Progression P { get { return hero.Stats.Progress; } }

        public float Remaining(AbilityDef a)
        {
            float t;
            return a != null && readyAt.TryGetValue(a.id, out t) ? Mathf.Max(0f, t - Time.time) : 0f;
        }

        public void OnLearned(AbilityDef a)
        {
            if (System.Array.IndexOf(Slots, a) >= 0) return;
            for (int i = 0; i < SlotCount; i++)
                if (Slots[i] == null)
                {
                    Slots[i] = a;
                    S.Say("New ability: " + a.name + ". Press " + (i + 1) + " to use it.");
                    return;
                }
            S.Say("New ability: " + a.name + ". Put it on a key in the skills screen (K).");
        }

        public void Assign(int slot, AbilityDef a)
        {
            if (slot < 0 || slot >= SlotCount) return;
            for (int i = 0; i < SlotCount; i++) if (Slots[i] == a) Slots[i] = null;
            Slots[slot] = a;
        }

        public void ClearSlots() { for (int i = 0; i < SlotCount; i++) Slots[i] = null; }

        void Update()
        {
            if (hero.InputLocked || S.Dead) return;
            for (int i = 0; i < SlotCount; i++)
                if (GameInput.KeyDown(KeyCode.Alpha1 + i)) Use(Slots[i]);
        }

        public bool Use(AbilityDef a)
        {
            if (a == null || S.Dead || S.Rolling) return false;
            float left = Remaining(a);
            if (left > 0f) { S.Say(a.name + " is ready in " + Mathf.CeilToInt(left) + "s."); return false; }
            var w = hero.Combat.Current;
            if (a.needs == "Melee" && w.Kind != WeaponKind.Melee) { S.Say(a.name + " needs a melee weapon in hand."); return false; }
            if (a.needs == "Firearm" && w.Kind != WeaponKind.Firearm) { S.Say(a.name + " needs a firearm in hand."); return false; }
            if (a.stamina > 0f && !S.UseStamina(a.stamina)) { S.Say("Too exhausted for " + a.name + "!"); return false; }
            readyAt[a.id] = Time.time + a.cooldown;

            switch (a.id)
            {
                case "whirlwind": Whirlwind(a, w); break;
                case "lunge": StartCoroutine(Lunge(a, w)); break;
                case "dead_eye":
                    P.DeadEyeUntil = Time.time + a.seconds;
                    S.Say("Dead-Eye: your next shot will not miss.");
                    break;
                case "smoke_thunder": SmokeAndThunder(a); break;
                case "second_wind":
                    S.Heal(S.MaxHealth * a.power);
                    S.Stamina = S.MaxStamina;
                    S.Poison *= 0.5f;
                    Effects.Puff(transform.position + Vector3.up, new Color(0.5f, 0.9f, 0.5f), 1.2f, 0.8f);
                    S.Say("Second Wind! You catch your breath.");
                    break;
                case "battle_cry": BattleCry(a); break;
                default: S.Say(a.name + " does nothing yet."); break;
            }
            return true;
        }

        void Whirlwind(AbilityDef a, WeaponDef w)
        {
            hero.Visual.Rig.PlayAttack(0.5f);
            for (int i = 0; i < 8; i++)
            {
                var dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                Effects.Puff(transform.position + Vector3.up + dir * (w.range * 0.8f), new Color(0.8f, 0.8f, 0.85f), 0.35f, 0.35f);
            }
            int hits = hero.Combat.MeleeHit(w, transform.forward, w.range + a.radius, 360f, a.power);
            if (hits > 0 && hero.Cam != null) hero.Cam.Shake(0.6f);
        }

        IEnumerator Lunge(AbilityDef a, WeaponDef w)
        {
            var dir = transform.forward;
            if (hero.Cam != null) dir = Quaternion.Euler(0f, hero.Cam.Yaw, 0f) * Vector3.forward;
            transform.rotation = Quaternion.LookRotation(dir);
            const float time = 0.3f;
            hero.Controller.Dash(dir, a.radius / time, time);
            hero.Visual.Rig.PlayAttack(0.45f);
            yield return new WaitForSeconds(time * 0.85f);
            if (S.Dead || hero.Combat.Current != w) yield break;
            int hits = hero.Combat.MeleeHit(w, transform.forward, w.range + 0.6f, 100f, a.power);
            if (hits > 0 && hero.Cam != null) hero.Cam.Shake(0.7f);
        }

        void SmokeAndThunder(AbilityDef a)
        {
            var at = transform.position + Vector3.up;
            Effects.MuzzleFlash(at + transform.forward * 0.5f);
            for (int i = 0; i < 10; i++)
                Effects.Puff(at + Random.insideUnitSphere * a.radius * 0.5f, new Color(0.25f, 0.25f, 0.25f), 1.4f, 1.6f);
            int hits = 0;
            foreach (var e in Nearby(a.radius))
            {
                e.TakeDamage(new DamageInfo { Amount = a.power * P.FirearmDamageMult, From = transform.position, Source = gameObject, IsPlayer = true });
                e.Stun(a.stun);
                hits++;
            }
            if (hero.Cam != null) hero.Cam.Shake(1f);
            if (hits > 0) S.TrainSkill(S.Marksmanship, 2f * hits);
        }

        void BattleCry(AbilityDef a)
        {
            P.WarCryUntil = Time.time + a.seconds;
            Effects.Puff(transform.position + Vector3.up * 1.6f, new Color(0.9f, 0.3f, 0.2f), 1.5f, 0.6f);
            foreach (var e in Nearby(a.radius)) e.Stun(a.stun);
            if (hero.Cam != null) hero.Cam.Shake(0.5f);
            S.Say("You roar! +" + Mathf.RoundToInt(a.power * 100f) + "% damage, -" + Mathf.RoundToInt(a.guard * 100f) + "% damage taken.");
        }

        List<EnemyAI> Nearby(float radius)
        {
            var list = new List<EnemyAI>();
            foreach (var e in EnemyAI.All)
                if (e != null && e.IsAlive && e.Current != EnemyAI.State.Hidden && (e.transform.position - transform.position).sqrMagnitude < radius * radius)
                    list.Add(e);
            return list;
        }
    }
}
