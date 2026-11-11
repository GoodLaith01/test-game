using System.Collections.Generic;
using Hollowmark.Crafting;
using Hollowmark.Data;
using UnityEngine;

namespace Hollowmark.Player
{
    public class ActiveBuff
    {
        public BuffDef Def;
        public float Remaining;
    }

    /// <summary>
    /// Timed effects from cooked meals and homestead buildings, plus the permanent bonuses of what is built.
    /// Eating the same dish again refreshes its timer instead of stacking.
    /// </summary>
    public class Buffs
    {
        public readonly List<ActiveBuff> Active = new List<ActiveBuff>();

        public void Apply(BuffDef def, PlayerStats stats)
        {
            if (def == null) return;
            var existing = Active.Find(a => a.Def == def);
            if (existing != null) existing.Remaining = Mathf.Max(existing.Remaining, def.seconds);
            else Active.Add(new ActiveBuff { Def = def, Remaining = def.seconds });
            if (def.curesPoison && stats.Poison > 0f) { stats.Poison = 0f; stats.Say("The poison leaves your blood."); }
            stats.Say(def.name + ": " + def.description + " (" + Mathf.RoundToInt(def.seconds / 60f) + " min).");
        }

        public void Tick(float dt, PlayerStats stats)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var a = Active[i];
                if (a.Def.healthPerSecond > 0f) stats.Heal(a.Def.healthPerSecond * dt);
                if (a.Def.sanityPerSecond > 0f) stats.Insanity = Mathf.Max(0f, stats.Insanity - a.Def.sanityPerSecond * dt);
                a.Remaining -= dt;
                if (a.Remaining <= 0f)
                {
                    Active.RemoveAt(i);
                    stats.Say(a.Def.name + " has worn off.");
                }
            }
        }

        public void Clear() { Active.Clear(); }

        /// <summary>A multiplier left out of the JSON (0) means "no change".</summary>
        static float Mult(float v) { return v > 0f ? v : 1f; }

        public float DamageMultiplier
        {
            get { float m = 1f; foreach (var a in Active) m *= Mult(a.Def.damageMultiplier); return m; }
        }

        public float DamageTakenMultiplier
        {
            get { float m = 1f; foreach (var a in Active) m *= Mathf.Clamp(Mult(a.Def.damageTakenMultiplier), 0.2f, 2f); return m; }
        }

        public float StaminaRegenMultiplier
        {
            get { float m = 1f; foreach (var a in Active) m *= Mult(a.Def.staminaRegenMultiplier); return m; }
        }

        /// <summary>0..1, the share of incoming poison that is ignored.</summary>
        public float PoisonResist
        {
            get
            {
                float keep = 1f - Homestead.Instance.PoisonResist;
                foreach (var a in Active) keep *= 1f - Mathf.Clamp01(a.Def.poisonResist);
                return Mathf.Clamp01(1f - keep);
            }
        }

        public float MaxHealthBonus { get { return Homestead.Instance.MaxHealthBonus; } }
        public float MaxStaminaBonus { get { return Homestead.Instance.MaxStaminaBonus; } }
    }
}
