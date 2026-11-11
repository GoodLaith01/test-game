using System.Collections.Generic;
using Hollowmark.Data;
using UnityEngine;

namespace Hollowmark.Visuals
{
    /// <summary>
    /// A picture for every weapon, armor piece and supply, painted in code the first time it is shown and then cached.
    /// Each known id gets its own drawing; new ids from the JSON files fall back to a picture for their kind,
    /// tinted with the entry's colour. To use real art instead, put a texture at Resources/Icons/&lt;id&gt;.
    /// </summary>
    public static class ItemIcons
    {
        public const int Size = 128;
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D For(WeaponDef d) { return d == null ? null : Get("w:" + d.id, d.id, c => PaintWeapon(c, d)); }
        public static Texture2D For(ArmorDef d) { return d == null ? null : Get("a:" + d.id, d.id, c => PaintArmor(c, d)); }
        public static Texture2D For(ItemDef d) { return d == null ? null : Get("i:" + d.id, d.id, c => PaintItem(c, d)); }

        static Queue<System.Action> warmQueue;

        /// <summary>
        /// Paints one not-yet-cached picture. Call once a frame while a menu is open so the shop never has to
        /// paint a whole page at once. Returns false when every picture is ready.
        /// </summary>
        public static bool WarmUp()
        {
            if (warmQueue == null)
            {
                warmQueue = new Queue<System.Action>();
                foreach (var w in GameDatabase.Weapons.Values) { var d = w; warmQueue.Enqueue(() => For(d)); }
                foreach (var a in GameDatabase.Armor.Values) { var d = a; warmQueue.Enqueue(() => For(d)); }
                foreach (var i in GameDatabase.Items.Values) { var d = i; warmQueue.Enqueue(() => For(d)); }
            }
            if (warmQueue.Count == 0) return false;
            warmQueue.Dequeue()();
            return true;
        }

        static Texture2D Get(string key, string id, System.Action<IconCanvas> paint)
        {
            Texture2D tex;
            if (cache.TryGetValue(key, out tex) && tex != null) return tex;
            tex = Resources.Load<Texture2D>("Icons/" + id);
            if (tex == null)
            {
                var c = new IconCanvas(Size);
                try { paint(c); }
                catch (System.Exception e)
                {
                    Debug.LogWarning("Could not paint the picture for " + id + ": " + e.Message);
                    c.Clear();
                    Sack(c, new Color(0.6f, 0.5f, 0.35f));
                }
                c.Finish();
                tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
                tex.name = "Icon_" + id;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Trilinear;
                tex.SetPixels(c.Px);
                tex.Apply(true, true);
            }
            cache[key] = tex;
            return tex;
        }

        /// <summary>
        /// An opaque square picture for a painted shop sign: the item drawn on a wooden board with a light border.
        /// </summary>
        public static Texture2D SignTexture(System.Action<IconCanvas> paint, Color board)
        {
            var c = new IconCanvas(Size);
            paint(c);
            c.Finish(1.6f, false);
            var px = new Color[Size * Size];
            float edge = Size * 0.06f;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float grain = 0.85f + 0.2f * IconCanvas.Noise(new Vector2(x * 0.05f, y * 0.6f));
                    bool border = x < edge || y < edge || x >= Size - edge || y >= Size - edge;
                    var bg = IconCanvas.Mul(border ? board * 0.6f : board, grain);
                    bg.a = 1f;
                    // Shrink the picture a little so it sits inside the border.
                    float u = (x - Size * 0.5f) / 0.8f + Size * 0.5f, v = (y - Size * 0.5f) / 0.8f + Size * 0.5f;
                    int ix = Mathf.Clamp(Mathf.RoundToInt(u), 0, Size - 1), iy = Mathf.Clamp(Mathf.RoundToInt(v), 0, Size - 1);
                    var fg = (u < 0f || v < 0f || u >= Size || v >= Size) ? new Color(0f, 0f, 0f, 0f) : c.Px[iy * Size + ix];
                    px[y * Size + x] = Color.Lerp(bg, new Color(fg.r, fg.g, fg.b, 1f), fg.a);
                }
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = "Sign", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            tex.SetPixels(px);
            tex.Apply(true, true);
            return tex;
        }

        // ---------- Palette ----------

        static readonly Color Steel = new Color(0.72f, 0.74f, 0.78f);
        static readonly Color DarkSteel = new Color(0.36f, 0.37f, 0.4f);
        static readonly Color Brass = new Color(0.86f, 0.66f, 0.26f);
        static readonly Color Wood = new Color(0.46f, 0.28f, 0.14f);
        static readonly Color DarkWood = new Color(0.26f, 0.16f, 0.09f);
        static readonly Color Leather = new Color(0.4f, 0.25f, 0.13f);
        static readonly Color Linen = new Color(0.93f, 0.9f, 0.82f);
        static readonly Color Bone = new Color(0.88f, 0.84f, 0.7f);
        static readonly Color Glass = new Color(0.78f, 0.9f, 0.95f, 0.5f);
        static readonly Color Ember = new Color(1f, 0.45f, 0.1f);

        static Vector2 V(float x, float y) { return new Vector2(x, y); }

        static Vector2[] Mirror(params Vector2[] rightHalf)
        {
            // rightHalf runs from the top centre clockwise to the bottom centre; the left half is mirrored back up.
            var pts = new List<Vector2>(rightHalf);
            for (int i = rightHalf.Length - 1; i >= 0; i--)
                if (Mathf.Abs(rightHalf[i].x - 50f) > 0.01f) pts.Add(V(100f - rightHalf[i].x, rightHalf[i].y));
            return pts.ToArray();
        }

        static Vector2[] Arc(Vector2 c, float rx, float ry, float fromDeg, float toDeg, int n)
        {
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, i / (float)(n - 1)) * Mathf.Deg2Rad;
                pts[i] = c + V(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
            }
            return pts;
        }

        static Vector2[] Concat(params Vector2[][] parts)
        {
            var l = new List<Vector2>();
            foreach (var p in parts) l.AddRange(p);
            return l.ToArray();
        }

        /// <summary>A straight axis through the picture: t runs 0..1 from a to b, s is a sideways offset in units.</summary>
        struct Frame
        {
            public Vector2 O, U, Side;
            public Frame(Vector2 a, Vector2 b) { O = a; U = b - a; Side = new Vector2(-U.y, U.x).normalized; }
            public Vector2 P(float t, float s) { return O + U * t + Side * s; }
            public float Deg { get { return Mathf.Atan2(U.y, U.x) * Mathf.Rad2Deg; } }
        }

        // =====================================================================
        // Weapons
        // =====================================================================

        enum Tip { Point, Clip, Blunt }
        enum Guard { Simple, Cross, D, Cup, Basket, None }

        public static void PaintWeapon(IconCanvas c, WeaponDef d)
        {
            var metal = Color.Lerp(Steel, d.Tint, 0.5f);
            switch (d.id)
            {
                case "hanger": Sword(c, new Frame(V(17, 17), V(85, 85)), metal, 0.34f, 7f, -5f, Tip.Point, Guard.D, DarkWood, Brass); break;
                case "dirk": Sword(c, new Frame(V(20, 20), V(82, 82)), metal, 0.4f, 8f, 0f, Tip.Point, Guard.Simple, new Color(0.15f, 0.12f, 0.1f), Brass, 0.75f); break;
                case "smallsword": Sword(c, new Frame(V(16, 16), V(88, 88)), metal, 0.26f, 3.4f, 0f, Tip.Point, Guard.Cup, new Color(0.75f, 0.75f, 0.8f), new Color(0.85f, 0.85f, 0.9f)); break;
                case "cutlass": Sword(c, new Frame(V(17, 17), V(85, 85)), metal, 0.3f, 10.5f, -4f, Tip.Clip, Guard.D, new Color(0.15f, 0.12f, 0.1f), DarkSteel); break;
                case "sabre": Sword(c, new Frame(V(14, 14), V(88, 88)), metal, 0.24f, 7f, -10f, Tip.Point, Guard.D, Leather, Brass); break;
                case "broadsword": Sword(c, new Frame(V(14, 14), V(88, 88)), metal, 0.28f, 8.5f, 0f, Tip.Point, Guard.Basket, new Color(0.5f, 0.12f, 0.1f), Steel); break;
                case "claymore": Sword(c, new Frame(V(10, 10), V(91, 91)), metal, 0.27f, 9f, 0f, Tip.Point, Guard.Cross, Leather, DarkSteel); break;
                case "executioner": Sword(c, new Frame(V(12, 12), V(88, 88)), metal, 0.28f, 13f, 0f, Tip.Blunt, Guard.Cross, DarkWood, DarkSteel); break;
                case "bone_cleaver": Cleaver(c, d.Tint); break;
                case "gnarl_hook": Sickle(c, d.Tint); break;
                case "tomahawk": Axe(c, new Frame(V(26, 16), V(76, 86)), metal, 0.8f, false, true); break;
                case "boarding_axe": Axe(c, new Frame(V(24, 14), V(78, 86)), metal, 1f, true, false); break;
                case "spontoon": Polearm(c, metal, PoleHead.Spontoon); break;
                case "halberd": Polearm(c, metal, PoleHead.Halberd); break;
                case "ember_glaive": Polearm(c, d.Tint, PoleHead.Glaive); break;
                case "flintlock_pistol": Pistol(c, d.Tint); break;
                case "brown_bess": LongGun(c, d.Tint, false, true); break;
                case "blunderbuss": Blunderbuss(c, d.Tint); break;
                case "long_rifle": LongGun(c, Color.Lerp(d.Tint, new Color(0.78f, 0.48f, 0.22f), 0.55f), true, false); break;
                default:
                    if (d.Kind == WeaponKind.Firearm)
                    {
                        if (d.pellets > 1) Blunderbuss(c, d.Tint);
                        else if (d.twoHanded || d.length > 1f) LongGun(c, d.Tint, false, false);
                        else Pistol(c, d.Tint);
                    }
                    else if (d.length > 1.6f) Polearm(c, metal, PoleHead.Spontoon);
                    else if (d.Weight == WeightClass.Great) Sword(c, new Frame(V(10, 10), V(91, 91)), metal, 0.27f, 10f, 0f, Tip.Point, Guard.Cross, Leather, DarkSteel);
                    else if (d.Weight == WeightClass.Light) Sword(c, new Frame(V(22, 22), V(82, 82)), metal, 0.34f, 6f, -3f, Tip.Point, Guard.D, Leather, Brass);
                    else Sword(c, new Frame(V(16, 16), V(86, 86)), metal, 0.28f, 8f, 0f, Tip.Point, Guard.Cross, Leather, Brass);
                    break;
            }
        }

        static void Sword(IconCanvas c, Frame f, Color steel, float tb, float baseW, float curve, Tip tip, Guard guard, Color grip, Color fitting, float taperAll = 0.35f)
        {
            float len = f.U.magnitude;
            baseW *= 1.35f; // a little bolder than life, so the blade reads at icon size
            curve *= 1.2f;
            const int n = 24;
            var edgeA = new Vector2[n];
            var edgeB = new Vector2[n];
            var spine = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)(n - 1);
                float t = Mathf.Lerp(tb, 1f, u);
                float off = curve * u * u;
                float w;
                switch (tip)
                {
                    case Tip.Blunt: w = baseW * (u > 0.97f ? 0.85f : 1f); break;
                    case Tip.Clip: w = baseW * Mathf.Lerp(1f, 1.1f, u) * Mathf.Sqrt(Mathf.Clamp01((1f - u) / 0.22f)); break;
                    default: w = baseW * Mathf.Lerp(1f, 1f - taperAll, u) * Mathf.Sqrt(Mathf.Clamp01((1f - u) / 0.14f)); break;
                }
                if (tip == Tip.Clip) off += -w * 0.25f * u; // the tip swept down toward the edge
                spine[i] = f.P(t, off);
                edgeA[i] = f.P(t, off + w * 0.5f);
                edgeB[i] = f.P(t, off - w * 0.5f);
            }
            var outline = new List<Vector2>(edgeA);
            for (int i = n - 1; i >= 0; i--) outline.Add(edgeB[i]);
            c.Poly(outline.ToArray(), IconCanvas.Mul(steel, 0.85f), 0.2f);
            // The lit bevel: from the spine to the upper-left edge.
            var bevel = new List<Vector2>(edgeA);
            for (int i = n - 1; i >= 0; i--) bevel.Add(Vector2.Lerp(spine[i], edgeA[i], 0.1f));
            c.Poly(bevel.ToArray(), IconCanvas.Mul(steel, 1.12f), 0.5f);
            // Fuller groove and a bright edge line.
            if (baseW > 5f && tip != Tip.Blunt)
                c.Capsule(Vector2.Lerp(spine[1], edgeB[1], 0.2f), Vector2.Lerp(spine[n / 2], edgeB[n / 2], 0.2f), baseW * 0.09f, IconCanvas.Mul(steel, 0.7f));
            for (int i = 0; i + 1 < n; i++) c.Line(edgeA[i], edgeA[i + 1], 0.35f, new Color(1f, 1f, 1f, 0.55f));
            c.Glint(Vector2.Lerp(spine[n / 3], edgeA[n / 3], 0.6f), 4f);

            float gripLen = Mathf.Max(9f, len * 0.13f) / len;
            if (guard == Guard.Cross) gripLen *= 1.6f;
            float tg = tb - gripLen;
            System.Func<Vector2, float> wrap = p => 0.82f + 0.25f * Mathf.Abs(Mathf.Sin(Vector2.Dot(p - f.O, f.U.normalized) * 1.6f));

            if (guard == Guard.Basket)
            {
                // The red-lined steel cage that wraps the whole hand.
                var bc = f.P(tb - gripLen * 0.45f, -1.5f);
                c.Ellipse(bc, 13f, 10f, new Color(0.45f, 0.08f, 0.06f), 0f, f.Deg);
                c.Capsule(f.P(tb, 0f), f.P(tg, 0f), 2.6f, grip, 0f, -1f, wrap);
                System.Func<Vector2, float> lattice = p =>
                {
                    var q = p - bc;
                    float a = Mathf.Abs(Mathf.Sin(q.x * 0.55f + q.y * 0.55f)) * Mathf.Abs(Mathf.Sin(q.x * 0.55f - q.y * 0.55f));
                    return a > 0.35f ? 0.25f : 1.05f;
                };
                c.Ellipse(bc, 13f, 10f, fitting, 0.6f, f.Deg, lattice);
                c.Ring(bc, 10.5f, 1.1f, fitting, 0.8f);
                c.Circle(f.P(tg - 0.03f, 0f), 3.6f, fitting, 0.8f);
                return;
            }

            c.Capsule(f.P(tb, 0f), f.P(tg, 0f), guard == Guard.Cup ? 1.8f : 2.6f, grip, 0.2f, -1f, wrap);
            switch (guard)
            {
                case Guard.Cross:
                    c.Capsule(f.P(tb, -13f), f.P(tb, 13f), 2f, fitting, 0.7f);
                    c.Circle(f.P(tb, -13.5f), 3f, fitting, 0.7f);
                    c.Circle(f.P(tb, 13.5f), 3f, fitting, 0.7f);
                    c.Ellipse(f.P(tb + 0.02f, 0f), 3.5f, 6f, fitting, 0.6f, f.Deg);
                    break;
                case Guard.D:
                    c.Capsule(f.P(tb, -6f), f.P(tb, 5f), 1.8f, fitting, 0.7f);
                    c.Stroke(new[] { f.P(tb, -6f), f.P(tb - gripLen * 0.35f, -8.5f), f.P(tb - gripLen * 0.75f, -7f), f.P(tg - 0.01f, -2f) }, 1.2f, fitting, 0.6f);
                    break;
                case Guard.Cup:
                    c.Ellipse(f.P(tb + 0.01f, 0f), 2.4f, 7.5f, fitting, 0.8f, f.Deg);
                    c.Stroke(new[] { f.P(tb, -4f), f.P(tb - gripLen * 0.5f, -6f), f.P(tg, -1.5f) }, 0.9f, fitting, 0.6f);
                    c.Capsule(f.P(tb, -6f), f.P(tb, 6f), 1.1f, fitting, 0.7f);
                    break;
                case Guard.Simple:
                    c.Capsule(f.P(tb, -6f), f.P(tb, 6f), 1.8f, fitting, 0.7f);
                    break;
            }
            c.Circle(f.P(tg - 0.025f, 0f), guard == Guard.Cup ? 2.8f : 3.6f, fitting, 0.8f);
        }

        static void Cleaver(IconCanvas c, Color bone)
        {
            var f = new Frame(V(20, 20), V(84, 84));
            c.Capsule(f.P(0f, 0f), f.P(0.42f, 0f), 3f, IconCanvas.Mul(bone, 0.8f), 0.2f, 2.6f, p => 0.85f + 0.2f * IconCanvas.Noise(p * 0.6f));
            c.Circle(f.P(0f, 0f), 4f, IconCanvas.Mul(bone, 0.9f), 0.3f);
            var blade = new[] { f.P(0.38f, -4f), f.P(0.4f, 15f), f.P(1f, 19f), f.P(1.02f, 4f), f.P(0.95f, 1f), f.P(0.85f, -2f), f.P(0.7f, 0f), f.P(0.55f, -3f) };
            var bladeSdf = IconCanvas.Subtract(IconCanvas.SdPoly(blade), IconCanvas.SdCircle(f.P(0.52f, 9f), 3f));
            c.Field(V(10, 10), V(95, 95), bladeSdf, bone, p => 0.8f + 0.3f * IconCanvas.Noise(p * 0.35f));
            c.Line(f.P(0.6f, 13f), f.P(0.8f, 8f), 0.4f, IconCanvas.Mul(bone, 0.55f));
            c.Line(f.P(0.8f, 8f), f.P(0.86f, 12f), 0.4f, IconCanvas.Mul(bone, 0.55f));
            c.Stroke(new[] { f.P(0.98f, 4f), f.P(0.9f, 0.5f), f.P(0.8f, -1.2f), f.P(0.68f, 1f) }, 0.6f, new Color(0.55f, 0.15f, 0.1f, 0.8f));
        }

        static void Sickle(IconCanvas c, Color tint)
        {
            var f = new Frame(V(24, 18), V(54, 58));
            c.Capsule(f.P(0f, 0f), f.P(1f, 0f), 3f, Leather, 0.1f, -1f, p => 0.8f + 0.25f * Mathf.Abs(Mathf.Sin(p.y * 1.4f)));
            var metal = Color.Lerp(tint, Steel, 0.35f);
            var outer = Arc(V(64, 62), 24f, 24f, 200f, 20f, 18);
            var inner = Arc(V(68, 58), 17f, 17f, 30f, 200f, 18);
            c.Poly(Concat(outer, inner), metal, 0.4f);
            for (int i = 2; i < 16; i += 3)
            {
                var b = outer[i];
                c.Poly(new[] { b, b + (b - V(64, 62)).normalized * 4f + V(2, 1), outer[i + 1] }, IconCanvas.Mul(metal, 0.8f));
            }
            c.Stroke(outer, 0.4f, new Color(1f, 1f, 1f, 0.4f));
            c.Capsule(f.P(1f, -3f), f.P(1f, 3f), 2f, DarkSteel, 0.5f);
        }

        static void Axe(IconCanvas c, Frame f, Color metal, float size, bool spike, bool pipe)
        {
            c.Capsule(f.P(0f, 0f), f.P(1f, 0f), 2.6f, Wood, 0.3f, 2.2f, p => 0.85f + 0.2f * IconCanvas.Noise(new Vector2(p.x * 0.3f, p.y * 2f)));
            c.Capsule(f.P(0.05f, 0f), f.P(0.22f, 0f), 3f, Leather, 0.1f, -1f, p => 0.8f + 0.25f * Mathf.Abs(Mathf.Sin((p.x + p.y) * 1.3f)));
            float h = 0.84f;
            float s = 14f * size;
            var blade = new[] { f.P(h + 0.04f, 2f), f.P(h + 0.12f * size, s), f.P(h - 0.14f * size, s + 2f), f.P(h - 0.06f, 2f) };
            c.Poly(blade, metal, 0.3f);
            c.Stroke(new[] { f.P(h + 0.12f * size, s), f.P(h - 0.01f, s + 2.6f), f.P(h - 0.14f * size, s + 2f) }, 0.9f, IconCanvas.Shine(metal, 0.5f));
            c.Capsule(f.P(h - 0.05f, 0f), f.P(h + 0.05f, 0f), 4f, IconCanvas.Mul(metal, 0.9f), 0.5f);
            if (spike) c.Poly(new[] { f.P(h + 0.04f, -2f), f.P(h, -15f * size), f.P(h - 0.04f, -2f) }, metal, 0.4f);
            if (pipe)
            {
                c.Capsule(f.P(h, -2f), f.P(h, -7f), 2.6f, Brass, 0.6f, 3.4f);
                for (int i = 0; i < 2; i++)
                    c.Capsule(f.P(0.3f + i * 0.06f, 2f), f.P(0.24f + i * 0.06f, 9f), 1.4f, i == 0 ? new Color(0.9f, 0.88f, 0.8f) : new Color(0.55f, 0.15f, 0.1f), 0f, 0.6f);
            }
            c.Glint(f.P(h + 0.02f, s * 0.6f), 3.5f);
        }

        enum PoleHead { Spontoon, Halberd, Glaive }

        static void Polearm(IconCanvas c, Color metal, PoleHead head)
        {
            var f = new Frame(V(10, 10), V(90, 90));
            bool ember = head == PoleHead.Glaive;
            if (ember) c.Glow(f.P(0.82f, 3f), 26f, new Color(1f, 0.5f, 0.1f, 0.55f));
            c.Capsule(f.P(0f, 0f), f.P(0.78f, 0f), 1.9f, Wood, 0.3f, -1f, p => 0.85f + 0.2f * IconCanvas.Noise(new Vector2(p.x * 0.4f, p.y * 2f)));
            c.Capsule(f.P(0f, 0f), f.P(0.04f, 0f), 2.4f, DarkSteel, 0.5f);
            switch (head)
            {
                case PoleHead.Spontoon:
                    c.Poly(new[] { f.P(0.73f, 0f), f.P(0.8f, 6.5f), f.P(1f, 0f), f.P(0.8f, -6.5f) }, metal, 0.4f);
                    c.Poly(new[] { f.P(0.73f, 0f), f.P(0.8f, 6.5f), f.P(1f, 0f) }, IconCanvas.Mul(metal, 1.12f), 0.5f);
                    c.Capsule(f.P(0.735f, -6f), f.P(0.735f, 6f), 1.3f, Brass, 0.6f);
                    c.Capsule(f.P(0.7f, 0f), f.P(0.74f, 0f), 2.6f, Brass, 0.6f);
                    for (int i = -1; i <= 1; i++) c.Capsule(f.P(0.69f, 2f * i), f.P(0.62f, -4f + 2f * i), 1f, new Color(0.75f, 0.12f, 0.1f), 0f, 0.4f);
                    break;
                case PoleHead.Halberd:
                    c.Poly(new[] { f.P(0.86f, -1.5f), f.P(1f, 0f), f.P(0.86f, 1.5f) }, metal, 0.4f);
                    c.Poly(new[] { f.P(0.83f, 1.5f), f.P(0.92f, 15f), f.P(0.73f, 16f), f.P(0.75f, 1.5f) }, metal, 0.4f);
                    c.Stroke(new[] { f.P(0.92f, 15f), f.P(0.82f, 17.5f), f.P(0.73f, 16f) }, 0.9f, IconCanvas.Shine(metal, 0.5f));
                    c.Poly(new[] { f.P(0.83f, -1.5f), f.P(0.86f, -11f), f.P(0.8f, -9f), f.P(0.76f, -1.5f) }, IconCanvas.Mul(metal, 0.9f), 0.3f);
                    c.Capsule(f.P(0.74f, 0f), f.P(0.87f, 0f), 2.4f, IconCanvas.Mul(metal, 0.85f), 0.5f);
                    c.Glint(f.P(0.84f, 10f), 3.5f);
                    break;
                case PoleHead.Glaive:
                    var blade = new[] { f.P(0.68f, -2f), f.P(0.7f, 6f), f.P(0.86f, 10f), f.P(1f, 6f), f.P(0.95f, 1f), f.P(0.8f, -2.5f) };
                    c.Poly(blade, metal, 0.2f, p => 0.85f + 0.4f * IconCanvas.Noise(p * 0.5f));
                    c.Stroke(new[] { f.P(0.7f, 6f), f.P(0.86f, 10f), f.P(1f, 6f) }, 0.8f, new Color(1f, 0.85f, 0.4f));
                    c.Stroke(new[] { f.P(0.74f, 1f), f.P(0.82f, 4.5f), f.P(0.88f, 3.5f), f.P(0.95f, 5.5f) }, 0.55f, new Color(1f, 0.95f, 0.6f));
                    c.Capsule(f.P(0.66f, 0f), f.P(0.72f, 0f), 2.6f, new Color(0.25f, 0.15f, 0.12f), 0.3f);
                    for (int i = 0; i < 6; i++)
                        c.Circle(f.P(0.75f + 0.04f * i, 14f + 3f * IconCanvas.Hash(i, 3f)), 0.8f + 0.6f * IconCanvas.Hash(i, 7f), new Color(1f, 0.8f, 0.3f));
                    break;
            }
        }

        static void GunLock(IconCanvas c, Frame f, float t, float s)
        {
            c.Poly(new[] { f.P(t - 0.05f, s - 1.2f), f.P(t + 0.07f, s - 1.2f), f.P(t + 0.08f, s + 1.6f), f.P(t - 0.06f, s + 1.6f) }, DarkSteel, 0.5f);
            c.Poly(new[] { f.P(t - 0.04f, s + 1f), f.P(t - 0.02f, s + 6f), f.P(t + 0.01f, s + 6.5f), f.P(t + 0.0f, s + 4.5f), f.P(t + 0.01f, s + 1f) }, Steel, 0.6f); // the cock
            c.Poly(new[] { f.P(t + 0.04f, s + 1.6f), f.P(t + 0.05f, s + 5f), f.P(t + 0.07f, s + 1.6f) }, Steel, 0.5f); // frizzen
        }

        static void TriggerGuard(IconCanvas c, Frame f, float t, float s)
        {
            c.Stroke(new[] { f.P(t - 0.05f, s), f.P(t - 0.03f, s - 4f), f.P(t + 0.03f, s - 4.5f), f.P(t + 0.06f, s) }, 0.9f, Brass, 0.6f);
            c.Capsule(f.P(t, s), f.P(t - 0.008f, s - 3f), 0.7f, DarkSteel);
        }

        static void LongGun(IconCanvas c, Color stock, bool rifle, bool bayonet)
        {
            var f = new Frame(V(5, 30), V(95, 68));
            float muzzle = bayonet ? 0.84f : 1f;
            float fore = rifle ? muzzle - 0.04f : muzzle - 0.1f;
            System.Func<Vector2, float> grain = p => 0.85f + 0.25f * IconCanvas.Noise(new Vector2(Vector2.Dot(p, f.U.normalized) * 0.15f, Vector2.Dot(p, f.Side) * 1.2f));
            c.Capsule(f.P(0.3f, 3f), f.P(muzzle, 3f), rifle ? 1.6f : 1.8f, DarkSteel, 0.6f);
            var body = new[] { f.P(0f, -9f), f.P(0f, 4.5f), f.P(0.24f, 2.6f), f.P(0.32f, 2.2f), f.P(fore, 2.2f), f.P(fore + 0.01f, 0.3f), f.P(0.42f, -1.4f), f.P(0.34f, -2.6f), f.P(0.27f, -3.6f), f.P(0.04f, -9f) };
            c.Poly(body, stock, 0.15f, grain);
            c.Capsule(f.P(0.45f, -0.3f), f.P(fore + 0.02f, -0.3f), 0.6f, new Color(0.55f, 0.4f, 0.25f));
            c.Poly(new[] { f.P(-0.005f, -9f), f.P(-0.005f, 4.5f), f.P(0.015f, 4.3f), f.P(0.015f, -8.6f) }, Brass, 0.6f);
            if (rifle) c.Ellipse(f.P(0.09f, -2f), 6f, 2.4f, Brass, 0.7f, f.Deg);
            else for (int i = 0; i < 3; i++) c.Capsule(f.P(0.55f + i * 0.13f, 0.4f), f.P(0.55f + i * 0.13f, 4.5f), 0.9f, Brass, 0.6f);
            GunLock(c, f, 0.33f, 1.8f);
            TriggerGuard(c, f, 0.33f, -2.4f);
            c.Capsule(f.P(muzzle - 0.01f, 3f), f.P(muzzle, 3f), 2.1f, DarkSteel, 0.5f);
            if (bayonet)
            {
                c.Capsule(f.P(0.81f, 4.5f), f.P(0.86f, 4.5f), 1.6f, Steel, 0.6f);
                c.Poly(new[] { f.P(0.85f, 3.8f), f.P(0.86f, 7.2f), f.P(1f, 5.4f) }, Steel, 0.6f);
            }
            c.Glint(f.P(0.6f, 3.6f), 3f);
        }

        static void Blunderbuss(IconCanvas c, Color stock)
        {
            var f = new Frame(V(10, 30), V(90, 62));
            c.Capsule(f.P(0.32f, 3f), f.P(0.84f, 3f), 2.2f, Brass, 0.6f, 2.8f);
            c.Poly(new[] { f.P(0.83f, 0.4f), f.P(1f, -3.5f), f.P(1f, 9.5f), f.P(0.83f, 5.6f) }, Brass, 0.6f);
            c.Ellipse(f.P(1f, 3f), 1.6f, 6.6f, new Color(0.18f, 0.12f, 0.06f), 0f, f.Deg);
            var body = new[] { f.P(0f, -10f), f.P(0f, 4.5f), f.P(0.26f, 2.6f), f.P(0.34f, 2.4f), f.P(0.72f, 2.2f), f.P(0.72f, 0f), f.P(0.42f, -1.4f), f.P(0.34f, -2.6f), f.P(0.28f, -4f), f.P(0.05f, -10f) };
            c.Poly(body, stock, 0.15f, p => 0.85f + 0.2f * IconCanvas.Noise(p * 0.25f));
            c.Poly(new[] { f.P(-0.005f, -10f), f.P(-0.005f, 4.5f), f.P(0.018f, 4.3f), f.P(0.018f, -9.6f) }, Brass, 0.6f);
            GunLock(c, f, 0.36f, 1.8f);
            TriggerGuard(c, f, 0.36f, -2.4f);
            c.Glint(f.P(0.6f, 4.4f), 3f);
        }

        static void Pistol(IconCanvas c, Color stock)
        {
            var f = new Frame(V(16, 44), V(88, 66));
            c.Capsule(f.P(0.3f, 2.8f), f.P(1f, 2.8f), 2f, DarkSteel, 0.6f);
            var body = new[] { f.P(0.02f, -15f), f.P(0.13f, -14f), f.P(0.24f, -4f), f.P(0.34f, -2.6f), f.P(0.8f, -0.6f), f.P(0.82f, 2f), f.P(0.3f, 3f), f.P(0.14f, 1f), f.P(0.04f, -6f) };
            c.Poly(body, stock, 0.15f, p => 0.85f + 0.2f * IconCanvas.Noise(p * 0.25f));
            c.Circle(f.P(0.06f, -14.5f), 4.2f, Brass, 0.8f);
            c.Capsule(f.P(0.82f, -0.6f), f.P(0.84f, 2.4f), 1.2f, Brass, 0.6f);
            GunLock(c, f, 0.33f, 1.4f);
            TriggerGuard(c, f, 0.3f, -3.3f);
            c.Capsule(f.P(0.99f, 2.8f), f.P(1f, 2.8f), 2.5f, Brass, 0.6f);
            c.Glint(f.P(0.65f, 3.6f), 3f);
        }

        // =====================================================================
        // Armor
        // =====================================================================

        public static void PaintArmor(IconCanvas c, ArmorDef d)
        {
            var t = d.Tint;
            switch (d.id)
            {
                case "tricorne": Tricorne(c, t); break;
                case "leather_cap": LeatherCap(c, t); break;
                case "morion": Morion(c, Color.Lerp(Steel, t, 0.5f)); break;
                case "lobster_pot": LobsterPot(c, Color.Lerp(Steel, t, 0.5f)); break;
                case "gnarl_mask": SkullMask(c, t); break;
                case "forsaken_helm": GreatHelm(c, t); break;
                case "wool_coat": Coat(c, t, Brass, new Color(0.9f, 0.86f, 0.74f)); break;
                case "buff_coat": BuffCoat(c, t); break;
                case "chain_shirt": MailShirt(c, t); break;
                case "cuirass": Cuirass(c, Color.Lerp(Steel, t, 0.5f), false); break;
                case "brute_hide": HideVest(c, t); break;
                case "forsaken_plate": Cuirass(c, t, true); break;
                case "leather_gloves": Glove(c, t, false); break;
                case "steel_gauntlets": Glove(c, Color.Lerp(Steel, t, 0.5f), true); break;
                case "ashen_bracers": Bracers(c, t); break;
                case "breeches": Breeches(c, t); break;
                case "jackboots": Jackboots(c, t); break;
                case "tassets": Tassets(c, Color.Lerp(Steel, t, 0.5f), false); break;
                case "crawler_carapace": Tassets(c, t, true); break;
                default:
                    switch (d.Slot)
                    {
                        case ArmorSlot.Head: if (d.defense >= 8f) Morion(c, t); else LeatherCap(c, t); break;
                        case ArmorSlot.Hands: Glove(c, t, d.defense >= 6f); break;
                        case ArmorSlot.Legs: if (d.defense >= 8f) Tassets(c, t, d.monsterArmor); else Breeches(c, t); break;
                        default: if (d.defense >= 14f) Cuirass(c, t, d.monsterArmor); else Coat(c, t, Brass, Linen); break;
                    }
                    break;
            }
        }

        static void Tricorne(IconCanvas c, Color felt)
        {
            if (felt.maxColorComponent < 0.25f) felt = new Color(0.27f, 0.25f, 0.3f); // black felt, lifted so it reads
            // Seen from the front: the crown, the cocked-up back brim behind it, and two front flaps meeting in a point.
            c.Poly(new[] { V(8, 60), V(22, 74), V(50, 80), V(78, 74), V(92, 60), V(50, 56) }, IconCanvas.Mul(felt, 0.75f));
            c.Ellipse(V(50, 62), 20f, 13f, IconCanvas.Mul(felt, 1.2f), 0.3f);
            c.Poly(new[] { V(6, 60), V(50, 22), V(50, 50), V(26, 54) }, IconCanvas.Mul(felt, 1.35f), 0.3f);
            c.Poly(new[] { V(94, 60), V(74, 54), V(50, 50), V(50, 22) }, felt, 0.15f);
            c.Stroke(new[] { V(8, 60), V(22, 74), V(50, 80), V(78, 74), V(92, 60) }, 1.2f, Brass, 0.6f);
            c.Stroke(new[] { V(6, 60), V(50, 22), V(94, 60) }, 1.3f, Brass, 0.6f);
            c.Circle(V(30, 48), 6f, new Color(0.1f, 0.1f, 0.12f), 0f, p => 0.7f + 0.4f * Mathf.Abs(Mathf.Sin(Mathf.Atan2(p.y - 48f, p.x - 30f) * 6f)));
            c.Circle(V(30, 48), 2f, Brass, 0.8f);
        }

        static void LeatherCap(IconCanvas c, Color leather)
        {
            var dome = Concat(Arc(V(50, 36), 30f, 36f, 0f, 180f, 24));
            c.Poly(dome, leather, 0.15f, p => 0.88f + 0.18f * IconCanvas.Noise(p * 0.3f));
            for (int i = -1; i <= 1; i++)
                c.Stroke(new[] { V(50 + i * 16f, 38), V(50 + i * 9f, 60), V(50, 71) }, 0.45f, IconCanvas.Mul(leather, 1.5f));
            c.Poly(new[] { V(18, 30), V(82, 30), V(82, 40), V(18, 40) }, IconCanvas.Mul(leather, 0.7f));
            for (int i = 0; i < 7; i++) c.Circle(V(23 + i * 9f, 35), 1.6f, Brass, 0.8f);
        }

        static void Morion(IconCanvas c, Color steel)
        {
            c.Poly(Concat(Arc(V(50, 40), 23f, 26f, 0f, 180f, 20)), steel, 0.5f);
            c.Poly(Concat(Arc(V(50, 54), 20f, 22f, 15f, 165f, 18), Arc(V(50, 54), 12f, 14f, 160f, 20f, 14)), IconCanvas.Mul(steel, 0.95f), 0.5f);
            var brimTop = Arc(V(50, 72), 46f, 36f, 200f, 340f, 16);
            var brimBot = Arc(V(50, 74), 42f, 42f, 335f, 205f, 16);
            c.Poly(Concat(brimTop, brimBot), IconCanvas.Mul(steel, 0.85f), 0.4f);
            for (int i = 0; i < 8; i++) c.Circle(V(29 + i * 6f, 41f - Mathf.Abs(i - 3.5f) * 0.4f), 1.2f, Brass, 0.8f);
            c.Glint(V(40, 60), 4f);
        }

        static void LobsterPot(IconCanvas c, Color steel)
        {
            for (int i = 3; i >= 0; i--)
                c.Poly(new[] { V(26 - i * 2.5f, 42 - i * 6f), V(74 + i * 2.5f, 42 - i * 6f), V(76 + i * 2.5f, 34 - i * 6f), V(24 - i * 2.5f, 34 - i * 6f) }, IconCanvas.Mul(steel, 0.75f + i * 0.05f), 0.3f);
            c.Poly(Concat(Arc(V(50, 44), 25f, 30f, 0f, 180f, 22)), steel, 0.6f);
            c.Stroke(Arc(V(50, 44), 25f, 30f, 120f, 60f, 8), 0.5f, IconCanvas.Shine(steel, 0.6f));
            c.Poly(new[] { V(22, 46), V(78, 46), V(84, 40), V(16, 40) }, IconCanvas.Mul(steel, 0.9f), 0.4f);
            c.Capsule(V(50, 52), V(50, 22), 2.2f, IconCanvas.Mul(steel, 1.05f), 0.6f, 1.6f);
            c.Capsule(V(50, 74), V(50, 50), 1.2f, IconCanvas.Mul(steel, 0.8f));
            c.Glint(V(40, 62), 4f);
        }

        static void SkullMask(IconCanvas c, Color bone)
        {
            c.Capsule(V(14, 54), V(86, 54), 2.2f, Leather);
            c.Ellipse(V(50, 58), 25f, 27f, bone, 0.1f, 0f, p => 0.85f + 0.2f * IconCanvas.Noise(p * 0.3f));
            c.Poly(new[] { V(32, 40), V(68, 40), V(62, 20), V(38, 20) }, IconCanvas.Mul(bone, 0.92f));
            var socket = new Color(0.12f, 0.08f, 0.06f);
            c.Ellipse(V(40, 56), 7f, 6f, socket, 0f, -15f);
            c.Ellipse(V(60, 56), 7f, 6f, socket, 0f, 15f);
            c.Glow(V(40, 56), 4f, new Color(0.7f, 0.9f, 0.3f, 0.8f));
            c.Glow(V(60, 56), 4f, new Color(0.7f, 0.9f, 0.3f, 0.8f));
            c.Poly(new[] { V(47, 44), V(53, 44), V(50, 50) }, socket);
            for (int i = 0; i < 6; i++) c.Poly(new[] { V(36 + i * 5f, 32), V(40 + i * 5f, 32), V(39 + i * 5f, 24), V(37 + i * 5f, 24) }, IconCanvas.Mul(bone, 1.08f));
            c.Stroke(new[] { V(56, 84), V(58, 76), V(55, 70), V(58, 64) }, 0.45f, IconCanvas.Mul(bone, 0.45f));
            c.Stroke(new[] { V(30, 70), V(35, 66), V(33, 62) }, 0.45f, IconCanvas.Mul(bone, 0.45f));
        }

        static void GreatHelm(IconCanvas c, Color iron)
        {
            iron = Color.Lerp(iron, new Color(0.4f, 0.32f, 0.3f), 0.3f);
            var helm = Concat(Arc(V(50, 62), 24f, 22f, 0f, 180f, 18), new[] { V(26, 20), V(74, 20) });
            c.Poly(helm, iron, 0.25f, p => IconCanvas.Noise(p * 0.4f) > 0.68f ? 0.6f : 0.95f + 0.1f * IconCanvas.Noise(p * 0.9f));
            c.Capsule(V(50, 83), V(50, 21), 1.6f, IconCanvas.Mul(iron, 1.25f), 0.4f);
            c.Capsule(V(27, 52), V(73, 52), 1.4f, IconCanvas.Mul(iron, 0.6f));
            c.Glow(V(50, 52), 18f, new Color(0.9f, 0.15f, 0.1f, 0.45f));
            for (int i = 0; i < 6; i++) { c.Circle(V(31 + i * 7.6f, 26), 1.3f, IconCanvas.Mul(iron, 1.4f), 0.6f); c.Circle(V(31 + i * 7.6f, 46), 1.3f, IconCanvas.Mul(iron, 1.4f), 0.6f); }
            for (int i = 0; i < 5; i++) c.Circle(V(60 + (i % 3) * 3f, 32 + i * 2f), 0.6f, new Color(0.15f, 0.1f, 0.1f));
        }

        static Vector2[] CoatShape()
        {
            return Mirror(V(50, 86), V(60, 87), V(74, 82), V(91, 48), V(81, 43), V(71, 64), V(71, 42), V(80, 12), V(56, 12), V(50, 30));
        }

        static void Coat(IconCanvas c, Color cloth, Color buttons, Color lining)
        {
            c.Poly(new[] { V(43, 84), V(57, 84), V(57, 30), V(43, 30) }, lining);
            c.Poly(CoatShape(), cloth, 0.1f, p => 0.92f + 0.1f * IconCanvas.Noise(p * 0.8f));
            c.Poly(new[] { V(42, 85), V(50, 70), V(44, 50) }, IconCanvas.Mul(cloth, 1.25f));
            c.Poly(new[] { V(58, 85), V(56, 50), V(50, 70) }, IconCanvas.Mul(cloth, 0.85f));
            c.Poly(new[] { V(80, 44), V(91, 48), V(89, 54), V(78, 50) }, IconCanvas.Mul(cloth, 0.75f));
            c.Poly(new[] { V(20, 44), V(9, 48), V(11, 54), V(22, 50) }, IconCanvas.Mul(cloth, 1.1f));
            for (int i = 0; i < 5; i++) { c.Circle(V(40, 64 - i * 7f), 1.7f, buttons, 0.8f); c.Circle(V(60, 64 - i * 7f), 1.7f, buttons, 0.8f); }
            c.Poly(new[] { V(43, 30), V(57, 30), V(55, 12), V(45, 12) }, new Color(0, 0, 0, 0.25f));
        }

        static void BuffCoat(IconCanvas c, Color leather)
        {
            c.Poly(CoatShape(), leather, 0.15f, p => 0.86f + 0.2f * IconCanvas.Noise(p * 0.35f));
            c.Line(V(50, 86), V(50, 12), 0.6f, IconCanvas.Mul(leather, 0.55f));
            for (int i = 0; i < 6; i++)
            {
                float y = 76 - i * 7.5f;
                c.Line(V(46, y), V(54, y - 3.5f), 0.55f, Linen);
                c.Line(V(54, y), V(46, y - 3.5f), 0.55f, Linen);
            }
            c.Poly(new[] { V(29, 38), V(71, 38), V(71, 33), V(29, 33) }, IconCanvas.Mul(Leather, 0.8f));
            c.Poly(new[] { V(45, 39), V(55, 39), V(55, 32), V(45, 32) }, Brass, 0.6f);
            c.Poly(new[] { V(47.5f, 37.5f), V(52.5f, 37.5f), V(52.5f, 33.5f), V(47.5f, 33.5f) }, IconCanvas.Mul(leather, 0.8f));
        }

        static void MailShirt(IconCanvas c, Color steel)
        {
            var shirt = Mirror(V(50, 84), V(60, 86), V(76, 80), V(88, 58), V(77, 54), V(70, 64), V(72, 16), V(50, 14));
            System.Func<Vector2, float> rings = p =>
            {
                float row = Mathf.Floor(p.y / 3f);
                float x = p.x / 3f + (row % 2f) * 0.5f;
                var q = new Vector2(x - Mathf.Floor(x) - 0.5f, p.y / 3f - row - 0.5f);
                float r = q.magnitude;
                return r > 0.18f && r < 0.42f ? 1.08f : 0.5f;
            };
            c.Poly(shirt, steel, 0.35f, rings);
            c.Poly(Concat(Arc(V(50, 86), 10f, 8f, 0f, -180f, 10)), new Color(0.1f, 0.08f, 0.07f));
            c.Poly(new[] { V(28, 20), V(72, 20), V(72, 14), V(28, 14) }, IconCanvas.Mul(steel, 0.8f), 0.3f, rings);
            c.Glint(V(38, 66), 4f);
        }

        static void Cuirass(IconCanvas c, Color steel, bool cursed)
        {
            var plate = Mirror(V(50, 80), V(60, 84), V(70, 80), V(75, 52), V(68, 22), V(50, 15));
            System.Func<Vector2, float> rust = cursed ? (System.Func<Vector2, float>)(p => IconCanvas.Noise(p * 0.25f) > 0.62f ? 0.62f : 0.9f + 0.15f * IconCanvas.Noise(p)) : null;
            c.Capsule(V(36, 82), V(30, 88), 2.4f, Leather);
            c.Capsule(V(64, 82), V(70, 88), 2.4f, Leather);
            c.Poly(plate, steel, cursed ? 0.15f : 0.7f, rust);
            c.Poly(new[] { V(50, 80), V(60, 84), V(70, 80), V(75, 52), V(68, 22), V(50, 15) }, IconCanvas.Mul(steel, 0.86f), 0f, rust); // the shaded right half
            c.Stroke(Arc(V(84, 64), 14f, 16f, 110f, 250f, 8), 1.1f, IconCanvas.Mul(steel, 1.2f), 0.5f);
            c.Stroke(Arc(V(16, 64), 14f, 16f, 70f, -70f, 8), 1.1f, IconCanvas.Mul(steel, 1.2f), 0.5f);
            c.Line(V(50, 80), V(50, 16), 0.6f, IconCanvas.Shine(steel, cursed ? 0.1f : 0.6f));
            c.Poly(new[] { V(32, 22), V(68, 22), V(70, 16), V(30, 16) }, IconCanvas.Mul(steel, 0.8f), 0.3f, rust);
            for (int i = 0; i < 6; i++) c.Circle(V(34 + i * 6.4f, 19), 1.1f, cursed ? IconCanvas.Mul(steel, 1.4f) : Brass, 0.8f);
            if (cursed)
            {
                var crack = new[] { V(44, 70), V(48, 60), V(43, 50), V(50, 40), V(46, 30) };
                c.Stroke(crack, 2.2f, new Color(1f, 0.25f, 0.1f, 0.35f));
                c.Stroke(crack, 0.7f, new Color(1f, 0.55f, 0.3f));
            }
            else c.Glint(V(40, 62), 5f);
        }

        static void HideVest(IconCanvas c, Color hide)
        {
            var vest = new List<Vector2> { V(50, 70), V(60, 86), V(72, 82), V(74, 60), V(72, 40) };
            for (int i = 0; i <= 8; i++) vest.Add(V(74 - i * 6f + (i % 2) * 2f, i % 2 == 0 ? 14f : 20f));
            vest.AddRange(new[] { V(28, 40), V(26, 60), V(28, 82), V(40, 86) });
            c.Poly(vest.ToArray(), hide, 0.1f, p => 0.75f + 0.4f * IconCanvas.Noise(p * 0.22f));
            for (int i = 0; i < 6; i++)
            {
                c.Line(V(50 + 3f, 66 - i * 8f), V(50 - 3f, 63 - i * 8f), 0.6f, Bone);
            }
            c.Line(V(50, 70), V(50, 18), 0.5f, IconCanvas.Mul(hide, 0.5f));
            c.Ellipse(V(32, 78), 9f, 6f, new Color(0.35f, 0.3f, 0.22f), 0f, 30f, p => 0.7f + 0.5f * Mathf.Abs(Mathf.Sin(p.x * 2f + p.y)));
            c.Ellipse(V(68, 78), 9f, 6f, new Color(0.35f, 0.3f, 0.22f), 0f, -30f, p => 0.7f + 0.5f * Mathf.Abs(Mathf.Sin(p.x * 2f - p.y)));
            c.Poly(new[] { V(62, 34), V(66, 44), V(68, 34) }, Bone, 0.3f);
        }

        static void Glove(IconCanvas c, Color mat, bool plate)
        {
            System.Func<Vector2, float> seg = plate ? (System.Func<Vector2, float>)(p => Mathf.Repeat(p.y, 5f) < 0.8f ? 0.55f : 1f) : null;
            float shine = plate ? 0.7f : 0.15f;
            c.Poly(new[] { V(32, 8), V(70, 8), V(66, 34), V(36, 34) }, IconCanvas.Mul(mat, plate ? 0.95f : 0.85f), shine, seg);
            c.Capsule(V(37, 44), V(24, 62), 5f, mat, shine, 4.2f, seg);
            c.Poly(new[] { V(36, 32), V(66, 32), V(68, 60), V(36, 62) }, mat, shine * 0.5f);
            float[] xs = { 40f, 48.5f, 57f, 65f }, ys = { 84f, 89f, 86f, 77f };
            for (int i = 0; i < 4; i++) c.Capsule(V(xs[i], 58), V(xs[i] + (i - 1.5f) * 0.8f, ys[i]), 4.1f, IconCanvas.Mul(mat, 1f + i * 0.02f), shine, 3.6f, seg);
            if (plate) { for (int i = 0; i < 3; i++) c.Capsule(V(38, 40 + i * 6f), V(66, 40 + i * 6f), 0.5f, IconCanvas.Mul(mat, 0.6f)); c.Glint(V(44, 20), 4f); }
            else
            {
                c.Line(V(52, 30), V(52, 56), 0.4f, IconCanvas.Mul(mat, 1.5f));
                c.Line(V(36, 34), V(66, 34), 0.5f, IconCanvas.Mul(mat, 0.6f));
            }
        }

        static void Bracers(IconCanvas c, Color mat)
        {
            for (int i = 0; i < 2; i++)
            {
                float x = 34f + i * 32f, lean = i == 0 ? -4f : 4f;
                var b = new[] { V(x - 11 + lean, 84), V(x + 11 + lean, 84), V(x + 9, 16), V(x - 9, 16) };
                c.Glow(V(x + lean * 0.5f, 50), 22f, new Color(1f, 0.45f, 0.1f, 0.25f));
                c.Poly(b, mat, 0.2f, p => 0.8f + 0.3f * IconCanvas.Noise(p * 0.3f));
                for (int k = 0; k < 3; k++) c.Capsule(V(x - 10 + lean * (0.75f - k * 0.3f), 74 - k * 24f), V(x + 10 + lean * (0.75f - k * 0.3f), 74 - k * 24f), 1.4f, IconCanvas.Mul(mat, 0.6f));
                var vein = new[] { V(x - 3 + lean * 0.6f, 70), V(x + 3 + lean * 0.4f, 58), V(x - 2, 44), V(x + 2, 30) };
                c.Stroke(vein, 1.8f, new Color(1f, 0.3f, 0.05f, 0.35f));
                c.Stroke(vein, 0.6f, new Color(1f, 0.7f, 0.3f));
            }
        }

        static void Shoe(IconCanvas c, float x, float dir)
        {
            c.Poly(new[] { V(x - 8 * dir, 16), V(x + 6 * dir, 16), V(x + 15 * dir, 10), V(x + 15 * dir, 6), V(x - 9 * dir, 6) }, new Color(0.12f, 0.09f, 0.07f), 0.3f);
            c.Poly(new[] { V(x - 1, 15), V(x + 5, 15), V(x + 5, 11), V(x - 1, 11) }, Steel, 0.8f);
        }

        static void Breeches(IconCanvas c, Color cloth)
        {
            var stocking = new Color(0.93f, 0.92f, 0.88f);
            c.Capsule(V(39, 54), V(38, 14), 6.6f, stocking, 0.2f, 5.4f);
            c.Capsule(V(61, 54), V(62, 14), 6.6f, stocking, 0.2f, 5.4f);
            Shoe(c, 36f, -1f);
            Shoe(c, 64f, 1f);
            c.Poly(new[] { V(28, 90), V(72, 90), V(74, 52), V(55, 50), V(50, 72), V(45, 50), V(26, 52) }, cloth, 0.1f, p => 0.92f + 0.1f * IconCanvas.Noise(p * 0.8f));
            c.Poly(new[] { V(28, 90), V(72, 90), V(72, 84), V(28, 84) }, IconCanvas.Mul(cloth, 0.8f));
            for (int i = 0; i < 2; i++) c.Circle(V(i == 0 ? 33 : 67, 54), 1.4f, Brass, 0.8f);
            c.Line(V(50, 84), V(50, 72), 0.5f, IconCanvas.Mul(cloth, 0.6f));
        }

        static void Jackboots(IconCanvas c, Color boot)
        {
            if (boot.maxColorComponent < 0.25f) boot = new Color(0.17f, 0.12f, 0.09f);
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? 36f : 64f, dir = i == 0 ? -1f : 1f;
                c.Poly(new[] { V(x - 12, 88), V(x + 12, 88), V(x + 10, 70), V(x - 10, 70) }, IconCanvas.Mul(Leather, 1.2f), 0.2f);
                c.Poly(new[] { V(x - 9, 72), V(x + 9, 72), V(x + 7, 16), V(x + 17 * dir, 10), V(x + 17 * dir, 5), V(x - 10 * dir, 5), V(x - 8, 16) }, boot, 0.45f);
                c.Poly(new[] { V(x - 12, 72), V(x + 12, 72), V(x + 10, 60), V(x - 10, 60) }, IconCanvas.Mul(boot, 1.4f), 0.4f);
                c.Capsule(V(x - 8, 30), V(x + 8, 30), 1f, Leather);
                c.Poly(new[] { V(x - 2, 32), V(x + 2, 32), V(x + 2, 28), V(x - 2, 28) }, Brass, 0.7f);
                c.Glint(V(x - 4, 48), 3.5f);
            }
        }

        static void Tassets(IconCanvas c, Color mat, bool chitin)
        {
            c.Poly(new[] { V(24, 90), V(76, 90), V(76, 84), V(24, 84) }, Leather);
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? 37f : 63f;
                for (int k = 0; k < 5; k++)
                {
                    float y = 84 - k * 13f, w = 14f - k * 0.8f;
                    var plate = Concat(new[] { V(x - w, y), V(x + w, y) }, Arc(V(x, y - 6), w, 9f, 0f, -180f, 12));
                    c.Poly(plate, IconCanvas.Mul(mat, 1f - k * 0.04f), chitin ? 0.5f : 0.7f, chitin ? (System.Func<Vector2, float>)(p => 0.8f + 0.3f * IconCanvas.Noise(p * 0.5f)) : null);
                    if (!chitin) c.Circle(V(x, y - 3f), 1.2f, Brass, 0.8f);
                    else c.Line(V(x - w * 0.6f, y - 4f), V(x + w * 0.6f, y - 4f), 0.4f, new Color(0.7f, 0.85f, 0.9f, 0.6f));
                }
            }
        }

        // =====================================================================
        // Supplies
        // =====================================================================

        public static void PaintItem(IconCanvas c, ItemDef d)
        {
            var t = d.Tint;
            switch (d.id)
            {
                case "bread": Bread(c, t); break;
                case "salt_pork": Ham(c, t); break;
                case "apple": Apple(c, t); break;
                case "cheese": Cheese(c, t); break;
                case "hardtack": Biscuit(c, t); break;
                case "cave_mushroom": Mushroom(c, t); break;
                case "water_flask": Bottle(c, BottleShape.Flask, t, 0.7f, Leather, Color.clear); break;
                case "small_beer": Tankard(c, t); break;
                case "hot_tea": TeaCup(c, t); break;
                case "snowmelt": Bottle(c, BottleShape.Jar, t, 0.75f, Steel, Color.clear); Snowflake(c, V(50, 40), 9f); break;
                case "bandage": Bandage(c, t); break;
                case "physic": Bottle(c, BottleShape.Tall, t, 0.65f, Wood, Linen); break;
                case "laudanum": Bottle(c, BottleShape.Small, new Color(0.55f, 0.25f, 0.1f), 0.8f, new Color(0.7f, 0.1f, 0.1f), t); break;
                case "smelling_salts": Salts(c, t); break;
                case "powder_shot": Cartridge(c); break;
                default:
                    string id = d.id ?? "";
                    if (id.Contains("recipe") || id.Contains("blueprint") || id.Contains("scroll")) Scroll(c, t);
                    else switch (d.Kind)
                        {
                            case ItemKind.Food: Bread(c, t); break;
                            case ItemKind.Drink: Bottle(c, BottleShape.Flask, t, 0.7f, Leather, Color.clear); break;
                            case ItemKind.Medicine: Bottle(c, BottleShape.Tall, t, 0.65f, Wood, Linen); break;
                            case ItemKind.Tonic: Bottle(c, BottleShape.Small, t, 0.8f, Wood, Linen); break;
                            case ItemKind.Ammo: Cartridge(c); break;
                            case ItemKind.Meal: Bowl(c, t); break;
                            default: Sack(c, t); break;
                        }
                    break;
            }
        }

        static void Bread(IconCanvas c, Color crust)
        {
            c.Ellipse(V(50, 42), 38f, 22f, IconCanvas.Mul(crust, 0.75f), 0.15f, -8f);
            c.Ellipse(V(50, 46), 35f, 18f, crust, 0.25f, -8f, p => 0.9f + 0.15f * IconCanvas.Noise(p * 0.5f));
            for (int i = 0; i < 4; i++)
            {
                var a = V(30 + i * 13f, 40 + i * -1.8f);
                c.Capsule(a, a + V(7, 13), 2.2f, IconCanvas.Shine(crust, 0.45f));
            }
            for (int i = 0; i < 14; i++) c.Circle(V(26 + IconCanvas.Hash(i, 1f) * 50f, 44 + IconCanvas.Hash(i, 2f) * 14f), 0.7f, new Color(1f, 0.98f, 0.92f, 0.8f));
        }

        static void Ham(IconCanvas c, Color meat)
        {
            c.Capsule(V(78, 58), V(88, 64), 4f, Bone, 0.3f);
            c.Circle(V(90, 65), 4.2f, Bone, 0.3f);
            c.Ellipse(V(44, 44), 34f, 26f, new Color(0.95f, 0.88f, 0.75f), 0.1f, 15f);
            c.Ellipse(V(42, 42), 31f, 23f, IconCanvas.Mul(meat, 0.9f), 0.25f, 15f, p => 0.85f + 0.25f * IconCanvas.Noise(p * 0.3f));
            c.Ellipse(V(70, 52), 11f, 20f, new Color(0.96f, 0.9f, 0.8f), 0f, 15f);
            c.Ellipse(V(70, 52), 8.5f, 17f, new Color(0.85f, 0.45f, 0.45f), 0.2f, 15f, p => IconCanvas.Noise(p * 0.6f) > 0.65f ? 1.25f : 1f);
            c.Circle(V(72, 54), 3.4f, Bone, 0.4f);
            for (int i = 0; i < 18; i++) c.Circle(V(16 + IconCanvas.Hash(i, 4f) * 50f, 28 + IconCanvas.Hash(i, 5f) * 30f), 0.8f, new Color(1f, 1f, 1f, 0.85f));
        }

        static void Apple(IconCanvas c, Color skin)
        {
            c.Capsule(V(50, 66), V(54, 82), 1.6f, new Color(0.4f, 0.25f, 0.12f));
            c.Ellipse(V(64, 80), 9f, 4.5f, new Color(0.3f, 0.6f, 0.2f), 0.3f, 25f);
            c.Line(V(56, 78), V(70, 83), 0.4f, new Color(0.2f, 0.4f, 0.12f));
            c.Circle(V(42, 44), 24f, skin, 0.7f, p => 0.9f + 0.18f * IconCanvas.Noise(new Vector2(p.x * 0.8f, p.y * 0.15f)));
            c.Circle(V(58, 44), 24f, IconCanvas.Mul(skin, 0.95f), 0.7f, p => 0.9f + 0.18f * IconCanvas.Noise(new Vector2(p.x * 0.8f, p.y * 0.15f)));
            c.Ellipse(V(50, 66), 6f, 2.5f, IconCanvas.Mul(skin, 0.5f));
            c.Glint(V(38, 56), 4f);
        }

        static void Cheese(IconCanvas c, Color cheese)
        {
            var rind = Color.Lerp(cheese, new Color(0.8f, 0.45f, 0.1f), 0.4f);
            c.Ellipse(V(42, 34), 32f, 10f, IconCanvas.Mul(rind, 0.8f));
            c.Poly(new[] { V(10, 34), V(74, 34), V(74, 54), V(10, 54) }, rind, 0.1f);
            c.Ellipse(V(42, 54), 32f, 10f, cheese, 0.2f);
            c.Line(V(42, 54), V(70, 58), 0.5f, IconCanvas.Mul(cheese, 0.7f));
            c.Line(V(42, 54), V(72, 50), 0.5f, IconCanvas.Mul(cheese, 0.7f));
            var wedgeTop = new[] { V(56, 34), V(92, 30), V(78, 18) };
            c.Poly(new[] { V(56, 34), V(78, 18), V(78, 8), V(56, 22) }, IconCanvas.Mul(cheese, 0.85f));
            c.Poly(new[] { V(78, 18), V(92, 30), V(92, 20), V(78, 8) }, rind);
            c.Poly(wedgeTop, IconCanvas.Shine(cheese, 0.15f));
            c.Circle(V(66, 27), 2f, IconCanvas.Mul(cheese, 0.7f));
            c.Circle(V(78, 26), 1.6f, IconCanvas.Mul(cheese, 0.7f));
            c.Circle(V(66, 16), 1.8f, IconCanvas.Mul(cheese, 0.65f));
            c.Circle(V(26, 56), 2.2f, IconCanvas.Mul(cheese, 0.75f));
            c.Circle(V(50, 50), 1.7f, IconCanvas.Mul(cheese, 0.75f));
        }

        static void Biscuit(IconCanvas c, Color dough)
        {
            var sq = new[] { V(22, 20), V(80, 16), V(84, 74), V(26, 80) };
            c.Poly(new[] { V(22, 16), V(80, 12), V(84, 70), V(26, 76) }, IconCanvas.Mul(dough, 0.7f));
            c.Poly(sq, dough, 0.1f, p => 0.85f + 0.2f * IconCanvas.Noise(p * 0.6f));
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                {
                    var p = Vector2.Lerp(Vector2.Lerp(sq[0], sq[1], 0.2f + x * 0.2f), Vector2.Lerp(sq[3], sq[2], 0.2f + x * 0.2f), 0.2f + y * 0.2f);
                    c.Circle(p, 1.5f, IconCanvas.Mul(dough, 0.55f));
                }
        }

        static void Mushroom(IconCanvas c, Color cap)
        {
            var stem = new Color(0.9f, 0.86f, 0.78f);
            c.Capsule(V(70, 14), V(70, 34), 4f, stem, 0.1f, 3.4f);
            c.Poly(Concat(Arc(V(70, 34), 15f, 12f, 0f, 180f, 14)), IconCanvas.Mul(cap, 0.9f), 0.3f);
            c.Capsule(V(42, 12), V(44, 46), 7.5f, stem, 0.1f, 6f);
            c.Ellipse(V(44, 48), 25f, 5f, IconCanvas.Mul(cap, 0.45f));
            c.Poly(Concat(Arc(V(44, 48), 26f, 26f, 0f, 180f, 20)), cap, 0.4f);
            for (int i = 0; i < 6; i++) c.Circle(V(28 + IconCanvas.Hash(i, 8f) * 32f, 56 + IconCanvas.Hash(i, 9f) * 14f), 1.6f + IconCanvas.Hash(i, 3f) * 1.4f, new Color(0.95f, 0.9f, 1f));
            c.Glow(V(44, 60), 34f, new Color(0.7f, 0.5f, 1f, 0.25f));
        }

        enum BottleShape { Flask, Tall, Small, Jar }

        static void Bottle(IconCanvas c, BottleShape shape, Color liquid, float fill, Color cork, Color label)
        {
            IconCanvas.Sdf body; Vector2 min, max; Vector2 neckA, neckB; float neckR;
            switch (shape)
            {
                case BottleShape.Flask:
                    body = IconCanvas.SdCircle(V(50, 40), 26f); min = V(24, 14); max = V(76, 66); neckA = V(50, 62); neckB = V(50, 76); neckR = 6f; break;
                case BottleShape.Tall:
                    body = IconCanvas.SdPoly(new[] { V(32, 10), V(68, 10), V(68, 56), V(58, 64), V(42, 64), V(32, 56) }); min = V(32, 10); max = V(68, 64); neckA = V(50, 62); neckB = V(50, 78); neckR = 5f; break;
                case BottleShape.Small:
                    body = IconCanvas.SdPoly(new[] { V(36, 12), V(64, 12), V(64, 48), V(56, 56), V(44, 56), V(36, 48) }); min = V(36, 12); max = V(64, 56); neckA = V(50, 54); neckB = V(50, 68); neckR = 4f; break;
                default:
                    body = IconCanvas.SdPoly(new[] { V(26, 12), V(74, 12), V(76, 62), V(24, 62) }); min = V(24, 12); max = V(76, 62); neckA = V(50, 60); neckB = V(50, 66); neckR = 22f; break;
            }
            // Glass, the liquid inside it, a bright reflection, the cork and a paper label.
            c.Field(min, max, body, new Color(0.75f, 0.85f, 0.9f, 0.45f));
            float fillY = Mathf.Lerp(min.y, max.y, fill);
            var liquidSdf = IconCanvas.Intersect(p => body(p) + 2.2f, p => p.y - fillY);
            c.Field(min, max, liquidSdf, IconCanvas.A(liquid, 0.9f));
            c.Field(min, max, IconCanvas.Intersect(p => body(p) + 2.2f, p => Mathf.Abs(p.y - fillY + 1f) - 1f), IconCanvas.A(IconCanvas.Shine(liquid, 0.4f), 0.9f));
            c.Capsule(V(min.x + 6f, min.y + 8f), V(min.x + 6f, Mathf.Min(max.y - 8f, min.y + 34f)), 1.8f, new Color(1f, 1f, 1f, 0.55f));
            if (shape == BottleShape.Jar)
            {
                c.Poly(new[] { V(22, 62), V(78, 62), V(78, 72), V(22, 72) }, cork, 0.6f);
                c.Line(V(24, 67), V(76, 67), 0.5f, IconCanvas.Mul(cork, 0.6f));
            }
            else
            {
                c.Capsule(neckA, neckB, neckR, new Color(0.75f, 0.85f, 0.9f, 0.55f));
                c.Capsule(neckB - V(0, 2), neckB + V(0, 6), neckR * 0.9f, cork, 0.2f, neckR * 1.05f);
                if (shape == BottleShape.Flask)
                {
                    c.Stroke(new[] { V(24, 52), V(16, 66), V(26, 84), V(50, 88), V(74, 84), V(84, 66), V(76, 52) }, 1.3f, Leather);
                }
            }
            if (label.a > 0f)
            {
                float ly = Mathf.Lerp(min.y, max.y, 0.32f);
                var lab = new[] { V(min.x + 5, ly - 8), V(max.x - 5, ly - 8), V(max.x - 5, ly + 8), V(min.x + 5, ly + 8) };
                c.Poly(lab, label, 0f, p => 0.92f + 0.1f * IconCanvas.Noise(p * 0.7f));
                c.Line(V(min.x + 9, ly + 3), V(max.x - 9, ly + 3), 0.6f, IconCanvas.Mul(label, 0.35f));
                c.Line(V(min.x + 9, ly - 2), V(max.x - 13, ly - 2), 0.5f, IconCanvas.Mul(label, 0.5f));
            }
        }

        static void Tankard(IconCanvas c, Color beer)
        {
            var pewter = new Color(0.62f, 0.64f, 0.66f);
            c.Ring(V(70, 44), 12f, 3f, pewter, 0.6f);
            c.Poly(new[] { V(26, 10), V(70, 10), V(66, 70), V(30, 70) }, pewter, 0.6f);
            c.Capsule(V(26, 14), V(70, 14), 2.4f, IconCanvas.Mul(pewter, 0.9f), 0.5f);
            c.Capsule(V(30, 64), V(66, 64), 2f, IconCanvas.Mul(pewter, 0.9f), 0.5f);
            c.Ellipse(V(48, 70), 18f, 4f, IconCanvas.Mul(beer, 0.9f));
            var foam = new Color(0.98f, 0.95f, 0.85f);
            for (int i = 0; i < 6; i++) c.Circle(V(30 + i * 7f, 72 + 3f * IconCanvas.Hash(i, 2f)), 6f + 2f * IconCanvas.Hash(i, 5f), foam, 0.2f);
            c.Capsule(V(31, 66), V(28, 52), 2.4f, foam, 0.1f, 1.6f);
            c.Glint(V(36, 40), 4f);
        }

        static void TeaCup(IconCanvas c, Color tea)
        {
            var china = new Color(0.96f, 0.95f, 0.92f);
            c.Ellipse(V(50, 20), 38f, 9f, IconCanvas.Mul(china, 0.92f), 0.4f);
            c.Ring(V(76, 40), 9f, 2.6f, china, 0.5f);
            c.Poly(Concat(new[] { V(20, 52), V(80, 52) }, Arc(V(50, 52), 30f, 30f, 0f, -180f, 18)), china, 0.5f);
            c.Capsule(V(24, 44), V(76, 44), 1.4f, new Color(0.25f, 0.4f, 0.75f));
            for (int i = 0; i < 5; i++) c.Circle(V(32 + i * 9f, 37), 1.4f, new Color(0.25f, 0.4f, 0.75f));
            c.Ellipse(V(50, 52), 30f, 6f, china);
            c.Ellipse(V(50, 52), 27f, 4.6f, tea, 0.2f);
            for (int i = 0; i < 3; i++)
            {
                float x = 38 + i * 12f;
                c.Stroke(new[] { V(x, 62), V(x + 4, 70), V(x - 2, 78), V(x + 3, 88) }, 1.4f, new Color(1f, 1f, 1f, 0.55f));
            }
        }

        static void Snowflake(IconCanvas c, Vector2 at, float r)
        {
            var w = new Color(1f, 1f, 1f, 0.95f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                var d = V(Mathf.Cos(a), Mathf.Sin(a)) * r;
                c.Line(at - d, at + d, 0.7f, w);
            }
        }

        static void Bandage(IconCanvas c, Color linen)
        {
            c.Poly(new[] { V(52, 30), V(62, 22), V(76, 26), V(90, 14), V(94, 20), V(80, 34), V(66, 32), V(58, 38) }, IconCanvas.Mul(linen, 0.95f), 0f, p => 0.9f + 0.1f * Mathf.Abs(Mathf.Sin(p.x * 2f)));
            c.Poly(new[] { V(18, 34), V(58, 34), V(58, 74), V(18, 74) }, linen, 0.1f, p => 0.9f + 0.08f * Mathf.Abs(Mathf.Sin(p.y * 2.2f)));
            c.Ellipse(V(58, 54), 9f, 20f, IconCanvas.Mul(linen, 0.95f), 0f);
            for (int i = 0; i < 4; i++) c.Ring(V(58, 54), 2f + i * 4.4f, 0.5f, IconCanvas.Mul(linen, 0.7f));
            c.Ellipse(V(18, 54), 9f, 20f, IconCanvas.Mul(linen, 0.8f), 0f);
            c.Poly(new[] { V(30, 50), V(46, 50), V(46, 58), V(30, 58) }, new Color(0.75f, 0.12f, 0.1f));
            c.Poly(new[] { V(34, 46), V(42, 46), V(42, 62), V(34, 62) }, new Color(0.75f, 0.12f, 0.1f));
        }

        static void Salts(IconCanvas c, Color crystals)
        {
            var body = IconCanvas.SdPoly(new[] { V(30, 12), V(70, 12), V(72, 58), V(28, 58) });
            c.Glow(V(50, 40), 36f, IconCanvas.A(crystals, 0.35f));
            c.Field(V(28, 12), V(72, 58), body, new Color(0.75f, 0.85f, 0.9f, 0.45f));
            for (int i = 0; i < 26; i++)
            {
                var p = V(34 + IconCanvas.Hash(i, 1f) * 32f, 16 + IconCanvas.Hash(i, 2f) * 28f);
                c.Poly(new[] { p + V(0, 3), p + V(2.6f, 0), p + V(0, -3), p + V(-2.6f, 0) }, IconCanvas.Mul(crystals, 0.8f + 0.4f * IconCanvas.Hash(i, 3f)), 0.6f);
            }
            c.Capsule(V(34, 18), V(34, 50), 1.6f, new Color(1f, 1f, 1f, 0.5f));
            c.Poly(new[] { V(26, 58), V(74, 58), V(74, 70), V(26, 70) }, Steel, 0.8f);
            for (int i = 0; i < 8; i++) c.Line(V(30 + i * 6f, 59), V(30 + i * 6f, 69), 0.4f, DarkSteel);
            c.Circle(V(50, 74), 4f, Steel, 0.8f);
        }

        static void Cartridge(IconCanvas c)
        {
            var paper = new Color(0.9f, 0.85f, 0.7f);
            var f = new Frame(V(16, 40), V(74, 70));
            c.Capsule(f.P(0f, 0f), f.P(0.85f, 0f), 8f, paper, 0.1f, -1f, p => 0.9f + 0.12f * Mathf.Abs(Mathf.Sin((p.x - p.y) * 0.9f)));
            c.Poly(new[] { f.P(0.84f, -6f), f.P(1f, -2f), f.P(1.05f, 2f), f.P(0.84f, 6f) }, IconCanvas.Mul(paper, 0.9f));
            c.Capsule(f.P(0.82f, -7.5f), f.P(0.82f, 7.5f), 1.1f, new Color(0.6f, 0.45f, 0.3f));
            c.Circle(f.P(0f, 0f), 7.4f, new Color(0.45f, 0.47f, 0.5f), 0.6f);
            c.Circle(V(72, 28), 9f, new Color(0.42f, 0.44f, 0.48f), 0.7f);
            c.Circle(V(54, 18), 6f, new Color(0.42f, 0.44f, 0.48f), 0.7f);
            for (int i = 0; i < 30; i++) c.Circle(V(20 + IconCanvas.Hash(i, 6f) * 40f, 14 + IconCanvas.Hash(i, 7f) * 16f), 0.6f + IconCanvas.Hash(i, 8f) * 0.6f, new Color(0.08f, 0.08f, 0.08f));
        }

        static void Scroll(IconCanvas c, Color tint)
        {
            var parchment = new Color(0.92f, 0.85f, 0.66f);
            c.Poly(new[] { V(22, 24), V(78, 24), V(78, 80), V(22, 80) }, parchment, 0f, p => 0.88f + 0.14f * IconCanvas.Noise(p * 0.3f));
            c.Capsule(V(18, 20), V(82, 20), 5f, IconCanvas.Mul(parchment, 0.9f), 0.2f);
            c.Capsule(V(18, 82), V(82, 82), 5f, IconCanvas.Mul(parchment, 0.9f), 0.2f);
            for (int i = 0; i < 6; i++) c.Line(V(30, 70 - i * 7f), V(i % 3 == 2 ? 56 : 70, 70 - i * 7f), 0.6f, new Color(0.35f, 0.25f, 0.15f));
            c.Circle(V(64, 34), 7f, tint.a > 0f ? tint : new Color(0.7f, 0.1f, 0.1f), 0.4f);
        }

        static void Bowl(IconCanvas c, Color food)
        {
            var wood = new Color(0.55f, 0.36f, 0.2f);
            for (int i = 0; i < 3; i++)
            {
                float x = 38 + i * 12f;
                c.Stroke(new[] { V(x, 60), V(x + 4, 70), V(x - 2, 78), V(x + 3, 88) }, 1.4f, new Color(1f, 1f, 1f, 0.5f));
            }
            c.Poly(Concat(new[] { V(14, 52), V(86, 52) }, Arc(V(50, 52), 36f, 32f, 0f, -180f, 20)), wood, 0.2f, p => 0.85f + 0.2f * IconCanvas.Noise(new Vector2(p.x * 0.2f, p.y * 1.5f)));
            c.Ellipse(V(50, 52), 36f, 8f, IconCanvas.Mul(wood, 1.15f));
            c.Ellipse(V(50, 53), 32f, 6f, food, 0.3f, 0f, p => 0.85f + 0.3f * IconCanvas.Noise(p * 0.5f));
            for (int i = 0; i < 5; i++) c.Circle(V(30 + i * 10f, 53 + 2f * IconCanvas.Hash(i, 4f)), 2.2f, IconCanvas.Mul(food, 1.3f), 0.4f);
            c.Capsule(V(66, 54), V(86, 84), 2.2f, IconCanvas.Mul(wood, 1.2f), 0.3f);
        }

        static void Sack(IconCanvas c, Color tint)
        {
            var burlap = Color.Lerp(new Color(0.7f, 0.58f, 0.38f), tint, 0.3f);
            c.Poly(new[] { V(30, 70), V(70, 70), V(84, 40), V(76, 12), V(24, 12), V(16, 40) }, burlap, 0f, p => 0.85f + 0.2f * Mathf.Abs(Mathf.Sin(p.x * 2f) * Mathf.Sin(p.y * 2f)));
            c.Poly(new[] { V(38, 70), V(62, 70), V(70, 86), V(30, 86) }, IconCanvas.Mul(burlap, 0.95f));
            c.Capsule(V(34, 71), V(66, 71), 2f, new Color(0.5f, 0.35f, 0.2f));
        }
    }
}
