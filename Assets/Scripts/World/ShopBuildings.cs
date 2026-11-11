using System.Collections.Generic;
using System.Linq;
using Hollowmark.Data;
using Hollowmark.Visuals;
using UnityEngine;

namespace Hollowmark.World
{
    /// <summary>
    /// One building per market shop, each with its own look:
    /// Hale's Forge (soot-dark stone smithy with a glowing hearth, anvil, chimney smoke and a rack of real weapons),
    /// Briggs' Armoury (two-storey timber-framed house with blue shutters and armor stands),
    /// and Widow Marsh's Provisions (thatched cottage with a striped awning, barrels, crates and sacks).
    /// Every building faces the main road, has a counter in a wide shop window with the keeper behind it,
    /// and a hanging sign painted with one of the shop's own item pictures.
    /// Local space: the shop front faces -Z; the caller turns the building to face the road.
    /// </summary>
    public static class ShopBuildings
    {
        static readonly Color Timber = new Color(0.36f, 0.24f, 0.14f);
        static readonly Color DarkTimber = new Color(0.22f, 0.15f, 0.1f);
        static readonly Color Iron = new Color(0.2f, 0.2f, 0.22f);
        static readonly Color Brass = new Color(0.8f, 0.62f, 0.26f);
        static readonly Color WindowGlass = new Color(0.95f, 0.78f, 0.45f);
        const float Face = 0.17f; // walls are 0.3 thick: trim sits just proud of the outer face

        // =====================================================================
        // The three shops
        // =====================================================================

        public static GameObject Weaponsmith(Transform parent, Vector3 pos, float yaw, string keeper, int seed)
        {
            var stone = new Color(0.4f, 0.39f, 0.38f);
            var slate = new Color(0.24f, 0.26f, 0.3f);
            var accent = new Color(0.6f, 0.2f, 0.18f);
            const float W = 7.5f, D = 6f, H = 3.4f;
            var t = Root(parent, "Shop_Weaponsmith", pos, yaw);

            Plinth(t, W, D, stone * 0.8f);
            Shell(t, W, D, H, stone, 4.2f, 1.1f, p => 0.9f + 0.2f * Mathf.PerlinNoise(p.x * 1.7f, p.y * 1.3f));
            // Light quoin stones on the front corners.
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 6; i++)
                    Prim.Box(t, new Vector3(side * (W / 2f - 0.05f), 0.3f + i * 0.55f, -D / 2f - 0.02f), new Vector3(i % 2 == 0 ? 0.6f : 0.4f, 0.42f, 0.36f), stone * 1.25f, false, "Quoin");
            GableRoof(t, W, D, H, 2.2f, slate, slate * 0.8f, stone);
            Chimney(t, new Vector3(-W / 2f + 0.9f, 0f, D / 2f - 0.8f), H + 3.6f, stone * 0.9f, true);

            // The forge hearth beside the shop front, glowing.
            var forge = Group(t, "Forge", new Vector3(-W / 2f - 1.3f, 0f, -0.6f));
            Prim.Box(forge, new Vector3(0f, 0.5f, 0f), new Vector3(1.8f, 1f, 1.8f), stone * 0.85f, true, "Hearth");
            Prim.Make(PrimitiveType.Cube, forge, new Vector3(0f, 1.02f, 0f), new Vector3(1.3f, 0.06f, 1.3f), new Color(1f, 0.42f, 0.08f), "Coals", false, true);
            for (int i = 0; i < 7; i++)
                Prim.Make(PrimitiveType.Sphere, forge, new Vector3(Random.Range(-0.5f, 0.5f), 1.08f, Random.Range(-0.5f, 0.5f)), Vector3.one * Random.Range(0.12f, 0.22f), new Color(1f, Random.Range(0.55f, 0.8f), 0.2f), "Ember", false, true);
            Prim.Box(forge, new Vector3(0f, 2.3f, 0.3f), new Vector3(1.6f, 0.5f, 1.2f), stone * 0.75f, false, "Hood");
            Prim.Box(forge, new Vector3(0f, 3.4f, 0.5f), new Vector3(0.7f, 1.8f, 0.7f), stone * 0.75f, false, "Flue");
            Prim.Box(forge, new Vector3(-0.75f, 1.6f, 0.75f), new Vector3(0.15f, 1.3f, 0.15f), DarkTimber, false);
            Prim.Box(forge, new Vector3(0.75f, 1.6f, 0.75f), new Vector3(0.15f, 1.3f, 0.15f), DarkTimber, false);
            var fire = Prim.PointLight(forge, new Vector3(0f, 1.6f, -0.6f), new Color(1f, 0.5f, 0.2f), 7f, 2.2f);
            fire.gameObject.AddComponent<Flicker>();
            // Bellows.
            var bellows = Prim.Box(forge, new Vector3(0.7f, 0.95f, 1.1f), new Vector3(0.6f, 0.15f, 0.9f), new Color(0.35f, 0.22f, 0.12f), false, "Bellows");
            bellows.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);

            // Anvil on a stump, and a quench barrel.
            var anvil = Group(t, "Anvil", new Vector3(-W / 2f - 1.2f, 0f, -2.6f));
            Prim.Make(PrimitiveType.Cylinder, anvil, new Vector3(0f, 0.3f, 0f), new Vector3(0.7f, 0.3f, 0.7f), Timber, "Stump", true);
            Prim.Box(anvil, new Vector3(0f, 0.68f, 0f), new Vector3(0.32f, 0.16f, 0.22f), Iron, false, "Waist");
            Prim.Box(anvil, new Vector3(0f, 0.84f, 0f), new Vector3(0.7f, 0.18f, 0.28f), Iron, false, "Face");
            var horn = Prim.Make(PrimitiveType.Capsule, anvil, new Vector3(0.45f, 0.86f, 0f), new Vector3(0.14f, 0.2f, 0.14f), Iron, "Horn");
            horn.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Prim.Box(anvil, new Vector3(-0.1f, 0.96f, 0f), new Vector3(0.28f, 0.05f, 0.05f), new Color(0.55f, 0.55f, 0.55f), false, "Hammer");
            Barrel(t, new Vector3(-W / 2f - 0.3f, 0f, -3.6f), new Color(0.3f, 0.45f, 0.55f));

            // Grindstone on the other side.
            var grind = Group(t, "Grindstone", new Vector3(W / 2f + 1f, 0f, -2.2f));
            Prim.Box(grind, new Vector3(0f, 0.4f, 0f), new Vector3(0.9f, 0.8f, 0.3f), Timber, true, "Frame");
            var wheel = Prim.Make(PrimitiveType.Cylinder, grind, new Vector3(0f, 0.95f, 0f), new Vector3(0.9f, 0.08f, 0.9f), new Color(0.6f, 0.56f, 0.5f), "Wheel");
            wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Weapon rack on the front wall showing real weapons the smith sells.
            var rack = Group(t, "WeaponRack", new Vector3(W / 2f - 1.3f, 0f, -D / 2f - 0.35f));
            Prim.Box(rack, new Vector3(0f, 0.25f, 0f), new Vector3(1.8f, 0.12f, 0.35f), DarkTimber, true);
            Prim.Box(rack, new Vector3(0f, 1.6f, 0.12f), new Vector3(1.8f, 0.1f, 0.1f), DarkTimber, false);
            var shown = GameDatabase.Weapons.Values.Where(w => w.sold && w.Kind == WeaponKind.Melee && w.length < 1.6f).OrderBy(w => w.price).Take(4).ToList();
            for (int i = 0; i < shown.Count; i++)
            {
                var mount = new GameObject("Display_" + shown[i].id).transform;
                mount.SetParent(rack, false);
                mount.localPosition = new Vector3(-0.66f + i * 0.44f, 0.33f, 0f);
                mount.localRotation = Quaternion.Euler(-90f, 0f, 0f); // weapon length runs straight up
                CharacterBuilder.BuildWeapon(shown[i], mount);
            }
            // Crossed long guns on the wall left of the counter.
            var guns = GameDatabase.Weapons.Values.Where(w => w.sold && w.Kind == WeaponKind.Firearm).OrderBy(w => w.price).Take(2).ToList();
            for (int i = 0; i < guns.Count; i++)
            {
                var mount = new GameObject("WallGun_" + guns[i].id).transform;
                mount.SetParent(t, false);
                mount.localPosition = new Vector3(-W / 2f + 1.6f, 1.2f, -D / 2f - 0.25f);
                mount.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 35f : -35f) * Quaternion.Euler(-90f, 0f, 0f);
                CharacterBuilder.BuildWeapon(guns[i], mount);
            }

            var signWeapon = GameDatabase.Weapon("broadsword") ?? GameDatabase.Weapons.Values.FirstOrDefault(w => w.sold);
            HangingSign(t, new Vector3(W / 2f + 0.1f, H - 0.4f, -D / 2f - 0.1f), signWeapon != null ? (System.Action<IconCanvas>)(c => ItemIcons.PaintWeapon(c, signWeapon)) : null, accent);
            Lantern(t, new Vector3(-W / 2f + 0.5f, 2.6f, -D / 2f - 0.3f));
            Counter(t, ShopKind.Weapons, keeper, accent, seed, D, DarkTimber);
            Nameplate(t, "Hale's Forge", D, H);
            return t.gameObject;
        }

        public static GameObject Armorer(Transform parent, Vector3 pos, float yaw, string keeper, int seed)
        {
            var plaster = new Color(0.88f, 0.86f, 0.8f);
            var tile = new Color(0.62f, 0.3f, 0.2f);
            var blue = new Color(0.2f, 0.32f, 0.6f);
            const float W = 7f, D = 6f, H = 3f, H2 = 2.6f, Jetty = 0.6f;
            var t = Root(parent, "Shop_Armorer", pos, yaw);

            Plinth(t, W, D, new Color(0.5f, 0.48f, 0.45f));
            Shell(t, W, D, H, plaster, 4f, 1.1f, null);
            HalfTimber(t, W, D, 0f, H, -D / 2f - Face, 4f);
            // A jettied upper storey that leans out over the street.
            var upper = Group(t, "UpperStorey", new Vector3(0f, H, -Jetty / 2f));
            Prim.Box(upper, new Vector3(0f, 0.12f, 0f), new Vector3(W + 0.3f, 0.24f, D + Jetty + 0.3f), DarkTimber, false, "Floor");
            Shell(upper, W, D + Jetty, H2, plaster, 0f, 0f, null);
            HalfTimber(upper, W, D + Jetty, 0f, H2, -(D + Jetty) / 2f - Face);
            for (int i = -1; i <= 1; i += 2) Window(upper, new Vector3(i * 2.1f, 1.3f, -(D + Jetty) / 2f - Face), blue, true);
            Window(upper, new Vector3(0f, 1.3f, -(D + Jetty) / 2f - Face), blue, false);
            var roofBase = Group(t, "Roof", new Vector3(0f, 0f, -Jetty / 2f));
            GableRoof(roofBase, W, D + Jetty, H + H2, 2.4f, tile, tile * 0.8f, plaster);
            Chimney(t, new Vector3(W / 2f - 1f, 0f, D / 2f - 0.6f), H + H2 + 3f, new Color(0.55f, 0.32f, 0.25f), false);

            // Armor stands either side of the shop window, dressed in pieces the armorer sells.
            var chest = GameDatabase.ArmorPiece("cuirass") ?? GameDatabase.Armor.Values.FirstOrDefault(a => a.sold && a.Slot == ArmorSlot.Chest);
            var head = GameDatabase.ArmorPiece("morion") ?? GameDatabase.Armor.Values.FirstOrDefault(a => a.sold && a.Slot == ArmorSlot.Head);
            var coat = GameDatabase.ArmorPiece("buff_coat") ?? chest;
            var hat = GameDatabase.ArmorPiece("tricorne") ?? head;
            ArmorStand(t, new Vector3(-W / 2f - 0.9f, 0f, -D / 2f - 0.6f), chest, head);
            ArmorStand(t, new Vector3(W / 2f + 0.9f, 0f, -D / 2f - 0.6f), coat, hat);

            // A shield plaque over the window.
            var plaque = Group(t, "Shield", new Vector3(0f, H - 0.35f, -D / 2f - 0.18f));
            var shield = Prim.Make(PrimitiveType.Cylinder, plaque, Vector3.zero, new Vector3(0.75f, 0.05f, 0.9f), blue, "Shield");
            shield.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim.Make(PrimitiveType.Sphere, plaque, new Vector3(0f, 0f, -0.06f), new Vector3(0.22f, 0.22f, 0.12f), Brass, "ShieldBoss");
            Prim.Box(plaque, new Vector3(0f, 0f, -0.04f), new Vector3(0.08f, 0.75f, 0.04f), Brass, false);
            Prim.Box(plaque, new Vector3(0f, 0.1f, -0.04f), new Vector3(0.6f, 0.08f, 0.04f), Brass, false);

            var signPiece = chest;
            HangingSign(t, new Vector3(W / 2f + 0.1f, H - 0.3f, -D / 2f - 0.1f), signPiece != null ? (System.Action<IconCanvas>)(c => ItemIcons.PaintArmor(c, signPiece)) : null, blue);
            Lantern(t, new Vector3(-W / 2f + 0.4f, 2.4f, -D / 2f - 0.3f));
            Counter(t, ShopKind.Armor, keeper, blue, seed, D, Timber);
            Nameplate(t, "Briggs' Armoury", D + Jetty, H + H2);
            return t.gameObject;
        }

        public static GameObject Provisioner(Transform parent, Vector3 pos, float yaw, string keeper, int seed)
        {
            var plaster = new Color(0.93f, 0.85f, 0.66f);
            var thatch = new Color(0.66f, 0.54f, 0.3f);
            var green = new Color(0.25f, 0.5f, 0.28f);
            const float W = 7f, D = 5.5f, H = 3f;
            var t = Root(parent, "Shop_Provisioner", pos, yaw);

            Plinth(t, W, D, new Color(0.52f, 0.5f, 0.46f));
            Shell(t, W, D, H, plaster, 4f, 1.1f, null);
            // Exposed corner posts and a low sill beam.
            for (int side = -1; side <= 1; side += 2) Prim.Box(t, new Vector3(side * W / 2f, H / 2f, -D / 2f - Face), new Vector3(0.28f, H, 0.16f), Timber, false);
            Prim.Box(t, new Vector3(0f, 0.25f, -D / 2f - Face), new Vector3(W, 0.2f, 0.14f), Timber, false);
            // A thick, steep thatch roof that hangs low over the walls, with a dormer window.
            GableRoof(t, W, D, H, 2.6f, thatch, thatch * 0.85f, plaster, 0.4f, 0.5f);
            var dormer = Group(t, "Dormer", new Vector3(-1.6f, H + 0.55f, -D / 2f + 0.75f));
            Prim.Box(dormer, new Vector3(0f, 0.55f, 0f), new Vector3(1.3f, 1.1f, 1.1f), plaster, false);
            GableRoof(dormer, 1.3f, 1.1f, 1.1f, 0.65f, thatch, thatch * 0.85f, plaster, 0.3f, 0.3f);
            Window(dormer, new Vector3(0f, 0.55f, -0.58f), green, false, 0.6f);
            Chimney(t, new Vector3(W / 2f - 1.2f, 0f, 0.6f), H + 4f, new Color(0.5f, 0.46f, 0.42f), true);
            Window(t, new Vector3(-2.75f, 1.7f, -D / 2f - Face), green, false, 0.7f);
            FlowerBox(t, new Vector3(-2.75f, 1.12f, -D / 2f - 0.35f));

            // Green-and-cream striped awning over the counter.
            var awning = Group(t, "Awning", new Vector3(0f, H - 0.55f, -D / 2f - 0.85f));
            awning.localRotation = Quaternion.Euler(22f, 0f, 0f);
            for (int i = 0; i < 9; i++)
                Prim.Box(awning, new Vector3(-2f + i * 0.5f, 0f, 0f), new Vector3(0.5f, 0.05f, 1.6f), i % 2 == 0 ? green : new Color(0.95f, 0.93f, 0.85f), false, "Stripe");
            for (int i = 0; i < 9; i++)
            {
                var flap = Prim.Box(awning, new Vector3(-2f + i * 0.5f, -0.14f, -0.8f), new Vector3(0.5f, 0.3f, 0.04f), i % 2 == 0 ? green : new Color(0.95f, 0.93f, 0.85f), false, "Valance");
                flap.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            }
            for (int side = -1; side <= 1; side += 2)
                Prim.Box(t, new Vector3(side * 2.2f, (H - 0.85f) / 2f, -D / 2f - 1.55f), new Vector3(0.1f, H - 0.85f, 0.1f), Timber, false, "AwningPost");
            // Herbs drying under the awning.
            for (int i = 0; i < 5; i++)
            {
                var herb = Prim.Make(PrimitiveType.Cylinder, t, new Vector3(-1.2f + i * 0.6f, H - 0.95f, -D / 2f - 0.45f), new Vector3(0.14f, 0.18f, 0.14f), Prim.Vary(new Color(0.35f, 0.5f, 0.22f), 0.2f), "Herbs");
                herb.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-8f, 8f));
            }

            // Goods out front: barrels, crates of apples, sacks of flour.
            Barrel(t, new Vector3(-W / 2f - 0.6f, 0f, -D / 2f - 0.6f), new Color(0.5f, 0.32f, 0.18f));
            Barrel(t, new Vector3(-W / 2f - 0.6f, 0f, -D / 2f + 0.5f), new Color(0.46f, 0.3f, 0.17f));
            Barrel(t, new Vector3(-W / 2f - 0.6f, 1.05f, -D / 2f - 0.05f), new Color(0.5f, 0.32f, 0.18f), true);
            Crate(t, new Vector3(W / 2f + 0.7f, 0f, -D / 2f - 0.8f), new Color(0.8f, 0.12f, 0.1f));
            Crate(t, new Vector3(W / 2f + 0.7f, 0f, -D / 2f + 0.3f), new Color(0.95f, 0.75f, 0.2f));
            for (int i = 0; i < 3; i++) Sack(t, new Vector3(W / 2f + 0.5f + (i % 2) * 0.5f, 0f, D / 2f - 1.6f + i * 0.6f));
            Chalkboard(t, new Vector3(-0.9f - 1.6f, 0f, -D / 2f - 1.4f));

            var bread = GameDatabase.Item("bread") ?? GameDatabase.Items.Values.FirstOrDefault(i => i.sold);
            HangingSign(t, new Vector3(W / 2f + 0.1f, H - 0.3f, -D / 2f - 0.1f), bread != null ? (System.Action<IconCanvas>)(c => ItemIcons.PaintItem(c, bread)) : null, green);
            Lantern(t, new Vector3(-W / 2f + 0.4f, 2.3f, -D / 2f - 0.3f));
            Counter(t, ShopKind.Provisions, keeper, green, seed, D, Timber);
            Nameplate(t, "Widow Marsh's Provisions", D, H + 1.5f);
            return t.gameObject;
        }

        // =====================================================================
        // Shared parts
        // =====================================================================

        static Transform Root(Transform parent, string name, Vector3 pos, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        static Transform Group(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        static void Plinth(Transform t, float w, float d, Color c)
        {
            Prim.Box(t, new Vector3(0f, 0.1f, 0f), new Vector3(w + 0.4f, 0.2f, d + 0.4f), c, false, "Plinth");
        }

        /// <summary>
        /// Four walls with colliders. The front wall has a wide shop window (openW x openH, its sill at sillH)
        /// where the counter goes; openW 0 makes a solid front.
        /// </summary>
        static void Shell(Transform t, float w, float d, float h, Color wall, float openW, float sillH, System.Func<Vector2, float> shade)
        {
            const float th = 0.3f;
            // Optional per-wall shade variation (e.g. soot on stone), keyed by a fixed spot on each wall.
            System.Func<Vector3, Color> col = p => shade == null ? wall : wall * shade(new Vector2(p.x, p.y + p.z));
            Prim.Box(t, new Vector3(0f, h / 2f, d / 2f), new Vector3(w, h, th), col(new Vector3(0f, 1f, 1f)), true, "BackWall");
            Prim.Box(t, new Vector3(-w / 2f, h / 2f, 0f), new Vector3(th, h, d), col(new Vector3(-1f, 2f, 0f)), true, "SideWall");
            Prim.Box(t, new Vector3(w / 2f, h / 2f, 0f), new Vector3(th, h, d), col(new Vector3(1f, 3f, 0f)), true, "SideWall");
            if (openW <= 0f)
            {
                Prim.Box(t, new Vector3(0f, h / 2f, -d / 2f), new Vector3(w, h, th), wall, true, "FrontWall");
                return;
            }
            float side = (w - openW) / 2f, openTop = Mathf.Min(h - 0.5f, sillH + 1.7f);
            Prim.Box(t, new Vector3(-(openW + side) / 2f, h / 2f, -d / 2f), new Vector3(side, h, th), col(new Vector3(-2f, 0f, -1f)), true, "FrontWall");
            Prim.Box(t, new Vector3((openW + side) / 2f, h / 2f, -d / 2f), new Vector3(side, h, th), col(new Vector3(2f, 0f, -1f)), true, "FrontWall");
            Prim.Box(t, new Vector3(0f, (h + openTop) / 2f, -d / 2f), new Vector3(openW, h - openTop, th), wall, true, "Lintel");
            Prim.Box(t, new Vector3(0f, openTop + 0.06f, -d / 2f - Face), new Vector3(openW + 0.3f, 0.14f, 0.14f), DarkTimber, false, "LintelBeam");
            // Dim interior so the window doesn't show the sky through the back.
            Prim.Box(t, new Vector3(0f, 0.02f, 0f), new Vector3(w - 0.4f, 0.04f, d - 0.4f), new Color(0.3f, 0.22f, 0.15f), false, "Floor");
            Prim.Box(t, new Vector3(0f, h - 0.05f, 0f), new Vector3(w - 0.4f, 0.1f, d - 0.4f), DarkTimber, false, "Ceiling");
            Prim.PointLight(t, new Vector3(0f, h - 0.6f, 0.5f), new Color(1f, 0.78f, 0.5f), 6f, 1f);
        }

        /// <summary>Dark timber posts, a mid rail and diagonal braces on a plaster wall face.</summary>
        static void HalfTimber(Transform t, float w, float d, float y0, float h, float z, float openW = 0f)
        {
            int posts = 5;
            for (int i = 0; i < posts; i++)
            {
                float x = -w / 2f + i * w / (posts - 1);
                if (Mathf.Abs(x) < openW / 2f + 0.1f) continue; // keep the shop window clear
                Prim.Box(t, new Vector3(x, y0 + h / 2f, z), new Vector3(0.2f, h, 0.1f), DarkTimber, false, "Post");
            }
            if (openW > 0f)
                for (int s = -1; s <= 1; s += 2)
                    Prim.Box(t, new Vector3(s * (openW / 2f + 0.1f), y0 + h / 2f, z), new Vector3(0.2f, h, 0.1f), DarkTimber, false, "Post");
            Prim.Box(t, new Vector3(0f, y0 + h - 0.1f, z), new Vector3(w, 0.18f, 0.1f), DarkTimber, false, "Rail");
            Prim.Box(t, new Vector3(0f, y0 + 0.1f, z), new Vector3(w, 0.18f, 0.1f), DarkTimber, false, "Sill");
            float bay = w / (posts - 1);
            for (int s = -1; s <= 1; s += 2)
            {
                // One brace in each outer bay, between the corner and the window post.
                float inner = openW > 0f ? openW / 2f + 0.1f : w / 2f - bay;
                float span = w / 2f - inner, x = s * (inner + span / 2f);
                var brace = Prim.Box(t, new Vector3(x, y0 + h / 2f, z), new Vector3(0.14f, Mathf.Sqrt(span * span + h * h) * 0.95f, 0.08f), DarkTimber, false, "Brace");
                brace.transform.localRotation = Quaternion.Euler(0f, 0f, s * Mathf.Atan2(span, h) * Mathf.Rad2Deg);
            }
            // Side walls get corner braces too.
            for (int s = -1; s <= 1; s += 2)
            {
                var b = Prim.Box(t, new Vector3(s * (w / 2f + 0.03f), y0 + h / 2f, 0f), new Vector3(0.08f, h * 1.1f, 0.14f), DarkTimber, false, "Brace");
                b.transform.localRotation = Quaternion.Euler(s * 35f, 0f, 0f);
                Prim.Box(t, new Vector3(s * (w / 2f + 0.03f), y0 + h - 0.1f, 0f), new Vector3(0.08f, 0.18f, d), DarkTimber, false, "Rail");
            }
        }

        /// <summary>
        /// A gable roof whose ridge runs left-right: filled triangular gable ends and two overhanging roof slopes.
        /// </summary>
        static void GableRoof(Transform t, float w, float d, float y, float rise, Color roof, Color roofShade, Color gable, float thickness = 0.22f, float overhang = 0.6f)
        {
            Prim.Shape(GableMesh(), t, new Vector3(0f, y, 0f), new Vector3(w, rise, d), gable, "Gable");
            float half = d / 2f + overhang;
            float slope = Mathf.Atan2(rise, d / 2f) * Mathf.Rad2Deg;
            float len = Mathf.Sqrt(half * half + rise * rise * (half / (d / 2f)) * (half / (d / 2f)));
            for (int s = -1; s <= 1; s += 2)
            {
                var slab = Prim.Box(t, Vector3.zero, new Vector3(w + overhang * 0.8f, thickness, len), s < 0 ? roof : roofShade, false, s < 0 ? "RoofFront" : "RoofBack");
                slab.transform.localRotation = Quaternion.Euler(s < 0 ? -slope : slope, 0f, 0f);
                // Centre each slab on its slope, lifted by half its thickness so it sits on the gable.
                var dir = slab.transform.localRotation * Vector3.forward;
                var up = slab.transform.localRotation * Vector3.up;
                var ridge = new Vector3(0f, y + rise, 0f);
                slab.transform.localPosition = ridge + dir * (s * len / 2f) + up * (thickness / 2f);
            }
            Prim.Box(t, new Vector3(0f, y + rise + thickness * 0.6f, 0f), new Vector3(w + overhang * 0.8f, thickness * 1.2f, thickness * 1.6f), roofShade * 0.85f, false, "Ridge");
        }

        static Mesh gable;

        /// <summary>A unit prism: 1 wide (x), 1 deep (z), 1 tall at the ridge, base at y 0. Flat shaded.</summary>
        static Mesh GableMesh()
        {
            if (gable != null) return gable;
            var a = new Vector3(-0.5f, 0f, -0.5f); var b = new Vector3(-0.5f, 0f, 0.5f); var c = new Vector3(-0.5f, 1f, 0f);
            var a2 = new Vector3(0.5f, 0f, -0.5f); var b2 = new Vector3(0.5f, 0f, 0.5f); var c2 = new Vector3(0.5f, 1f, 0f);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            System.Action<Vector3, Vector3, Vector3, Vector3> tri = (p, q, r, outward) =>
            {
                if (Vector3.Dot(Vector3.Cross(q - p, r - p), outward) < 0f) { var tmp = q; q = r; r = tmp; }
                int i = verts.Count;
                verts.Add(p); verts.Add(q); verts.Add(r);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            };
            tri(a, b, c, Vector3.left);
            tri(a2, b2, c2, Vector3.right);
            var front = new Vector3(0f, 0.5f, -1f); var back = new Vector3(0f, 0.5f, 1f);
            tri(a, c, a2, front); tri(c, c2, a2, front);
            tri(b, b2, c, back); tri(c, b2, c2, back);
            gable = new Mesh { name = "Gable" };
            gable.SetVertices(verts);
            gable.SetTriangles(tris, 0);
            gable.RecalculateNormals();
            gable.RecalculateBounds();
            return gable;
        }

        static void Chimney(Transform t, Vector3 basePos, float height, Color c, bool smoke)
        {
            var ch = Group(t, "Chimney", basePos);
            Prim.Box(ch, new Vector3(0f, height / 2f, 0f), new Vector3(0.8f, height, 0.8f), c, false, "Stack");
            Prim.Box(ch, new Vector3(0f, height + 0.05f, 0f), new Vector3(1f, 0.15f, 1f), c * 0.8f, false, "Cap");
            if (smoke)
            {
                var s = new GameObject("Smoke").AddComponent<ChimneySmoke>();
                s.transform.SetParent(ch, false);
                s.transform.localPosition = new Vector3(0f, height + 0.3f, 0f);
            }
        }

        static void Window(Transform t, Vector3 pos, Color shutters, bool withShutters, float size = 0.9f)
        {
            var w = Group(t, "Window", pos);
            Prim.Make(PrimitiveType.Cube, w, Vector3.zero, new Vector3(size, size * 1.15f, 0.04f), WindowGlass * 0.85f, "Glass", false, true);
            Prim.Box(w, new Vector3(0f, 0f, -0.02f), new Vector3(size + 0.14f, size * 1.15f + 0.14f, 0.03f), DarkTimber, false, "Frame");
            Prim.Make(PrimitiveType.Cube, w, new Vector3(0f, 0f, -0.035f), new Vector3(size, size * 1.15f, 0.01f), WindowGlass * 0.85f, "Pane", false, true);
            Prim.Box(w, new Vector3(0f, 0f, -0.05f), new Vector3(0.06f, size * 1.15f, 0.03f), DarkTimber, false, "Mullion");
            Prim.Box(w, new Vector3(0f, 0f, -0.05f), new Vector3(size, 0.06f, 0.03f), DarkTimber, false, "Transom");
            Prim.Box(w, new Vector3(0f, -size * 0.62f, -0.06f), new Vector3(size + 0.2f, 0.1f, 0.16f), DarkTimber, false, "Sill");
            if (!withShutters) return;
            for (int s = -1; s <= 1; s += 2)
            {
                Prim.Box(w, new Vector3(s * (size * 0.5f + size * 0.32f), 0f, -0.03f), new Vector3(size * 0.5f, size * 1.15f, 0.05f), shutters, false, "Shutter");
                for (int k = -1; k <= 1; k++)
                    Prim.Box(w, new Vector3(s * (size * 0.5f + size * 0.32f), k * size * 0.35f, -0.06f), new Vector3(size * 0.44f, 0.04f, 0.02f), shutters * 0.7f, false, "Slat");
            }
        }

        static void FlowerBox(Transform t, Vector3 pos)
        {
            var f = Group(t, "FlowerBox", pos);
            Prim.Box(f, Vector3.zero, new Vector3(1.1f, 0.22f, 0.3f), Timber, false);
            var colors = new[] { new Color(0.9f, 0.3f, 0.35f), new Color(1f, 0.85f, 0.3f), new Color(0.75f, 0.5f, 0.9f) };
            for (int i = 0; i < 6; i++)
            {
                Prim.Make(PrimitiveType.Sphere, f, new Vector3(-0.45f + i * 0.18f, 0.2f, 0f), Vector3.one * 0.16f, new Color(0.3f, 0.5f, 0.2f), "Leaves");
                Prim.Make(PrimitiveType.Sphere, f, new Vector3(-0.45f + i * 0.18f, 0.28f, -0.04f), Vector3.one * 0.09f, colors[i % 3], "Flower");
            }
        }

        static void Lantern(Transform t, Vector3 pos)
        {
            var l = Group(t, "Lantern", pos);
            Prim.Box(l, new Vector3(0f, 0.25f, 0.2f), new Vector3(0.05f, 0.05f, 0.4f), Iron, false, "Bracket");
            Prim.Box(l, new Vector3(0f, 0f, 0f), new Vector3(0.22f, 0.3f, 0.22f), Iron, false, "Cage");
            Prim.Make(PrimitiveType.Cube, l, new Vector3(0f, 0f, 0f), new Vector3(0.17f, 0.24f, 0.24f), new Color(1f, 0.82f, 0.5f), "Flame", false, true);
            Prim.Make(PrimitiveType.Cube, l, new Vector3(0f, 0f, 0f), new Vector3(0.24f, 0.24f, 0.17f), new Color(1f, 0.82f, 0.5f), "Flame", false, true);
            Prim.Box(l, new Vector3(0f, 0.18f, 0f), new Vector3(0.28f, 0.06f, 0.28f), Iron, false, "Top");
            Prim.PointLight(l, new Vector3(0f, 0f, -0.3f), new Color(1f, 0.75f, 0.45f), 6f, 0.9f).gameObject.AddComponent<Flicker>().Amount = 0.08f;
        }

        /// <summary>A sign board on an iron bracket, sticking out from the wall so it can be read from up and down the road.</summary>
        static void HangingSign(Transform t, Vector3 wallPos, System.Action<IconCanvas> paint, Color trim)
        {
            var s = Group(t, "Sign", wallPos);
            Prim.Box(s, new Vector3(0f, 0f, -0.55f), new Vector3(0.06f, 0.06f, 1.3f), Iron, false, "Arm");
            var brace = Prim.Box(s, new Vector3(0f, -0.3f, -0.3f), new Vector3(0.04f, 0.04f, 0.8f), Iron, false, "Brace");
            brace.transform.localRotation = Quaternion.Euler(-45f, 0f, 0f);
            var board = Group(s, "Board", new Vector3(0f, -0.62f, -0.75f));
            for (int i = -1; i <= 1; i += 2) Prim.Box(s, new Vector3(0f, -0.15f, -0.75f + i * 0.38f), new Vector3(0.02f, 0.3f, 0.02f), Iron, false, "Chain");
            Prim.Box(board, Vector3.zero, new Vector3(0.08f, 0.9f, 0.9f), trim, false, "Frame");
            if (paint == null) return;
            var tex = ItemIcons.SignTexture(paint, new Color(0.42f, 0.28f, 0.16f));
            var mat = new Material(Prim.Lit);
            Prim.SetColor(mat, Color.white);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.1f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            for (int side = -1; side <= 1; side += 2)
            {
                var q = Prim.Make(PrimitiveType.Quad, board, new Vector3(side * 0.045f, 0f, 0f), new Vector3(0.78f, 0.78f, 1f), Color.white, "Picture");
                q.transform.localRotation = Quaternion.Euler(0f, side * -90f, 0f);
                q.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        /// <summary>The counter in the shop window, the keeper behind it, and the trigger the hero uses to trade.</summary>
        static void Counter(Transform t, ShopKind kind, string keeper, Color accent, int seed, float d, Color wood)
        {
            var c = Group(t, "Counter", new Vector3(0f, 0f, -d / 2f + 0.2f));
            Prim.Box(c, new Vector3(0f, 0.55f, 0f), new Vector3(3.8f, 1.1f, 0.7f), wood, true, "Counter");
            Prim.Box(c, new Vector3(0f, 1.13f, -0.05f), new Vector3(4f, 0.08f, 0.85f), wood * 1.25f, false, "CounterTop");
            Prim.Box(c, new Vector3(0f, 0.55f, -0.36f), new Vector3(3.6f, 0.15f, 0.02f), accent, false, "Trim");
            Goods(c, kind);

            Color skin;
            var rig = CharacterBuilder.BuildHuman(c, 1.75f, seed, out skin, kind == ShopKind.Provisions);
            rig.transform.localPosition = new Vector3(0.6f, 0f, 0.9f);
            rig.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            CharacterBuilder.DressVillager(rig, Prim.Vary(accent, 0.15f), kind == ShopKind.Provisions);
            if (kind == ShopKind.Weapons)
            {
                rig.Cover(new Color(0.3f, 0.2f, 0.12f), 0.02f, "Apron", BodyRegion.Chest, BodyRegion.Pelvis);
                rig.CollectRenderers();
            }

            // Customers stand in front of the counter, out on the street.
            var trade = new GameObject("Trade");
            trade.transform.SetParent(c, false);
            trade.transform.localPosition = new Vector3(0f, 0f, -1.4f);
            var trigger = trade.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(4.4f, 2.4f, 2.2f);
            trigger.center = new Vector3(0f, 1.2f, 0f);
            var shop = trade.AddComponent<ShopStall>();
            shop.Kind = kind;
            shop.Keeper = keeper;
            Prim.Label(c, new Vector3(0f, 2.15f, -0.7f), keeper, Color.white, 0.06f);
        }

        /// <summary>A few of the shop's real wares laid out on the counter top.</summary>
        static void Goods(Transform counter, ShopKind kind)
        {
            float y = 1.18f;
            switch (kind)
            {
                case ShopKind.Weapons:
                    var blades = GameDatabase.Weapons.Values.Where(w => w.sold && w.Kind == WeaponKind.Melee && w.length < 1f).OrderBy(w => w.price).Take(2).ToList();
                    for (int i = 0; i < blades.Count; i++)
                    {
                        var m = new GameObject("CounterWeapon").transform;
                        m.SetParent(counter, false);
                        m.localPosition = new Vector3(-1.3f + i * 0.9f, y + 0.03f, -0.1f);
                        m.localRotation = Quaternion.Euler(0f, 70f + i * 20f, 0f); // lying flat on the counter
                        CharacterBuilder.BuildWeapon(blades[i], m);
                    }
                    break;
                case ShopKind.Armor:
                    Prim.Make(PrimitiveType.Sphere, counter, new Vector3(-1.2f, y + 0.12f, -0.1f), new Vector3(0.36f, 0.28f, 0.36f), new Color(0.68f, 0.68f, 0.72f), "Helmet");
                    Prim.Box(counter, new Vector3(-1.2f, y + 0.03f, -0.1f), new Vector3(0.5f, 0.04f, 0.42f), new Color(0.6f, 0.6f, 0.64f), false, "Brim");
                    Prim.Box(counter, new Vector3(-0.3f, y + 0.04f, -0.05f), new Vector3(0.4f, 0.08f, 0.3f), new Color(0.4f, 0.26f, 0.14f), false, "Gloves");
                    Prim.Box(counter, new Vector3(1.4f, y + 0.08f, 0f), new Vector3(0.5f, 0.16f, 0.3f), new Color(0.45f, 0.45f, 0.48f), false, "Rings");
                    break;
                default:
                    for (int i = 0; i < 5; i++)
                        Prim.Make(PrimitiveType.Sphere, counter, new Vector3(-1.5f + i * 0.12f, y + 0.06f, -0.1f + (i % 2) * 0.1f), Vector3.one * 0.12f, new Color(0.8f, 0.12f, 0.1f), "Apple");
                    Prim.Make(PrimitiveType.Capsule, counter, new Vector3(-0.6f, y + 0.08f, -0.05f), new Vector3(0.2f, 0.18f, 0.16f), new Color(0.75f, 0.52f, 0.25f), "Loaf").transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Prim.Make(PrimitiveType.Cylinder, counter, new Vector3(0.1f, y + 0.07f, -0.05f), new Vector3(0.3f, 0.07f, 0.3f), new Color(0.95f, 0.8f, 0.3f), "Cheese");
                    for (int i = 0; i < 3; i++)
                        Prim.Make(PrimitiveType.Cylinder, counter, new Vector3(1.0f + i * 0.2f, y + 0.12f, -0.05f), new Vector3(0.1f, 0.12f, 0.1f), i == 1 ? new Color(0.3f, 0.6f, 0.3f) : new Color(0.55f, 0.3f, 0.15f), "Bottle");
                    break;
            }
        }

        static void Nameplate(Transform t, string text, float d, float h)
        {
            Prim.Label(t, new Vector3(0f, h + 1.8f, -d / 2f), text, new Color(1f, 0.92f, 0.7f), 0.1f);
        }

        static void ArmorStand(Transform t, Vector3 pos, ArmorDef chest, ArmorDef head)
        {
            var s = Group(t, "ArmorStand", pos);
            Prim.Make(PrimitiveType.Cylinder, s, new Vector3(0f, 0.04f, 0f), new Vector3(0.6f, 0.04f, 0.6f), DarkTimber, "Base", true);
            Prim.Box(s, new Vector3(0f, 0.75f, 0f), new Vector3(0.08f, 1.5f, 0.08f), Timber, false, "Pole");
            Prim.Box(s, new Vector3(0f, 1.45f, 0f), new Vector3(0.7f, 0.08f, 0.08f), Timber, false, "Shoulders");
            var chestCol = chest != null ? chest.Tint : new Color(0.7f, 0.7f, 0.75f);
            var torso = Prim.Make(PrimitiveType.Sphere, s, new Vector3(0f, 1.2f, 0f), new Vector3(0.55f, 0.65f, 0.34f), chestCol, "Chest");
            torso.name = chest != null ? "Chest_" + chest.id : "Chest";
            Prim.Box(s, new Vector3(0f, 0.9f, 0f), new Vector3(0.48f, 0.12f, 0.32f), chestCol * 0.85f, false, "Fauld");
            for (int i = -1; i <= 1; i += 2)
                Prim.Make(PrimitiveType.Sphere, s, new Vector3(i * 0.3f, 1.45f, 0f), new Vector3(0.24f, 0.18f, 0.26f), chestCol * 0.9f, "Pauldron");
            if (head != null)
            {
                Prim.Make(PrimitiveType.Sphere, s, new Vector3(0f, 1.72f, 0f), new Vector3(0.28f, 0.24f, 0.3f), head.Tint, "Helmet_" + head.id);
                Prim.Box(s, new Vector3(0f, 1.63f, 0f), new Vector3(0.42f, 0.03f, 0.4f), head.Tint * 0.9f, false, "Brim");
            }
            var col = s.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.height = 1.8f;
            col.radius = 0.3f;
        }

        static void Barrel(Transform t, Vector3 pos, Color wood, bool lying = false)
        {
            var b = Group(t, "Barrel", pos);
            var body = Prim.Make(PrimitiveType.Cylinder, b, new Vector3(0f, 0.5f, 0f), new Vector3(0.75f, 0.5f, 0.75f), wood, "Staves", !lying);
            for (int i = -1; i <= 1; i++)
                Prim.Make(PrimitiveType.Cylinder, b, new Vector3(0f, 0.5f + i * 0.33f, 0f), new Vector3(0.79f, 0.03f, 0.79f), Iron, "Hoop");
            Prim.Make(PrimitiveType.Sphere, b, new Vector3(0f, 0.5f, 0f), new Vector3(0.82f, 0.82f, 0.82f), wood * 0.95f, "Belly");
            if (lying) b.localRotation = Quaternion.Euler(0f, 0f, 90f);
            if (lying) b.localPosition += new Vector3(0.5f, -0.5f, 0f);
            body.name = "Barrel";
        }

        static void Crate(Transform t, Vector3 pos, Color fruit)
        {
            var c = Group(t, "Crate", pos);
            Prim.Box(c, new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.6f, 0.7f), new Color(0.6f, 0.45f, 0.28f), true, "Box");
            for (int i = -1; i <= 1; i++) Prim.Box(c, new Vector3(0f, 0.3f + i * 0.2f, -0.36f), new Vector3(0.92f, 0.06f, 0.02f), new Color(0.45f, 0.33f, 0.2f), false, "Slat");
            for (int x = 0; x < 4; x++)
                for (int z = 0; z < 3; z++)
                    Prim.Make(PrimitiveType.Sphere, c, new Vector3(-0.32f + x * 0.21f, 0.64f + ((x + z) % 2) * 0.03f, -0.22f + z * 0.22f), Vector3.one * 0.19f, Prim.Vary(fruit, 0.08f), "Fruit");
        }

        static void Sack(Transform t, Vector3 pos)
        {
            var s = Group(t, "Sack", pos);
            Prim.Make(PrimitiveType.Sphere, s, new Vector3(0f, 0.32f, 0f), new Vector3(0.55f, 0.65f, 0.5f), new Color(0.78f, 0.68f, 0.5f), "Sack", true);
            Prim.Make(PrimitiveType.Cylinder, s, new Vector3(0f, 0.68f, 0f), new Vector3(0.18f, 0.08f, 0.18f), new Color(0.7f, 0.6f, 0.42f), "Neck");
            Prim.Make(PrimitiveType.Sphere, s, new Vector3(0f, 0.75f, 0f), new Vector3(0.22f, 0.1f, 0.22f), Color.white * 0.95f, "Flour");
        }

        static void Chalkboard(Transform t, Vector3 pos)
        {
            var c = Group(t, "Chalkboard", pos);
            c.localRotation = Quaternion.Euler(0f, 20f, 0f);
            for (int i = -1; i <= 1; i += 2)
            {
                var leg = Prim.Box(c, new Vector3(i * 0.3f, 0.5f, 0f), new Vector3(0.05f, 1.05f, 0.05f), Timber, false, "Leg");
                leg.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            }
            var board = Prim.Box(c, new Vector3(0f, 0.65f, -0.06f), new Vector3(0.65f, 0.75f, 0.03f), new Color(0.12f, 0.14f, 0.13f), false, "Board");
            board.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            var text = Prim.Label(c, new Vector3(0f, 0.68f, -0.12f), "Bread 4\nPork 8\nTea 5\nPhysic 30", new Color(0.95f, 0.95f, 0.9f), 0.025f);
            Object.Destroy(text.GetComponent<Billboard>());
            text.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
        }
    }

    /// <summary>Puffs of grey smoke drifting up from a chimney. Only a handful exist at once.</summary>
    public class ChimneySmoke : MonoBehaviour
    {
        float next;

        void Update()
        {
            if (Time.time < next) return;
            next = Time.time + Random.Range(0.6f, 1.1f);
            var cam = Camera.main;
            if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > 90f * 90f) return; // nobody to see it
            var puff = Prim.Make(PrimitiveType.Sphere, null, transform.position + Random.insideUnitSphere * 0.15f,
                Vector3.one * Random.Range(0.3f, 0.45f), new Color(0.55f, 0.55f, 0.55f) * Random.Range(0.85f, 1.1f), "Smoke");
            puff.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var f = puff.AddComponent<Fader>();
            f.Life = Random.Range(2.5f, 3.5f);
            f.Velocity = new Vector3(Random.Range(0.1f, 0.35f), Random.Range(0.6f, 0.9f), Random.Range(-0.1f, 0.1f));
        }
    }
}
