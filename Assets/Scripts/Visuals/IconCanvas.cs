using UnityEngine;

namespace Hollowmark.Visuals
{
    /// <summary>
    /// A small software painter for item pictures. Shapes are signed distance fields in a 0..100 square
    /// (y points up), so every edge is anti-aliased and the pictures stay crisp at any icon size.
    /// Lighting always comes from the top left, which makes round things look round and metal shine.
    /// </summary>
    public class IconCanvas
    {
        public delegate float Sdf(Vector2 p);
        public delegate Color Shade(Vector2 p, float depth);

        public readonly int N;
        public readonly Color[] Px;
        readonly float k; // pixels per unit

        public static readonly Vector2 Light2 = new Vector2(-0.6f, 0.8f);
        static readonly Vector3 Light3 = new Vector3(-0.45f, 0.6f, 0.66f).normalized;
        public static readonly Color Ink = new Color(0.09f, 0.07f, 0.05f, 1f);

        public IconCanvas(int size)
        {
            N = size;
            Px = new Color[size * size];
            k = size / 100f;
        }

        public void Clear() { for (int i = 0; i < Px.Length; i++) Px[i] = new Color(0f, 0f, 0f, 0f); }

        // ---------- Core fill ----------

        /// <summary>Paints every pixel inside the field. depth is how far inside the shape the pixel is, in units.</summary>
        public void Fill(Vector2 min, Vector2 max, Sdf sdf, Shade shade)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(min.x * k) - 2, 0, N - 1), x1 = Mathf.Clamp(Mathf.CeilToInt(max.x * k) + 2, 0, N - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(min.y * k) - 2, 0, N - 1), y1 = Mathf.Clamp(Mathf.CeilToInt(max.y * k) + 2, 0, N - 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2((x + 0.5f) / k, (y + 0.5f) / k);
                    float d = sdf(p);
                    float cov = Mathf.Clamp01(0.5f - d * k);
                    if (cov <= 0f) continue;
                    Blend(y * N + x, shade(p, -d), cov);
                }
        }

        void Blend(int i, Color c, float cov)
        {
            float a = Mathf.Clamp01(c.a * cov);
            if (a <= 0f) return;
            var d = Px[i];
            float oa = a + d.a * (1f - a);
            if (oa < 1e-5f) return;
            float r = (c.r * a + d.r * d.a * (1f - a)) / oa;
            float g = (c.g * a + d.g * d.a * (1f - a)) / oa;
            float b = (c.b * a + d.b * d.a * (1f - a)) / oa;
            Px[i] = new Color(r, g, b, oa);
        }

        // ---------- Distance fields ----------

        public static Sdf SdCircle(Vector2 c, float r) { return p => (p - c).magnitude - r; }

        public static Sdf SdEllipse(Vector2 c, float rx, float ry, float deg = 0f)
        {
            float cs = Mathf.Cos(-deg * Mathf.Deg2Rad), sn = Mathf.Sin(-deg * Mathf.Deg2Rad);
            float m = Mathf.Min(rx, ry);
            return p =>
            {
                var q = p - c;
                q = new Vector2(q.x * cs - q.y * sn, q.x * sn + q.y * cs);
                return (new Vector2(q.x / rx, q.y / ry).magnitude - 1f) * m;
            };
        }

        /// <summary>A capsule whose radius goes from ra at a to rb at b.</summary>
        public static Sdf SdCapsule(Vector2 a, Vector2 b, float ra, float rb)
        {
            var ab = b - a;
            float len2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
            return p =>
            {
                float h = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                return (p - (a + ab * h)).magnitude - Mathf.Lerp(ra, rb, h);
            };
        }

        public static Sdf SdPoly(Vector2[] v)
        {
            return p =>
            {
                float d = (p - v[0]).sqrMagnitude, s = 1f;
                for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
                {
                    var e = v[j] - v[i];
                    var w = p - v[i];
                    var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Mathf.Max(e.sqrMagnitude, 1e-6f));
                    d = Mathf.Min(d, b.sqrMagnitude);
                    bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
                }
                return s * Mathf.Sqrt(d);
            };
        }

        public static Sdf SdRing(Vector2 c, float r, float halfWidth) { return p => Mathf.Abs((p - c).magnitude - r) - halfWidth; }
        public static Sdf Intersect(Sdf a, Sdf b) { return p => Mathf.Max(a(p), b(p)); }
        public static Sdf Subtract(Sdf a, Sdf b) { return p => Mathf.Max(a(p), -b(p)); }

        // ---------- Colour helpers ----------

        public static Color Mul(Color c, float l) { return new Color(Mathf.Clamp01(c.r * l), Mathf.Clamp01(c.g * l), Mathf.Clamp01(c.b * l), c.a); }
        public static Color Shine(Color c, float s) { var o = Color.Lerp(c, Color.white, Mathf.Clamp01(s)); o.a = c.a; return o; }
        public static Color A(Color c, float a) { c.a = a; return c; }

        public static float Hash(float x, float y)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        /// <summary>Smooth value noise in 0..1.</summary>
        public static float Noise(Vector2 p)
        {
            float ix = Mathf.Floor(p.x), iy = Mathf.Floor(p.y);
            float fx = p.x - ix, fy = p.y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix, iy), b = Hash(ix + 1f, iy), c = Hash(ix, iy + 1f), d = Hash(ix + 1f, iy + 1f);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        // ---------- Shapes with built-in shading ----------

        /// <summary>Sphere-like shading: bright at the top left, dark at the bottom right, optional glossy highlight.</summary>
        public void Ellipse(Vector2 c, float rx, float ry, Color col, float shine = 0f, float deg = 0f, System.Func<Vector2, float> pattern = null)
        {
            float r = Mathf.Max(rx, ry);
            float cs = Mathf.Cos(-deg * Mathf.Deg2Rad), sn = Mathf.Sin(-deg * Mathf.Deg2Rad);
            Fill(c - Vector2.one * r, c + Vector2.one * r, SdEllipse(c, rx, ry, deg), (p, depth) =>
            {
                var q = p - c;
                q = new Vector2(q.x * cs - q.y * sn, q.x * sn + q.y * cs);
                var n2 = new Vector2(q.x / rx, q.y / ry);
                // rotate the normal back into picture space
                n2 = new Vector2(n2.x * cs + n2.y * sn, -n2.x * sn + n2.y * cs);
                float z = Mathf.Sqrt(Mathf.Max(0f, 1f - n2.sqrMagnitude));
                var n = new Vector3(n2.x, n2.y, z);
                float l = 0.55f + 0.6f * Mathf.Max(0f, Vector3.Dot(n, Light3));
                var o = Mul(col, l * (pattern != null ? pattern(p) : 1f));
                if (shine > 0f)
                {
                    float spec = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(n, (Light3 + Vector3.forward).normalized)), 24f);
                    o = Shine(o, spec * shine);
                }
                return o;
            });
        }

        public void Circle(Vector2 c, float r, Color col, float shine = 0f, System.Func<Vector2, float> pattern = null)
        {
            Ellipse(c, r, r, col, shine, 0f, pattern);
        }

        /// <summary>Cylinder shading across the width: good for handles, barrels, rolls and fingers.</summary>
        public void Capsule(Vector2 a, Vector2 b, float ra, Color col, float shine = 0f, float rb = -1f, System.Func<Vector2, float> pattern = null)
        {
            if (rb < 0f) rb = ra;
            var dir = (b - a);
            var perp = new Vector2(-dir.y, dir.x).normalized;
            float rm = Mathf.Max(ra, rb);
            var min = Vector2.Min(a, b) - Vector2.one * rm;
            var max = Vector2.Max(a, b) + Vector2.one * rm;
            float len2 = Mathf.Max(dir.sqrMagnitude, 1e-6f);
            float side = Vector2.Dot(perp, Light2) >= 0f ? 1f : -1f;
            Fill(min, max, SdCapsule(a, b, ra, rb), (p, depth) =>
            {
                float h = Mathf.Clamp01(Vector2.Dot(p - a, dir) / len2);
                float r = Mathf.Max(0.01f, Mathf.Lerp(ra, rb, h));
                float s = Mathf.Clamp(Vector2.Dot(p - (a + dir * h), perp) / r, -1f, 1f) * side; // +1 = lit edge
                float l = 0.72f + 0.38f * s - 0.15f * s * s;
                var o = Mul(col, l * (pattern != null ? pattern(p) : 1f));
                if (shine > 0f) o = Shine(o, Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Abs(s - 0.45f) * 3f), 2f) * shine);
                return o;
            });
        }

        /// <summary>Flat polygon with a soft top-left gradient, a lit rim and an optional surface pattern.</summary>
        public void Poly(Vector2[] pts, Color col, float shine = 0f, System.Func<Vector2, float> pattern = null, float rim = 1f)
        {
            Vector2 min = pts[0], max = pts[0];
            foreach (var v in pts) { min = Vector2.Min(min, v); max = Vector2.Max(max, v); }
            var center = (min + max) * 0.5f;
            float ext = Mathf.Max(1f, (max - min).magnitude * 0.5f);
            Fill(min, max, SdPoly(pts), (p, depth) =>
            {
                float g = Vector2.Dot((p - center) / ext, Light2);
                float l = 0.9f + 0.22f * g;
                if (depth < rim) l *= 1.12f;
                var o = Mul(col, l * (pattern != null ? pattern(p) : 1f));
                if (shine > 0f) o = Shine(o, Mathf.Pow(Mathf.Clamp01(g), 3f) * shine);
                return o;
            });
        }

        /// <summary>Any distance field with the polygon-style gradient.</summary>
        public void Field(Vector2 min, Vector2 max, Sdf sdf, Color col, System.Func<Vector2, float> pattern = null)
        {
            var center = (min + max) * 0.5f;
            float ext = Mathf.Max(1f, (max - min).magnitude * 0.5f);
            Fill(min, max, sdf, (p, depth) =>
            {
                float l = 0.9f + 0.22f * Vector2.Dot((p - center) / ext, Light2);
                return Mul(col, l * (pattern != null ? pattern(p) : 1f));
            });
        }

        /// <summary>A line through several points, as joined capsules.</summary>
        public void Stroke(Vector2[] pts, float r, Color col, float shine = 0f)
        {
            for (int i = 0; i + 1 < pts.Length; i++) Capsule(pts[i], pts[i + 1], r, col, shine);
        }

        /// <summary>A thin flat line, for stitches, cracks and engraving.</summary>
        public void Line(Vector2 a, Vector2 b, float r, Color col)
        {
            var min = Vector2.Min(a, b) - Vector2.one * r;
            var max = Vector2.Max(a, b) + Vector2.one * r;
            Fill(min, max, SdCapsule(a, b, r, r), (p, d) => col);
        }

        public void Ring(Vector2 c, float r, float halfWidth, Color col, float shine = 0f)
        {
            float o = r + halfWidth;
            Fill(c - Vector2.one * o, c + Vector2.one * o, SdRing(c, r, halfWidth), (p, depth) =>
            {
                var n = (p - c).normalized;
                float l = 0.8f + 0.3f * Vector2.Dot(n, Light2);
                var col2 = Mul(col, l);
                return shine > 0f ? Shine(col2, Mathf.Clamp01(Vector2.Dot(n, Light2)) * shine * 0.6f) : col2;
            });
        }

        /// <summary>A soft radial glow (fire, magic, steam).</summary>
        public void Glow(Vector2 c, float r, Color col)
        {
            Fill(c - Vector2.one * r, c + Vector2.one * r, SdCircle(c, r), (p, depth) =>
            {
                float t = Mathf.Clamp01(depth / r);
                return A(col, col.a * t * t);
            });
        }

        /// <summary>Small four-pointed sparkle on polished metal.</summary>
        public void Glint(Vector2 c, float r)
        {
            var w = new Color(1f, 1f, 0.95f, 0.9f);
            Fill(c - Vector2.one * r, c + Vector2.one * r, p =>
            {
                var q = p - c;
                float ax = Mathf.Abs(q.x), ay = Mathf.Abs(q.y);
                return Mathf.Min(ax * 4f + ay, ay * 4f + ax) * 0.25f - r * 0.25f;
            }, (p, d) => w);
        }

        // ---------- Finishing ----------

        /// <summary>Adds a dark ink outline and a soft drop shadow, so every picture reads clearly on any background.</summary>
        public void Finish(float outlinePx = 1.6f, bool shadow = true)
        {
            var src = (Color[])Px.Clone();
            var alpha = new float[src.Length];
            for (int i = 0; i < src.Length; i++) alpha[i] = src[i].a;

            // Outline: the largest alpha within the outline radius.
            int r = Mathf.CeilToInt(outlinePx);
            var grown = new float[src.Length];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float m = 0f;
                    for (int dy = -r; dy <= r; dy++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= N || yy >= N) continue;
                            float dist = Mathf.Sqrt(dx * dx + dy * dy);
                            float w = Mathf.Clamp01(outlinePx + 0.5f - dist);
                            m = Mathf.Max(m, alpha[yy * N + xx] * w);
                        }
                    grown[y * N + x] = m;
                }

            // Shadow: the grown shape, blurred and nudged down-right.
            var shadowA = new float[src.Length];
            if (shadow)
            {
                int off = Mathf.Max(1, N / 40), blur = Mathf.Max(1, N / 50);
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float sum = 0f; int cnt = 0;
                        for (int dy = -blur; dy <= blur; dy++)
                            for (int dx = -blur; dx <= blur; dx++)
                            {
                                int xx = x - off + dx, yy = y + off + dy;
                                cnt++;
                                if (xx < 0 || yy < 0 || xx >= N || yy >= N) continue;
                                sum += grown[yy * N + xx];
                            }
                        shadowA[y * N + x] = sum / cnt * 0.4f;
                    }
            }

            for (int i = 0; i < Px.Length; i++)
            {
                Px[i] = new Color(0f, 0f, 0f, shadowA[i]);
                Blend(i, A(Ink, 1f), grown[i]);
                Blend(i, A(src[i], 1f), src[i].a);
            }
        }
    }
}
