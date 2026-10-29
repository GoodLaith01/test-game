using System;
using System.Collections.Generic;
using System.Linq;
using Hollowmark.Data;
using UnityEngine;

namespace Hollowmark.Player
{
    [Serializable]
    public class ArmorInstance
    {
        public ArmorDef Def;
        public float Durability;
        public ArmorInstance(ArmorDef def) { Def = def; Durability = def.durability; }
        public bool Broken { get { return Durability <= 0f; } }
        public float Condition { get { return Def.durability <= 0f ? 0f : Durability / Def.durability; } }
    }

    /// <summary>
    /// Everything the player carries and wears. Armor wears down from hits; when every piece is gone
    /// the player is fighting in their underwear, with zero defense.
    /// </summary>
    public class Inventory
    {
        public readonly List<WeaponDef> Weapons = new List<WeaponDef>();
        public readonly List<ArmorInstance> Armor = new List<ArmorInstance>();
        public readonly Dictionary<string, int> Items = new Dictionary<string, int>();
        public readonly Dictionary<ArmorSlot, ArmorInstance> Worn = new Dictionary<ArmorSlot, ArmorInstance>();
        public WeaponDef Equipped;
        public int LoadedRounds;   // firearms: 1 when loaded
        public int Gold;

        public event Action Changed;
        public void NotifyChanged() { if (Changed != null) Changed(); }

        public bool InUnderwear { get { return Worn.Count == 0; } }

        public float Defense { get { return Worn.Values.Sum(a => a.Def.defense); } }
        public float Insulation { get { return Mathf.Clamp(Worn.Values.Sum(a => a.Def.insulation), 0f, 0.9f); } }
        public float ArmorWeight { get { return Worn.Values.Sum(a => a.Def.weight); } }

        public void AddWeapon(WeaponDef def) { if (def != null) { Weapons.Add(def); NotifyChanged(); } }
        public void AddArmor(ArmorDef def) { if (def != null) { Armor.Add(new ArmorInstance(def)); NotifyChanged(); } }

        public void AddItem(string id, int count = 1)
        {
            int have;
            Items.TryGetValue(id, out have);
            Items[id] = have + count;
            NotifyChanged();
        }

        public int Count(string id) { int n; return Items.TryGetValue(id, out n) ? n : 0; }

        public bool TakeItem(string id, int count = 1)
        {
            int have = Count(id);
            if (have < count) return false;
            if (have == count) Items.Remove(id); else Items[id] = have - count;
            NotifyChanged();
            return true;
        }

        public void Wear(ArmorInstance piece)
        {
            if (piece == null || piece.Broken) return;
            ArmorInstance current;
            if (Worn.TryGetValue(piece.Def.Slot, out current)) Armor.Add(current);
            Armor.Remove(piece);
            Worn[piece.Def.Slot] = piece;
            NotifyChanged();
        }

        public void TakeOff(ArmorSlot slot)
        {
            ArmorInstance current;
            if (!Worn.TryGetValue(slot, out current)) return;
            Worn.Remove(slot);
            Armor.Add(current);
            NotifyChanged();
        }

        /// <summary>Spreads wear over worn pieces. Returns the names of pieces that broke and fell off.</summary>
        public List<string> WearArmor(float damage)
        {
            var broken = new List<string>();
            if (Worn.Count == 0) return broken;
            float wear = damage * GameDatabase.Balance.armorWearPerDamage;
            // The chest takes most hits; other pieces take the rest at random.
            foreach (var piece in Worn.Values.ToList())
            {
                float share = piece.Def.Slot == ArmorSlot.Chest ? 0.5f : 0.25f;
                piece.Durability -= wear * share * UnityEngine.Random.Range(0.6f, 1.4f);
                if (piece.Broken)
                {
                    Worn.Remove(piece.Def.Slot);
                    broken.Add(piece.Def.name);
                }
            }
            if (broken.Count > 0) NotifyChanged();
            return broken;
        }

        public ItemDef BestOf(ItemKind kind, Func<ItemDef, float> score)
        {
            ItemDef best = null;
            float bestScore = float.MinValue;
            foreach (var kv in Items)
            {
                var def = GameDatabase.Item(kv.Key);
                if (def == null || def.Kind != kind) continue;
                float s = score(def);
                if (s > bestScore) { best = def; bestScore = s; }
            }
            return best;
        }
    }
}
