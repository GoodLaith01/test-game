using Hollowmark.Data;
using Hollowmark.Enemies;
using Hollowmark.Player;
using Hollowmark.Visuals;
using UnityEngine;

namespace Hollowmark.World
{
    /// <summary>Rolls drops when an enemy dies: gold, a random weapon, a piece of the armor it wore, and supplies.</summary>
    public static class LootTable
    {
        public static void DropFor(EnemyAI enemy)
        {
            var def = enemy.Def;
            var pos = enemy.transform.position + Vector3.up * 0.3f;
            int gold = Random.Range(def.goldMin, def.goldMax + 1) * enemy.Tier;
            if (gold > 0) LootPickup.Gold(pos, gold);

            var boss = enemy as BossAI;
            if (boss != null)
            {
                var w = GameDatabase.Weapon(boss.Boss.guaranteedWeapon);
                if (w != null) LootPickup.Weapon(pos + Vector3.right * 1.2f, w);
                var a = GameDatabase.ArmorPiece(boss.Boss.guaranteedArmor);
                if (a != null) LootPickup.Armor(pos + Vector3.left * 1.2f, a);
            }

            if (Random.value < def.weaponDropChance)
            {
                WeaponDef w = def.weaponDrops != null && def.weaponDrops.Length > 0 && Random.value < 0.7f
                    ? GameDatabase.Weapon(def.weaponDrops[Random.Range(0, def.weaponDrops.Length)])
                    : GameDatabase.RandomWeaponForTier(enemy.Tier + 1);
                if (w != null) LootPickup.Weapon(pos + Random.insideUnitSphere * 0.6f, w);
            }
            if (Random.value < def.armorDropChance)
            {
                ArmorDef a = def.armorDrops != null && def.armorDrops.Length > 0 && Random.value < 0.75f
                    ? GameDatabase.ArmorPiece(def.armorDrops[Random.Range(0, def.armorDrops.Length)])
                    : GameDatabase.RandomArmorForTier(enemy.Tier + 1);
                if (a != null) LootPickup.Armor(pos + Random.insideUnitSphere * 0.6f, a);
            }
            if (Random.value < def.itemDropChance && def.itemDrops != null && def.itemDrops.Length > 0)
            {
                var item = GameDatabase.Item(def.itemDrops[Random.Range(0, def.itemDrops.Length)]);
                if (item != null) LootPickup.Item(pos + Random.insideUnitSphere * 0.6f, item, item.Kind == ItemKind.Ammo ? Random.Range(2, 6) : 1);
            }
        }
    }

    /// <summary>A glowing item on the ground. Walk up and press E.</summary>
    public class LootPickup : Interactable
    {
        public WeaponDef WeaponDrop;
        public ArmorDef ArmorDrop;
        public ItemDef ItemDrop;
        public int Count = 1;
        public int GoldAmount;
        float spin;

        static LootPickup Make(Vector3 pos, Color c, string label)
        {
            pos.y = Mathf.Max(pos.y, 0.3f);
            var go = new GameObject("Loot_" + label);
            go.transform.position = pos;
            if (GameManager.Instance != null) go.transform.SetParent(GameManager.Instance.WorldRoot, true);
            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.6f;
            var p = go.AddComponent<LootPickup>();
            Prim.Make(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.35f, 0.35f, 0.35f), c, "Crate", false, true);
            var l = Prim.PointLight(go.transform, Vector3.up * 0.5f, c, 3f, 1.2f);
            l.shadows = LightShadows.None;
            Prim.Label(go.transform, Vector3.up * 0.8f, label, c, 0.06f);
            return p;
        }

        public static void Gold(Vector3 pos, int amount) { Make(pos, new Color(1f, 0.85f, 0.2f), amount + " gold").GoldAmount = amount; }
        public static void Weapon(Vector3 pos, WeaponDef w) { Make(pos, new Color(0.7f, 0.85f, 1f), w.name).WeaponDrop = w; }
        public static void Armor(Vector3 pos, ArmorDef a) { Make(pos, new Color(0.5f, 1f, 0.6f), a.name).ArmorDrop = a; }
        public static void Item(Vector3 pos, ItemDef i, int count)
        {
            var p = Make(pos, i.Tint, count > 1 ? i.name + " x" + count : i.name);
            p.ItemDrop = i;
            p.Count = count;
        }

        void Update()
        {
            spin += Time.deltaTime * 90f;
            if (transform.childCount > 0) transform.GetChild(0).localRotation = Quaternion.Euler(0f, spin, 0f);
        }

        public override string Prompt(Hero hero)
        {
            if (GoldAmount > 0) return "Take " + GoldAmount + " gold";
            if (WeaponDrop != null)
            {
                string req = hero.CanWield(WeaponDrop) ? "" : " (needs Strength " + WeaponDrop.requiredStrength + ")";
                return string.Format("Take {0} [dmg {1}, {2}]{3}", WeaponDrop.name, WeaponDrop.damage, WeaponDrop.weightClass, req);
            }
            if (ArmorDrop != null) return string.Format("Take {0} [{1}, def {2}]", ArmorDrop.name, ArmorDrop.slot, ArmorDrop.defense);
            if (ItemDrop != null) return "Take " + ItemDrop.name + (Count > 1 ? " x" + Count : "");
            return "Take";
        }

        public override void Interact(Hero hero)
        {
            var inv = hero.Stats.Inventory;
            if (GoldAmount > 0) { inv.Gold += GoldAmount; inv.NotifyChanged(); hero.Say("+" + GoldAmount + " gold"); }
            if (WeaponDrop != null)
            {
                inv.AddWeapon(WeaponDrop);
                hero.Say("Picked up " + WeaponDrop.name + ". Press Tab to equip it.");
            }
            if (ArmorDrop != null)
            {
                inv.AddArmor(ArmorDrop);
                // Put it on straight away if that slot is empty.
                if (!inv.Worn.ContainsKey(ArmorDrop.Slot)) inv.Wear(inv.Armor[inv.Armor.Count - 1]);
                hero.Say("Picked up " + ArmorDrop.name + ".");
            }
            if (ItemDrop != null) { inv.AddItem(ItemDrop.id, Count); hero.Say("Picked up " + ItemDrop.name + (Count > 1 ? " x" + Count : "") + "."); }
            Destroy(gameObject);
        }
    }
}
