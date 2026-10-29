using Hollowmark.Data;
using Hollowmark.Visuals;
using UnityEngine;

namespace Hollowmark.World
{
    /// <summary>
    /// Builds the village hub from primitives: the hero's house (bed), the training yard (dummies, weights),
    /// the market (weapons, armor, provisions), the well, villagers, and one gate per dungeon in dungeons.json.
    /// </summary>
    public static class VillageBuilder
    {
        static readonly Color Grass = new Color(0.33f, 0.45f, 0.24f);
        static readonly Color Dirt = new Color(0.5f, 0.4f, 0.28f);
        static readonly Color Timber = new Color(0.4f, 0.28f, 0.16f);
        static readonly Color Plaster = new Color(0.86f, 0.82f, 0.72f);
        static readonly Color Thatch = new Color(0.62f, 0.5f, 0.26f);
        static readonly Color Stone = new Color(0.55f, 0.55f, 0.52f);

        public static Vector3 Build(Transform root, out float yaw)
        {
            Prim.Box(root, new Vector3(0f, -0.5f, 0f), new Vector3(130f, 1f, 130f), Grass, true, "Ground");
            Prim.Box(root, new Vector3(0f, 0.01f, 10f), new Vector3(6f, 0.02f, 70f), Dirt, false, "MainRoad");
            Prim.Box(root, new Vector3(0f, 0.01f, 0f), new Vector3(60f, 0.02f, 5f), Dirt, false, "CrossRoad");

            Palisade(root, 55f);
            HeroHouse(root, new Vector3(-20f, 0f, 2f));
            TrainingYard(root, new Vector3(-20f, 0f, -16f));
            Market(root, new Vector3(18f, 0f, 0f));
            Well(root, new Vector3(0f, 0f, 0f));
            Gates(root);

            // Decorative houses for the villagers.
            Building(root, new Vector3(18f, 0f, -18f), new Vector3(7f, 3.5f, 6f), Plaster, false, "Cottage");
            Building(root, new Vector3(30f, 0f, -16f), new Vector3(6f, 3.5f, 6f), Plaster, false, "Cottage");
            Building(root, new Vector3(-32f, 0f, 16f), new Vector3(6f, 3.5f, 7f), Plaster, false, "Cottage");
            Building(root, new Vector3(30f, 0f, 18f), new Vector3(8f, 5f, 7f), Stone, false, "Church");

            for (int i = 0; i < 6; i++)
            {
                var pos = new Vector3(Random.Range(-25f, 25f), 0f, Random.Range(-20f, 20f));
                Villager.Spawn(root, pos, 300 + i);
            }

            for (int i = 0; i < 6; i++) LampPost(root, new Vector3(i % 2 == 0 ? -4f : 4f, 0f, -18f + i * 8f));

            yaw = 0f;
            return new Vector3(0f, 0.1f, -8f);
        }

        public static GameObject Building(Transform root, Vector3 pos, Vector3 size, Color wall, bool open, string name)
        {
            var b = new GameObject(name).transform;
            b.SetParent(root, false);
            b.localPosition = pos;
            float t = 0.3f, doorW = 2f;
            // Back and side walls.
            Prim.Box(b, new Vector3(0f, size.y / 2f, size.z / 2f), new Vector3(size.x, size.y, t), wall);
            Prim.Box(b, new Vector3(-size.x / 2f, size.y / 2f, 0f), new Vector3(t, size.y, size.z), wall);
            Prim.Box(b, new Vector3(size.x / 2f, size.y / 2f, 0f), new Vector3(t, size.y, size.z), wall);
            // Front wall with a doorway (or fully open).
            if (!open)
            {
                float side = (size.x - doorW) / 2f;
                Prim.Box(b, new Vector3(-(doorW + side) / 2f, size.y / 2f, -size.z / 2f), new Vector3(side, size.y, t), wall);
                Prim.Box(b, new Vector3((doorW + side) / 2f, size.y / 2f, -size.z / 2f), new Vector3(side, size.y, t), wall);
                Prim.Box(b, new Vector3(0f, size.y - 0.5f, -size.z / 2f), new Vector3(doorW, 1f, t), wall);
            }
            // Timber framing and a roof.
            for (int i = -1; i <= 1; i += 2)
                Prim.Box(b, new Vector3(i * size.x / 2f, size.y / 2f, -size.z / 2f - 0.05f), new Vector3(0.25f, size.y, 0.12f), Timber, false);
            var r1 = Prim.Box(b, new Vector3(0f, size.y + 0.7f, -size.z * 0.25f), new Vector3(size.x + 1f, 0.25f, size.z * 0.62f), Thatch, false, "RoofFront");
            r1.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            var r2 = Prim.Box(b, new Vector3(0f, size.y + 0.7f, size.z * 0.25f), new Vector3(size.x + 1f, 0.25f, size.z * 0.62f), Thatch, false, "RoofBack");
            r2.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
            return b.gameObject;
        }

        static void HeroHouse(Transform root, Vector3 pos)
        {
            var house = Building(root, pos, new Vector3(8f, 3.5f, 7f), Plaster, false, "HeroHouse").transform;
            Prim.Box(house, new Vector3(0f, 0.02f, 0f), new Vector3(7.6f, 0.04f, 6.6f), Timber * 0.8f, false, "Floorboards");
            var bed = new GameObject("Bed");
            bed.transform.SetParent(house, false);
            bed.transform.localPosition = new Vector3(-2.5f, 0f, 2f);
            Prim.Box(bed.transform, new Vector3(0f, 0.3f, 0f), new Vector3(1.4f, 0.6f, 2.2f), Timber, true, "Frame");
            Prim.Box(bed.transform, new Vector3(0f, 0.65f, 0f), new Vector3(1.3f, 0.15f, 2.1f), new Color(0.85f, 0.85f, 0.8f), false, "Mattress");
            Prim.Box(bed.transform, new Vector3(0f, 0.78f, 0.75f), new Vector3(0.8f, 0.12f, 0.4f), Color.white, false, "Pillow");
            bed.AddComponent<BoxCollider>().size = new Vector3(2f, 1.5f, 2.6f);
            bed.GetComponent<BoxCollider>().isTrigger = true;
            bed.AddComponent<Bed>();
            Prim.PointLight(house, new Vector3(0f, 3f, 0f), new Color(1f, 0.8f, 0.5f), 8f, 1.2f);
            Prim.Label(house, new Vector3(0f, 5.2f, -3.6f), "Your House", Color.white, 0.12f);
        }

        static void TrainingYard(Transform root, Vector3 pos)
        {
            var yard = new GameObject("TrainingYard").transform;
            yard.SetParent(root, false);
            yard.localPosition = pos;
            Prim.Box(yard, new Vector3(0f, 0.015f, 0f), new Vector3(14f, 0.03f, 10f), Dirt * 1.1f, false, "Sand");
            for (int i = 0; i < 8; i++)
            {
                float x = -7f + i * 2f;
                Prim.Box(yard, new Vector3(x, 0.5f, -5f), new Vector3(0.15f, 1f, 0.15f), Timber);
                Prim.Box(yard, new Vector3(x, 0.5f, 5f), new Vector3(0.15f, 1f, 0.15f), Timber);
            }
            Prim.Box(yard, new Vector3(0f, 0.8f, 5f), new Vector3(14f, 0.1f, 0.1f), Timber, false);

            for (int i = 0; i < 2; i++)
            {
                var dummy = new GameObject("TrainingDummy");
                dummy.transform.SetParent(yard, false);
                dummy.transform.localPosition = new Vector3(-3f + i * 3f, 0f, 1.5f);
                var body = new GameObject("Body").transform;
                body.SetParent(dummy.transform, false);
                Prim.Box(body, new Vector3(0f, 0.9f, 0f), new Vector3(0.15f, 1.8f, 0.15f), Timber, false);
                Prim.Make(PrimitiveType.Cylinder, body, new Vector3(0f, 1.3f, 0f), new Vector3(0.5f, 0.45f, 0.5f), Thatch, "Straw");
                Prim.Box(body, new Vector3(0f, 1.4f, 0f), new Vector3(1.2f, 0.12f, 0.12f), Timber, false, "Arms");
                Prim.Make(PrimitiveType.Sphere, body, new Vector3(0f, 1.95f, 0f), Vector3.one * 0.35f, Thatch, "Head");
                var col = dummy.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 1f, 0f);
                col.height = 2f;
                col.radius = 0.4f;
                dummy.AddComponent<TrainingDummy>();
            }

            for (int i = 0; i < 2; i++)
            {
                var rack = new GameObject(i == 0 ? "Weights_Light" : "Weights_Heavy");
                rack.transform.SetParent(yard, false);
                rack.transform.localPosition = new Vector3(4f, 0f, -2f + i * 3f);
                float w = i == 0 ? 1f : 2f;
                Prim.Box(rack.transform, new Vector3(0f, 0.4f, 0f), new Vector3(2f, 0.1f, 0.6f), Timber, true);
                Prim.Make(PrimitiveType.Cylinder, rack.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.06f, 1f, 0.06f), Stone, "Bar").transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                for (int s = -1; s <= 1; s += 2)
                    Prim.Make(PrimitiveType.Cylinder, rack.transform, new Vector3(s * 0.8f, 0.6f, 0f), new Vector3(0.35f * w, 0.08f, 0.35f * w), new Color(0.2f, 0.2f, 0.22f), "Plate").transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                var trigger = rack.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(2.5f, 1.5f, 1.5f);
                trigger.center = Vector3.up * 0.7f;
                rack.AddComponent<WeightRack>().Weight = w;
                Prim.Label(rack.transform, new Vector3(0f, 1.5f, 0f), i == 0 ? "Light Weights" : "Heavy Weights", Color.white, 0.07f);
            }
            Prim.Label(yard, new Vector3(0f, 3.5f, 0f), "Training Yard", Color.white, 0.12f);
        }

        static void Market(Transform root, Vector3 pos)
        {
            var market = new GameObject("Market").transform;
            market.SetParent(root, false);
            market.localPosition = pos;
            Stall(market, new Vector3(0f, 0f, -8f), ShopKind.Weapons, "Weaponsmith Hale", new Color(0.6f, 0.2f, 0.2f), 11);
            Stall(market, new Vector3(0f, 0f, 0f), ShopKind.Armor, "Armorer Briggs", new Color(0.2f, 0.3f, 0.6f), 12);
            Stall(market, new Vector3(0f, 0f, 8f), ShopKind.Provisions, "Widow Marsh (food, water, medicine)", new Color(0.25f, 0.5f, 0.25f), 13);
            Prim.Label(market, new Vector3(0f, 5f, 0f), "Market", Color.white, 0.14f);
        }

        static void Stall(Transform parent, Vector3 pos, ShopKind kind, string keeper, Color awning, int seed)
        {
            var stall = new GameObject("Stall_" + kind);
            stall.transform.SetParent(parent, false);
            stall.transform.localPosition = pos;
            stall.transform.localRotation = Quaternion.Euler(0f, -90f, 0f); // facing the main road
            var t = stall.transform;
            Prim.Box(t, new Vector3(0f, 0.5f, 0f), new Vector3(4f, 1f, 1f), Timber);
            for (int i = -1; i <= 1; i += 2)
            {
                Prim.Box(t, new Vector3(i * 2f, 1.5f, 0.4f), new Vector3(0.15f, 3f, 0.15f), Timber, false);
                Prim.Box(t, new Vector3(i * 2f, 1.5f, 1.8f), new Vector3(0.15f, 3f, 0.15f), Timber, false);
            }
            var roof = Prim.Box(t, new Vector3(0f, 3.1f, 1.1f), new Vector3(4.6f, 0.1f, 2.4f), awning, false, "Awning");
            roof.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);

            // Goods on the counter.
            for (int i = 0; i < 4; i++)
                Prim.Box(t, new Vector3(-1.5f + i, 1.1f, 0f), new Vector3(0.3f, 0.2f, 0.4f), Prim.Vary(awning, 0.3f), false, "Goods");

            Color skin;
            var keeperRig = Visuals.CharacterBuilder.BuildHuman(t, 1.75f, seed, out skin);
            keeperRig.transform.localPosition = new Vector3(0f, 0f, 1.2f);
            keeperRig.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Prim.Make(PrimitiveType.Cube, keeperRig.Body, new Vector3(0f, 1.23f, 0f), new Vector3(0.5f, 0.68f, 0.3f), Prim.Vary(awning, 0.2f), "Apron");
            Prim.Make(PrimitiveType.Cube, keeperRig.Body, new Vector3(0f, 0.6f, 0f), new Vector3(0.48f, 0.6f, 0.28f), new Color(0.3f, 0.25f, 0.2f), "Breeches");

            var trigger = stall.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(4.5f, 2.5f, 3f);
            trigger.center = new Vector3(0f, 1f, -1f);
            var shop = stall.AddComponent<ShopStall>();
            shop.Kind = kind;
            shop.Keeper = keeper;
            Prim.Label(t, new Vector3(0f, 3.8f, 0f), keeper, Color.white, 0.07f);
        }

        static void Well(Transform root, Vector3 pos)
        {
            var well = new GameObject("Well");
            well.transform.SetParent(root, false);
            well.transform.localPosition = pos;
            Prim.Make(PrimitiveType.Cylinder, well.transform, new Vector3(0f, 0.45f, 0f), new Vector3(2f, 0.45f, 2f), Stone, "Ring", true);
            Prim.Make(PrimitiveType.Cylinder, well.transform, new Vector3(0f, 0.92f, 0f), new Vector3(1.6f, 0.02f, 1.6f), new Color(0.15f, 0.25f, 0.4f), "Water");
            for (int i = -1; i <= 1; i += 2) Prim.Box(well.transform, new Vector3(i * 0.9f, 1.4f, 0f), new Vector3(0.12f, 2f, 0.12f), Timber, false);
            Prim.Box(well.transform, new Vector3(0f, 2.4f, 0f), new Vector3(2.3f, 0.12f, 1.2f), Thatch, false);
            var t = well.AddComponent<SphereCollider>();
            t.isTrigger = true;
            t.radius = 1.8f;
            well.AddComponent<Well>();
        }

        static void Gates(Transform root)
        {
            var dungeons = GameDatabase.Dungeons;
            float spacing = 9f;
            float start = -spacing * (dungeons.Count - 1) / 2f;
            for (int i = 0; i < dungeons.Count; i++)
            {
                var d = dungeons[i];
                var gate = new GameObject("Gate_" + d.id);
                gate.transform.SetParent(root, false);
                gate.transform.localPosition = new Vector3(start + i * spacing, 0f, 44f);
                var t = gate.transform;
                Prim.Box(t, new Vector3(-1.8f, 2.2f, 0f), new Vector3(0.9f, 4.4f, 0.9f), d.Gate);
                Prim.Box(t, new Vector3(1.8f, 2.2f, 0f), new Vector3(0.9f, 4.4f, 0.9f), d.Gate);
                Prim.Box(t, new Vector3(0f, 4.8f, 0f), new Vector3(4.6f, 0.9f, 1f), d.Gate);
                // The portal surface glows in the dungeon's accent colour.
                bool unlocked = GameManager.Instance.IsUnlocked(d);
                Prim.Make(PrimitiveType.Cube, t, new Vector3(0f, 2.1f, 0.1f), new Vector3(2.7f, 4.2f, 0.1f),
                    unlocked ? d.Accent * 0.8f : new Color(0.1f, 0.1f, 0.1f), "Portal", false, true);
                if (!unlocked)
                    for (int b = 0; b < 4; b++) Prim.Box(t, new Vector3(-1.05f + b * 0.7f, 2.1f, -0.1f), new Vector3(0.12f, 4.2f, 0.12f), new Color(0.25f, 0.2f, 0.2f), false, "Bar");
                if (GameManager.Instance.BossDefeated(d))
                    Prim.Make(PrimitiveType.Sphere, t, new Vector3(0f, 5.8f, 0f), Vector3.one * 0.6f, new Color(1f, 0.85f, 0.2f), "Trophy", false, true);
                Prim.PointLight(t, new Vector3(0f, 2f, -1.5f), d.Accent, 6f, unlocked ? 1.5f : 0.3f);
                var trigger = gate.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(3f, 4f, 3f);
                trigger.center = new Vector3(0f, 2f, -1f);
                gate.AddComponent<DungeonGate>().Dungeon = d;
                Prim.Label(t, new Vector3(0f, 6.6f, 0f), d.name + "\nTier " + d.tier, Color.white, 0.08f);
            }
        }

        static void Palisade(Transform root, float half)
        {
            for (int side = 0; side < 4; side++)
            {
                for (float x = -half; x <= half; x += 1.2f)
                {
                    // Leave a gap in the north wall behind the dungeon gates so they read as "outside".
                    Vector3 p = side == 0 ? new Vector3(x, 1.8f, -half) : side == 1 ? new Vector3(x, 1.8f, half + 2f)
                        : side == 2 ? new Vector3(-half, 1.8f, x) : new Vector3(half, 1.8f, x);
                    Prim.Make(PrimitiveType.Cylinder, root, p, new Vector3(0.5f, 1.8f + Random.Range(-0.2f, 0.3f), 0.5f), Timber, "Stake", true);
                }
            }
        }

        static void LampPost(Transform root, Vector3 pos)
        {
            Prim.Box(root, pos + Vector3.up * 1.5f, new Vector3(0.15f, 3f, 0.15f), new Color(0.15f, 0.15f, 0.15f), true, "Lamp");
            Prim.Make(PrimitiveType.Cube, root, pos + Vector3.up * 3.1f, Vector3.one * 0.3f, new Color(1f, 0.85f, 0.5f), "LampGlass", false, true);
            Prim.PointLight(root, pos + Vector3.up * 3f, new Color(1f, 0.75f, 0.45f), 9f, 1f);
        }
    }
}
