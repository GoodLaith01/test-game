using System.Collections.Generic;
using UnityEngine;

namespace Hollowmark.Visuals
{
    /// <summary>The pieces a body is made of. Clothing and armor are fitted by covering regions.</summary>
    public enum BodyRegion { Head, Neck, Chest, Pelvis, UpperArm, Forearm, Hand, Thigh, Shin, Foot }

    /// <summary>
    /// Smooth, rounded body-part meshes generated in code (no art files needed). Each part is a "loft":
    /// a stack of oval rings joined into one surface, which gives tapered limbs, a chest, a waist and a
    /// jaw instead of boxes. Meshes are built once for a 1.8 m reference human and shared; characters of
    /// other sizes and builds just scale them. Inflating a part a few millimetres gives a fitted garment.
    /// </summary>
    public static class BodyMesh
    {
        // Ring tables: (y along the part, x radius, z radius, z offset). Units are metres on a 1.8 m human.
        static readonly Vector4[] PelvisRings =
        {
            new Vector4(-0.10f, 0.03f, 0.03f, 0f), new Vector4(-0.08f, 0.11f, 0.08f, 0f),
            new Vector4(-0.04f, 0.155f, 0.105f, -0.005f), new Vector4(0.02f, 0.162f, 0.11f, -0.01f),
            new Vector4(0.08f, 0.148f, 0.1f, 0f), new Vector4(0.15f, 0.132f, 0.092f, 0f), new Vector4(0.2f, 0.11f, 0.08f, 0f),
        };
        static readonly Vector4[] ChestRings =
        {
            new Vector4(0.13f, 0.134f, 0.094f, 0f), new Vector4(0.2f, 0.14f, 0.1f, 0.005f),
            new Vector4(0.3f, 0.155f, 0.11f, 0.012f), new Vector4(0.4f, 0.165f, 0.115f, 0.015f),
            new Vector4(0.48f, 0.175f, 0.103f, 0.005f), new Vector4(0.53f, 0.162f, 0.085f, -0.005f),
            new Vector4(0.57f, 0.1f, 0.06f, -0.01f), new Vector4(0.59f, 0.06f, 0.05f, -0.01f),
        };
        static readonly Vector4[] NeckRings =
        {
            new Vector4(0.54f, 0.056f, 0.06f, -0.005f), new Vector4(0.6f, 0.05f, 0.054f, 0f),
            new Vector4(0.66f, 0.046f, 0.05f, 0.008f), new Vector4(0.69f, 0.03f, 0.03f, 0.01f),
        };
        static readonly Vector4[] HeadRings =
        {
            new Vector4(-0.02f, 0.012f, 0.012f, 0.07f), new Vector4(-0.01f, 0.035f, 0.035f, 0.06f),
            new Vector4(0.02f, 0.055f, 0.07f, 0.035f), new Vector4(0.06f, 0.068f, 0.09f, 0.02f),
            new Vector4(0.1f, 0.074f, 0.098f, 0.012f), new Vector4(0.14f, 0.079f, 0.102f, 0.006f),
            new Vector4(0.18f, 0.076f, 0.098f, 0f), new Vector4(0.21f, 0.064f, 0.085f, -0.005f),
            new Vector4(0.235f, 0.038f, 0.05f, -0.01f), new Vector4(0.248f, 0.005f, 0.005f, -0.012f),
        };
        static readonly Vector4[] UpperArmRings =
        {
            new Vector4(0.03f, 0.02f, 0.02f, 0f), new Vector4(0f, 0.05f, 0.052f, 0f),
            new Vector4(-0.05f, 0.052f, 0.05f, 0f), new Vector4(-0.15f, 0.043f, 0.045f, 0f),
            new Vector4(-0.26f, 0.037f, 0.038f, 0f), new Vector4(-0.3f, 0.033f, 0.035f, 0f),
            new Vector4(-0.315f, 0.02f, 0.02f, 0f),
        };
        static readonly Vector4[] ForearmRings =
        {
            new Vector4(0.02f, 0.02f, 0.02f, 0f), new Vector4(0f, 0.036f, 0.036f, 0f),
            new Vector4(-0.06f, 0.04f, 0.037f, 0.004f), new Vector4(-0.16f, 0.032f, 0.028f, 0f),
            new Vector4(-0.24f, 0.027f, 0.02f, 0f), new Vector4(-0.26f, 0.02f, 0.015f, 0f),
        };
        // The hand hangs with the palm facing the body, so it is thin in x and wide in z.
        static readonly Vector4[] HandRings =
        {
            new Vector4(0.01f, 0.014f, 0.02f, 0f), new Vector4(-0.02f, 0.016f, 0.035f, 0.003f),
            new Vector4(-0.07f, 0.017f, 0.043f, 0.006f), new Vector4(-0.1f, 0.014f, 0.042f, 0.008f),
            new Vector4(-0.15f, 0.01f, 0.035f, 0.01f), new Vector4(-0.175f, 0.007f, 0.02f, 0.01f),
            new Vector4(-0.18f, 0.004f, 0.004f, 0.01f),
        };
        static readonly Vector4[] ThumbRings =
        {
            new Vector4(0f, 0.013f, 0.013f, 0f), new Vector4(-0.04f, 0.012f, 0.012f, 0f),
            new Vector4(-0.065f, 0.009f, 0.009f, 0f), new Vector4(-0.072f, 0.002f, 0.002f, 0f),
        };
        static readonly Vector4[] ThighRings =
        {
            new Vector4(0.04f, 0.055f, 0.06f, 0f), new Vector4(0f, 0.08f, 0.088f, -0.005f),
            new Vector4(-0.08f, 0.08f, 0.084f, 0.005f), new Vector4(-0.2f, 0.07f, 0.072f, 0.008f),
            new Vector4(-0.33f, 0.055f, 0.058f, 0.005f), new Vector4(-0.41f, 0.05f, 0.052f, 0.008f),
            new Vector4(-0.44f, 0.03f, 0.03f, 0.01f),
        };
        static readonly Vector4[] ShinRings =
        {
            new Vector4(0.03f, 0.04f, 0.04f, 0.01f), new Vector4(0f, 0.05f, 0.052f, 0.01f),
            new Vector4(-0.05f, 0.052f, 0.055f, 0f), new Vector4(-0.12f, 0.054f, 0.058f, -0.012f),
            new Vector4(-0.25f, 0.04f, 0.042f, -0.005f), new Vector4(-0.38f, 0.03f, 0.03f, 0f),
            new Vector4(-0.43f, 0.032f, 0.032f, 0f), new Vector4(-0.45f, 0.02f, 0.02f, 0f),
        };
        // Lofted forwards from the ankle: (forward, half width, half height, depth below the ankle).
        static readonly Vector4[] FootRings =
        {
            new Vector4(-0.06f, 0.02f, 0.02f, 0.035f), new Vector4(-0.045f, 0.035f, 0.04f, 0.035f),
            new Vector4(0f, 0.04f, 0.045f, 0.03f), new Vector4(0.06f, 0.045f, 0.035f, 0.04f),
            new Vector4(0.12f, 0.048f, 0.025f, 0.048f), new Vector4(0.17f, 0.04f, 0.018f, 0.052f),
            new Vector4(0.19f, 0.02f, 0.01f, 0.055f),
        };
        static readonly Vector4[] NoseRings =
        {
            new Vector4(0.15f, 0.007f, 0.006f, 0.104f), new Vector4(0.12f, 0.01f, 0.01f, 0.112f),
            new Vector4(0.095f, 0.015f, 0.014f, 0.12f), new Vector4(0.082f, 0.014f, 0.01f, 0.116f),
            new Vector4(0.078f, 0.005f, 0.004f, 0.11f),
        };
        static readonly Vector4[] QueueRings =
        {
            new Vector4(0.1f, 0.012f, 0.012f, -0.1f), new Vector4(0.06f, 0.016f, 0.016f, -0.108f),
            new Vector4(-0.02f, 0.012f, 0.012f, -0.11f), new Vector4(-0.05f, 0.004f, 0.004f, -0.108f),
        };
        static readonly Vector4[] BunRings =
        {
            new Vector4(0.2f, 0.01f, 0.01f, -0.09f), new Vector4(0.19f, 0.04f, 0.035f, -0.095f),
            new Vector4(0.15f, 0.045f, 0.04f, -0.1f), new Vector4(0.11f, 0.035f, 0.03f, -0.095f),
            new Vector4(0.1f, 0.01f, 0.01f, -0.09f),
        };
        static readonly Vector4[] CoatTailRings =
        {
            new Vector4(0.16f, 0.14f, 0.1f, 0f), new Vector4(0.05f, 0.172f, 0.12f, -0.01f),
            new Vector4(-0.15f, 0.215f, 0.155f, -0.03f), new Vector4(-0.38f, 0.235f, 0.17f, -0.05f),
            new Vector4(-0.42f, 0.2f, 0.14f, -0.05f),
        };
        static readonly Vector4[] GownRings =
        {
            new Vector4(0.16f, 0.14f, 0.1f, 0f), new Vector4(0.05f, 0.18f, 0.13f, 0f),
            new Vector4(-0.25f, 0.23f, 0.19f, 0f), new Vector4(-0.6f, 0.27f, 0.23f, 0f),
            new Vector4(-0.84f, 0.29f, 0.25f, 0f), new Vector4(-0.86f, 0.2f, 0.18f, 0f),
        };
        static readonly Vector4[] SnoutRings =
        {
            new Vector4(0f, 0.05f, 0.04f, 0f), new Vector4(0.04f, 0.045f, 0.038f, -0.005f),
            new Vector4(0.08f, 0.035f, 0.03f, -0.005f), new Vector4(0.1f, 0.02f, 0.018f, 0f),
            new Vector4(0.105f, 0.005f, 0.005f, 0f),
        };
        static readonly Vector4[] ConeRings =
        {
            new Vector4(0f, 1f, 1f, 0f), new Vector4(0.3f, 0.75f, 0.75f, 0.08f),
            new Vector4(0.65f, 0.4f, 0.4f, 0.25f), new Vector4(1f, 0.02f, 0.02f, 0.5f),
        };
        static readonly Vector4[] CarapaceRings =
        {
            new Vector4(-0.5f, 0.05f, 0.05f, 0f), new Vector4(-0.4f, 0.32f, 0.26f, 0f),
            new Vector4(-0.15f, 0.5f, 0.4f, -0.05f), new Vector4(0.15f, 0.48f, 0.38f, -0.05f),
            new Vector4(0.38f, 0.32f, 0.26f, 0f), new Vector4(0.5f, 0.08f, 0.08f, 0f),
        };

        /// <summary>
        /// A muscle: a smooth swelling of the surface centred at height y and angle a around the part
        /// (a = 90 degrees is the front, 270 the back, 0 the +x side), as a fraction of the local radius.
        /// </summary>
        struct Muscle
        {
            public float y, yWidth, angle, angleWidth, amount;
            public Muscle(float y, float yWidth, float angleDeg, float angleWidthDeg, float amount)
            {
                this.y = y; this.yWidth = yWidth; angle = angleDeg * Mathf.Deg2Rad; angleWidth = angleWidthDeg * Mathf.Deg2Rad; this.amount = amount;
            }
        }

        // Anatomy, sculpted onto the rings for muscular bodies. Pairs are mirrored left and right.
        static readonly Muscle[] ChestMuscles =
        {
            new Muscle(0.415f, 0.05f, 64f, 22f, 0.2f), new Muscle(0.415f, 0.05f, 116f, 22f, 0.2f),     // pectorals
            new Muscle(0.17f, 0.024f, 80f, 9f, 0.08f), new Muscle(0.17f, 0.024f, 100f, 9f, 0.08f),        // abdominals
            new Muscle(0.23f, 0.024f, 80f, 9f, 0.085f), new Muscle(0.23f, 0.024f, 100f, 9f, 0.085f),
            new Muscle(0.29f, 0.024f, 80f, 9f, 0.09f), new Muscle(0.29f, 0.024f, 100f, 9f, 0.09f),
            new Muscle(0.35f, 0.07f, 0f, 22f, 0.1f), new Muscle(0.35f, 0.07f, 180f, 22f, 0.1f),         // lats
            new Muscle(0.53f, 0.035f, 245f, 30f, 0.12f), new Muscle(0.53f, 0.035f, 295f, 30f, 0.12f),    // trapezius
            new Muscle(0.43f, 0.05f, 240f, 20f, 0.06f), new Muscle(0.43f, 0.05f, 300f, 20f, 0.06f),      // shoulder blades
            new Muscle(0.5f, 0.04f, 0f, 18f, 0.07f), new Muscle(0.5f, 0.04f, 180f, 18f, 0.07f),          // front of the deltoids
        };
        static readonly Muscle[] PelvisMuscles =
        {
            new Muscle(0.0f, 0.06f, 245f, 25f, 0.08f), new Muscle(0.0f, 0.06f, 295f, 25f, 0.08f),        // glutes
            new Muscle(0.09f, 0.04f, 30f, 18f, 0.04f), new Muscle(0.09f, 0.04f, 150f, 18f, 0.04f),       // obliques
        };
        static readonly Muscle[] NeckMuscles =
        {
            new Muscle(0.6f, 0.04f, 50f, 18f, 0.08f), new Muscle(0.6f, 0.04f, 130f, 18f, 0.08f),
        };
        static readonly Muscle[] UpperArmMuscles =
        {
            new Muscle(-0.04f, 0.05f, 0f, 70f, 0.18f), new Muscle(-0.04f, 0.05f, 180f, 70f, 0.18f),     // deltoid cap
            new Muscle(-0.15f, 0.06f, 90f, 35f, 0.22f),                                                  // biceps
            new Muscle(-0.11f, 0.07f, 270f, 35f, 0.15f),                                                 // triceps
        };
        static readonly Muscle[] ForearmMuscles =
        {
            new Muscle(-0.05f, 0.05f, 90f, 50f, 0.16f), new Muscle(-0.06f, 0.05f, 270f, 45f, 0.06f),
        };
        static readonly Muscle[] ThighMuscles =
        {
            new Muscle(-0.2f, 0.1f, 90f, 40f, 0.14f),                                                     // quadriceps
            new Muscle(-0.34f, 0.04f, 90f, 50f, 0.06f),                                                  // above the knee
            new Muscle(-0.15f, 0.08f, 270f, 40f, 0.07f),                                                 // hamstrings
        };
        static readonly Muscle[] ShinMuscles =
        {
            new Muscle(-0.11f, 0.06f, 250f, 22f, 0.18f), new Muscle(-0.11f, 0.06f, 290f, 22f, 0.18f),   // calves
            new Muscle(-0.12f, 0.06f, 90f, 22f, 0.04f),
        };

        static readonly Muscle[] TrunkMuscles = Concat(ChestMuscles, PelvisMuscles);

        static Muscle[] Concat(Muscle[] a, Muscle[] b)
        {
            var all = new Muscle[a.Length + b.Length];
            a.CopyTo(all, 0); b.CopyTo(all, a.Length);
            return all;
        }

        static Muscle[] MusclesFor(string key)
        {
            switch (key)
            {
                case "Chest": return ChestMuscles;
                case "Trunk": return TrunkMuscles;
                case "Pelvis": return PelvisMuscles;
                case "Neck": return NeckMuscles;
                case "UpperArm": return UpperArmMuscles;
                case "Forearm": return ForearmMuscles;
                case "Thigh": return ThighMuscles;
                case "Shin": return ShinMuscles;
                default: return null;
            }
        }

        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        /// <summary>
        /// The shared mesh for a body region, optionally inflated outwards (for clothes and armor).
        /// muscle 0 is a slim body, 1 a strongly muscled one with sculpted chest, abs, shoulders, arms and legs.
        /// </summary>
        /// shoulders widens the chest towards the top only, so broad shoulders taper into a normal waist.
        public static Mesh Get(BodyRegion region, float inflate = 0f, float muscle = 0f, float shoulders = 1f)
        {
            return Get(region.ToString(), inflate, muscle, shoulders);
        }

        public static Mesh Get(string key, float inflate = 0f, float muscle = 0f, float shoulders = 1f)
        {
            var muscles = MusclesFor(key);
            muscle = muscles == null ? 0f : Mathf.Round(Mathf.Clamp(muscle, 0f, 1.5f) * 10f) / 10f;
            shoulders = key == "Chest" || key == "Trunk" ? Mathf.Round(Mathf.Clamp(shoulders, 0.7f, 1.6f) * 20f) / 20f : 1f;
            string id = key + "/" + Mathf.RoundToInt(inflate * 10000f) + "/" + Mathf.RoundToInt(muscle * 10f) + "/" + Mathf.RoundToInt(shoulders * 20f);
            Mesh mesh;
            if (cache.TryGetValue(id, out mesh) && mesh != null) return mesh;

            var b = new Builder { Muscles = muscles, Strength = muscle, Shoulders = shoulders };
            var forward = Quaternion.Euler(90f, 0f, 0f);
            switch (key)
            {
                case "Pelvis": b.Loft(PelvisRings, 2.3f, inflate); break;
                case "Chest": b.Loft(ChestRings, 2.5f, inflate); break;
                case "Trunk":
                {
                    // Pelvis and chest as one seamless skin; clothes still fit the two halves separately.
                    var rings = new List<Vector4>();
                    foreach (var r in PelvisRings) if (r.x < 0.1f) rings.Add(r);
                    rings.AddRange(ChestRings);
                    b.Loft(rings.ToArray(), 2.4f, inflate);
                    break;
                }
                case "Neck": b.Loft(NeckRings, 2f, inflate); break;
                case "Head": b.Loft(HeadRings, 2.1f, inflate); break;
                case "UpperArm": b.Loft(UpperArmRings, 2f, inflate); break;
                case "Forearm": b.Loft(ForearmRings, 2f, inflate); break;
                case "Hand":
                    b.Loft(HandRings, 2.4f, inflate);
                    b.Loft(ThumbRings, 2f, inflate, Quaternion.Euler(-35f, 0f, 0f), new Vector3(0f, -0.025f, 0.028f));
                    break;
                case "Thigh": b.Loft(ThighRings, 2f, inflate); break;
                case "Shin": b.Loft(ShinRings, 2f, inflate); break;
                case "Foot": b.Loft(FootRings, 2.2f, inflate, forward, Vector3.zero); break;
                case "Nose": b.Loft(NoseRings, 2f, inflate); break;
                case "Queue": b.Loft(QueueRings, 2f, inflate); break;
                case "Bun": b.Loft(BunRings, 2f, inflate); break;
                case "CoatTail": b.Loft(CoatTailRings, 2.3f, inflate); break;
                case "Gown": b.Loft(GownRings, 2f, inflate); break;
                case "Snout": b.Loft(SnoutRings, 2f, inflate, forward, Vector3.zero); break;
                case "Cone": b.Loft(ConeRings, 2f, inflate); break; // unit-sized: scale it
                case "Carapace": b.Loft(CarapaceRings, 2f, inflate, forward, Vector3.zero); break;
                case "Hair":
                {
                    // Covers the crown and the back of the head; the hairline slopes from the forehead down to the nape.
                    var rings = new List<Vector4>();
                    foreach (var r in HeadRings)
                    {
                        if (r.x < 0.02f) continue;
                        float back = 0.02f + Mathf.Max(0f, 0.15f - r.x) * 0.7f;
                        rings.Add(new Vector4(r.x + 0.006f, r.y * (r.x < 0.1f ? 0.97f : 1f), r.z, r.w - back));
                    }
                    b.Loft(rings.ToArray(), 2.1f, inflate + 0.008f);
                    break;
                }
                case "Cap":
                {
                    // A helmet shell over the top of the skull.
                    var rings = new List<Vector4>();
                    foreach (var r in HeadRings) if (r.x >= 0.12f) rings.Add(new Vector4(r.x + 0.008f, r.y, r.z, r.w - 0.012f));
                    b.Loft(rings.ToArray(), 2.1f, inflate + 0.016f);
                    break;
                }
                case "Tricorne": b.Tricorne(inflate); break;
                default: b.Loft(ConeRings, 2f, inflate); break;
            }
            mesh = b.ToMesh(id);
            cache[id] = mesh;
            return mesh;
        }

        class Builder
        {
            const int Sides = 24, Subdiv = 5;
            public Muscle[] Muscles;
            public float Strength, Shoulders = 1f;
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<int> tris = new List<int>();

            static Vector4 CatmullRom(Vector4 p0, Vector4 p1, Vector4 p2, Vector4 p3, float t)
            {
                float t2 = t * t, t3 = t2 * t;
                return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
            }

            static float SuperAxis(float c, float power) { return Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / power); }

            float Swell(float y, float angle)
            {
                if (Muscles == null || Strength <= 0f) return 1f;
                float sum = 0f;
                foreach (var m in Muscles)
                {
                    float dy = (y - m.y) / m.yWidth;
                    float da = Mathf.Atan2(Mathf.Sin(angle - m.angle), Mathf.Cos(angle - m.angle)) / m.angleWidth;
                    sum += m.amount * Mathf.Exp(-(dy * dy + da * da));
                }
                return 1f + Strength * sum;
            }

            public void Loft(Vector4[] rings, float power, float inflate) { Loft(rings, power, inflate, Quaternion.identity, Vector3.zero); }

            /// <summary>Joins smoothed oval rings into a closed surface. power 2 = ellipse, higher = squarer.</summary>
            public void Loft(Vector4[] rings, float power, float inflate, Quaternion orient, Vector3 offset)
            {
                // Smooth the profile so the silhouette curves between the hand-placed rings.
                var smooth = new List<Vector4>();
                int n = rings.Length;
                for (int i = 0; i < n - 1; i++)
                    for (int k = 0; k < Subdiv; k++)
                    {
                        var r = CatmullRom(rings[Mathf.Max(i - 1, 0)], rings[i], rings[i + 1], rings[Mathf.Min(i + 2, n - 1)], k / (float)Subdiv);
                        r.y = Mathf.Max(r.y, 0.001f); r.z = Mathf.Max(r.z, 0.001f);
                        smooth.Add(r);
                    }
                smooth.Add(rings[n - 1]);

                int start = verts.Count, triStart = tris.Count;
                foreach (var r in smooth)
                    for (int j = 0; j < Sides; j++)
                    {
                        float a = j * Mathf.PI * 2f / Sides;
                        float swell = Swell(r.x, a);
                        float widen = Mathf.Lerp(1f, Shoulders, Mathf.SmoothStep(0f, 1f, (r.x - 0.18f) / 0.3f));
                        float x = SuperAxis(Mathf.Cos(a), power) * (r.y * swell * widen + inflate);
                        float z = SuperAxis(Mathf.Sin(a), power) * (r.z * swell + inflate) + r.w;
                        verts.Add(orient * new Vector3(x, r.x, z) + offset);
                    }
                int ringCount = smooth.Count;
                for (int i = 0; i < ringCount - 1; i++)
                    for (int j = 0; j < Sides; j++)
                    {
                        int a = start + i * Sides + j, b = start + i * Sides + (j + 1) % Sides;
                        int c = a + Sides, d = b + Sides;
                        tris.Add(a); tris.Add(c); tris.Add(b);
                        tris.Add(b); tris.Add(c); tris.Add(d);
                    }

                // Make sure faces point outwards whichever way the rings run.
                int mid = start + (ringCount / 2) * Sides;
                var centre = Vector3.zero;
                for (int j = 0; j < Sides; j++) centre += verts[mid + j];
                centre /= Sides;
                int t0 = triStart + (ringCount / 2) * Sides * 6;
                FaceOutwards(triStart, tris.Count, verts[tris[t0]] - centre, t0);

                Cap(start, start + Sides, offset + orient * new Vector3(0f, smooth[0].x, smooth[0].w));
                int last = start + (ringCount - 1) * Sides;
                Cap(last, last - Sides, offset + orient * new Vector3(0f, smooth[ringCount - 1].x, smooth[ringCount - 1].w));
            }

            void Cap(int ring, int neighbour, Vector3 centre)
            {
                int c = verts.Count, triStart = tris.Count;
                verts.Add(centre);
                for (int j = 0; j < Sides; j++) { tris.Add(c); tris.Add(ring + j); tris.Add(ring + (j + 1) % Sides); }
                var inner = Vector3.zero;
                for (int j = 0; j < Sides; j++) inner += verts[neighbour + j];
                FaceOutwards(triStart, tris.Count, centre - inner / Sides, triStart);
            }

            /// <summary>Flips a run of triangles if the reference one does not face the given direction.</summary>
            void FaceOutwards(int from, int to, Vector3 outward, int reference)
            {
                var a = verts[tris[reference]]; var b = verts[tris[reference + 1]]; var c = verts[tris[reference + 2]];
                var normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-12f || Vector3.Dot(normal, outward) >= 0f) return;
                for (int t = from; t < to; t += 3) { int tmp = tris[t + 1]; tris[t + 1] = tris[t + 2]; tris[t + 2] = tmp; }
            }

            /// <summary>A three-cornered hat: a crown plus a brim turned up on three sides.</summary>
            public void Tricorne(float inflate)
            {
                Loft(new[]
                {
                    new Vector4(0.13f, 0.092f, 0.116f, -0.012f), new Vector4(0.2f, 0.094f, 0.118f, -0.012f),
                    new Vector4(0.25f, 0.086f, 0.108f, -0.012f), new Vector4(0.27f, 0.055f, 0.065f, -0.012f),
                    new Vector4(0.275f, 0.005f, 0.005f, -0.01f),
                }, 2f, inflate);

                const int rings = 5, around = 36;
                for (int side = 0; side < 2; side++)
                {
                    int start = verts.Count;
                    for (int i = 0; i < rings; i++)
                    {
                        float t = i / (float)(rings - 1);
                        for (int j = 0; j < around; j++)
                        {
                            float a = j * Mathf.PI * 2f / around;
                            float lobe = Mathf.Cos(3f * (a - Mathf.PI * 0.5f)); // a corner points forwards
                            float r = Mathf.Lerp(0.09f, 0.2f + 0.04f * lobe, t) + inflate;
                            float y = 0.14f + t * t * 0.07f * (1f - lobe) - side * 0.008f;
                            verts.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r * 1.05f - 0.01f));
                        }
                    }
                    int triStart = tris.Count;
                    for (int i = 0; i < rings - 1; i++)
                        for (int j = 0; j < around; j++)
                        {
                            int a = start + i * around + j, b = start + i * around + (j + 1) % around, c = a + around, d = b + around;
                            tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d);
                        }
                    FaceOutwards(triStart, tris.Count, side == 0 ? Vector3.up : Vector3.down, triStart);
                }
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = "Body_" + name };
                m.SetVertices(verts);
                m.SetTriangles(tris, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
