using System.Collections.Generic;
using Hollowmark.Data;
using Hollowmark.Visuals;
using UnityEngine;

namespace Hollowmark.Player
{
    /// <summary>
    /// Builds the hero's body and shows worn armor, the held weapon and, when every piece of
    /// armor has broken, nothing but a pair of white 18th-century smallclothes.
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        public CharacterRig Rig { get; private set; }

        readonly Dictionary<ArmorSlot, List<GameObject>> armorParts = new Dictionary<ArmorSlot, List<GameObject>>();
        GameObject weaponModel;
        WeaponDef shownWeapon;
        List<GameObject> underwear;
        PlayerStats stats;

        public void Build()
        {
            stats = GetComponent<PlayerStats>();
            Color skin;
            Rig = CharacterBuilder.BuildHuman(transform, 1.8f, 1700, out skin);

            // Smallclothes, always worn under everything else.
            underwear = Rig.Cover(new Color(0.96f, 0.95f, 0.9f), 0.005f, "Underwear", BodyRegion.Pelvis);

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
                    if (def.id == "tricorne") list.Add(Prim.Shape(BodyMesh.Get("Tricorne"), Rig.Skull, Vector3.zero, Vector3.one, c, "Tricorne"));
                    else if (def.id == "gnarl_mask" || def.id == "forsaken_helm") list.AddRange(Rig.Cover(c, 0.012f, "Helm", BodyRegion.Head));
                    else
                    {
                        list.Add(Prim.Shape(BodyMesh.Get("Cap"), Rig.Skull, Vector3.zero, Vector3.one * 1.04f, c, "Helmet"));
                        if (def.id == "morion") // the crest along the top
                            list.Add(Prim.Make(PrimitiveType.Sphere, Rig.Skull, new Vector3(0f, 0.25f, -0.01f), new Vector3(0.012f, 0.06f, 0.2f), c, "Crest"));
                    }
                    break;
                case ArmorSlot.Chest:
                    bool plate = def.id == "cuirass" || def.id == "forsaken_plate";
                    list.AddRange(Rig.Cover(c, plate ? 0.02f : 0.012f, "ChestArmor", BodyRegion.Chest));
                    if (!plate) list.AddRange(Rig.Cover(c, 0.01f, "Sleeve", BodyRegion.UpperArm, BodyRegion.Forearm));
                    if (def.id.EndsWith("_coat"))
                        list.Add(Prim.Shape(BodyMesh.Get("CoatTail"), Rig.Torso, Vector3.zero, Rig.TrunkScale * 1.05f, c, "CoatTail"));
                    break;
                case ArmorSlot.Hands:
                    list.AddRange(Rig.Cover(c, 0.007f, "Glove", BodyRegion.Hand));
                    break;
                case ArmorSlot.Legs:
                    var shoes = new Color(0.1f, 0.08f, 0.06f);
                    if (def.id == "breeches")
                    {
                        list.AddRange(Rig.Cover(c, 0.009f, "Breeches", BodyRegion.Pelvis, BodyRegion.Thigh));
                        list.AddRange(Rig.Cover(new Color(0.9f, 0.88f, 0.84f), 0.005f, "Stockings", BodyRegion.Shin));
                        list.AddRange(Rig.Cover(shoes, 0.007f, "Shoes", BodyRegion.Foot));
                    }
                    else if (def.id == "jackboots")
                    {
                        list.AddRange(Rig.Cover(c * 1.4f, 0.01f, "Chaps", BodyRegion.Pelvis, BodyRegion.Thigh));
                        list.AddRange(Rig.Cover(c, 0.015f, "Boots", BodyRegion.Shin, BodyRegion.Foot));
                    }
                    else
                    {
                        list.AddRange(Rig.Cover(c, 0.018f, "LegArmor", BodyRegion.Pelvis, BodyRegion.Thigh, BodyRegion.Shin));
                        list.AddRange(Rig.Cover(shoes, 0.008f, "Shoes", BodyRegion.Foot));
                    }
                    break;
            }
        }

        void Update()
        {
            // In underwear mode the smallclothes wobble a little, for comedy.
            if (underwear == null || !stats.Inventory.InUnderwear) return;
            var basis = Rig.TrunkScale;
            foreach (var u in underwear)
                if (u != null) u.transform.localScale = new Vector3(basis.x * (1f + Mathf.Sin(Time.time * 12f) * 0.02f * Rig.MoveSpeed), basis.y, basis.z);
        }
    }
}
