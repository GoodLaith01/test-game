using System.Collections.Generic;
using UnityEngine;

namespace Hollowmark.Visuals
{
    /// <summary>
    /// Small helpers for building hard-surface meshes in code (blades, axe heads, gun stocks, crowns):
    /// a loft through a series of cross-sections, flat shaded so edges stay crisp, with UVs in metres.
    /// </summary>
    public static class MeshKit
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        public static Mesh Cached(string key, System.Func<Mesh> make)
        {
            Mesh m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            m = make();
            m.name = key;
            cache[key] = m;
            return m;
        }

        /// <summary>Lofts closed polygons (all with the same point count). Caps close the first and last ring.</summary>
        public static Mesh Loft(List<Vector3[]> rings, bool capStart = true, bool capEnd = true)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            var centres = new Vector3[rings.Count];
            for (int r = 0; r < rings.Count; r++) { foreach (var p in rings[r]) centres[r] += p; centres[r] /= rings[r].Length; }
            for (int r = 0; r + 1 < rings.Count; r++)
            {
                var a = rings[r]; var b = rings[r + 1];
                var inside = (centres[r] + centres[r + 1]) * 0.5f;
                for (int i = 0; i < a.Length; i++)
                {
                    int j = (i + 1) % a.Length;
                    Tri(v, t, a[i], a[j], b[j], inside);
                    Tri(v, t, a[i], b[j], b[i], inside);
                }
            }
            if (rings.Count > 1)
            {
                if (capStart) Cap(v, t, rings[0], centres[0], centres[0] + (centres[0] - centres[1]).normalized * -0.001f);
                if (capEnd) { int last = rings.Count - 1; Cap(v, t, rings[last], centres[last], centres[last] + (centres[last] - centres[last - 1]).normalized * -0.001f); }
            }
            return Finish(v, t);
        }

        /// <summary>Adds a triangle wound so that its front face looks away from the inside point.</summary>
        public static void Tri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-14f) return;
            int s = v.Count;
            v.Add(a);
            if (Vector3.Dot(n, (a + b + c) / 3f - inside) >= 0f) { v.Add(b); v.Add(c); }
            else { v.Add(c); v.Add(b); }
            t.Add(s); t.Add(s + 1); t.Add(s + 2);
        }

        static void Cap(List<Vector3> v, List<int> t, Vector3[] ring, Vector3 c, Vector3 inside)
        {
            for (int i = 0; i < ring.Length; i++) Tri(v, t, c, ring[i], ring[(i + 1) % ring.Length], inside);
        }

        static Mesh Finish(List<Vector3> v, List<int> t)
        {
            var m = new Mesh();
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.uv = Surfaces.BoxUV(m.vertices, m.normals, Vector3.one);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// A blade along +y. curve bends it towards -x (a sabre), singleEdge gives it a thick back,
        /// squareTip makes an executioner's flat end.
        /// </summary>
        public static Mesh Blade(float length, float width, float thick, float curve, bool singleEdge, bool squareTip)
        {
            string key = string.Format("Blade{0:0.000}_{1:0.000}_{2:0.000}_{3:0.00}_{4}_{5}", length, width, thick, curve, singleEdge, squareTip);
            return Cached(key, () =>
            {
                var rings = new List<Vector3[]>();
                const int steps = 14;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float w = width * (1f - 0.22f * t);
                    if (!squareTip) w *= Mathf.Clamp01((1f - t) / 0.2f);
                    float th = thick * (1f - 0.45f * t);
                    if (!squareTip && i == steps) th = 0f;
                    float x = -curve * length * t * t;
                    float y = t * length;
                    if (singleEdge)
                        rings.Add(new[] { new Vector3(x - w * 0.5f, y, 0f), new Vector3(x + w * 0.5f, y, th * 0.5f), new Vector3(x + w * 0.5f, y, -th * 0.5f) });
                    else
                        rings.Add(new[] { new Vector3(x - w * 0.5f, y, 0f), new Vector3(x, y, th * 0.5f), new Vector3(x + w * 0.5f, y, 0f), new Vector3(x, y, -th * 0.5f) });
                }
                return Loft(rings, true, squareTip);
            });
        }

        /// <summary>A wedge along +x, from a thick socket to a thin wide edge (axe heads, cleavers).</summary>
        public static Mesh Wedge(float length, float socketH, float edgeH, float socketT, float flare)
        {
            string key = string.Format("Wedge{0:0.000}_{1:0.000}_{2:0.000}_{3:0.000}_{4:0.00}", length, socketH, edgeH, socketT, flare);
            return Cached(key, () =>
            {
                var rings = new List<Vector3[]>();
                const int steps = 6;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float h = Mathf.Lerp(socketH, edgeH, Mathf.Pow(t, flare));
                    float th = Mathf.Lerp(socketT, socketT * 0.08f, t);
                    float x = t * length;
                    rings.Add(new[] { new Vector3(x, -h * 0.5f, -th * 0.5f), new Vector3(x, h * 0.5f, -th * 0.5f), new Vector3(x, h * 0.5f, th * 0.5f), new Vector3(x, -h * 0.5f, th * 0.5f) });
                }
                return Loft(rings);
            });
        }

        /// <summary>A rounded-rectangle bar along +y, through (y, half width, half depth, z offset) stations. For gun stocks and grips.</summary>
        public static Mesh Bar(string key, params Vector4[] stations)
        {
            return Cached("Bar" + key, () =>
            {
                var rings = new List<Vector3[]>();
                foreach (var s in stations)
                {
                    var ring = new Vector3[8];
                    for (int i = 0; i < 8; i++)
                    {
                        float a = (i + 0.5f) / 8f * Mathf.PI * 2f;
                        ring[i] = new Vector3(Mathf.Cos(a) * s.y * 1.08f, s.x, s.w + Mathf.Sin(a) * s.z * 1.08f);
                    }
                    rings.Add(ring);
                }
                return Loft(rings);
            });
        }

        /// <summary>A ring of spikes (a crown), radius r, height h, with n points.</summary>
        public static Mesh Crown(float r, float band, float spike, int points)
        {
            string key = string.Format("Crown{0:0.000}_{1:0.000}_{2:0.000}_{3}", r, band, spike, points);
            return Cached(key, () =>
            {
                var v = new List<Vector3>();
                var t = new List<int>();
                int seg = points * 2;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i / (float)seg * Mathf.PI * 2f, a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                    var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)); var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    float top0 = band + (i % 2 == 0 ? 0f : spike), top1 = band + (i % 2 == 0 ? spike : 0f);
                    foreach (float rr in new[] { r, r * 0.92f })
                    {
                        // Outer face looks outwards, inner face inwards.
                        var a = d0 * rr; var b = d1 * rr; var c = d1 * rr + Vector3.up * top1; var d = d0 * rr + Vector3.up * top0;
                        var axis = Vector3.up * (band * 0.5f);
                        var inside = rr == r ? axis : (a + b) * 0.5f * 2f + axis;
                        Tri(v, t, a, b, c, inside);
                        Tri(v, t, a, c, d, inside);
                    }
                }
                var m = new Mesh();
                m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals();
                m.uv = Surfaces.BoxUV(m.vertices, m.normals, Vector3.one);
                m.RecalculateBounds();
                return m;
            });
        }
    }
}
