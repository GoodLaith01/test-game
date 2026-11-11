using System.Linq;
using Hollowmark.Data;
using Hollowmark.Player;
using Hollowmark.UI;
using Hollowmark.Visuals;
using Hollowmark.World;
using UnityEngine;

namespace Hollowmark.Crafting
{
    /// <summary>
    /// Adds the homestead to the village: a cooking hearth, one building plot per blueprint, the four
    /// quest-giving villagers, and two merchants (Goody Pell's larder and Master Crowe's very expensive cart).
    /// </summary>
    public static class HomesteadBuilder
    {
        public static readonly Color Timber = new Color(0.4f, 0.28f, 0.16f);
        public static readonly Color DarkTimber = new Color(0.26f, 0.18f, 0.1f);
        public static readonly Color Stone = new Color(0.55f, 0.55f, 0.52f);
        public static readonly Color Soil = new Color(0.3f, 0.22f, 0.14f);
        public static readonly Color Thatch = new Color(0.62f, 0.5f, 0.26f);
        public static readonly Color Leaf = new Color(0.28f, 0.5f, 0.2f);
        public static readonly Color Fire = new Color(1f, 0.55f, 0.15f);

        // West of the hero's house, inside the palisade.
        static readonly Vector3 Centre = new Vector3(-40f, 0f, 0f);
        static readonly Vector3[] PlotOffsets =
        {
            new Vector3(-8f, 0f, -8f), new Vector3(0f, 0f, -8f), new Vector3(8f, 0f, -8f),
            new Vector3(-8f, 0f, 8f), new Vector3(0f, 0f, 8f), new Vector3(8f, 0f, 8f),
        };

        static bool toldPlayer;

        public static void Build(Transform root)
        {
            var home = new GameObject("Homestead").transform;
            home.SetParent(root, false);
            home.localPosition = Centre;
            Prim.Box(home, new Vector3(0f, 0.012f, 0f), new Vector3(26f, 0.02f, 4f), new Color(0.5f, 0.4f, 0.28f), false, "Path");
            Prim.Label(home, new Vector3(0f, 6.5f, 0f), "Your Homestead", Color.white, 0.12f);

            Hearth(home, new Vector3(3.5f, 0f, 0f));

            var blueprints = CraftingDatabase.Blueprints;
            for (int i = 0; i < blueprints.Count && i < PlotOffsets.Length; i++)
            {
                var go = new GameObject("Plot_" + blueprints[i].id);
                go.transform.SetParent(home, false);
                go.transform.localPosition = PlotOffsets[i];
                // Face the path between the two rows.
                go.transform.localRotation = Quaternion.Euler(0f, PlotOffsets[i].z < 0f ? 180f : 0f, 0f);
                var trigger = go.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(6.5f, 3f, 7f);
                trigger.center = new Vector3(0f, 1.5f, -0.5f);
                go.AddComponent<HomesteadPlot>().Blueprint = blueprints[i];
            }

            QuestGiver.Spawn(home, new Vector3(5.5f, 0f, 1.6f), "Granny Hobb", 41, new Color(0.45f, 0.25f, 0.3f), true, 1.58f);
            QuestGiver.Spawn(home, new Vector3(-4f, 0f, 1.4f), "Abel the Joiner", 42, new Color(0.35f, 0.3f, 0.2f), false, 1.8f);
            QuestGiver.Spawn(root, new Vector3(-28f, 0f, 36f), "Sergeant Pike", 43, new Color(0.55f, 0.1f, 0.1f), false, 1.85f);
            QuestGiver.Spawn(root, new Vector3(22f, 0f, 25f), "Mistress Thorne", 44, new Color(0.25f, 0.4f, 0.3f), true, 1.7f);

            if (!toldPlayer)
            {
                toldPlayer = true;
                Hud.Push("Your homestead is west of your house: cook at the hearth and build on the plots.");
                Hud.Push("Villagers with a yellow ! have tasks for you. Goody Pell sells ingredients and materials.");
            }

            var cfg = CraftingDatabase.Config;
            Merchant(root, new Vector3(-10f, 0f, 27f), -90f, CraftMerchant.Kind.Larder, cfg.larderName + "'s Larder & Lumber",
                "Ingredients and building materials", new Color(0.3f, 0.45f, 0.2f), 45);
            Merchant(root, new Vector3(10f, 0f, 27f), 90f, CraftMerchant.Kind.Peddler, cfg.peddlerName + "'s Fine Goods",
                "Rare dishes, recipes and blueprints. Very expensive.", new Color(0.45f, 0.15f, 0.5f), 46);
        }

        static void Hearth(Transform parent, Vector3 pos)
        {
            var go = new GameObject("CookingHearth");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var t = go.transform;
            // A stone fire pit with an iron cauldron on a tripod, under a little thatched shelter.
            Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0f, 0.2f, 0f), new Vector3(1.8f, 0.2f, 1.8f), Stone, "Pit", true);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Prim.Make(PrimitiveType.Sphere, t, new Vector3(Mathf.Cos(a) * 0.85f, 0.42f, Mathf.Sin(a) * 0.85f), new Vector3(0.42f, 0.3f, 0.42f), Prim.Vary(Stone, 0.15f), "Rim");
            }
            for (int i = 0; i < 3; i++)
            {
                var log = Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0f, 0.45f, 0f), new Vector3(0.12f, 0.45f, 0.12f), DarkTimber, "Log");
                log.transform.localRotation = Quaternion.Euler(80f, i * 60f, 0f);
            }
            Prim.Make(PrimitiveType.Sphere, t, new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 0.4f, 0.6f), Fire, "Flames", false, true);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                var leg = Prim.Make(PrimitiveType.Cylinder, t, new Vector3(Mathf.Cos(a) * 0.55f, 1f, Mathf.Sin(a) * 0.55f), new Vector3(0.05f, 1f, 0.05f), new Color(0.15f, 0.15f, 0.15f), "Tripod");
                leg.transform.localRotation = Quaternion.Euler(Mathf.Sin(a) * 18f, 0f, -Mathf.Cos(a) * 18f);
            }
            Prim.Make(PrimitiveType.Sphere, t, new Vector3(0f, 1.15f, 0f), new Vector3(0.75f, 0.6f, 0.75f), new Color(0.12f, 0.12f, 0.13f), "Cauldron");
            Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0f, 1.42f, 0f), new Vector3(0.6f, 0.01f, 0.6f), new Color(0.55f, 0.35f, 0.15f), "Stew");
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    Prim.Box(t, new Vector3(i * 1.4f, 1.4f, j * 1.4f), new Vector3(0.15f, 2.8f, 0.15f), Timber, false);
            Prim.Box(t, new Vector3(0f, 2.9f, 0f), new Vector3(3.4f, 0.2f, 3.4f), Thatch, false, "Shelter");
            // A work table for chopping.
            Prim.Box(t, new Vector3(0f, 0.45f, 1.9f), new Vector3(1.6f, 0.9f, 0.7f), Timber, true, "Table");
            Prim.Make(PrimitiveType.Sphere, t, new Vector3(-0.4f, 0.98f, 1.9f), new Vector3(0.18f, 0.16f, 0.18f), new Color(0.85f, 0.7f, 0.4f), "Onion");
            Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0.3f, 0.95f, 1.9f), new Vector3(0.3f, 0.05f, 0.3f), new Color(0.7f, 0.55f, 0.35f), "Board");
            var light = Prim.PointLight(t, new Vector3(0f, 1.4f, 0f), new Color(1f, 0.6f, 0.3f), 9f, 1.6f);
            go.AddComponent<Flicker>().Target = light;
            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2.4f;
            go.AddComponent<CookingHearth>();
            Prim.Label(t, new Vector3(0f, 3.6f, 0f), "Cooking Hearth", Color.white, 0.08f);
        }

        static void Merchant(Transform root, Vector3 pos, float yaw, CraftMerchant.Kind kind, string title, string sub, Color colour, int seed)
        {
            var go = new GameObject("Merchant_" + kind);
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var t = go.transform;
            if (kind == CraftMerchant.Kind.Peddler)
            {
                // A painted travelling wagon with a striped canopy and gilded trim.
                Prim.Box(t, new Vector3(0f, 1.1f, 0.6f), new Vector3(3.6f, 1.2f, 2f), colour, true, "Wagon");
                Prim.Box(t, new Vector3(0f, 1.75f, -0.42f), new Vector3(3.7f, 0.1f, 0.1f), new Color(0.95f, 0.75f, 0.2f), false, "Gilt");
                for (int i = 0; i < 6; i++)
                {
                    var stripe = Prim.Box(t, new Vector3(-1.5f + i * 0.6f, 3f, 0.3f), new Vector3(0.6f, 0.08f, 2.8f), i % 2 == 0 ? colour * 1.2f : new Color(0.95f, 0.9f, 0.75f), false, "Canopy");
                    stripe.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
                }
                for (int i = -1; i <= 1; i += 2)
                {
                    var wheel = Prim.Make(PrimitiveType.Cylinder, t, new Vector3(i * 1.3f, 0.5f, -0.45f), new Vector3(1f, 0.06f, 1f), DarkTimber, "Wheel");
                    wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Prim.Box(t, new Vector3(i * 1.85f, 2.2f, -0.35f), new Vector3(0.12f, 1.8f, 0.12f), new Color(0.95f, 0.75f, 0.2f), false, "Post");
                }
                // Bottles and jars of fine food on the counter.
                for (int i = 0; i < 5; i++)
                    Prim.Make(PrimitiveType.Cylinder, t, new Vector3(-1.2f + i * 0.6f, 1.9f, -0.2f), new Vector3(0.18f, 0.2f, 0.18f),
                        i % 2 == 0 ? new Color(0.3f, 0.6f, 0.4f) : new Color(0.8f, 0.4f, 0.2f), "Jar", false, i == 2);
                Prim.PointLight(t, new Vector3(0f, 2.6f, -1.2f), new Color(1f, 0.8f, 0.55f), 7f, 1.2f);
            }
            else
            {
                // An open lean-to with sacks, crates and stacked timber and stone.
                Prim.Box(t, new Vector3(0f, 0.5f, 0f), new Vector3(4f, 1f, 1f), Timber, true, "Counter");
                for (int i = -1; i <= 1; i += 2)
                    Prim.Box(t, new Vector3(i * 2f, 1.4f, 1.6f), new Vector3(0.18f, 2.8f, 0.18f), Timber, false);
                var roof = Prim.Box(t, new Vector3(0f, 2.7f, 0.8f), new Vector3(4.6f, 0.15f, 2.6f), Thatch, false, "Roof");
                roof.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
                for (int i = 0; i < 3; i++)
                    Prim.Make(PrimitiveType.Sphere, t, new Vector3(-1.4f + i * 0.5f, 1.25f, 0f), new Vector3(0.45f, 0.5f, 0.4f), new Color(0.85f, 0.8f, 0.65f), "Sack");
                Prim.Make(PrimitiveType.Sphere, t, new Vector3(0.6f, 1.15f, 0f), new Vector3(0.3f, 0.3f, 0.3f), new Color(0.8f, 0.6f, 0.2f), "Honey");
                for (int i = 0; i < 4; i++)
                    Prim.Box(t, new Vector3(2.8f, 0.15f + i * 0.22f, -0.2f), new Vector3(0.6f, 0.2f, 2.4f), Prim.Vary(Timber, 0.15f), true, "Plank");
                for (int i = 0; i < 3; i++)
                    Prim.Box(t, new Vector3(-2.9f + (i % 2) * 0.1f, 0.3f + i * 0.5f, -0.2f), new Vector3(0.7f, 0.48f, 0.7f), Prim.Vary(Stone, 0.1f), true, "Stone");
            }

            Color skin;
            var keeper = CharacterBuilder.BuildHuman(t, kind == CraftMerchant.Kind.Peddler ? 1.82f : 1.62f, seed, out skin, kind == CraftMerchant.Kind.Larder);
            keeper.transform.localPosition = new Vector3(0f, 0f, kind == CraftMerchant.Kind.Peddler ? -1f : 1f);
            keeper.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            CharacterBuilder.DressVillager(keeper, Prim.Vary(colour, 0.15f), kind == CraftMerchant.Kind.Larder);

            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(4.6f, 2.5f, 3.4f);
            trigger.center = new Vector3(0f, 1f, -1.4f);
            var m = go.AddComponent<CraftMerchant>();
            m.Type = kind;
            m.Title = title;
            Prim.Label(t, new Vector3(0f, 4f, 0f), title + "\n<size=30>" + sub + "</size>", Color.white, 0.07f);
        }

        // ---------- Building visuals ----------

        /// <summary>Draws a finished building, or an empty staked-out plot, under the given parent.</summary>
        public static void DrawPlot(Transform t, BlueprintDef b, bool built, bool known)
        {
            if (!built)
            {
                Prim.Box(t, new Vector3(0f, 0.015f, 0f), new Vector3(5.5f, 0.03f, 5.5f), Soil, false, "Dirt");
                for (int i = -1; i <= 1; i += 2)
                    for (int j = -1; j <= 1; j += 2)
                        Prim.Box(t, new Vector3(i * 2.7f, 0.4f, j * 2.7f), new Vector3(0.12f, 0.8f, 0.12f), Timber, false, "Stake");
                for (int i = -1; i <= 1; i += 2)
                {
                    Prim.Box(t, new Vector3(0f, 0.7f, i * 2.7f), new Vector3(5.4f, 0.03f, 0.03f), new Color(0.75f, 0.65f, 0.45f), false, "Rope");
                    Prim.Box(t, new Vector3(i * 2.7f, 0.7f, 0f), new Vector3(0.03f, 0.03f, 5.4f), new Color(0.75f, 0.65f, 0.45f), false, "Rope");
                }
                Prim.Box(t, new Vector3(1.8f, 0.6f, -2.9f), new Vector3(0.1f, 1.2f, 0.1f), Timber, false, "SignPost");
                Prim.Box(t, new Vector3(1.8f, 1.15f, -2.95f), new Vector3(0.9f, 0.5f, 0.05f), new Color(0.75f, 0.6f, 0.4f), false, "Sign");
                Prim.Label(t, new Vector3(0f, 2.2f, -2.5f), known ? "Plot: " + b.name + "\n<size=30>press E to build</size>" : "Empty plot\n<size=30>needs a blueprint</size>",
                    known ? new Color(1f, 0.9f, 0.6f) : new Color(0.75f, 0.75f, 0.75f), 0.06f);
                return;
            }

            switch (b.id)
            {
                case "herb_garden": Garden(t); break;
                case "stone_oven": Oven(t); break;
                case "apothecary_still": Still(t); break;
                case "grinding_wheel": Wheel(t); break;
                case "smokehouse": Smokehouse(t); break;
                case "bathhouse": Bathhouse(t); break;
                default: VillageBuilder.Building(t, Vector3.zero, new Vector3(4.5f, 3f, 4.5f), new Color(0.86f, 0.82f, 0.72f), false, b.name); break;
            }
            Prim.Label(t, new Vector3(0f, 4.6f, 0f), b.name, Color.white, 0.07f);
        }

        static void Garden(Transform t)
        {
            for (int row = 0; row < 3; row++)
            {
                float z = -1.6f + row * 1.6f;
                Prim.Box(t, new Vector3(0f, 0.2f, z), new Vector3(4.6f, 0.4f, 1.1f), Timber, true, "Bed");
                Prim.Box(t, new Vector3(0f, 0.41f, z), new Vector3(4.4f, 0.04f, 0.9f), Soil, false, "Soil");
                for (int i = 0; i < 7; i++)
                {
                    var c = row == 0 ? Leaf : row == 1 ? new Color(0.85f, 0.85f, 0.75f) : new Color(0.55f, 0.65f, 0.25f);
                    Prim.Make(PrimitiveType.Sphere, t, new Vector3(-1.95f + i * 0.65f, 0.55f, z), new Vector3(0.32f, 0.28f + (i % 3) * 0.06f, 0.32f), Prim.Vary(c, 0.12f), "Plant");
                }
            }
            for (int i = 0; i < 9; i++) Prim.Box(t, new Vector3(-2.8f + i * 0.7f, 0.35f, 2.8f), new Vector3(0.08f, 0.7f, 0.08f), Timber, false, "Picket");
            Prim.Box(t, new Vector3(0f, 0.55f, 2.8f), new Vector3(5.8f, 0.06f, 0.06f), Timber, false, "Rail");
            Prim.Make(PrimitiveType.Cylinder, t, new Vector3(2.5f, 0.3f, -2.6f), new Vector3(0.5f, 0.3f, 0.5f), DarkTimber, "WaterButt", true);
        }

        static void Oven(Transform t)
        {
            Prim.Box(t, new Vector3(0f, 0.5f, 0.5f), new Vector3(3.4f, 1f, 3f), Prim.Vary(Stone, 0.05f), true, "Base");
            Prim.Make(PrimitiveType.Sphere, t, new Vector3(0f, 1.1f, 0.5f), new Vector3(2.8f, 2f, 2.6f), new Color(0.6f, 0.45f, 0.35f), "Dome", true);
            Prim.Make(PrimitiveType.Cube, t, new Vector3(0f, 1.35f, -0.82f), new Vector3(0.9f, 0.6f, 0.1f), Fire, "OvenMouth", false, true);
            Prim.Box(t, new Vector3(0.8f, 2.6f, 1f), new Vector3(0.5f, 1.6f, 0.5f), Stone * 0.85f, false, "Chimney");
            for (int i = 0; i < 4; i++)
            {
                var log = Prim.Make(PrimitiveType.Cylinder, t, new Vector3(-2.2f, 0.15f + (i / 2) * 0.28f, -0.3f + (i % 2) * 0.3f), new Vector3(0.25f, 0.6f, 0.25f), DarkTimber, "Firewood");
                log.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            Prim.Box(t, new Vector3(1.6f, 0.45f, -1.6f), new Vector3(1.2f, 0.9f, 0.6f), Timber, true, "BakersTable");
            for (int i = 0; i < 3; i++)
                Prim.Make(PrimitiveType.Sphere, t, new Vector3(1.25f + i * 0.35f, 0.98f, -1.6f), new Vector3(0.3f, 0.16f, 0.22f), new Color(0.8f, 0.55f, 0.25f), "Loaf");
            var light = Prim.PointLight(t, new Vector3(0f, 1.3f, -1.4f), new Color(1f, 0.55f, 0.25f), 6f, 1.4f);
            t.gameObject.AddComponent<Flicker>().Target = light;
        }

        static void Still(Transform t)
        {
            ShedFrame(t, new Color(0.35f, 0.42f, 0.32f));
            var copper = new Color(0.72f, 0.42f, 0.2f);
            Prim.Make(PrimitiveType.Sphere, t, new Vector3(-0.6f, 0.9f, 0.6f), new Vector3(1.1f, 1.2f, 1.1f), copper, "Pot", true);
            Prim.Make(PrimitiveType.Cylinder, t, new Vector3(-0.6f, 1.7f, 0.6f), new Vector3(0.25f, 0.3f, 0.25f), copper, "Neck");
            var pipe = Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0.1f, 1.8f, 0.6f), new Vector3(0.08f, 0.6f, 0.08f), copper, "Pipe");
            pipe.transform.localRotation = Quaternion.Euler(0f, 0f, 70f);
            Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0.9f, 0.6f, 0.6f), new Vector3(0.6f, 0.6f, 0.6f), DarkTimber, "Condenser", true);
            Prim.Make(PrimitiveType.Sphere, t, new Vector3(-0.6f, 0.2f, 0.6f), new Vector3(0.6f, 0.3f, 0.6f), Fire, "Burner", false, true);
            Prim.Box(t, new Vector3(1.2f, 1.6f, 1.6f), new Vector3(1.4f, 0.08f, 0.4f), Timber, false, "Shelf");
            for (int i = 0; i < 4; i++)
                Prim.Make(PrimitiveType.Sphere, t, new Vector3(0.7f + i * 0.33f, 1.78f, 1.6f), Vector3.one * 0.22f, i % 2 == 0 ? new Color(0.3f, 0.9f, 0.5f) : new Color(0.6f, 0.9f, 0.3f), "Flask", false, true);
            Prim.PointLight(t, new Vector3(0f, 1.6f, 0f), new Color(0.5f, 1f, 0.6f), 5f, 0.9f);
        }

        static void Wheel(Transform t)
        {
            ShedFrame(t, Thatch);
            Prim.Box(t, new Vector3(-0.5f, 0.6f, 0.4f), new Vector3(0.15f, 1.2f, 0.9f), Timber, true, "FrameL");
            Prim.Box(t, new Vector3(0.5f, 0.6f, 0.4f), new Vector3(0.15f, 1.2f, 0.9f), Timber, true, "FrameR");
            var wheel = Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0f, 1.1f, 0.4f), new Vector3(1.4f, 0.12f, 1.4f), new Color(0.7f, 0.68f, 0.62f), "Grindstone");
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.AddComponent<Spin>().Axis = Vector3.up;
            Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0f, 0.45f, -0.3f), new Vector3(0.5f, 0.25f, 0.5f), DarkTimber, "Trough");
            Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0f, 0.7f, -0.3f), new Vector3(0.42f, 0.01f, 0.42f), new Color(0.2f, 0.35f, 0.5f), "Water");
            Prim.Box(t, new Vector3(1.6f, 0.45f, 1f), new Vector3(1f, 0.9f, 0.6f), Timber, true, "Bench");
            var blade = Prim.Box(t, new Vector3(1.6f, 0.95f, 1f), new Vector3(0.9f, 0.03f, 0.08f), new Color(0.8f, 0.8f, 0.85f), false, "Blade");
            blade.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
        }

        static void Smokehouse(Transform t)
        {
            var wall = new Color(0.3f, 0.22f, 0.15f);
            Prim.Box(t, new Vector3(0f, 1.6f, 0.6f), new Vector3(3.2f, 3.2f, 0.2f), wall, true, "Back");
            Prim.Box(t, new Vector3(-1.6f, 1.6f, -0.4f), new Vector3(0.2f, 3.2f, 2.2f), wall, true, "Left");
            Prim.Box(t, new Vector3(1.6f, 1.6f, -0.4f), new Vector3(0.2f, 3.2f, 2.2f), wall, true, "Right");
            Prim.Box(t, new Vector3(-1.05f, 1.6f, -1.5f), new Vector3(0.9f, 3.2f, 0.2f), wall, true, "FrontL");
            Prim.Box(t, new Vector3(1.05f, 1.6f, -1.5f), new Vector3(0.9f, 3.2f, 0.2f), wall, true, "FrontR");
            for (int i = -1; i <= 1; i += 2)
            {
                var r = Prim.Box(t, new Vector3(i * 0.9f, 3.6f, -0.4f), new Vector3(2.1f, 0.15f, 2.6f), new Color(0.2f, 0.18f, 0.16f), false, "Roof");
                r.transform.localRotation = Quaternion.Euler(0f, 0f, -i * 35f);
            }
            Prim.Box(t, new Vector3(0f, 4.4f, -0.4f), new Vector3(0.5f, 0.8f, 0.5f), Stone * 0.7f, false, "Vent");
            Prim.Box(t, new Vector3(0f, 2.4f, -0.4f), new Vector3(2.9f, 0.06f, 0.06f), Timber, false, "Rail");
            for (int i = 0; i < 4; i++)
            {
                Prim.Box(t, new Vector3(-1f + i * 0.65f, 2.2f, -0.4f), new Vector3(0.02f, 0.4f, 0.02f), new Color(0.6f, 0.5f, 0.35f), false, "String");
                Prim.Make(PrimitiveType.Sphere, t, new Vector3(-1f + i * 0.65f, 1.8f, -0.4f), new Vector3(0.35f, 0.55f, 0.3f), new Color(0.55f, 0.25f, 0.18f), "Ham");
            }
            Prim.Make(PrimitiveType.Sphere, t, new Vector3(0f, 0.2f, -0.2f), new Vector3(0.7f, 0.25f, 0.7f), new Color(0.9f, 0.35f, 0.1f), "Embers", false, true);
            Prim.PointLight(t, new Vector3(0f, 0.6f, -0.4f), new Color(1f, 0.45f, 0.2f), 4f, 1f);
        }

        static void Bathhouse(Transform t)
        {
            VillageBuilder.Building(t, new Vector3(0f, 0f, 0.3f), new Vector3(5.2f, 3.2f, 4.6f), new Color(0.78f, 0.74f, 0.66f), false, "Bathhouse");
            Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0f, 0.4f, 0.8f), new Vector3(2.2f, 0.4f, 2.2f), DarkTimber, "Tub", true);
            Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0f, 0.81f, 0.8f), new Vector3(2f, 0.01f, 2f), new Color(0.35f, 0.6f, 0.8f), "Water", false, true);
            for (int i = 0; i < 5; i++)
                Prim.Box(t, new Vector3(-2.2f, 0.25f + i * 0.5f, 2.2f), new Vector3(0.5f, 0.45f, 0.5f), Prim.Vary(Stone, 0.1f), false, "StoveStone");
            Prim.Box(t, new Vector3(-2.2f, 3.6f, 2.2f), new Vector3(0.45f, 1.2f, 0.45f), Stone * 0.8f, false, "Chimney");
            Prim.PointLight(t, new Vector3(0f, 2f, 0.8f), new Color(0.75f, 0.85f, 1f), 6f, 1.1f);
        }

        static void ShedFrame(Transform t, Color roof)
        {
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    Prim.Box(t, new Vector3(i * 2f, 1.4f, j * 1.6f), new Vector3(0.18f, 2.8f, 0.18f), Timber, true, "Post");
            Prim.Box(t, new Vector3(0f, 1.4f, 1.7f), new Vector3(4f, 2.8f, 0.12f), Timber * 0.9f, true, "BackWall");
            var r = Prim.Box(t, new Vector3(0f, 3f, 0f), new Vector3(4.6f, 0.15f, 3.9f), roof, false, "Roof");
            r.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        }
    }

    // ---------- Small visual helpers ----------

    public class Flicker : MonoBehaviour
    {
        public Light Target;
        float baseIntensity = -1f;
        void Update()
        {
            if (Target == null) return;
            if (baseIntensity < 0f) baseIntensity = Target.intensity;
            Target.intensity = baseIntensity * (0.85f + Mathf.PerlinNoise(Time.time * 4f, transform.position.x) * 0.3f);
        }
    }

    public class Spin : MonoBehaviour
    {
        public Vector3 Axis = Vector3.up;
        public float Speed = 90f;
        void Update() { transform.Rotate(Axis, Speed * Time.deltaTime, Space.Self); }
    }

    // ---------- Interactables ----------

    public class CookingHearth : Interactable
    {
        public override string Prompt(Hero hero)
        {
            int ready = CraftingDatabase.Recipes.Count(r => Homestead.Instance.CookProblem(r) == null);
            return "Cook at the hearth" + (ready > 0 ? " (" + ready + " dish" + (ready > 1 ? "es" : "") + " ready to make)" : "");
        }
        public override void Interact(Hero hero) { CraftingUi.Instance.Open(CraftingUi.Panel.Cook, this); }
    }

    /// <summary>One plot per blueprint. Empty plots open the building plans; finished buildings are used directly.</summary>
    public class HomesteadPlot : Interactable
    {
        public BlueprintDef Blueprint;
        Transform visual;
        int drawnState = -1;

        void Update()
        {
            var home = Homestead.Instance;
            int state = home.Built.Contains(Blueprint.id) ? 2 : home.KnownBlueprints.Contains(Blueprint.id) ? 1 : 0;
            if (state == drawnState) return;
            drawnState = state;
            if (visual != null) Destroy(visual.gameObject);
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            HomesteadBuilder.DrawPlot(visual, Blueprint, state == 2, state >= 1);
        }

        bool IsBuilt { get { return Homestead.Instance.Built.Contains(Blueprint.id); } }

        public override string Prompt(Hero hero)
        {
            var home = Homestead.Instance;
            if (!IsBuilt)
                return home.KnownBlueprints.Contains(Blueprint.id) ? "Build " + Blueprint.name + (home.BuildProblem(Blueprint) == null ? " (you have everything)" : "")
                    : "Empty plot (see building plans)";
            var parts = new System.Collections.Generic.List<string>();
            if (home.CanHarvest(Blueprint)) parts.Add("collect today's goods");
            if (!string.IsNullOrEmpty(Blueprint.useBuff))
            {
                float wait = home.HoursUntilReady(Blueprint);
                parts.Add(wait > 0f ? "ready in " + Mathf.CeilToInt(wait) + "h" : "use it");
            }
            if (Blueprint.id == "stone_oven" || Blueprint.id == "apothecary_still") parts.Add("cook");
            return Blueprint.name + (parts.Count > 0 ? ": " + string.Join(", ", parts.ToArray()) : "");
        }

        public override void Interact(Hero hero)
        {
            if (!IsBuilt) { CraftingUi.Instance.Open(CraftingUi.Panel.Build, this); return; }
            bool station = CraftingDatabase.Recipes.Any(r => r.station == Blueprint.id);
            bool usable = Homestead.Instance.CanHarvest(Blueprint) || (!string.IsNullOrEmpty(Blueprint.useBuff) && Homestead.Instance.HoursUntilReady(Blueprint) <= 0f);
            if (station && !usable) { CraftingUi.Instance.Open(CraftingUi.Panel.Cook, this); return; }
            Homestead.Instance.Use(Blueprint);
        }
    }

    /// <summary>A villager who stands in one place and hands out mini quests.</summary>
    public class QuestGiver : Interactable
    {
        public string GiverName;
        TextMesh marker;
        float nextRefresh;

        public static QuestGiver Spawn(Transform root, Vector3 pos, string name, int seed, Color clothes, bool feminine, float height)
        {
            var go = new GameObject("QuestGiver_" + name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Color skin;
            var rig = CharacterBuilder.BuildHuman(go.transform, height, seed, out skin, feminine);
            CharacterBuilder.DressVillager(rig, clothes, feminine);
            var col = go.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 1f, 0f);
            col.height = 2f;
            col.radius = 1.1f;
            var q = go.AddComponent<QuestGiver>();
            q.GiverName = name;
            Prim.Label(go.transform, new Vector3(0f, height + 0.35f, 0f), name, Color.white, 0.045f);
            q.marker = Prim.Label(go.transform, new Vector3(0f, height + 0.85f, 0f), "", Color.yellow, 0.1f);
            return q;
        }

        void Update()
        {
            if (marker == null || Time.time < nextRefresh) return;
            nextRefresh = Time.time + 0.5f;
            Homestead.QuestState state;
            Homestead.Instance.CurrentQuest(GiverName, out state);
            switch (state)
            {
                case Homestead.QuestState.Available: marker.text = "!"; marker.color = new Color(1f, 0.85f, 0.2f); break;
                case Homestead.QuestState.Ready: marker.text = "?"; marker.color = new Color(0.4f, 1f, 0.4f); break;
                case Homestead.QuestState.Active: marker.text = "?"; marker.color = new Color(0.6f, 0.6f, 0.6f); break;
                default: marker.text = ""; break;
            }
            // Turn to face the hero when they come close.
            var hero = Hero.Instance;
            if (hero != null)
            {
                var to = hero.transform.position - transform.position; to.y = 0f;
                if (to.magnitude < 6f && to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to);
            }
        }

        public override string Prompt(Hero hero)
        {
            Homestead.QuestState state;
            var q = Homestead.Instance.CurrentQuest(GiverName, out state);
            switch (state)
            {
                case Homestead.QuestState.Available: return "Talk to " + GiverName + " (new task: " + q.title + ")";
                case Homestead.QuestState.Ready: return "Talk to " + GiverName + " (hand in: " + q.title + ")";
                case Homestead.QuestState.Active: return "Talk to " + GiverName + " (" + q.title + ")";
                default: return "Talk to " + GiverName;
            }
        }

        public override void Interact(Hero hero) { CraftingUi.Instance.OpenQuest(this); }
    }

    public class CraftMerchant : Interactable
    {
        public enum Kind { Larder, Peddler }
        public Kind Type;
        public string Title;
        public override string Prompt(Hero hero) { return "Trade at " + Title; }
        public override void Interact(Hero hero) { CraftingUi.Instance.Open(Type == Kind.Larder ? CraftingUi.Panel.Larder : CraftingUi.Panel.Peddler, this); }
    }
}
