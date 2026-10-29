using System.Collections.Generic;
using Hollowmark.Data;
using Hollowmark.World;
using UnityEngine;

namespace Hollowmark.Player
{
    [System.Serializable]
    public class Skill
    {
        public string Name;
        public int Level = 1;
        public float Xp;
        public Skill(string name) { Name = name; }
        public float XpToNext { get { return 60f + Level * 50f; } }

        /// <summary>Returns true when the skill levels up.</summary>
        public bool Add(float xp)
        {
            Xp += xp;
            if (Xp < XpToNext) return false;
            Xp -= XpToNext;
            Level++;
            return true;
        }
    }

    /// <summary>
    /// Health, stamina and the survival meters: hunger, thirst, body temperature and the insanity
    /// that builds when the hero goes too long without sleep.
    /// </summary>
    public class PlayerStats : MonoBehaviour, IDamageable
    {
        public float Health, Stamina;
        public float Hunger = 80f, Thirst = 80f;      // 100 = full, 0 = starving / parched
        public float BodyTemp = 37f;
        public float Insanity;                         // 0..100
        public float HoursAwake;
        public bool Blocking, Rolling;

        public readonly Skill Strength = new Skill("Strength");
        public readonly Skill Endurance = new Skill("Endurance");
        public readonly Skill Swordsmanship = new Skill("Swordsmanship");

        public Inventory Inventory = new Inventory();
        public bool Dead { get; private set; }
        public bool IsAlive { get { return !Dead; } }

        public float MaxHealth { get { return GameDatabase.Balance.baseHealth + (Strength.Level - 1) * 6f + (Endurance.Level - 1) * 4f; } }
        public float MaxStamina { get { return GameDatabase.Balance.baseStamina + (Endurance.Level - 1) * 12f + (Strength.Level - 1) * 3f; } }

        public bool Cold { get { return BodyTemp < 35.8f; } }
        public bool Hot { get { return BodyTemp > 38.3f; } }
        public float EnvironmentTemp { get; private set; }

        public float LastDamageTime { get; private set; }
        public System.Action<string> OnMessage;

        float lastPhantom;

        BalanceConfig B { get { return GameDatabase.Balance; } }

        void Awake()
        {
            Health = MaxHealth;
            Stamina = MaxStamina;
        }

        void Message(string text) { if (OnMessage != null) OnMessage(text); }

        public void Tick(float dt, float gameMinutes)
        {
            if (Dead) return;
            float hours = gameMinutes / 60f;
            var weather = WeatherSystem.Instance;
            bool safe = GameManager.Instance != null && GameManager.Instance.InVillage;

            // Hunger and thirst.
            float heatThirst = Hot ? 1.8f : 1f;
            Hunger = Mathf.Max(0f, Hunger - B.hungerPerGameHour * hours * (Cold ? 1.4f : 1f));
            Thirst = Mathf.Max(0f, Thirst - B.thirstPerGameHour * hours * heatThirst);
            if (Hunger <= 0f) Hurt(B.starvingDamagePerSecond * dt, "starvation");
            if (Thirst <= 0f) Hurt(B.dehydratedDamagePerSecond * dt, "thirst");

            // Temperature: armor insulates against both cold and heat. Without armor you feel everything.
            EnvironmentTemp = weather != null ? weather.Temperature : 18f;
            float insulation = Inventory.Insulation;
            float exposure = 0f;
            if (EnvironmentTemp < B.comfortableMin) exposure = (EnvironmentTemp - B.comfortableMin) * (1f - insulation);
            else if (EnvironmentTemp > B.comfortableMax) exposure = (EnvironmentTemp - B.comfortableMax) * (1f - insulation * 0.7f);
            float targetTemp = 37f + exposure * 0.12f;
            BodyTemp = Mathf.MoveTowards(BodyTemp, targetTemp, dt * 0.05f);
            if (BodyTemp < 34.5f) Hurt(B.temperatureDamagePerSecond * dt, "the cold");
            if (BodyTemp > 39.5f) Hurt(B.temperatureDamagePerSecond * dt, "the heat");

            // Sleep deprivation and insanity.
            HoursAwake += hours;
            if (HoursAwake > B.hoursAwakeBeforeInsanity)
            {
                float mult = weather != null ? weather.InsanityMultiplier : 1f;
                float before = Insanity;
                Insanity = Mathf.Min(100f, Insanity + B.insanityPerGameHour * hours * mult);
                if (before < 30f && Insanity >= 30f) Message("You have not slept in a long time. Your hands are shaking.");
                if (before < 60f && Insanity >= 60f) Message("Shapes move at the edge of your sight. Find a tent and sleep!");
                if (before < 90f && Insanity >= 90f) Message("Your mind is breaking. Sleep, or die.");
            }
            if (Insanity >= 100f) Hurt(B.insanityDamagePerSecond * dt, "madness");

            // Stamina regeneration.
            float regen = B.staminaRegenPerSecond;
            if (Hunger < 25f) regen *= 0.5f;
            if (Thirst < 25f) regen *= 0.6f;
            if (Cold || Hot) regen *= 0.6f;
            regen *= 1f / (1f + Inventory.ArmorWeight * 0.08f);
            if (weather != null) regen *= weather.StaminaRegenMultiplier;
            if (Time.time - LastDamageTime > 0.8f && !Blocking) Stamina = Mathf.Min(MaxStamina, Stamina + regen * dt);

            // Slow natural healing when fed and watered.
            if (Hunger > 50f && Thirst > 50f && Time.time - LastDamageTime > 6f)
                Health = Mathf.Min(MaxHealth, Health + dt * (safe ? 2f : 0.4f));

            if (Insanity >= 60f && Time.time - lastPhantom > 25f && Random.value < dt * 0.1f)
            {
                lastPhantom = Time.time;
                var effects = GetComponent<InsanityEffects>();
                if (effects != null) effects.SpawnPhantom();
            }
        }

        public bool UseStamina(float amount)
        {
            if (Stamina < amount * 0.5f) return false; // can still act when nearly drained, but not at zero
            Stamina = Mathf.Max(0f, Stamina - amount);
            return true;
        }

        /// <summary>0..1 penalty to weapon handling from insanity, used for misses and wobble.</summary>
        public float MindPenalty { get { return Mathf.Clamp01((Insanity - 25f) / 75f); } }

        public void TakeDamage(DamageInfo info)
        {
            if (Dead || Rolling) return;
            float dmg = info.Amount;

            if (Blocking && info.IsMelee)
            {
                var toAttacker = info.From - transform.position; toAttacker.y = 0f;
                if (Vector3.Angle(transform.forward, toAttacker) < 70f && UseStamina(B.blockStaminaPerHit))
                {
                    dmg *= 0.25f;
                    Message("Blocked!");
                }
            }

            if (Inventory.InUnderwear)
            {
                dmg *= B.underwearDamageMultiplier;
            }
            else
            {
                float def = Inventory.Defense;
                dmg *= 100f / (100f + def * 4f);
                var broken = Inventory.WearArmor(info.Amount);
                foreach (var name in broken) Message("Your " + name + " broke and fell off!");
                if (broken.Count > 0 && Inventory.InUnderwear) Message(UnderwearLine());
            }

            LastDamageTime = Time.time;
            Hurt(dmg, "wounds");
        }

        static readonly string[] UnderwearLines =
        {
            "All your armor is gone! You are fighting in your underwear. Defense: 0.",
            "Nothing left but your smallclothes. Every hit will hurt. A lot.",
            "Your breeches are gone. The monsters are laughing at you.",
            "Underwear mode! Defense 0. Run, or fight very carefully.",
        };

        public static string UnderwearLine() { return UnderwearLines[Random.Range(0, UnderwearLines.Length)]; }

        void Hurt(float amount, string cause)
        {
            if (Dead || amount <= 0f) return;
            Health -= amount;
            if (Health <= 0f)
            {
                Health = 0f;
                Dead = true;
                if (GameManager.Instance != null) GameManager.Instance.OnPlayerDied(cause);
            }
        }

        public void Heal(float amount) { Health = Mathf.Min(MaxHealth, Health + amount); }

        public void Consume(ItemDef item)
        {
            if (item == null || item.Kind == ItemKind.Ammo) return;
            if (!Inventory.TakeItem(item.id)) return;
            Hunger = Mathf.Clamp(Hunger + item.hunger, 0f, 100f);
            Thirst = Mathf.Clamp(Thirst + item.thirst, 0f, 100f);
            Heal(item.health);
            Stamina = Mathf.Clamp(Stamina + item.stamina, 0f, MaxStamina);
            Insanity = Mathf.Clamp(Insanity + item.insanity, 0f, 100f);
            BodyTemp = Mathf.Clamp(BodyTemp + item.warmth, 33f, 41f);
            Message("Used " + item.name + ".");
        }

        /// <summary>Sleeping in a bed or tent resets stamina and calms the mind.</summary>
        public void Sleep(float hours, bool inBed)
        {
            HoursAwake = 0f;
            Insanity = Mathf.Max(0f, Insanity - B.sleepInsanityRecoveryPerHour * hours * (inBed ? 1.5f : 1f));
            Stamina = MaxStamina;
            Heal(hours * (inBed ? 10f : 5f));
            BodyTemp = Mathf.MoveTowards(BodyTemp, 37f, hours * 0.5f);
            Hunger = Mathf.Max(0f, Hunger - B.hungerPerGameHour * hours * 0.5f);
            Thirst = Mathf.Max(0f, Thirst - B.thirstPerGameHour * hours * 0.5f);
        }

        public void Revive()
        {
            Dead = false;
            Health = MaxHealth;
            Stamina = MaxStamina;
            Hunger = Mathf.Max(Hunger, 50f);
            Thirst = Mathf.Max(Thirst, 50f);
            Insanity = Mathf.Min(Insanity, 20f);
            HoursAwake = 0f;
            BodyTemp = 37f;
        }

        public void TrainSkill(Skill skill, float xp)
        {
            if (skill.Add(xp))
            {
                Message(skill.Name + " increased to " + skill.Level + "!");
                if (skill == Strength) Message("You can now wield weapons that need Strength " + skill.Level + ".");
            }
        }

        public List<string> SkillSummary()
        {
            return new List<string>
            {
                string.Format("Strength {0} ({1:0}/{2:0})", Strength.Level, Strength.Xp, Strength.XpToNext),
                string.Format("Endurance {0} ({1:0}/{2:0})", Endurance.Level, Endurance.Xp, Endurance.XpToNext),
                string.Format("Swordsmanship {0} ({1:0}/{2:0})", Swordsmanship.Level, Swordsmanship.Xp, Swordsmanship.XpToNext),
            };
        }
    }
}
