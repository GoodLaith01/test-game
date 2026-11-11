using System.Collections.Generic;
using Hollowmark.Data;
using Hollowmark.Visuals;
using UnityEngine;

namespace Hollowmark.World
{
    /// <summary>
    /// Decoration added after a world is built: a forest, rolling hills, rocks and grass around the village,
    /// puddles, bones and chains in the dungeons, and living flames on every torch, lamp and campfire.
    /// Nothing here has gameplay colliders, so it can never block a path.
    /// </summary>
    public static class Scenery
    {
        static readonly Color Leaf = new Color(0.25f, 0.38f, 0.17f);
        static readonly Color Pine = new Color(0.15f, 0.27f, 0.17f);
        static readonly Color BarkColor = new Color(0.32f, 0.24f, 0.17f);
        static readonly Color Rock = new Color(0.5f, 0.5f, 0.47f);

        public static void Village(Transform root)
        {
            var oldState = Random.state;
            Random.InitState(1771);
            var deco = new GameObject("Scenery").transform;
            deco.SetParent(root, false);

            // Fields beyond the village and hills on the horizon, so the world does not end at the palisade.
            Prim.Box(deco, new Vector3(0f, -0.52f, 0f), new Vector3(420f, 1f, 420f), new Color(0.3f, 0.42f, 0.22f), false, "OuterGround");
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f + Random.Range(-0.1f, 0.1f);
                float r = Random.Range(150f, 185f);
                var hill = Prim.Make(PrimitiveType.Sphere, deco, new Vector3(Mathf.Cos(a) * r, -6f, Mathf.Sin(a) * r),
                    new Vector3(Random.Range(70f, 110f), Random.Range(22f, 45f), Random.Range(50f, 80f)), Prim.Vary(new Color(0.27f, 0.36f, 0.22f), 0.06f), "HillGround");
                hill.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
                hill.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // A forest outside the palisade.
            for (int i = 0; i < 110; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float r = Random.Range(60f, 120f);
                var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (p.z > 40f && Mathf.Abs(p.x) < 35f && p.z < 75f) continue; // keep the view of the dungeon gates open
                if (Random.value < 0.4f) PineTree(deco, p, Random.Range(7f, 13f));
                else Tree(deco, p, Random.Range(5f, 9f));
            }
            // A few trees inside the walls, away from everything else.
            Physics.SyncTransforms();
            for (int i = 0, placed = 0; i < 60 && placed < 9; i++)
            {
                var p = new Vector3(Random.Range(-50f, 50f), 0f, Random.Range(-50f, 38f));
                if (Mathf.Abs(p.x) < 7f || Mathf.Abs(p.z) < 6f || Blocked(p, 3.5f)) continue;
                Tree(deco, p, Random.Range(4.5f, 7f));
                placed++;
            }

            // Rocks and grass tufts across the green.
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3(Random.Range(-64f, 64f), 0f, Random.Range(-64f, 64f));
                if (Mathf.Abs(p.x) < 5f || Blocked(p, 1.5f)) continue;
                Boulder(deco, p, Random.Range(0.3f, 1.1f));
            }
            GrassTufts(deco, 2600, 64f, true);
            Random.state = oldState;
        }

        public static void Dungeon(Transform root, DungeonDef def)
        {
            var oldState = Random.state;
            var deco = new GameObject("Scenery").transform;
            deco.SetParent(root, false);
            Physics.SyncTransforms();
            foreach (Transform room in root)
            {
                if (!room.name.StartsWith("Room") && room.name != "BossArena") continue;
                float size = def.roomSize * (room.name == "BossArena" ? 1.4f : 1f);
                // Puddles that catch the torchlight.
                for (int i = 0; i < 3; i++)
                {
                    var p = room.position + new Vector3(Random.Range(-size * 0.4f, size * 0.4f), 0.015f, Random.Range(-size * 0.4f, size * 0.4f));
                    var puddle = Prim.Make(PrimitiveType.Cylinder, deco, p, new Vector3(Random.Range(0.8f, 2.4f), 0.005f, Random.Range(0.6f, 1.8f)),
                        Color.Lerp(def.Floor, Color.black, 0.55f), "Water");
                    puddle.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 180f), 0f);
                }
                // Scattered bones.
                for (int i = 0; i < 5; i++)
                {
                    var p = room.position + new Vector3(Random.Range(-size * 0.45f, size * 0.45f), 0.04f, Random.Range(-size * 0.45f, size * 0.45f));
                    var bone = Prim.Make(PrimitiveType.Capsule, deco, p, new Vector3(0.06f, Random.Range(0.12f, 0.25f), 0.06f), new Color(0.82f, 0.78f, 0.66f), "Bone");
                    bone.transform.localRotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
                }
                if (Random.value < 0.5f)
                {
                    var skull = Prim.Make(PrimitiveType.Sphere, deco, room.position + new Vector3(Random.Range(-size * 0.4f, size * 0.4f), 0.1f, Random.Range(-size * 0.4f, size * 0.4f)),
                        new Vector3(0.18f, 0.2f, 0.22f), new Color(0.84f, 0.8f, 0.68f), "SkullProp");
                    skull.transform.localRotation = Random.rotation;
                }
                // Chains hanging from the dark above.
                for (int i = 0; i < 2; i++)
                {
                    var top = room.position + new Vector3(Random.Range(-size * 0.35f, size * 0.35f), def.wallHeight, Random.Range(-size * 0.35f, size * 0.35f));
                    int links = Random.Range(5, 10);
                    for (int l = 0; l < links; l++)
                    {
                        var link = Prim.Make(PrimitiveType.Capsule, deco, top + Vector3.down * (l * 0.17f), new Vector3(0.05f, 0.1f, 0.025f), new Color(0.3f, 0.29f, 0.28f), "IronLink");
                        link.transform.localRotation = Quaternion.Euler(0f, l % 2 == 0 ? 0f : 90f, 0f);
                    }
                }
                // Moss and grit in damp, earthy dungeons.
                float hue, sat, val;
                Color.RGBToHSV(def.Floor, out hue, out sat, out val);
                if (hue > 0.15f && hue < 0.45f && sat > 0.15f) GrassTuftsAt(deco, room.position, size * 0.45f, 50, def.Floor * 1.4f);
            }
            Random.state = oldState;
        }

        /// <summary>Living flames on torches, lamps and campfires, and flickering warm lights.</summary>
        public static void Dress(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name;
                if (n == "Torch")
                    Flame.Create(r.transform.parent, r.transform.localPosition + Vector3.up * (r.transform.localScale.y * 0.5f + 0.02f), 0.32f, ColorOf(r.sharedMaterial, new Color(1f, 0.6f, 0.2f)));
                else if (n == "Flame" && r.GetComponentInParent<Flame>() == null)
                {
                    // The campfire's glowing ball becomes a real fire.
                    r.enabled = false;
                    Flame.Create(r.transform.parent, r.transform.localPosition - Vector3.up * 0.25f, 1.1f, new Color(1f, 0.5f, 0.15f));
                }
                else if (n == "LampGlass")
                    Flame.Create(r.transform.parent, r.transform.localPosition - Vector3.up * 0.1f, 0.18f, new Color(1f, 0.7f, 0.3f));
            }
            foreach (var l in root.GetComponentsInChildren<Light>(true))
            {
                if (l.type != LightType.Point || l.GetComponent<Flicker>() != null) continue;
                float h, s, v;
                Color.RGBToHSV(l.color, out h, out s, out v);
                bool warm = h < 0.15f || h > 0.95f;
                var f = l.gameObject.AddComponent<Flicker>();
                f.Amount = warm ? 0.2f : 0.08f;
                f.Speed = warm ? 8f : 2f;
            }
        }

        static Color ColorOf(Material m, Color fallback)
        {
            if (m == null) return fallback;
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty("_Color")) return m.GetColor("_Color");
            return fallback;
        }

        static bool Blocked(Vector3 p, float radius)
        {
            foreach (var c in Physics.OverlapSphere(p + Vector3.up * (radius + 0.3f), radius))
                if (!c.isTrigger) return true;
            return false;
        }

        static void Tree(Transform parent, Vector3 pos, float height)
        {
            var t = new GameObject("Tree").transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float trunkH = height * 0.55f, trunkR = height * 0.045f;
            Prim.Shape(BodyMesh.Get("Cone"), t, Vector3.zero, new Vector3(trunkR * 2.2f, trunkH * 1.15f, trunkR * 2.2f), BarkColor, "Trunk")
                .GetComponent<Renderer>().sharedMaterial = Prim.Mat(BarkColor, Surface.Bark);
            for (int b = 0; b < 2; b++)
            {
                var branch = Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0f, trunkH * (0.65f + b * 0.15f), 0f), new Vector3(trunkR * 0.7f, height * 0.18f, trunkR * 0.7f), BarkColor, "Log");
                branch.transform.localRotation = Quaternion.Euler(Random.Range(35f, 55f), b * 160f + Random.Range(-20f, 20f), 0f);
                branch.transform.localPosition += branch.transform.up * height * 0.15f;
            }
            var leaf = Prim.Vary(Leaf, 0.12f);
            int clumps = Random.Range(5, 8);
            for (int i = 0; i < clumps; i++)
            {
                var off = new Vector3(Random.Range(-1f, 1f) * height * 0.22f, trunkH + Random.Range(0f, height * 0.35f), Random.Range(-1f, 1f) * height * 0.22f);
                float s = height * Random.Range(0.25f, 0.38f);
                var clump = Prim.Make(PrimitiveType.Sphere, t, off, new Vector3(s, s * Random.Range(0.7f, 0.9f), s), Prim.Vary(leaf, 0.05f), "Foliage");
                clump.transform.localRotation = Random.rotation;
            }
        }

        static void PineTree(Transform parent, Vector3 pos, float height)
        {
            var t = new GameObject("Pine").transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0f, height * 0.2f, 0f), new Vector3(height * 0.05f, height * 0.2f, height * 0.05f), BarkColor, "Log");
            var col = Prim.Vary(Pine, 0.08f);
            int tiers = 4;
            for (int i = 0; i < tiers; i++)
            {
                float f = i / (float)tiers;
                float w = height * Mathf.Lerp(0.42f, 0.14f, f);
                var tier = Prim.Shape(BodyMesh.Get("Cone"), t, new Vector3(0f, height * (0.18f + f * 0.62f), 0f), new Vector3(w * 2f, height * 0.38f, w * 2f), col, "Foliage");
                tier.GetComponent<Renderer>().sharedMaterial = Prim.Mat(col * (0.9f + f * 0.2f), Surface.Foliage);
                tier.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
        }

        static void Boulder(Transform parent, Vector3 pos, float size)
        {
            var b = Prim.Make(PrimitiveType.Sphere, parent, pos + Vector3.up * size * 0.15f,
                new Vector3(size * Random.Range(1f, 1.6f), size * Random.Range(0.5f, 0.8f), size * Random.Range(0.9f, 1.3f)), Prim.Vary(Rock, 0.06f), "Rock");
            b.transform.localRotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
        }

        static void GrassTufts(Transform parent, int count, float half, bool avoidRoads)
        {
            var spots = new List<Vector3>();
            for (int i = 0; i < count * 2 && spots.Count < count; i++)
            {
                var p = new Vector3(Random.Range(-half, half), 0f, Random.Range(-half, half));
                if (avoidRoads && (Mathf.Abs(p.x) < 4f && p.z > -30f && p.z < 48f || Mathf.Abs(p.z) < 3.5f && Mathf.Abs(p.x) < 32f)) continue;
                if (Blocked(p, 0.35f)) continue;
                spots.Add(p);
            }
            BuildTufts(parent, spots, new Color(0.42f, 0.55f, 0.28f));
        }

        static void GrassTuftsAt(Transform parent, Vector3 center, float half, int count, Color tint)
        {
            var spots = new List<Vector3>();
            for (int i = 0; i < count; i++)
            {
                var p = center + new Vector3(Random.Range(-half, half), 0f, Random.Range(-half, half));
                if (!Blocked(p, 0.3f)) spots.Add(p);
            }
            BuildTufts(parent, spots, tint);
        }

        /// <summary>Grass tufts as crossed, alpha-cut cards, merged into a few big meshes so they draw fast.</summary>
        static void BuildTufts(Transform parent, List<Vector3> spots, Color tint)
        {
            var mat = Surfaces.GrassCard(tint);
            if (mat == null || spots.Count == 0) return;
            const int perMesh = 900;
            for (int start = 0; start < spots.Count; start += perMesh)
            {
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var n = new List<Vector3>(); var tris = new List<int>();
                for (int i = start; i < Mathf.Min(spots.Count, start + perMesh); i++)
                {
                    var p = spots[i];
                    float w = Random.Range(0.35f, 0.7f), h = Random.Range(0.3f, 0.6f), yaw = Random.Range(0f, 180f);
                    for (int q = 0; q < 2; q++)
                    {
                        var dir = Quaternion.Euler(0f, yaw + q * 90f, 0f) * Vector3.right * w * 0.5f;
                        for (int side = 0; side < 2; side++) // both faces, for shaders that cull back faces
                        {
                            int b = v.Count;
                            v.Add(p - dir); v.Add(p + dir); v.Add(p + dir + Vector3.up * h); v.Add(p - dir + Vector3.up * h);
                            uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
                            for (int k = 0; k < 4; k++) n.Add(Vector3.up); // lit like the ground, so tufts never look dark from behind
                            if (side == 0) { tris.Add(b); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b); tris.Add(b + 3); tris.Add(b + 2); }
                            else { tris.Add(b); tris.Add(b + 1); tris.Add(b + 2); tris.Add(b); tris.Add(b + 2); tris.Add(b + 3); }
                        }
                    }
                }
                var mesh = new Mesh { name = "GrassTufts" };
                mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetNormals(n); mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                var go = new GameObject("GrassTufts");
                go.transform.SetParent(parent, true);
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}
