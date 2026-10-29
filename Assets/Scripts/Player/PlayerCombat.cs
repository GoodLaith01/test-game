using System.Collections;
using Hollowmark.Data;
using Hollowmark.Visuals;
using UnityEngine;

namespace Hollowmark.Player
{
    /// <summary>
    /// Melee swings and flintlock firearms. Damage scales with Swordsmanship (melee) and Strength (heavy weapons),
    /// and gets worse, with misses and wobble, as insanity rises.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        public const string AmmoId = "powder_shot";

        Hero hero;
        float nextAttack;
        float attackingUntil;
        WeaponDef fists;

        public bool Reloading { get; private set; }
        public float ReloadProgress { get; private set; }
        public bool Aiming { get; private set; }
        public bool WantsCameraFacing { get { return Aiming || hero.Stats.Blocking || Time.time < attackingUntil; } }

        void Awake()
        {
            hero = GetComponent<Hero>();
            fists = new WeaponDef { id = "fists", name = "Bare Fists", damage = 5, attackTime = 0.45f, range = 1.4f, arc = 70, staminaCost = 4, length = 0.01f };
            fists.Init();
        }

        public WeaponDef Current { get { return hero.Stats.Inventory.Equipped ?? fists; } }

        void Update()
        {
            var stats = hero.Stats;
            stats.Blocking = false;
            Aiming = false;
            if (hero.InputLocked || stats.Dead || stats.Rolling) return;

            var w = Current;
            if (w.Kind == WeaponKind.Melee)
            {
                stats.Blocking = GameInput.MouseHeld(1) && stats.Stamina > 1f;
                if (GameInput.MouseDown(0)) TryMelee(w);
            }
            else
            {
                Aiming = GameInput.MouseHeld(1);
                if (GameInput.KeyDown(KeyCode.R)) StartCoroutine(Reload(w));
                if (GameInput.MouseDown(0)) TryFire(w);
            }
        }

        float AttackTime(WeaponDef w)
        {
            float t = w.attackTime / (1f + 0.04f * (hero.Stats.Swordsmanship.Level - 1));
            return t * (1f + hero.Stats.MindPenalty * 0.4f);
        }

        float DamageFor(WeaponDef w)
        {
            var s = hero.Stats;
            float dmg = w.damage;
            if (w.Kind == WeaponKind.Melee) dmg *= 1f + 0.07f * (s.Swordsmanship.Level - 1);
            if (w.Weight == WeightClass.Heavy || w.Weight == WeightClass.Great) dmg *= 1f + 0.06f * (s.Strength.Level - 1);
            if (s.Hunger < 15f) dmg *= 0.8f;
            dmg *= 1f - 0.35f * s.MindPenalty;
            return dmg * Random.Range(0.9f, 1.1f);
        }

        void TryMelee(WeaponDef w)
        {
            if (Time.time < nextAttack || Reloading) return;
            if (!hero.Stats.UseStamina(w.staminaCost * (w.twoHanded ? 1.2f : 1f)))
            {
                hero.Say("Too exhausted to swing!");
                nextAttack = Time.time + 0.4f;
                return;
            }
            float t = AttackTime(w);
            nextAttack = Time.time + t;
            attackingUntil = Time.time + t;
            hero.Visual.Rig.PlayAttack(t * 0.9f);
            StartCoroutine(Strike(w, t * 0.38f));
        }

        IEnumerator Strike(WeaponDef w, float windup)
        {
            yield return new WaitForSeconds(windup);
            if (hero.Stats.Dead) yield break;
            var s = hero.Stats;
            // A sleepless mind fumbles.
            if (Random.value < s.MindPenalty * 0.45f)
            {
                hero.Say(Random.value < 0.5f ? "Your hands tremble. You miss!" : "You swing at something that isn't there.");
                yield break;
            }
            float wobble = s.MindPenalty * 40f;
            var fwd = Quaternion.Euler(0f, Random.Range(-wobble, wobble), 0f) * transform.forward;
            var info = new DamageInfo { Amount = DamageFor(w), From = transform.position, Source = gameObject, IsPlayer = true, IsMelee = true };
            int hits = Layers.HitCone(transform.position + Vector3.up, fwd, w.range, w.arc, info, gameObject);
            if (hits > 0)
            {
                s.TrainSkill(s.Swordsmanship, 2f * hits);
                if (hero.Cam != null) hero.Cam.Shake(0.4f);
            }
            s.TrainSkill(s.Endurance, 0.3f);
        }

        IEnumerator Reload(WeaponDef w)
        {
            var inv = hero.Stats.Inventory;
            if (Reloading || inv.LoadedRounds > 0) yield break;
            if (inv.Count(AmmoId) < Mathf.Max(1, w.ammoPerShot))
            {
                hero.Say("No powder and shot! Buy some at the provisioner.");
                yield break;
            }
            Reloading = true;
            hero.Say("Reloading " + w.name + "...");
            float duration = w.reloadTime * (1f + hero.Stats.MindPenalty * 0.8f) * (hero.Stats.Cold ? 1.3f : 1f);
            float t = 0f;
            while (t < duration)
            {
                if (hero.Stats.Dead || inv.Equipped != w) { Reloading = false; ReloadProgress = 0f; yield break; }
                t += Time.deltaTime;
                ReloadProgress = t / duration;
                yield return null;
            }
            inv.TakeItem(AmmoId, Mathf.Max(1, w.ammoPerShot));
            inv.LoadedRounds = 1;
            Reloading = false;
            ReloadProgress = 0f;
            inv.NotifyChanged();
        }

        void TryFire(WeaponDef w)
        {
            var inv = hero.Stats.Inventory;
            if (Reloading || Time.time < nextAttack) return;
            if (inv.LoadedRounds <= 0) { hero.Say("Not loaded. Press R to reload."); return; }
            inv.LoadedRounds = 0;
            nextAttack = Time.time + w.attackTime;
            attackingUntil = Time.time + 0.6f;
            inv.NotifyChanged();

            var cam = hero.Cam != null ? hero.Cam.transform : transform;
            Vector3 muzzle = transform.position + Vector3.up * 1.4f + transform.forward * 0.6f;
            // Aim at whatever is under the crosshair.
            Vector3 aimPoint = cam.position + cam.forward * w.range;
            RaycastHit hit;
            if (Physics.Raycast(cam.position + cam.forward * 2f, cam.forward, out hit, w.range, ~0, QueryTriggerInteraction.Ignore)) aimPoint = hit.point;

            float spread = w.spread * (Aiming ? 0.5f : 1f) + hero.Stats.MindPenalty * 12f;
            for (int i = 0; i < Mathf.Max(1, w.pellets); i++)
            {
                var dir = (aimPoint - muzzle).normalized;
                dir = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f) * dir;
                Vector3 end = muzzle + dir * w.range;
                if (Physics.Raycast(muzzle, dir, out hit, w.range, ~0, QueryTriggerInteraction.Collide))
                {
                    end = hit.point;
                    var target = hit.collider.GetComponentInParent<IDamageable>();
                    if (target != null && !hit.collider.transform.IsChildOf(transform))
                        target.TakeDamage(new DamageInfo { Amount = DamageFor(w), From = transform.position, Source = gameObject, IsPlayer = true });
                }
                Effects.Tracer(muzzle, end);
            }
            Effects.MuzzleFlash(muzzle);
            if (hero.Cam != null) hero.Cam.Shake(1f);
        }
    }
}
