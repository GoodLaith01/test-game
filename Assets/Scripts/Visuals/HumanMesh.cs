using System.Collections.Generic;
using UnityEngine;

namespace Hollowmark.Visuals
{
    /// <summary>
    /// Realistic human bodies from the MakeHuman base mesh (CC0, see Assets/Resources/Humans/LICENSE.txt).
    /// The sculpted mesh, its professional skin weights and several body shapes (slim, average, muscular,
    /// giant, female) are stored in Resources/Humans as compact .bytes files, and are skinned at runtime to the
    /// same skeleton the game animates, so walking, attacking and dying work exactly as before.
    /// </summary>
    public static class HumanMesh
    {
        // Bone order in the data files.
        const int Body = 0, Torso = 1, HeadBone = 2, ArmL = 3, ElbowL = 4, WristL = 5, ArmR = 6, ElbowR = 7, WristR = 8,
            LegL = 9, KneeL = 10, AnkleL = 11, LegR = 12, KneeR = 13, AnkleR = 14, BoneCount = 15;

        public const string Slim = "male_slim", Average = "male", Muscular = "male_muscular", Giant = "male_giant",
            Female = "female", FemaleSlim = "female_slim";

        class Topology
        {
            public int VertexCount;
            public int[] SkinTris, EyeTris;
            public BoneWeight[] Weights;
            public byte[] Regions;
        }

        class Shape
        {
            public Vector3[] Vertices;
            public Vector3[] Joints = new Vector3[BoneCount];
            public Vector3 GripL, GripR, EyeL, EyeR, Mouth, HeadTop, Skull;
            public float Height;
        }

        /// <summary>What a built body needs to fit garments later.</summary>
        public class Fit
        {
            public Mesh Mesh;
            public Transform[] Bones;
            public Matrix4x4[] BindPoses;
            public Vector3[] Normals;
            public byte[] Regions;
            public Transform Root;
            public string Key;
            public Vector3 EyeL, EyeR; // eyeball centres, in the root's space
            public float Scale;
        }

        static Topology topology;
        static bool missing;
        static readonly Dictionary<string, Shape> shapes = new Dictionary<string, Shape>();
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        /// <summary>True when the body data is in the project (otherwise the game falls back to the simple bodies).</summary>
        public static bool Available { get { return LoadTopology() != null; } }

        static Topology LoadTopology()
        {
            if (topology != null || missing) return topology;
            var asset = Resources.Load<TextAsset>("Humans/topology");
            if (asset == null) { missing = true; Debug.LogWarning("[Hollowmark] Resources/Humans/topology.bytes not found, using simple bodies."); return null; }
            var r = new Reader(asset.bytes);
            var t = new Topology();
            t.VertexCount = r.Int();
            int skin = r.Int(), eyes = r.Int();
            t.SkinTris = r.Ints(skin * 3);
            t.EyeTris = r.Ints(eyes * 3);
            var idx = r.Bytes(t.VertexCount * 4);
            var w = r.Floats(t.VertexCount * 4);
            t.Weights = new BoneWeight[t.VertexCount];
            for (int i = 0; i < t.VertexCount; i++)
                t.Weights[i] = new BoneWeight
                {
                    boneIndex0 = idx[i * 4], boneIndex1 = idx[i * 4 + 1], boneIndex2 = idx[i * 4 + 2], boneIndex3 = idx[i * 4 + 3],
                    weight0 = w[i * 4], weight1 = w[i * 4 + 1], weight2 = w[i * 4 + 2], weight3 = w[i * 4 + 3],
                };
            t.Regions = r.Bytes(t.VertexCount);
            topology = t;
            return t;
        }

        static Shape LoadShape(string name)
        {
            Shape s;
            if (shapes.TryGetValue(name, out s)) return s;
            var asset = Resources.Load<TextAsset>("Humans/" + name);
            if (asset == null) return null;
            var r = new Reader(asset.bytes);
            s = new Shape();
            int n = r.Int();
            s.Vertices = new Vector3[n];
            for (int i = 0; i < n; i++) s.Vertices[i] = r.Vec();
            for (int i = 0; i < BoneCount; i++) s.Joints[i] = r.Vec();
            s.GripL = r.Vec(); s.GripR = r.Vec(); s.EyeL = r.Vec(); s.EyeR = r.Vec(); s.Mouth = r.Vec(); s.HeadTop = r.Vec(); s.Skull = r.Vec();
            s.Height = s.HeadTop.y;
            shapes[name] = s;
            return s;
        }

        /// <summary>
        /// Builds a realistic body of the given shape and height. Returns null if the data is missing.
        /// headScale enlarges the head (goblins), trunk widens the torso (brutes), hunch bends the back.
        /// </summary>
        public static CharacterRig Build(Transform parent, string objectName, string shapeName, float height, Color skin,
            float headScale = 1f, Vector3? trunk = null, float hunch = 0f)
        {
            var topo = LoadTopology();
            var shape = topo == null ? null : LoadShape(shapeName);
            if (shape == null) return null;
            float k = height / shape.Height;

            var root = new GameObject(objectName);
            root.transform.SetParent(parent, false);
            var rig = root.AddComponent<CharacterRig>();
            rig.Height = height;
            rig.ArmRoll = 8f;

            // Skeleton in the sculpt's own pose (an A-pose), placed on the sculpt's joints.
            var bones = new Transform[BoneCount];
            var world = new Vector3[BoneCount];
            for (int i = 0; i < BoneCount; i++) world[i] = shape.Joints[i] * k;
            string[] names = { "Body", "Torso", "Head", "ArmL", "ElbowL", "WristL", "ArmR", "ElbowR", "WristR", "LegL", "KneeL", "AnkleL", "LegR", "KneeR", "AnkleR" };
            int[] parents = { -1, Body, Torso, Torso, ArmL, ElbowL, Torso, ArmR, ElbowR, Body, LegL, KneeL, Body, LegR, KneeR };
            var worldRot = new Quaternion[BoneCount];
            for (int i = 0; i < BoneCount; i++) worldRot[i] = Quaternion.identity;
            // Limbs point their local -y down the bone, so the animation's angles mean the same as on the simple rig.
            AimDown(worldRot, world, ArmL, ElbowL); AimDown(worldRot, world, ElbowL, WristL);
            AimDown(worldRot, world, ArmR, ElbowR); AimDown(worldRot, world, ElbowR, WristR);
            AimDown(worldRot, world, LegL, KneeL); AimDown(worldRot, world, KneeL, AnkleL);
            AimDown(worldRot, world, LegR, KneeR); AimDown(worldRot, world, KneeR, AnkleR);
            worldRot[WristL] = worldRot[ElbowL]; worldRot[WristR] = worldRot[ElbowR];

            for (int i = 0; i < BoneCount; i++)
            {
                var t = new GameObject(names[i]).transform;
                if (parents[i] < 0) { t.SetParent(root.transform, false); t.localPosition = world[i]; t.localRotation = worldRot[i]; }
                else
                {
                    t.SetParent(bones[parents[i]], false);
                    var inv = Quaternion.Inverse(worldRot[parents[i]]);
                    t.localPosition = inv * (world[i] - world[parents[i]]);
                    t.localRotation = inv * worldRot[i];
                }
                bones[i] = t;
            }

            var key = shapeName + "@" + Mathf.RoundToInt(k * 100f);
            var mesh = BodyMeshFor(topo, shape, k, key);

            var skinGo = new GameObject("Skin");
            skinGo.transform.SetParent(root.transform, false);
            var smr = skinGo.AddComponent<SkinnedMeshRenderer>();
            var bind = new Matrix4x4[BoneCount];
            for (int i = 0; i < BoneCount; i++) bind[i] = bones[i].worldToLocalMatrix * skinGo.transform.localToWorldMatrix;
            Setup(smr, mesh, bones, bind, height);
            smr.sharedMaterials = new[] { Prim.Mat(skin), Prim.Mat(new Color(0.95f, 0.94f, 0.92f)) };

            rig.Body = bones[Body]; rig.Torso = bones[Torso]; rig.Head = bones[HeadBone];
            rig.LeftArm = bones[ArmL]; rig.LeftElbow = bones[ElbowL]; rig.RightArm = bones[ArmR]; rig.RightElbow = bones[ElbowR];
            rig.Legs.Add(bones[LegL]); rig.Legs.Add(bones[LegR]);
            rig.Knees.Add(bones[KneeL]); rig.Knees.Add(bones[KneeR]);

            // The weapon sits in the right palm, pointing forwards like on the simple rig.
            rig.Hand = new GameObject("Grip").transform;
            rig.Hand.SetParent(bones[WristR], false);
            rig.Hand.position = root.transform.TransformPoint(shape.GripR * k);
            rig.Hand.localRotation = Quaternion.identity;

            // Features that are placed in the simple rig's head units (hats, hair ties, horns, creature eyes)
            // hang off a Skull pivot mapped onto this head: reference eyes (y 0.122, z 0.093) land on the real eyes.
            var eyeMid = (shape.EyeL + shape.EyeR) * 0.5f * k;
            float ks = (shape.HeadTop.y - shape.EyeL.y) * k / (0.248f - 0.122f);
            rig.Skull = new GameObject("Skull").transform;
            rig.Skull.SetParent(bones[HeadBone], false);
            rig.Skull.position = root.transform.TransformPoint(eyeMid - new Vector3(0f, 0.122f, 0.093f) * ks);
            rig.Skull.rotation = root.transform.rotation;
            rig.Skull.localScale = Vector3.one * ks;

            rig.Fit = new Fit { Mesh = mesh, Bones = bones, BindPoses = bind, Normals = mesh.normals, Regions = topo.Regions, Root = root.transform, Key = key,
                EyeL = shape.EyeL * k, EyeR = shape.EyeR * k, Scale = k };

            if (headScale != 1f) bones[HeadBone].localScale = Vector3.one * headScale;
            if (trunk.HasValue) bones[Torso].localScale = trunk.Value;
            if (hunch != 0f) bones[Torso].localRotation = Quaternion.Euler(hunch, 0f, 0f) * bones[Torso].localRotation;
            return rig;
        }

        static void AimDown(Quaternion[] rot, Vector3[] pos, int from, int to)
        {
            rot[from] = Quaternion.FromToRotation(Vector3.down, (pos[to] - pos[from]).normalized);
        }

        static void Setup(SkinnedMeshRenderer smr, Mesh mesh, Transform[] bones, Matrix4x4[] bind, float height)
        {
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = bones[Body];
            smr.localBounds = new Bounds(new Vector3(0f, height * 0.5f, 0f), new Vector3(height * 1.6f, height * 1.6f, height * 1.6f));
            smr.updateWhenOffscreen = false;
            // Bind poses live on the mesh; garments share the body's, so they always move with the skin.
            if (mesh.bindposes == null || mesh.bindposes.Length != bind.Length) mesh.bindposes = bind;
        }

        static Mesh BodyMeshFor(Topology topo, Shape shape, float k, string key)
        {
            Mesh mesh;
            if (meshes.TryGetValue(key, out mesh) && mesh != null) return mesh;
            var v = new Vector3[shape.Vertices.Length];
            for (int i = 0; i < v.Length; i++) v[i] = shape.Vertices[i] * k;
            mesh = new Mesh { name = "Human_" + key };
            if (v.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = v;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(topo.SkinTris, 0);
            mesh.SetTriangles(topo.EyeTris, 1);
            mesh.boneWeights = topo.Weights;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshes[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// A garment fitted to a realistic body: the skin over the chosen regions, pushed outwards by inflate metres,
        /// skinned to the same bones. extraFilter can trim it further (for example a hairline).
        /// </summary>
        public static GameObject Garment(CharacterRig rig, Color color, float inflate, string name, BodyRegion[] regions,
            System.Func<Vector3, bool> keep = null)
        {
            var fit = rig.Fit;
            var topo = LoadTopology();
            string key = fit.Key + "/" + name + "/" + Mathf.RoundToInt(inflate * 10000f);
            foreach (var r in regions) key += "," + (int)r;
            Mesh mesh;
            if (keep != null || !meshes.TryGetValue(key, out mesh) || mesh == null)
            {
                var wanted = new bool[16];
                foreach (var r in regions) wanted[(int)r] = true;
                var src = fit.Mesh.vertices;
                var map = new Dictionary<int, int>();
                var verts = new List<Vector3>();
                var weights = new List<BoneWeight>();
                var tris = new List<int>();
                var st = topo.SkinTris;
                // A triangle is part of the garment if any of its corners is; corners outside the regions are pushed
                // out less, so the edge tucks against the skin instead of leaving a jagged gap.
                var inside = new bool[src.Length];
                for (int vi = 0; vi < src.Length; vi++) inside[vi] = wanted[fit.Regions[vi]] && (keep == null || keep(src[vi]));
                for (int i = 0; i < st.Length; i += 3)
                {
                    if (!inside[st[i]] && !inside[st[i + 1]] && !inside[st[i + 2]]) continue;
                    for (int c = 0; c < 3; c++)
                    {
                        int vi = st[i + c], ni;
                        if (!map.TryGetValue(vi, out ni))
                        {
                            ni = verts.Count; map[vi] = ni;
                            verts.Add(src[vi] + fit.Normals[vi] * (inside[vi] ? inflate : inflate * 0.4f));
                            weights.Add(topo.Weights[vi]);
                        }
                        tris.Add(ni);
                    }
                }
                mesh = new Mesh { name = "Garment_" + name };
                mesh.SetVertices(verts);
                mesh.SetTriangles(tris, 0);
                mesh.boneWeights = weights.ToArray();
                mesh.bindposes = fit.BindPoses;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                if (keep == null) meshes[key] = mesh;
            }
            var go = new GameObject(name);
            go.transform.SetParent(fit.Root, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            Setup(smr, mesh, fit.Bones, fit.BindPoses, rig.Height);
            smr.sharedMaterial = Prim.Mat(color);
            return go;
        }

        /// <summary>Hair that grows from the scalp of a realistic head, swept back and down to the nape, clear of the ears.</summary>
        public static GameObject Hair(CharacterRig rig, Color color)
        {
            var fit = rig.Fit;
            var eye = (fit.EyeL + fit.EyeR) * 0.5f;
            float k = fit.Scale;
            return Garment(rig, color, 0.005f * k, "Hair", new[] { BodyRegion.Head }, p =>
            {
                float back = Mathf.Max(0f, eye.z - 0.05f * k - p.z);              // how far behind the face
                bool ear = Mathf.Abs(p.x) > 0.064f * k && p.y < eye.y + 0.04f * k && p.z > eye.z - 0.13f * k;
                return !ear && p.y > eye.y + 0.05f * k - back * 1.4f;
            });
        }

        /// <summary>Coloured irises on the sculpt's eyeballs (glowing ones for creatures).</summary>
        public static void Eyes(CharacterRig rig, Color iris, List<Renderer> into, bool glow = false, float size = 1f)
        {
            var fit = rig.Fit;
            foreach (var e in new[] { fit.EyeL, fit.EyeR })
            {
                var world = fit.Root.TransformPoint(e + new Vector3(0f, 0f, 0.0143f * fit.Scale));
                var eye = Prim.Make(PrimitiveType.Sphere, rig.Head, rig.Head.InverseTransformPoint(world),
                    new Vector3(0.011f, 0.011f, 0.004f) * fit.Scale * size / (rig.Head.localScale.x * rig.Torso.localScale.x), iris, glow ? "Eye" : "Iris", false, glow);
                into.Add(eye.GetComponent<Renderer>());
            }
        }

        class Reader
        {
            readonly byte[] b; int p;
            public Reader(byte[] bytes) { b = bytes; }
            public int Int() { int v = System.BitConverter.ToInt32(b, p); p += 4; return v; }
            public float Float() { float v = System.BitConverter.ToSingle(b, p); p += 4; return v; }
            public Vector3 Vec() { return new Vector3(Float(), Float(), Float()); }
            public int[] Ints(int n) { var a = new int[n]; System.Buffer.BlockCopy(b, p, a, 0, n * 4); p += n * 4; return a; }
            public float[] Floats(int n) { var a = new float[n]; System.Buffer.BlockCopy(b, p, a, 0, n * 4); p += n * 4; return a; }
            public byte[] Bytes(int n) { var a = new byte[n]; System.Buffer.BlockCopy(b, p, a, 0, n); p += n; return a; }
        }
    }
}
