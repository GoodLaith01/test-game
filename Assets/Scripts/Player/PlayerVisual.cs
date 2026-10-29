using System.Collections.Generic;
using Hollowmark.Data;
using Hollowmark.Visuals;
using UnityEngine;

namespace Hollowmark.Player
{
    /// <summary>
    /// Builds the hero's placeholder body and shows worn armor, the held weapon and, when every piece of
    /// armor has broken, nothing but a pair of white 18th-century smallclothes.
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        public CharacterRig Rig { get; private set; }

        readonly Dictionary<ArmorSlot, List<GameObject>> armorParts = new Dictionary<ArmorSlot, List<GameObject>>();
        GameObject weaponModel;
        WeaponDef shownWeapon;
        GameObject underwear;
        PlayerStats stats;

        public void Build()
        {
            stats = GetComponent<PlayerStats>();
            Color skin;
            Rig = CharacterBuilder.BuildHuman(transform, 1.8f, 1700, out skin);

            // Smallclothes, always worn under everything else.
            var hips = Rig.Body;
            underwear = Prim.Make(PrimitiveType.Cube, hips, new Vector3(0f, 0.87f, 0f), new Vector3(0.48f, 0.2f, 0.28f), new Color(0.96f, 0.95f, 0.9f), "Underwear");

            stats.Inventory.Changed += Refresh;
            Refresh();
        }

        void OnDestroy() { if (stats != null) stats.Inventory.Changed -= Refresh; }

        void Refresh()
        {
            var inv = stats.Inventory;
            foreach (ArmorSlot slot in System.Enum.GetValues(typeof(ArmorSlot)))
            {
                List<GameObject> parts;
                if (armorParts.TryGetValue(slot, out parts)) foreach (var p in parts) if (p != null) Destroy(p);
                armorParts[slot] = new List<GameObject>();
                ArmorInstance worn;
                if (inv.Worn.TryGetValue(slot, out worn)) BuildArmor(slot, worn.Def);
            }

            if (shownWeapon != inv.Equipped)
            {
                if (weaponModel != null) Destroy(weaponModel);
                shownWeapon = inv.Equipped;
                if (shownWeapon != null) weaponModel = CharacterBuilder.BuildWeapon(shownWeapon, Rig.Hand);
            }
            Rig.CollectRenderers();
        }

        void BuildArmor(ArmorSlot slot, ArmorDef def)
        {
            var list = armorParts[slot];
            var model = CharacterBuilder.TryModel(def.model, Rig.Body);
            if (model != null) { list.Add(model); return; }

            var c = def.Tint;
            switch (slot)
            {
                case ArmorSlot.Head:
                    if (def.id == "tricorne")
                        list.Add(Prim.Make(PrimitiveType.Cylinder, Rig.Head, new Vector3(0f, 0.16f, 0f), new Vector3(0.36f, 0.04f, 0.36f), c, "Tricorne"));
                    list.Add(Prim.Make(PrimitiveType.Sphere, Rig.Head, new Vector3(0f, 0.12f, -0.01f), new Vector3(0.27f, 0.2f, 0.28f), c, "Helmet"));
                    break;
                case ArmorSlot.Chest:
                    list.Add(Prim.Make(PrimitiveType.Cube, Rig.Body, new Vector3(0f, 1.23f, 0f), new Vector3(0.5f, 0.68f, 0.3f), c, "ChestArmor"));
                    list.Add(Prim.Make(PrimitiveType.Cube, Rig.LeftArm, new Vector3(0f, -0.2f, 0f), new Vector3(0.17f, 0.42f, 0.18f), c, "Sleeve"));
                    list.Add(Prim.Make(PrimitiveType.Cube, Rig.RightArm, new Vector3(0f, -0.2f, 0f), new Vector3(0.17f, 0.42f, 0.18f), c, "Sleeve"));
                    break;
                case ArmorSlot.Hands:
                    list.Add(Prim.Make(PrimitiveType.Cube, Rig.LeftArm, new Vector3(0f, -0.6f, 0f), new Vector3(0.17f, 0.16f, 0.18f), c, "Glove"));
                    list.Add(Prim.Make(PrimitiveType.Cube, Rig.RightArm, new Vector3(0f, -0.6f, 0f), new Vector3(0.17f, 0.16f, 0.18f), c, "Glove"));
                    break;
                case ArmorSlot.Legs:
                    foreach (var leg in Rig.Legs)
                        list.Add(Prim.Make(PrimitiveType.Cube, leg, new Vector3(0f, -0.42f, 0f), new Vector3(0.2f, 0.85f, 0.23f), c, "LegArmor"));
                    list.Add(Prim.Make(PrimitiveType.Cube, Rig.Body, new Vector3(0f, 0.87f, 0f), new Vector3(0.5f, 0.22f, 0.3f), c, "Waist"));
                    break;
            }
        }

        void Update()
        {
            // In underwear mode the smallclothes wobble a little, for comedy.
            if (underwear != null && stats.Inventory.InUnderwear)
                underwear.transform.localScale = new Vector3(0.48f + Mathf.Sin(Time.time * 12f) * 0.01f * Rig.MoveSpeed, 0.2f, 0.28f);
        }
    }
}
