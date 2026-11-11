using System.Collections.Generic;
using Hollowmark.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hollowmark.Visuals
{
    /// <summary>
    /// Detailed models built in code: 1700s weapons (curved sabres, basket-hilted broadswords, axes, halberds,
    /// flintlocks), period clothing details (buttons, cuffs, neckcloths, aprons, caps, buckles), creature claws and
    /// spines, and each boss's regalia and signature weapon.
    /// </summary>
    public static class Gear
    {
        // Bone order of the realistic body (see HumanMesh).
        public const int BoneBody = 0, BoneTorso = 1, BoneHead = 2, BoneArmL = 3, BoneElbowL = 4, BoneWristL = 5, BoneArmR = 6, BoneElbowR = 7, BoneWristR = 8,
            BoneLegL = 9, BoneKneeL = 10, BoneAnkleL = 11, BoneLegR = 12, BoneKneeR = 13, BoneAnkleR = 14;

        static readonly Color Brass = new Color(0.78f, 0.62f, 0.3f);
        static readonly Color Steel = new Color(0.72f, 0.73f, 0.75f);
        static readonly Color Iron = new Color(0.33f, 0.32f, 0.31f);
        static readonly Color Walnut = new Color(0.33f, 0.2f, 0.11f);
        static readonly Color GripLeather = new Color(0.16f, 0.1f, 0.07f);

        // ---------- placing things on a realistic body ----------

        /// <summary>Where a bone sat when the body was built, in the body's own space.</summary>
        public static Vector3 BonePos(CharacterRig rig, int bone)
        {
            return rig.Fit.BindPoses[bone].inverse.MultiplyPoint3x4(Vector3.zero);
        }

        /// <summary>
        /// The point on the skin at height y (metres) and sideways offset x, on the front (or back) of the given regions.
        /// Returns false if no skin is near.
        /// </summary>
        public static bool SkinPoint(CharacterRig rig, float y, float x, bool front, out Vector3 point, params BodyRegion[] regions)
        {
            var fit = rig.Fit;
            point = Vector3.zero;
            if (fit == null) return false;
            var verts = fit.Mesh.vertices;
            float best = front ? float.MinValue : float.MaxValue;
            float tol = 0.03f * fit.Scale;
            bool found = false;
            for (int i = 0; i < verts.Length; i++)
            {
                var p = verts[i];
                if (Mathf.Abs(p.y - y) > tol || Mathf.Abs(p.x - x) > tol) continue;
                if (regions.Length > 0 && System.Array.IndexOf(regions, (BodyRegion)fit.Regions[i]) < 0) continue;
                if (front ? p.z > best : p.z < best) { best = p.z; point = p; found = true; }
            }
            return found;
        }

        /// <summary>Attaches a mesh to a bone at a point given in the body's space, so it moves with that bone.</summary>
        public static GameObject Attach(CharacterRig rig, int bone, Vector3 bodyPoint, Mesh mesh, Vector3 size, Quaternion bodyRotation, Color color, Surface surface, string name)
        {
            var b = rig.Fit.Bones[bone];
            var bind = rig.Fit.BindPoses[bone];
            var go = new GameObject(name);
            go.transform.SetParent(b, false);
            go.transform.localPosition = bind.MultiplyPoint3x4(bodyPoint);
            go.transform.localRotation = bind.rotation * bodyRotation;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Prim.Mat(color, surface);
            return go;
        }

        static Mesh Primitive(PrimitiveType type)
        {
            var key = "Prim" + type;
            return MeshKit.Cached(key, () =>
            {
                var tmp = GameObject.CreatePrimitive(type);
                var m = Object.Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
                Object.DestroyImmediate(tmp);
                return m;
            });
        }

        /// <summary>A row of buttons down the front of the chest, from waist to collar.</summary>
        public static List<GameObject> Buttons(CharacterRig rig, Color color, int count, float x = 0f, float lift = 0.004f)
        {
            var made = new List<GameObject>();
            if (rig.Fit == null) return made;
            float k = rig.Fit.Scale, h = rig.Height;
            for (int i = 0; i < count; i++)
            {
                float y = Mathf.Lerp(0.585f, 0.75f, i / Mathf.Max(1f, count - 1f)) * h;
                Vector3 p;
                if (!SkinPoint(rig, y, x * k, true, out p, BodyRegion.Chest, BodyRegion.Pelvis)) continue;
                p.z += 0.016f * k + lift;
                made.Add(Attach(rig, BoneTorso, p, Primitive(PrimitiveType.Sphere), new Vector3(0.016f, 0.016f, 0.008f) * k, Quaternion.identity, color, Surface.Gold, "Button"));
            }
            return made;
        }

        /// <summary>A neckcloth (stock) wrapped at the throat, with its knot and falling ends.</summary>
        public static List<GameObject> Neckcloth(CharacterRig rig, Color color)
        {
            var made = new List<GameObject>();
            if (rig.Fit == null) return made;
            made.Add(HumanMesh.Garment(rig, color, 0.012f * rig.Fit.Scale, "Cravat", new[] { BodyRegion.Neck }));
            Vector3 p;
            if (SkinPoint(rig, 0.815f * rig.Height, 0f, true, out p, BodyRegion.Neck, BodyRegion.Chest))
            {
                float k = rig.Fit.Scale;
                made.Add(Attach(rig, BoneTorso, p + new Vector3(0f, -0.01f, 0.02f) * k, Primitive(PrimitiveType.Sphere), new Vector3(0.045f, 0.035f, 0.025f) * k, Quaternion.identity, color, Surface.Linen, "CravatKnot"));
                made.Add(Attach(rig, BoneTorso, p + new Vector3(0f, -0.06f, 0.018f) * k, Primitive(PrimitiveType.Cube), new Vector3(0.05f, 0.08f, 0.006f) * k, Quaternion.Euler(-12f, 0f, 0f), color, Surface.Linen, "CravatFall"));
            }
            return made;
        }

        /// <summary>A leather belt with a brass buckle at the waist.</summary>
        public static List<GameObject> Belt(CharacterRig rig, Color leather, float height = 0.565f)
        {
            var made = new List<GameObject>();
            if (rig.Fit == null) return made;
            float k = rig.Fit.Scale, y = height * rig.Height, band = 0.025f * k;
            made.Add(HumanMesh.Garment(rig, leather, 0.02f * k, "Belt", new[] { BodyRegion.Pelvis, BodyRegion.Chest }, p => Mathf.Abs(p.y - y) < band));
            Vector3 front;
            if (SkinPoint(rig, y, 0f, true, out front, BodyRegion.Pelvis, BodyRegion.Chest))
                made.Add(Attach(rig, BoneTorso, front + new Vector3(0f, 0f, 0.024f * k), Primitive(PrimitiveType.Cube), new Vector3(0.045f, 0.04f, 0.01f) * k, Quaternion.identity, Brass, Surface.Gold, "Buckle"));
            return made;
        }

        /// <summary>Broad turned-back cuffs at the wrists of a coat.</summary>
        public static List<GameObject> Cuffs(CharacterRig rig, Color color)
        {
            var made = new List<GameObject>();
            if (rig.Fit == null) return made;
            float k = rig.Fit.Scale;
            var wl = BonePos(rig, BoneWristL); var wr = BonePos(rig, BoneWristR);
            float reach = 0.1f * k;
            made.Add(HumanMesh.Garment(rig, color, 0.03f * k, "CoatCuff", new[] { BodyRegion.Forearm },
                p => Mathf.Min((p - wl).magnitude, (p - wr).magnitude) < reach && Mathf.Min((p - wl).magnitude, (p - wr).magnitude) > reach * 0.25f));
            return made;
        }

        /// <summary>Shoe buckles, the mark of a respectable 18th-century foot.</summary>
        public static List<GameObject> ShoeBuckles(CharacterRig rig, Color color)
        {
            var made = new List<GameObject>();
            if (rig.Fit == null) return made;
            float k = rig.Fit.Scale;
            foreach (var side in new[] { -1, 1 })
            {
                var ankle = BonePos(rig, side < 0 ? BoneAnkleL : BoneAnkleR);
                Vector3 p;
                if (!SkinPoint(rig, ankle.y - 0.015f * k, ankle.x + 0.0f, true, out p, BodyRegion.Foot)) continue;
                made.Add(Attach(rig, side < 0 ? BoneAnkleL : BoneAnkleR, p + new Vector3(0f, 0.012f, 0.012f) * k, Primitive(PrimitiveType.Cube),
                    new Vector3(0.035f, 0.006f, 0.028f) * k, Quaternion.Euler(-25f, 0f, 0f), color, Surface.Gold, "ShoeBuckle"));
            }
            return made;
        }

        /// <summary>A linen cap (mob cap) gathered over the hair.</summary>
        public static GameObject MobCap(CharacterRig rig, Color color)
        {
            var fit = rig.Fit;
            if (fit == null) return null;
            var eye = (fit.EyeL + fit.EyeR) * 0.5f;
            float k = fit.Scale;
            return HumanMesh.Garment(rig, color, 0.022f * k, "Cap", new[] { BodyRegion.Head }, p =>
            {
                float back = Mathf.Max(0f, eye.z - 0.03f * k - p.z);
                return p.y > eye.y + 0.035f * k - back * 1.1f;
            });
        }

        /// <summary>A hood and veil (for the drowned abbess) that frames the face and falls over the shoulders.</summary>
        public static GameObject Hood(CharacterRig rig, Color color)
        {
            var fit = rig.Fit;
            if (fit == null) return null;
            var eye = (fit.EyeL + fit.EyeR) * 0.5f;
            float k = fit.Scale;
            return HumanMesh.Garment(rig, color, 0.03f * k, "Hood", new[] { BodyRegion.Head, BodyRegion.Neck, BodyRegion.Chest }, p =>
            {
                bool face = p.z > eye.z - 0.05f * k && p.y < eye.y + 0.05f * k && p.y > eye.y - 0.12f * k;
                return !face && p.y > eye.y - 0.3f * k;
            });
        }

        /// <summary>A cape hanging from the shoulders down the back.</summary>
        public static GameObject Cape(CharacterRig rig, Color color)
        {
            if (rig.Fit == null) return null;
            float k = rig.Fit.Scale, top = 0.83f * rig.Height, bottom = 0.18f * rig.Height;
            var torso = BonePos(rig, BoneTorso);
            return HumanMesh.Garment(rig, color, 0.045f * k, "Cape",
                new[] { BodyRegion.Chest, BodyRegion.Pelvis, BodyRegion.Thigh, BodyRegion.Shin, BodyRegion.UpperArm },
                p => p.z < torso.z - 0.02f * k && p.y < top && p.y > bottom);
        }

        /// <summary>Rounded armour plates over both shoulders, moving with the arms.</summary>
        public static void Pauldrons(CharacterRig rig, Color color, Surface surface, float size = 1f)
        {
            if (rig.Fit == null) return;
            float k = rig.Fit.Scale;
            foreach (var bone in new[] { BoneArmL, BoneArmR })
            {
                var p = BonePos(rig, bone) + new Vector3(Mathf.Sign(BonePos(rig, bone).x) * 0.03f, 0.04f, 0f) * k;
                Attach(rig, bone, p, Primitive(PrimitiveType.Sphere), new Vector3(0.2f, 0.12f, 0.19f) * k * size, Quaternion.Euler(0f, 0f, Mathf.Sign(p.x) * -20f), color, surface, "Pauldron");
                Attach(rig, bone, p + new Vector3(Mathf.Sign(p.x) * 0.04f, -0.05f, 0f) * k, Primitive(PrimitiveType.Sphere), new Vector3(0.16f, 0.08f, 0.16f) * k * size,
                    Quaternion.Euler(0f, 0f, Mathf.Sign(p.x) * -35f), color * 0.85f, surface, "Pauldron");
            }
        }

        // ---------- villagers ----------

        /// <summary>
        /// Everyday 18th-century dress with period details. Men: shirt, neckcloth, buttoned waistcoat, breeches,
        /// stockings, buckled shoes, and sometimes a long coat with cuffs or a tricorne. Women: shift, stays, gown,
        /// apron, kerchief and often a linen cap.
        /// </summary>
        public static void DressVillager(CharacterRig rig, Color main, bool feminine)
        {
            var linen = new Color(0.92f, 0.9f, 0.84f);
            var shoes = new Color(0.12f, 0.09f, 0.07f);
            var stockings = Random.value < 0.7f ? new Color(0.88f, 0.86f, 0.82f) : Prim.Vary(new Color(0.35f, 0.33f, 0.3f), 0.2f);
            float k = rig.Fit != null ? rig.Fit.Scale : 1f, h = rig.Height;

            rig.Cover(linen, 0.006f, "Shirt", BodyRegion.Chest, BodyRegion.UpperArm, BodyRegion.Forearm);
            rig.Cover(stockings, 0.004f, "Stockings", BodyRegion.Shin);
            rig.Cover(shoes, 0.007f, "Shoes", BodyRegion.Foot);
            if (rig.Fit == null)
            {
                if (feminine) { rig.Cover(main, 0.013f, "Bodice", BodyRegion.Chest, BodyRegion.Pelvis); Prim.Shape(BodyMesh.Get("Gown"), rig.Torso, Vector3.zero, rig.TrunkScale, main * 0.85f, "Gown"); }
                else { rig.Cover(main, 0.013f, "Waistcoat", BodyRegion.Chest); rig.Cover(main * 0.7f, 0.008f, "Breeches", BodyRegion.Pelvis, BodyRegion.Thigh); }
                rig.CollectRenderers();
                return;
            }

            if (feminine)
            {
                var waist = 0.555f * h;
                HumanMesh.Garment(rig, main, 0.014f * k, "Bodice", new[] { BodyRegion.Chest, BodyRegion.Pelvis }, p => p.y > waist && p.y < 0.79f * h);
                var gownColor = Random.value < 0.5f ? main * 0.85f : Prim.Vary(main, 0.25f);
                var gown = Prim.Shape(BodyMesh.Get("Gown"), rig.Torso, Vector3.zero, rig.TrunkScale, gownColor, "Gown");
                gown.GetComponent<Renderer>().sharedMaterial = Prim.Mat(gownColor, Surface.Wool);
                Apron(rig, gown.GetComponent<MeshFilter>().sharedMesh, rig.TrunkScale, Random.value < 0.75f ? linen : Prim.Vary(new Color(0.6f, 0.55f, 0.45f), 0.1f));
                if (Random.value < 0.7f)
                    HumanMesh.Garment(rig, Random.value < 0.6f ? linen : Prim.Vary(new Color(0.7f, 0.55f, 0.45f), 0.2f), 0.022f * k, "Kerchief",
                        new[] { BodyRegion.Chest, BodyRegion.Neck, BodyRegion.UpperArm }, p => p.y > 0.75f * h && p.y < 0.84f * h);
                if (Random.value < 0.65f) MobCap(rig, linen);
            }
            else
            {
                var vest = main;
                HumanMesh.Garment(rig, vest, 0.013f * k, "Waistcoat", new[] { BodyRegion.Chest, BodyRegion.Pelvis }, p => p.y > 0.52f * h);
                Buttons(rig, Random.value < 0.5f ? Brass : new Color(0.6f, 0.6f, 0.62f), 6);
                var breeches = Random.value < 0.5f ? main * 0.7f : Prim.Vary(new Color(0.55f, 0.45f, 0.32f), 0.2f);
                breeches.a = 1f;
                rig.Cover(breeches, 0.009f, "Breeches", BodyRegion.Pelvis, BodyRegion.Thigh);
                Neckcloth(rig, linen);
                ShoeBuckles(rig, Brass);
                float roll = Random.value;
                if (roll < 0.45f)
                {
                    // A long coat worn open over the waistcoat.
                    var coat = Prim.Vary(Random.value < 0.5f ? new Color(0.25f, 0.22f, 0.2f) : main * 0.6f, 0.15f);
                    coat.a = 1f;
                    HumanMesh.Garment(rig, coat, 0.022f * k, "Coat", new[] { BodyRegion.Chest, BodyRegion.UpperArm, BodyRegion.Forearm, BodyRegion.Pelvis },
                        p => !(Mathf.Abs(p.x) < 0.07f * k && p.z > 0f) && p.y > 0.5f * h);
                    var tail = Prim.Shape(BodyMesh.Get("CoatTail"), rig.Torso, Vector3.zero, rig.TrunkScale * 1.06f, coat, "CoatTail");
                    tail.GetComponent<Renderer>().sharedMaterial = Prim.Mat(coat, Surface.Wool);
                    Cuffs(rig, coat * 0.85f);
                }
                else if (roll < 0.75f) Belt(rig, new Color(0.22f, 0.14f, 0.09f));
                if (Random.value < 0.35f)
                {
                    var felt = Random.value < 0.6f ? new Color(0.1f, 0.09f, 0.08f) : new Color(0.3f, 0.22f, 0.15f);
                    var hat = Prim.Shape(BodyMesh.Get("Tricorne"), rig.Skull, Vector3.zero, Vector3.one, felt, "Tricorne");
                    hat.GetComponent<Renderer>().sharedMaterial = Prim.Mat(felt, Surface.Wool);
                }
            }
            rig.CollectRenderers();
        }

        /// <summary>An apron: the front panel of the gown, pushed slightly forward and cut narrower.</summary>
        static void Apron(CharacterRig rig, Mesh gown, Vector3 gownScale, Color color)
        {
            if (gown == null) return;
            var src = gown.vertices; var nrm = gown.normals; var tris = gown.triangles;
            float halfW = 0.16f;
            var map = new Dictionary<int, int>();
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                bool keep = true;
                for (int c = 0; c < 3; c++)
                {
                    var p = Vector3.Scale(src[tris[i + c]], gownScale);
                    if (nrm[tris[i + c]].z < 0.35f || Mathf.Abs(p.x) > halfW * rig.Height / 1.8f) { keep = false; break; }
                }
                if (!keep) continue;
                for (int c = 0; c < 3; c++)
                {
                    int vi = tris[i + c], ni;
                    if (!map.TryGetValue(vi, out ni)) { ni = v.Count; map[vi] = ni; v.Add(src[vi] + nrm[vi] * 0.012f); }
                    t.Add(ni);
                }
            }
            if (t.Count == 0) return;
            var mesh = new Mesh { name = "Apron" };
            mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.RecalculateNormals();
            mesh.uv = Surfaces.BoxUV(mesh.vertices, mesh.normals, gownScale);
            mesh.RecalculateBounds();
            var go = new GameObject("Apron");
            go.transform.SetParent(rig.Torso, false);
            go.transform.localScale = gownScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Prim.Mat(color, Surface.Linen);
        }

        // ---------- creatures ----------

        /// <summary>Claws, spines and bony plates that make each creature type read clearly at a distance.</summary>
        public static void CreatureDetail(CharacterRig rig, BodyShape shape, Color skin)
        {
            var bone = new Color(0.86f, 0.82f, 0.7f);
            if (shape == BodyShape.Goblin || shape == BodyShape.Imp || shape == BodyShape.Brute)
                Surfaces.Swap(rig.GetComponentsInChildren<Renderer>(true), Surface.Skin, Surface.Scales);
            if (rig.Fit == null) return;
            float k = rig.Fit.Scale;
            var cone = BodyMesh.Get("Cone");

            if (shape == BodyShape.Goblin || shape == BodyShape.Imp || shape == BodyShape.Brute)
            {
                // Three hooked claws on each hand.
                foreach (var wrist in new[] { BoneWristL, BoneWristR })
                {
                    var w = BonePos(rig, wrist);
                    var elbow = BonePos(rig, wrist == BoneWristL ? BoneElbowL : BoneElbowR);
                    var down = (w - elbow).normalized;
                    for (int i = -1; i <= 1; i++)
                    {
                        var p = w + down * 0.095f * k + new Vector3(0f, 0f, i * 0.018f * k);
                        var rot = Quaternion.FromToRotation(Vector3.up, down) * Quaternion.Euler(i * 8f, 0f, 15f);
                        Attach(rig, wrist, p, cone, new Vector3(0.012f, 0.06f, 0.012f) * k, rot, bone, Surface.Carapace, "Claw");
                    }
                }
            }

            if (shape == BodyShape.Brute || (shape == BodyShape.Goblin && Random.value < 0.4f))
            {
                // A ridge of bony spines down the back.
                int spines = shape == BodyShape.Brute ? 6 : 4;
                for (int i = 0; i < spines; i++)
                {
                    float y = Mathf.Lerp(0.78f, 0.6f, i / (float)spines) * rig.Height;
                    Vector3 p;
                    if (!SkinPoint(rig, y, 0f, false, out p, BodyRegion.Chest, BodyRegion.Neck, BodyRegion.Pelvis)) continue;
                    Attach(rig, BoneTorso, p, cone, new Vector3(0.03f, Mathf.Lerp(0.09f, 0.05f, i / (float)spines), 0.03f) * k,
                        Quaternion.Euler(-120f, 0f, 0f), bone, Surface.Carapace, "Spine");
                }
            }
            if (shape == BodyShape.Brute) Pauldrons(rig, bone * 0.9f, Surface.Carapace, 0.9f);
            if (shape == BodyShape.Humanoid)
            {
                // The Hollowed: pale, sunken, with dark rings around the eyes.
                var fit = rig.Fit;
                foreach (var e in new[] { fit.EyeL, fit.EyeR })
                    Attach(rig, BoneHead, e + new Vector3(0f, 0f, 0.004f) * k, Primitive(PrimitiveType.Sphere), new Vector3(0.04f, 0.03f, 0.02f) * k, Quaternion.identity,
                        new Color(0.12f, 0.1f, 0.1f), Surface.Plain, "Socket");
            }
        }

        // ---------- bosses ----------

        /// <summary>Crowns, helms, capes, armour and a signature weapon for each boss, plus a glow in its eye colour.</summary>
        public static void BossRegalia(CharacterRig rig, BossDef boss)
        {
            var eye = EnumUtil.ParseColor(boss.eyeColor, Color.red);
            var body = EnumUtil.ParseColor(boss.color, Color.gray);
            float s = boss.height / 1.8f;
            if (rig.Fit != null)
            {
                switch (boss.id)
                {
                    case "gnarl_matriarch":
                        Hood(rig, new Color(0.24f, 0.22f, 0.17f));
                        Belt(rig, new Color(0.2f, 0.15f, 0.1f), 0.55f);
                        break;
                    case "frost_warden":
                        Crown(rig, new Color(0.75f, 0.9f, 1f), Surface.Wet, 9, 0.07f);
                        Pauldrons(rig, new Color(0.55f, 0.65f, 0.75f), Surface.Metal, 1.1f);
                        Cape(rig, new Color(0.3f, 0.38f, 0.48f));
                        break;
                    case "abbess_of_mire":
                        Hood(rig, new Color(0.12f, 0.14f, 0.13f));
                        Cape(rig, new Color(0.14f, 0.17f, 0.15f));
                        break;
                    case "forge_tyrant":
                        Helm(rig, Iron);
                        Pauldrons(rig, Iron, Surface.Metal, 1.2f);
                        HumanMesh.Garment(rig, new Color(0.25f, 0.16f, 0.1f), 0.03f * rig.Fit.Scale, "LeatherApron",
                            new[] { BodyRegion.Chest, BodyRegion.Pelvis, BodyRegion.Thigh }, p => p.z > 0f && Mathf.Abs(p.x) < 0.17f * rig.Fit.Scale && p.y < 0.78f * rig.Height);
                        break;
                    case "storm_colonel":
                    {
                        var coat = new Color(0.12f, 0.14f, 0.28f);
                        HumanMesh.Garment(rig, coat, 0.025f * rig.Fit.Scale, "Coat", new[] { BodyRegion.Chest, BodyRegion.UpperArm, BodyRegion.Forearm, BodyRegion.Pelvis }, p => p.y > 0.5f * rig.Height);
                        Buttons(rig, Brass, 8, 0.05f); Buttons(rig, Brass, 8, -0.05f);
                        Cuffs(rig, new Color(0.6f, 0.1f, 0.1f));
                        Pauldrons(rig, Brass, Surface.Gold, 0.6f); // epaulettes
                        var hat = Prim.Shape(BodyMesh.Get("Tricorne"), rig.Skull, Vector3.zero, Vector3.one * 1.05f, new Color(0.08f, 0.08f, 0.1f), "Tricorne");
                        hat.GetComponent<Renderer>().sharedMaterial = Prim.Mat(new Color(0.08f, 0.08f, 0.1f), Surface.Wool);
                        break;
                    }
                    case "hollow_king":
                        Crown(rig, Brass, Surface.Gold, 7, 0.1f);
                        Cape(rig, new Color(0.35f, 0.04f, 0.05f));
                        Pauldrons(rig, new Color(0.2f, 0.18f, 0.18f), Surface.Metal, 1.15f);
                        break;
                    default:
                        Cape(rig, body * 0.5f);
                        break;
                }
            }

            // A glow around the boss in its eye colour.
            var glow = Prim.PointLight(rig.Torso != null ? rig.Torso : rig.transform, Vector3.up * 0.5f * s, eye, boss.height * 1.6f, 1.4f);
            glow.gameObject.AddComponent<Flicker>().Amount = 0.1f;

            if (rig.Hand != null) BossWeapon(boss.id, rig.Hand, s, eye);
            rig.CollectRenderers();
        }

        static void Crown(CharacterRig rig, Color color, Surface surface, int points, float spike)
        {
            var fit = rig.Fit;
            var eye = (fit.EyeL + fit.EyeR) * 0.5f;
            float k = fit.Scale;
            var p = new Vector3(0f, eye.y + 0.07f * k, eye.z - 0.085f * k);
            Attach(rig, BoneHead, p, MeshKit.Crown(0.1f, 0.035f, spike, points), Vector3.one * k, Quaternion.Euler(-8f, 0f, 0f), color, surface, "Crown");
        }

        static void Helm(CharacterRig rig, Color color)
        {
            var helm = Prim.Shape(BodyMesh.Get("Cap"), rig.Skull, Vector3.zero, Vector3.one * 1.08f, color, "Helm");
            helm.GetComponent<Renderer>().sharedMaterial = Prim.Mat(color, Surface.Metal);
            var visor = Prim.Make(PrimitiveType.Cube, rig.Skull, new Vector3(0f, 0.135f, 0.12f), new Vector3(0.17f, 0.025f, 0.02f), new Color(0.05f, 0.05f, 0.05f), "Visor");
            visor.GetComponent<Renderer>().sharedMaterial = Prim.Mat(new Color(0.05f, 0.05f, 0.05f), Surface.Metal);
        }

        static void BossWeapon(string id, Transform hand, float s, Color eye)
        {
            var root = new GameObject("BossWeapon").transform;
            root.SetParent(hand, false);
            root.localRotation = Quaternion.Euler(90f, 0f, 0f);
            root.localScale = Vector3.one * s;
            switch (id)
            {
                case "gnarl_matriarch": // a coffin lid used as a club
                    Part(root, MeshKit.Bar("club", new Vector4(-0.1f, 0.025f, 0.025f, 0f), new Vector4(0.15f, 0.03f, 0.03f, 0f), new Vector4(0.25f, 0.12f, 0.03f, 0f), new Vector4(1.1f, 0.17f, 0.035f, 0f), new Vector4(1.25f, 0.1f, 0.035f, 0f)),
                        Vector3.zero, Quaternion.identity, Vector3.one, new Color(0.3f, 0.22f, 0.15f), Surface.Wood, "CoffinLid");
                    for (int i = 0; i < 3; i++)
                        Part(root, Primitive(PrimitiveType.Cube), new Vector3(0f, 0.45f + i * 0.28f, 0f), Quaternion.identity, new Vector3(0.36f, 0.04f, 0.08f), Iron, Surface.Metal, "Band");
                    break;
                case "frost_warden": // a frozen pick-hammer
                    Shaft(root, 1.5f, 0.035f);
                    Part(root, MeshKit.Wedge(0.35f, 0.12f, 0.03f, 0.1f, 1f), new Vector3(0.04f, 1.35f, 0f), Quaternion.Euler(0f, 0f, -10f), Vector3.one, new Color(0.7f, 0.85f, 0.95f), Surface.Wet, "Pick");
                    Part(root, Primitive(PrimitiveType.Cube), new Vector3(-0.12f, 1.35f, 0f), Quaternion.identity, new Vector3(0.22f, 0.16f, 0.16f), Steel, Surface.Metal, "HammerHead");
                    break;
                case "abbess_of_mire": // a censer on a chain, smoking green
                    for (int i = 0; i < 6; i++)
                        Part(root, Primitive(PrimitiveType.Capsule), new Vector3(0f, i * 0.1f, 0f), Quaternion.Euler(0f, i % 2 == 0 ? 0f : 90f, 0f), new Vector3(0.03f, 0.05f, 0.015f), Iron, Surface.Metal, "Chain");
                    Part(root, Primitive(PrimitiveType.Sphere), new Vector3(0f, 0.75f, 0f), Quaternion.identity, Vector3.one * 0.22f, Brass * 0.7f, Surface.Gold, "Censer");
                    Prim.Make(PrimitiveType.Sphere, root, new Vector3(0f, 0.75f, 0f), Vector3.one * 0.14f, eye, "CenserGlow", false, true);
                    Prim.PointLight(root, new Vector3(0f, 0.75f, 0f), eye, 6f, 1.5f).gameObject.AddComponent<Flicker>();
                    break;
                case "forge_tyrant": // an anvil swung on a chain
                    for (int i = 0; i < 7; i++)
                        Part(root, Primitive(PrimitiveType.Capsule), new Vector3(0f, i * 0.11f, 0f), Quaternion.Euler(0f, i % 2 == 0 ? 0f : 90f, 0f), new Vector3(0.04f, 0.06f, 0.02f), Iron, Surface.Metal, "Chain");
                    Part(root, Primitive(PrimitiveType.Cube), new Vector3(0f, 0.95f, 0f), Quaternion.identity, new Vector3(0.22f, 0.2f, 0.32f), Iron, Surface.Metal, "Anvil");
                    Part(root, Primitive(PrimitiveType.Cube), new Vector3(0f, 1.08f, 0.05f), Quaternion.identity, new Vector3(0.3f, 0.08f, 0.5f), Iron, Surface.Metal, "AnvilFace");
                    Part(root, BodyMesh.Get("Cone"), new Vector3(0f, 1.08f, 0.3f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.12f, 0.2f, 0.07f), Iron, Surface.Metal, "AnvilHorn");
                    Prim.Make(PrimitiveType.Cube, root, new Vector3(0f, 1.125f, 0.05f), new Vector3(0.24f, 0.012f, 0.4f), new Color(1f, 0.45f, 0.1f), "HotIron", false, true);
                    break;
                case "storm_colonel": // a lightning-struck halberd
                    Shaft(root, 2.1f, 0.03f);
                    Part(root, MeshKit.Blade(0.4f, 0.07f, 0.02f, 0f, false, false), new Vector3(0f, 2.05f, 0f), Quaternion.identity, Vector3.one, Steel, Surface.Metal, "Spike");
                    Part(root, MeshKit.Wedge(0.25f, 0.06f, 0.32f, 0.03f, 0.6f), new Vector3(0.02f, 1.85f, 0f), Quaternion.identity, Vector3.one, Steel, Surface.Metal, "AxeBlade");
                    Prim.Make(PrimitiveType.Cube, root, new Vector3(0f, 1.6f, 0.02f), new Vector3(0.012f, 0.5f, 0.012f), eye * 1.5f, "Spark", false, true);
                    break;
                default: // the Hollow King's crown-spiked greatblade
                    Part(root, MeshKit.Bar("kinggrip", new Vector4(-0.25f, 0.025f, 0.025f, 0f), new Vector4(0.12f, 0.022f, 0.022f, 0f)), Vector3.zero, Quaternion.identity, Vector3.one, GripLeather, Surface.Leather, "Grip");
                    Part(root, Primitive(PrimitiveType.Cube), new Vector3(0f, 0.14f, 0f), Quaternion.identity, new Vector3(0.42f, 0.06f, 0.06f), Brass * 0.6f, Surface.Gold, "Guard");
                    Part(root, MeshKit.Blade(1.6f, 0.14f, 0.035f, 0f, false, false), new Vector3(0f, 0.17f, 0f), Quaternion.identity, Vector3.one, new Color(0.25f, 0.24f, 0.25f), Surface.Metal, "Blade");
                    for (int i = 0; i < 4; i++)
                        Part(root, BodyMesh.Get("Cone"), new Vector3(0.07f, 0.4f + i * 0.3f, 0f), Quaternion.Euler(0f, 0f, -70f), new Vector3(0.03f, 0.09f, 0.02f), Brass * 0.6f, Surface.Gold, "CrownSpike");
                    Prim.Make(PrimitiveType.Cube, root, new Vector3(0f, 0.9f, 0f), new Vector3(0.012f, 1.3f, 0.037f), eye, "BladeGlow", false, true);
                    break;
            }
        }

        // ---------- weapons ----------

        static GameObject Part(Transform parent, Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale, Color color, Surface surface, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Prim.Mat(color, surface);
            return go;
        }

        static void Shaft(Transform root, float length, float radius)
        {
            Part(root, MeshKit.Bar("shaft" + length.ToString("0.00") + radius.ToString("0.000"), new Vector4(-0.35f, radius, radius, 0f), new Vector4(length - 0.35f, radius * 0.9f, radius * 0.9f, 0f)),
                Vector3.zero, Quaternion.identity, Vector3.one, Walnut, Surface.Wood, "Shaft");
        }

        static void Hilt(Transform root, Color guardColor, bool basket, bool knuckleBow, float guardWidth)
        {
            Part(root, MeshKit.Bar("grip", new Vector4(-0.07f, 0.016f, 0.014f, 0f), new Vector4(0.03f, 0.019f, 0.016f, 0f), new Vector4(0.12f, 0.015f, 0.013f, 0f)),
                Vector3.zero, Quaternion.identity, Vector3.one, GripLeather, Surface.Leather, "Grip");
            Part(root, Primitive(PrimitiveType.Sphere), new Vector3(0f, -0.085f, 0f), Quaternion.identity, new Vector3(0.04f, 0.035f, 0.04f), guardColor, Surface.Gold, "Pommel");
            Part(root, MeshKit.Bar("guard" + guardWidth.ToString("0.00"), new Vector4(-guardWidth * 0.5f, 0.008f, 0.008f, 0f), new Vector4(0f, 0.012f, 0.012f, 0f), new Vector4(guardWidth * 0.5f, 0.008f, 0.008f, 0f)),
                new Vector3(0f, 0.135f, 0f), Quaternion.Euler(0f, 0f, 90f), Vector3.one, guardColor, Surface.Gold, "Guard");
            if (knuckleBow)
                Part(root, MeshKit.Bar("bow", new Vector4(-0.1f, 0.004f, 0.004f, 0f), new Vector4(0.1f, 0.004f, 0.004f, 0f)),
                    new Vector3(0.045f, 0.03f, 0f), Quaternion.Euler(0f, 0f, 8f), Vector3.one, guardColor, Surface.Gold, "KnuckleBow");
            if (basket)
                Part(root, Primitive(PrimitiveType.Sphere), new Vector3(0f, 0.08f, 0f), Quaternion.identity, new Vector3(0.11f, 0.12f, 0.1f), guardColor, Surface.Metal, "BasketGuard");
        }

        /// <summary>A detailed model for a held weapon, chosen from its id and shape data.</summary>
        public static GameObject Weapon(WeaponDef def, Transform hand)
        {
            var root = new GameObject("Weapon_" + def.id).transform;
            root.SetParent(hand, false);
            root.localRotation = Quaternion.Euler(90f, 0f, 0f);
            string id = def.id ?? "";
            var tint = def.Tint;
            float len = Mathf.Max(0.3f, def.length);

            if (def.Kind == WeaponKind.Firearm) { Firearm(root, def, len); return root.gameObject; }

            bool pole = len > 1.6f;
            bool axe = id.Contains("axe") || id.Contains("tomahawk");
            bool cleaver = id.Contains("cleaver");
            bool hook = id.Contains("hook");
            if (pole)
            {
                Shaft(root, len, 0.022f);
                Part(root, Primitive(PrimitiveType.Cylinder), new Vector3(0f, len - 0.42f, 0f), Quaternion.identity, new Vector3(0.05f, 0.04f, 0.05f), Iron, Surface.Metal, "Socket");
                Part(root, MeshKit.Blade(0.32f, 0.06f, 0.018f, 0f, false, false), new Vector3(0f, len - 0.4f, 0f), Quaternion.identity, Vector3.one, tint, Surface.Metal, "Spearhead");
                if (id.Contains("halberd") || id.Contains("glaive"))
                    Part(root, MeshKit.Wedge(0.2f, 0.05f, 0.26f, 0.025f, 0.6f), new Vector3(0.015f, len - 0.5f, 0f), Quaternion.identity, Vector3.one, tint, Surface.Metal, "AxeBlade");
                else
                    Part(root, MeshKit.Bar("spontoonbar", new Vector4(-0.07f, 0.008f, 0.008f, 0f), new Vector4(0.07f, 0.008f, 0.008f, 0f)), new Vector3(0f, len - 0.41f, 0f), Quaternion.Euler(0f, 0f, 90f), Vector3.one, tint, Surface.Metal, "Crossbar");
                if (id.Contains("ember")) Prim.Make(PrimitiveType.Cube, root, new Vector3(0f, len - 0.25f, 0f), new Vector3(0.01f, 0.25f, 0.02f), new Color(1f, 0.45f, 0.1f), "Ember", false, true);
                return root.gameObject;
            }
            if (axe || cleaver)
            {
                Shaft(root, axe ? len + 0.25f : 0.5f, 0.018f);
                float headY = axe ? len - 0.15f : 0.12f;
                if (cleaver)
                    Part(root, MeshKit.Wedge(0.16f, 0.5f, 0.6f, 0.03f, 1f), new Vector3(0.01f, headY + 0.3f, 0f), Quaternion.identity, Vector3.one, new Color(0.85f, 0.8f, 0.68f), Surface.Carapace, "BoneCleaver");
                else
                {
                    Part(root, MeshKit.Wedge(id.Contains("boarding") ? 0.16f : 0.12f, 0.05f, id.Contains("boarding") ? 0.2f : 0.13f, 0.03f, 0.7f), new Vector3(0.015f, headY, 0f), Quaternion.identity, Vector3.one, tint, Surface.Metal, "AxeHead");
                    Part(root, BodyMesh.Get("Cone"), new Vector3(-0.015f, headY, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.02f, 0.06f, 0.02f), tint, Surface.Metal, "Spike");
                }
                return root.gameObject;
            }

            // Swords and knives.
            bool curved = id.Contains("sabre") || id.Contains("cutlass") || id.Contains("hanger") || hook;
            bool great = def.Weight == WeightClass.Great;
            bool light = def.Weight == WeightClass.Light;
            float width = great ? 0.06f : light ? 0.022f : 0.036f;
            if (id.Contains("smallsword")) width = 0.016f;
            if (id.Contains("broadsword") || id.Contains("claymore")) width = 0.05f;
            if (id.Contains("executioner")) width = 0.075f;
            if (id.Contains("cutlass")) width = 0.045f;
            bool basket = id.Contains("broadsword");
            bool bow = id.Contains("sabre") || id.Contains("cutlass") || id.Contains("smallsword") || id.Contains("hanger");
            float guardW = great ? 0.3f : id.Contains("dirk") ? 0.05f : 0.12f;
            Hilt(root, id.Contains("dirk") ? Walnut : Brass, basket, bow, guardW);
            float bladeLen = Mathf.Max(0.2f, len - 0.15f);
            Part(root, MeshKit.Blade(bladeLen, width, width * (id.Contains("smallsword") ? 0.5f : 0.22f), curved ? (hook ? 0.35f : 0.1f) : 0f, curved, id.Contains("executioner")),
                new Vector3(0f, 0.145f, 0f), Quaternion.identity, Vector3.one, tint, Surface.Metal, "Blade");
            if (great && !id.Contains("executioner")) // a claymore's quatrefoil guard ends
                for (int s = -1; s <= 1; s += 2)
                    Part(root, Primitive(PrimitiveType.Sphere), new Vector3(s * guardW * 0.5f, 0.135f, 0f), Quaternion.identity, Vector3.one * 0.03f, Brass, Surface.Gold, "GuardEnd");
            return root.gameObject;
        }

        static void Firearm(Transform root, WeaponDef def, float len)
        {
            bool pistol = len < 0.6f;
            bool blunder = def.pellets > 1;
            var wood = Walnut;
            if (pistol)
            {
                // Butt drops down and back from the hand; barrel runs forward.
                Part(root, MeshKit.Bar("pistolgrip", new Vector4(-0.04f, 0.018f, 0.022f, 0f), new Vector4(0.06f, 0.016f, 0.02f, 0f), new Vector4(0.3f, 0.016f, 0.017f, 0.012f)),
                    Vector3.zero, Quaternion.identity, Vector3.one, wood, Surface.Wood, "Stock");
                Part(root, MeshKit.Bar("pistolbutt", new Vector4(0f, 0.017f, 0.02f, 0f), new Vector4(0.1f, 0.02f, 0.028f, 0f), new Vector4(0.13f, 0.022f, 0.03f, 0f)),
                    new Vector3(0f, -0.02f, -0.02f), Quaternion.Euler(150f, 0f, 0f), Vector3.one, wood, Surface.Wood, "Butt");
                Part(root, Primitive(PrimitiveType.Sphere), new Vector3(0f, -0.13f, -0.075f), Quaternion.identity, new Vector3(0.042f, 0.035f, 0.05f), Brass, Surface.Gold, "ButtCap");
                Part(root, Primitive(PrimitiveType.Cylinder), new Vector3(0f, 0.2f, 0.022f), Quaternion.identity, new Vector3(0.022f, 0.16f, 0.022f), def.Tint, Surface.Metal, "Barrel");
            }
            else
            {
                // A long gun: butt against the shoulder behind the hand, stock and barrel forward.
                Part(root, MeshKit.Bar("longstock" + len.ToString("0.0"),
                        new Vector4(-0.38f, 0.022f, 0.06f, -0.04f), new Vector4(-0.12f, 0.018f, 0.03f, -0.01f), new Vector4(0.02f, 0.016f, 0.022f, 0f),
                        new Vector4(0.15f, 0.018f, 0.024f, 0.01f), new Vector4(len * 0.8f, 0.014f, 0.016f, 0.014f)),
                    Vector3.zero, Quaternion.identity, Vector3.one, wood, Surface.Wood, "Stock");
                Part(root, Primitive(PrimitiveType.Cylinder), new Vector3(0f, len * 0.52f, 0.03f), Quaternion.identity, new Vector3(0.024f, len * 0.5f, 0.024f), def.Tint, Surface.Metal, "Barrel");
                Part(root, Primitive(PrimitiveType.Cube), new Vector3(0f, -0.38f, -0.04f), Quaternion.identity, new Vector3(0.046f, 0.012f, 0.125f), Brass, Surface.Gold, "ButtPlate");
                for (int i = 0; i < 2; i++)
                    Part(root, Primitive(PrimitiveType.Cylinder), new Vector3(0f, len * (0.45f + i * 0.25f), 0.022f), Quaternion.identity, new Vector3(0.036f, 0.01f, 0.036f), Brass, Surface.Gold, "BarrelBand");
                if (blunder)
                    Part(root, BodyMesh.Get("Cone"), new Vector3(0f, len * 1.02f, 0.03f), Quaternion.Euler(180f, 0f, 0f), new Vector3(0.09f, 0.18f, 0.09f), Brass, Surface.Gold, "Bell");
            }
            // The flintlock: lock plate, cock with its flint, frizzen, and a brass trigger guard.
            float lockY = pistol ? 0.03f : 0.06f;
            Part(root, Primitive(PrimitiveType.Cube), new Vector3(0.022f, lockY, 0.012f), Quaternion.identity, new Vector3(0.006f, 0.09f, 0.025f), Steel, Surface.Metal, "LockPlate");
            Part(root, Primitive(PrimitiveType.Cube), new Vector3(0.026f, lockY - 0.03f, 0.035f), Quaternion.Euler(30f, 0f, 0f), new Vector3(0.008f, 0.035f, 0.012f), Steel, Surface.Metal, "Cock");
            Part(root, Primitive(PrimitiveType.Cube), new Vector3(0.026f, lockY + 0.02f, 0.032f), Quaternion.identity, new Vector3(0.008f, 0.012f, 0.025f), Steel, Surface.Metal, "Frizzen");
            Part(root, MeshKit.Bar("triggerguard", new Vector4(-0.05f, 0.004f, 0.003f, 0f), new Vector4(0.05f, 0.004f, 0.003f, 0f)),
                new Vector3(0f, 0.01f, -0.03f), Quaternion.identity, Vector3.one, Brass, Surface.Gold, "TriggerGuard");
            Part(root, Primitive(PrimitiveType.Cube), new Vector3(0f, 0.015f, -0.02f), Quaternion.Euler(-20f, 0f, 0f), new Vector3(0.004f, 0.006f, 0.022f), Steel, Surface.Metal, "Trigger");
        }
    }
}
