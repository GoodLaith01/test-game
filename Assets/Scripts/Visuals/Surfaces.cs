using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hollowmark.Visuals
{
    /// <summary>What a surface is made of. Each one has a tileable texture, a bump map and its own shine.</summary>
    public enum Surface
    {
        Auto, None, Plain, Grass, Dirt, Cobble, Flagstone, Brick, Stone, Wood, Bark, Plaster, Thatch,
        Linen, Wool, Leather, Metal, Gold, Skin, Scales, Carapace, Hair, Foliage, Wet, Canvas,
    }

    /// <summary>
    /// Textured, bump-mapped materials for every surface in the game. The textures live in Resources/Textures as
    /// .bytes PNGs (made by Tools/make_textures.py): RGB is neutral detail that is tinted with each object's colour,
    /// and alpha is a height map that is turned into a normal map here. If the files are missing, plain colours are used.
    /// </summary>
    public static class Surfaces
    {
        class Spec
        {
            public string File;
            public float Meters, Smooth, Metal, Bump;
            public Spec(string file, float meters, float smooth, float metal, float bump) { File = file; Meters = meters; Smooth = smooth; Metal = metal; Bump = bump; }
        }

        static readonly Dictionary<Surface, Spec> specs = new Dictionary<Surface, Spec>
        {
            { Surface.Plain,     new Spec("plain",     2.0f, 0.18f, 0f,   0.3f) },
            { Surface.Grass,     new Spec("grass",     3.5f, 0.08f, 0f,   0.8f) },
            { Surface.Dirt,      new Spec("dirt",      3.0f, 0.06f, 0f,   1.0f) },
            { Surface.Cobble,    new Spec("cobble",    2.5f, 0.22f, 0f,   1.4f) },
            { Surface.Flagstone, new Spec("flagstone", 4.0f, 0.25f, 0f,   1.3f) },
            { Surface.Brick,     new Spec("brick",     2.6f, 0.15f, 0f,   1.5f) },
            { Surface.Stone,     new Spec("stone",     2.5f, 0.18f, 0f,   1.2f) },
            { Surface.Wood,      new Spec("wood",      1.6f, 0.22f, 0f,   0.9f) },
            { Surface.Bark,      new Spec("bark",      1.2f, 0.08f, 0f,   1.6f) },
            { Surface.Plaster,   new Spec("plaster",   3.0f, 0.1f,  0f,   0.6f) },
            { Surface.Thatch,    new Spec("thatch",    2.0f, 0.05f, 0f,   1.6f) },
            { Surface.Linen,     new Spec("linen",     0.12f, 0.12f, 0f,  0.5f) },
            { Surface.Wool,      new Spec("wool",      0.18f, 0.08f, 0f,  0.6f) },
            { Surface.Canvas,    new Spec("linen",     0.5f, 0.1f,  0f,   0.7f) },
            { Surface.Leather,   new Spec("leather",   0.35f, 0.42f, 0f,  0.7f) },
            { Surface.Metal,     new Spec("metal",     0.8f, 0.7f,  0.85f, 0.4f) },
            { Surface.Gold,      new Spec("metal",     0.6f, 0.78f, 1f,   0.25f) },
            { Surface.Skin,      new Spec("skin",      0.22f, 0.38f, 0f,  0.35f) },
            { Surface.Scales,    new Spec("scales",    0.35f, 0.45f, 0f,  1.4f) },
            { Surface.Carapace,  new Spec("carapace",  0.9f, 0.72f, 0.1f, 1.0f) },
            { Surface.Hair,      new Spec("hair",      0.25f, 0.45f, 0f,  0.8f) },
            { Surface.Foliage,   new Spec("foliage",   1.6f, 0.15f, 0f,   1.2f) },
            { Surface.Wet,       new Spec("plain",     2.0f, 0.92f, 0f,   0.05f) },
        };

        static readonly Dictionary<string, Texture2D> albedo = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Texture2D> normals = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Color> means = new Dictionary<string, Color>();
        static readonly HashSet<string> missing = new HashSet<string>();
        static readonly Dictionary<long, Material> cache = new Dictionary<long, Material>();
        static Texture2D softDot;
        static readonly Dictionary<Material, KeyValuePair<Color, Surface>> made = new Dictionary<Material, KeyValuePair<Color, Surface>>();

        /// <summary>Swaps every material of one surface for another (same colour) on the given renderers, e.g. skin to scales.</summary>
        public static void Swap(IEnumerable<Renderer> renderers, Surface from, Surface to)
        {
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    KeyValuePair<Color, Surface> info;
                    if (mats[i] != null && made.TryGetValue(mats[i], out info) && info.Value == from) { mats[i] = Get(info.Key, to); changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        /// <summary>How many metres one repeat of the texture covers (UVs are generated in metres).</summary>
        public static float Meters(Surface s) { Spec sp; return specs.TryGetValue(s, out sp) ? sp.Meters : 1f; }

        static bool Load(string file)
        {
            if (albedo.ContainsKey(file)) return true;
            if (missing.Contains(file)) return false;
            var asset = Resources.Load<TextAsset>("Textures/" + file);
            if (asset == null) { missing.Add(file); return false; }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!tex.LoadImage(asset.bytes, false)) { missing.Add(file); return false; }
            tex.name = "Tex_" + file;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 8;

            // Height (alpha) -> tangent-space normal map, in a linear texture.
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            var nm = new Texture2D(w, h, TextureFormat.RGBA32, true, true) { name = "Nrm_" + file, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            var outPx = new Color32[px.Length];
            double r = 0, g = 0, b = 0;
            for (int y = 0; y < h; y++)
            {
                int yu = ((y + 1) % h) * w, yd = ((y - 1 + h) % h) * w, row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int xr = (x + 1) % w, xl = (x - 1 + w) % w;
                    float dx = (px[row + xr].a - px[row + xl].a) / 255f;
                    float dy = (px[yu + x].a - px[yd + x].a) / 255f;
                    var n = new Vector3(-dx * 4f, -dy * 4f, 1f).normalized;
                    outPx[row + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f), (byte)((n.y * 0.5f + 0.5f) * 255f), (byte)((n.z * 0.5f + 0.5f) * 255f), 255);
                    var c = px[row + x];
                    r += c.r; g += c.g; b += c.b;
                }
            }
            nm.SetPixels32(outPx);
            nm.Apply(true, true);

            // Albedo alpha back to opaque (the height was only needed for the normal map).
            for (int i = 0; i < px.Length; i++) px[i].a = 255;
            tex.SetPixels32(px);
            tex.Apply(true, true);

            double count = px.Length * 255.0;
            means[file] = new Color((float)(r / count), (float)(g / count), (float)(b / count));
            albedo[file] = tex;
            normals[file] = nm;
            return true;
        }

        /// <summary>A textured material of the given colour. The texture's average brightness is compensated so the colour stays true.</summary>
        public static Material Get(Color c, Surface s)
        {
            if (s == Surface.Auto || s == Surface.None) s = Surface.Plain;
            long key = ((long)c.GetHashCode() << 8) ^ (long)s;
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            var spec = specs[s];
            m = new Material(Prim.Lit) { name = s + "_" + ColorUtility.ToHtmlStringRGB(c) };
            var tint = c;
            if (Load(spec.File))
            {
                var mean = means[spec.File];
                tint = new Color(c.r / Mathf.Max(0.3f, mean.r), c.g / Mathf.Max(0.3f, mean.g), c.b / Mathf.Max(0.3f, mean.b), c.a);
                var scale = Vector2.one / spec.Meters;
                foreach (var prop in new[] { "_MainTex", "_BaseMap" })
                    if (m.HasProperty(prop)) { m.SetTexture(prop, albedo[spec.File]); m.SetTextureScale(prop, scale); }
                if (m.HasProperty("_BumpMap"))
                {
                    m.SetTexture("_BumpMap", normals[spec.File]);
                    m.SetTextureScale("_BumpMap", scale);
                    if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", spec.Bump);
                    m.EnableKeyword("_NORMALMAP");
                }
            }
            Prim.SetColor(m, tint);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", spec.Smooth);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", spec.Smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", spec.Metal);
            cache[key] = m;
            made[m] = new KeyValuePair<Color, Surface>(c, s);
            return m;
        }

        /// <summary>
        /// Picks a surface for an object from its name and colour, so everything built with Prim gets a fitting
        /// texture without each builder having to say so (new buildings included).
        /// </summary>
        public static Surface Guess(string name, Color c)
        {
            string n = (name ?? "").ToLowerInvariant();
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            bool green = h > 0.17f && h < 0.45f && s > 0.2f;

            if (n.Contains("puff") || n.Contains("smoke") || n.Contains("label")) return Surface.Plain;
            if (n.Contains("ground") || n.Contains("lawn")) return green ? Surface.Grass : Surface.Dirt;
            if (n.Contains("road") || n.Contains("path") || n.Contains("street")) return Surface.Cobble;
            if (n.Contains("sand") || n.Contains("dirt")) return Surface.Dirt;
            if (n.Contains("floorboard") || n.Contains("plank") || n.Contains("deck")) return Surface.Wood;
            if (n.Contains("floor")) return Surface.Flagstone;
            if (n.Contains("wall") || n.Contains("lintel") || n.Contains("pillar") || n.Contains("ring") || n.Contains("chimney")) return s < 0.25f ? Surface.Brick : (v > 0.7f ? Surface.Plaster : Surface.Brick);
            if (n.Contains("rubble") || n.Contains("rock") || n.Contains("boulder")) return Surface.Stone;
            if (n.Contains("roof") || n.Contains("thatch")) return h > 0.03f && h < 0.17f && s > 0.25f ? Surface.Thatch : Surface.Wood;
            if (n.Contains("awning") || n.Contains("canvas") || n.Contains("banner") || n.Contains("cloth") || n.Contains("tent")) return Surface.Canvas;
            if (n.Contains("straw") || n.Contains("hay")) return Surface.Thatch;
            if (n.Contains("log") || n.Contains("trunk") || n.Contains("stake")) return Surface.Bark;
            if (n.Contains("blade") || n.Contains("barrel") || n.Contains("bar") || n.Contains("plate") || n.Contains("lamp") || n.Contains("iron") || n.Contains("anvil")) return Surface.Metal;
            if (n.Contains("guard") || n.Contains("trophy") || n.Contains("coin") || n.Contains("buckle")) return Surface.Gold;
            if (n.Contains("leaf") || n.Contains("foliage") || n.Contains("bush") || n.Contains("hedge")) return Surface.Foliage;
            if (n.Contains("water")) return Surface.Wet;
            if (n.Contains("mattress") || n.Contains("pillow") || n.Contains("bedroll")) return Surface.Linen;
            if (n.Contains("crate") || n.Contains("frame") || n.Contains("grip") || n.Contains("stock") || n.Contains("shaft") || n.Contains("counter")
                || n.Contains("shelf") || n.Contains("table") || n.Contains("door") || n.Contains("post") || n.Contains("beam") || n.Contains("sign")) return Surface.Wood;

            // By colour: greys are stone, browns wood, creams plaster, greens grass, bright colours painted cloth.
            if (s < 0.12f) return v > 0.8f ? Surface.Plaster : Surface.Stone;
            if (green) return v < 0.4f ? Surface.Foliage : Surface.Grass;
            if (h < 0.13f || h > 0.95f)
            {
                if (s > 0.55f && v > 0.45f) return Surface.Canvas;
                if (v > 0.7f && s < 0.3f) return Surface.Plaster;
                if (v < 0.55f) return Surface.Wood;
                return s < 0.5f ? Surface.Thatch : Surface.Wood;
            }
            if (h < 0.17f) return v > 0.6f ? Surface.Thatch : Surface.Wood;
            return s > 0.35f ? Surface.Canvas : Surface.Stone;
        }

        /// <summary>The surface for a piece of clothing, from what it is called.</summary>
        public static Surface ForGarment(string name, Color c)
        {
            string n = (name ?? "").ToLowerInvariant();
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            switch (name)
            {
                case "Thigh": case "Shin": case "Foot": case "Neck": case "UpperArm": case "Forearm": case "Hand": case "Head":
                case "Trunk": case "Pelvis": case "Chest": case "Nose": case "HookNose": case "Ear": case "Snout": case "Tail": case "Wing": case "Skin":
                    return Surface.Skin;
                case "Horn": case "Tusk": case "Mandible": case "Carapace": case "Band": case "LegUpper": case "LegLower": case "Skull": case "Claw":
                    return Surface.Carapace;
            }
            if (n.Contains("hair") || n.Contains("queue") || n.Contains("bun") || n.Contains("brow") || n.Contains("beard")) return Surface.Hair;
            if (n.Contains("shoe") || n.Contains("boot") || n.Contains("glove") || n.Contains("chaps") || n.Contains("belt") || n.Contains("loincloth") || n.Contains("strap")) return Surface.Leather;
            if (n.Contains("shirt") || n.Contains("stocking") || n.Contains("underwear") || n.Contains("cravat") || n.Contains("apron") || n.Contains("cap") && v > 0.8f) return Surface.Linen;
            if (n.Contains("carapace")) return Surface.Carapace;
            if (n.Contains("hide")) return Surface.Scales;
            if (n.Contains("gauntlet") || n.Contains("chain")) return Surface.Metal;
            if (n.Contains("helm") || n.Contains("plate") || n.Contains("armor") || n.Contains("crown") || n.Contains("pauldron"))
                return s < 0.3f && v > 0.35f ? Surface.Metal : (h > 0.1f && h < 0.17f && s > 0.4f ? Surface.Gold : Surface.Leather);
            if (n.Contains("button") || n.Contains("buckle")) return Surface.Gold;
            return Surface.Wool;
        }

        /// <summary>A soft round dot for particles (rain, snow, dust), so they are not drawn as squares.</summary>
        public static Texture2D SoftDot
        {
            get
            {
                if (softDot != null) return softDot;
                const int n = 64;
                softDot = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "SoftDot", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                    }
                softDot.SetPixels32(px);
                softDot.Apply(true, true);
                return softDot;
            }
        }

        static Material grassCard;

        /// <summary>A two-sided alpha-cut material with grass blades, for ground tufts. Null if it cannot be made.</summary>
        public static Material GrassCard(Color tint)
        {
            if (grassCard != null) return grassCard;
            var asset = Resources.Load<TextAsset>("Textures/grass_card");
            if (asset == null) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!tex.LoadImage(asset.bytes, true)) return null;
            tex.wrapMode = TextureWrapMode.Clamp;
            var m = new Material(Prim.Lit) { name = "GrassCard" };
            foreach (var prop in new[] { "_MainTex", "_BaseMap" }) if (m.HasProperty(prop)) m.SetTexture(prop, tex);
            Prim.SetColor(m, tint);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.05f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 1f);            // Standard: Cutout
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);  // URP: alpha clipping
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);            // URP: both faces
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            m.SetOverrideTag("RenderType", "TransparentCutout");
            grassCard = m;
            return m;
        }

        // ---------- UVs in metres ----------

        static readonly Dictionary<long, Mesh> scaledMeshes = new Dictionary<long, Mesh>();

        /// <summary>
        /// A copy of a mesh whose UVs are a box projection in metres for the given scale, so a texture keeps the same
        /// size on a 1 m crate and a 130 m field. Copies are shared between objects of the same mesh and size.
        /// </summary>
        public static Mesh MetricCopy(Mesh source, Vector3 scale)
        {
            if (source == null) return null;
            int qx = Mathf.RoundToInt(scale.x * 10f), qy = Mathf.RoundToInt(scale.y * 10f), qz = Mathf.RoundToInt(scale.z * 10f);
            long key = ((long)source.GetInstanceID() * 73856093L) ^ ((long)qx * 19349663L) ^ ((long)qy * 83492791L) ^ ((long)qz * 2654435761L);
            Mesh m;
            if (scaledMeshes.TryGetValue(key, out m) && m != null) return m;
            if (scaledMeshes.Count > 4000) return source; // a safety cap; the default UVs are still fine
            m = Object.Instantiate(source);
            m.name = source.name + "_m";
            m.uv = BoxUV(m.vertices, m.normals, new Vector3(qx, qy, qz) / 10f);
            scaledMeshes[key] = m;
            return m;
        }

        /// <summary>Box-projected UVs (in metres, after scaling) from positions and normals.</summary>
        public static Vector2[] BoxUV(Vector3[] v, Vector3[] n, Vector3 scale)
        {
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var p = Vector3.Scale(v[i], scale);
                var nn = i < n.Length ? Vector3.Scale(n[i], new Vector3(1f / Mathf.Max(1e-4f, scale.x), 1f / Mathf.Max(1e-4f, scale.y), 1f / Mathf.Max(1e-4f, scale.z))) : Vector3.up;
                float ax = Mathf.Abs(nn.x), ay = Mathf.Abs(nn.y), az = Mathf.Abs(nn.z);
                if (ax >= ay && ax >= az) uv[i] = new Vector2(p.z * Mathf.Sign(nn.x), p.y);
                else if (ay >= az) uv[i] = new Vector2(p.x, p.z * Mathf.Sign(nn.y));
                else uv[i] = new Vector2(-p.x * Mathf.Sign(nn.z), p.y);
            }
            return uv;
        }

        /// <summary>Gives a generated mesh box-projected UVs (at its own scale) if it has none.</summary>
        public static void EnsureUV(Mesh mesh)
        {
            if (mesh == null || mesh.uv.Length == mesh.vertexCount) return;
            if (mesh.normals.Length != mesh.vertexCount) mesh.RecalculateNormals();
            mesh.uv = BoxUV(mesh.vertices, mesh.normals, Vector3.one);
        }
    }
}
