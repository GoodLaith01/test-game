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
                if (GameInput.KeyDown(KeyCode.R) && !Reloading) StartCoroutine(Reload(w));
                if (GameInput.MouseDown(0)) TryFire(w);
            }
        }

        float AttackTime(WeaponDef w)
        {
            float t = w.attackTime / (1f + 0.04f * (hero.Stats.Swordsmanship.Level - 1)) / hero.Stats.Progress.AttackSpeed;
            return t * (1f + hero.Stats.MindPenalty * 0.4f);
        }

        /// <summary>Damage of one hit before critical rolls, with skills, perks, hunger, sanity and meal buffs applied.</summary>
        public float DamageFor(WeaponDef w)
        {
            var s = hero.Stats;
            float dmg = w.damage;
            if (w.Kind == WeaponKind.Melee) dmg *= (1f + 0.07f * (s.Swordsmanship.Level - 1)) * s.Progress.MeleeDamageMult;
            else dmg *= s.Progress.FirearmDamageMult;
            if (w.Weight == WeightClass.Heavy || w.Weight == WeightClass.Great) dmg *= 1f + 0.06f * (s.Strength.Level - 1);
            if (s.Hunger < 15f) dmg *= 0.8f;
            dmg *= 1f - 0.35f * s.MindPenalty;
            dmg *= s.Buffs.DamageMultiplier; // cooked meals and the grinding wheel
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
            if (hero.Stats.Dead || hero.Stats.Rolling || Current != w) yield break;
            var s = hero.Stats;
            // A sleepless mind fumbles.
            if (Random.value < s.MindPenalty * 0.45f)
            {
                hero.Say(Random.value < 0.5f ? "Your hands tremble. You miss!" : "You swing at something that isn't there.");
                yield break;
            }
            float wobble = s.MindPenalty * 40f;
            var fwd = Quaternion.Euler(0f, Random.Range(-wobble, wobble), 0f) * transform.forward;
            int hits = MeleeHit(w, fwd, w.range, w.arc, 1f);
            if (hits > 0 && hero.Cam != null) hero.Cam.Shake(0.4f);
            s.TrainSkill(s.Endurance, 0.3f);
        }

        /// <summary>
        /// One melee strike: rolls a critical hit, damages everything in the cone, heals from Bloodthirst and trains
        /// Swordsmanship. Used by normal swings and by the Whirlwind and Lunge abilities.
        /// </summary>
        public int MeleeHit(WeaponDef w, Vector3 forward, float range, float arc, float multiplier)
        {
            var s = hero.Stats;
            float dmg = DamageFor(w) * multiplier;
            bool crit = Random.value < s.Progress.CritChance(true);
            if (crit) dmg *= s.Progress.CritDamage;
            var info = new DamageInfo { Amount = dmg, From = transform.position, Source = gameObject, IsPlayer = true, IsMelee = true };
            int hits = Layers.HitCone(transform.position + Vector3.up, forward, range, arc, info, gameObject);
            if (hits > 0)
            {
                if (crit) { hero.Say("<color=#ffd040>Critical hit!</color>"); Effects.HitSpark(transform.position + Vector3.up * 1.2f + forward * range * 0.6f, new Color(1f, 0.85f, 0.3f)); }
                float steal = s.Progress.LifeSteal;
                if (steal > 0f) s.Heal(dmg * hits * steal);
                s.TrainSkill(s.Swordsmanship, 2f * hits);
            }
            return hits;
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
            float duration = w.reloadTime * (1f + hero.Stats.MindPenalty * 0.8f) * (hero.Stats.Cold ? 1.3f : 1f) * hero.Stats.Progress.ReloadMult;
            float t = 0f;
            while (t < duration)
            {
                if (hero.Stats.Dead || inv.Equipped != w || inv.Count(AmmoId) < Mathf.Max(1, w.ammoPerShot)) { Reloading = false; ReloadProgress = 0f; yield break; }
                t += Time.deltaTime;
                ReloadProgress = t / duration;
                yield return null;
            }
            if (Random.value < hero.Stats.Progress.AmmoSave) hero.Say("Powder Thrift: that load cost you nothing.");
            else inv.TakeItem(AmmoId, Mathf.Max(1, w.ammoPerShot));
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
            if (Layers.RaycastSolid(cam.position, cam.forward, w.range + 8f, transform, out hit)) aimPoint = hit.point;
            // Never aim behind the muzzle (e.g. when the camera is pressed against a wall).
            if (Vector3.Dot(aimPoint - muzzle, transform.forward) < 0.5f) aimPoint = muzzle + cam.forward * w.range;

            var s = hero.Stats;
            bool deadEye = Time.time < s.Progress.DeadEyeUntil;
            s.Progress.DeadEyeUntil = 0f;
            var deadEyeDef = SkillDatabase.Ability("dead_eye");
            float spread = deadEye ? 0f : (w.spread * (Aiming ? 0.5f : 1f)) * s.Progress.SpreadMult + s.MindPenalty * 12f;
            bool crit = deadEye || Random.value < s.Progress.CritChance(false);
            float shotMult = (crit ? s.Progress.CritDamage : 1f) * (deadEye && deadEyeDef != null ? deadEyeDef.power : 1f);
            int landed = 0;
            for (int i = 0; i < Mathf.Max(1, w.pellets); i++)
            {
                var dir = (aimPoint - muzzle).normalized;
                dir = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f) * dir;
                Vector3 end = muzzle + dir * w.range;
                if (Layers.RaycastSolid(muzzle, dir, w.range, transform, out hit))
                {
                    end = hit.point;
                    var target = hit.collider.GetComponentInParent<IDamageable>();
                    if (target != null && target.IsAlive)
                    {
                        target.TakeDamage(new DamageInfo { Amount = DamageFor(w) * shotMult, From = transform.position, Source = gameObject, IsPlayer = true });
                        landed++;
                    }
                }
                Effects.Tracer(muzzle, end);
            }
            Effects.MuzzleFlash(muzzle);
            if (landed > 0)
            {
                if (crit) hero.Say(deadEye ? "<color=#ffd040>Dead-Eye!</color>" : "<color=#ffd040>Critical shot!</color>");
                s.TrainSkill(s.Marksmanship, 3f + landed);
            }
            if (hero.Cam != null) hero.Cam.Shake(1f);
        }
    }
}
