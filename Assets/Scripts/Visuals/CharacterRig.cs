using System.Collections.Generic;
using Hollowmark.Data;
using UnityEngine;

namespace Hollowmark.Visuals
{
    /// <summary>One piece of a body, remembered so clothes and armor can be fitted over it later.</summary>
    public class BodyPart
    {
        public BodyRegion Region;
        public Transform Pivot;
        public Vector3 Position, Scale;
        public float Muscle, Shoulders = 1f;
    }

    /// <summary>
    /// Proportions of a body, relative to an average 1.8 m human (1 = normal). Muscle runs from 0 (slim)
    /// through 1 (muscular, with sculpted chest, abs, shoulders, arms and legs) to 1.5 (a hulking giant).
    /// </summary>
    public class BodyPlan
    {
        public float Leg = 1f, Arm = 1f, Torso = 1f, Bulk = 1f, Shoulders = 1f, Head = 1f, Hunch = 0f, Muscle = 0.3f;

        public static BodyPlan Slim() { return new BodyPlan { Bulk = 0.9f, Shoulders = 0.96f, Muscle = 0.1f }; }
        public static BodyPlan Muscular() { return new BodyPlan { Bulk = 1.05f, Shoulders = 1.1f, Muscle = 1f }; }
        public static BodyPlan Giant() { return new BodyPlan { Arm = 1.08f, Torso = 1.05f, Bulk = 1.2f, Shoulders = 1.2f, Head = 0.92f, Muscle = 1.4f }; }
    }

    /// <summary>
    /// A jointed body (hips, knees, ankles, spine, shoulders, elbows, wrists, neck) built from smooth
    /// generated meshes, with procedural walk, idle, attack and death animation. Every character is built
    /// through here. If a prefab with an Animator is used instead (see TryModel), its Speed, Attack, Hurt
    /// and Dead parameters are driven when they exist, so a rigged model such as a Mixamo character works too.
    /// </summary>
    public class CharacterRig : MonoBehaviour
    {
        public Transform Body, Torso, Head, Skull, RightArm, LeftArm, RightElbow, LeftElbow, Hand;
        public readonly List<Transform> Legs = new List<Transform>();
        public readonly List<Transform> Knees = new List<Transform>();
        public readonly List<Renderer> Eyes = new List<Renderer>();
        public readonly List<Renderer> AllRenderers = new List<Renderer>();
        public readonly List<BodyPart> Parts = new List<BodyPart>();
        public float Height = 1.8f;
        public float ArmRoll = 7f; // arms hang slightly away from the body
        public HumanMesh.Fit Fit;  // set on realistic (sculpted) bodies

        // Driven by controllers.
        [HideInInspector] public float MoveSpeed;
        [HideInInspector] public bool Dead;

        float phase, attackT = -1f, attackDuration = 0.4f, hurtT, idleSeed;
        Vector3 bodyBase;
        Quaternion baseRotation, torsoBase;
        bool hasBase;

        Animator animator;
        bool animSpeed, animAttack, animHurt, animDead;

        public void PlayAttack(float duration)
        {
            attackT = 0f; attackDuration = Mathf.Max(0.15f, duration);
            if (animAttack) animator.SetTrigger("Attack");
        }

        public void PlayHurt()
        {
            hurtT = 0.15f;
            if (animHurt) animator.SetTrigger("Hurt");
        }

        public void CollectRenderers()
        {
            AllRenderers.Clear();
            AllRenderers.AddRange(GetComponentsInChildren<Renderer>(true));
        }

        public void SetVisible(bool visible, bool keepEyes)
        {
            foreach (var r in AllRenderers)
            {
                if (r == null) continue;
                r.enabled = visible || (keepEyes && Eyes.Contains(r));
            }
        }

        /// <summary>Fits a garment over the given body regions: the same shapes, slightly inflated, in another colour.</summary>
        public List<GameObject> Cover(Color color, float inflate, string name, params BodyRegion[] regions)
        {
            var made = new List<GameObject>();
            if (Fit != null) { made.Add(HumanMesh.Garment(this, color, inflate, name, regions)); return made; }
            foreach (var part in Parts)
            {
                if (System.Array.IndexOf(regions, part.Region) < 0) continue;
                made.Add(Prim.Shape(BodyMesh.Get(part.Region, inflate, part.Muscle, part.Shoulders), part.Pivot, part.Position, part.Scale, color, name));
            }
            return made;
        }

        /// <summary>The scale of the trunk, for garments such as coat tails and gowns that hang from the waist.</summary>
        public Vector3 TrunkScale
        {
            get
            {
                foreach (var p in Parts) if (p.Region == BodyRegion.Pelvis) return p.Scale;
                return Vector3.one * Height / 1.8f;
            }
        }

        void Start()
        {
            idleSeed = Random.value * 10f;
            animator = GetComponentInChildren<Animator>();
            if (animator == null) return;
            foreach (var p in animator.parameters)
            {
                if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) animSpeed = true;
                if (p.name == "Attack" && p.type == AnimatorControllerParameterType.Trigger) animAttack = true;
                if (p.name == "Hurt" && p.type == AnimatorControllerParameterType.Trigger) animHurt = true;
                if (p.name == "Dead" && p.type == AnimatorControllerParameterType.Bool) animDead = true;
            }
        }

        void Update()
        {
            if (animator != null)
            {
                if (animSpeed) animator.SetFloat("Speed", MoveSpeed);
                if (animDead) animator.SetBool("Dead", Dead);
                if (Body == null) return; // a rigged model animates itself
            }

            if (!hasBase && Body != null)
            {
                bodyBase = Body.localPosition; baseRotation = transform.localRotation;
                if (Torso != null) torsoBase = Torso.localRotation;
                hasBase = true;
            }
            if (Dead)
            {
                // Knees give way and the body topples over.
                transform.localRotation = Quaternion.Slerp(transform.localRotation, baseRotation * Quaternion.Euler(85f, 0f, 0f), Time.deltaTime * 4f);
                foreach (var k in Knees) if (k != null) k.localRotation = Quaternion.Slerp(k.localRotation, Quaternion.Euler(50f, 0f, 0f), Time.deltaTime * 4f);
                return;
            }
            if (hasBase) transform.localRotation = Quaternion.Slerp(transform.localRotation, baseRotation, Time.deltaTime * 6f);

            float dt = Time.deltaTime, time = Time.time + idleSeed;
            float move = Mathf.Clamp01(MoveSpeed / 3f);
            phase += dt * Mathf.Clamp(MoveSpeed, 0f, 10f) * 2.2f;
            float swing = Mathf.Sin(phase) * 35f * move;

            for (int i = 0; i < Legs.Count; i++)
            {
                if (Legs[i] == null) continue;
                float legPhase = phase + (i % 2 == 0 ? 0f : Mathf.PI);
                Legs[i].localRotation = Quaternion.Euler(Mathf.Sin(legPhase) * 35f * move, 0f, 0f);
                // The knee bends while the leg swings forwards, and stays a touch soft when standing.
                if (i < Knees.Count && Knees[i] != null)
                    Knees[i].localRotation = Quaternion.Euler(3f + Mathf.Max(0f, -Mathf.Cos(legPhase)) * 55f * move, 0f, 0f);
            }

            float breathe = Mathf.Sin(time * 1.8f);
            if (LeftArm != null) LeftArm.localRotation = Quaternion.Euler(-swing * 0.8f + breathe * 1.5f, 0f, -ArmRoll);
            if (LeftElbow != null) LeftElbow.localRotation = Quaternion.Euler(-(10f + 30f * move * Mathf.Max(0f, Mathf.Sin(phase))), 0f, 0f);

            if (RightArm != null)
            {
                if (attackT >= 0f)
                {
                    attackT += dt;
                    float t = attackT / attackDuration;
                    // Wind up with the elbow bent, then slash down and across while the arm straightens.
                    float pitch = t < 0.35f ? Mathf.Lerp(0f, -150f, t / 0.35f) : Mathf.Lerp(-150f, 40f, (t - 0.35f) / 0.65f);
                    float yaw = t < 0.35f ? 0f : Mathf.Lerp(0f, -50f, (t - 0.35f) / 0.65f);
                    float elbow = t < 0.35f ? Mathf.Lerp(-10f, -80f, t / 0.35f) : Mathf.Lerp(-80f, -5f, (t - 0.35f) / 0.65f);
                    RightArm.localRotation = Quaternion.Euler(pitch, yaw, ArmRoll);
                    if (RightElbow != null) RightElbow.localRotation = Quaternion.Euler(elbow, 0f, 0f);
                    if (t >= 1f) attackT = -1f;
                }
                else
                {
                    RightArm.localRotation = Quaternion.Slerp(RightArm.localRotation, Quaternion.Euler(swing * 0.8f - 10f + breathe * 1.5f, 0f, ArmRoll), dt * 10f);
                    if (RightElbow != null)
                        RightElbow.localRotation = Quaternion.Slerp(RightElbow.localRotation,
                            Quaternion.Euler(-(18f + 30f * move * Mathf.Max(0f, -Mathf.Sin(phase))), 0f, 0f), dt * 10f);
                }
            }

            if (Torso != null && hasBase)
                Torso.localRotation = torsoBase * Quaternion.Euler(move * 6f + breathe * 1.2f, Mathf.Sin(phase) * 5f * move, 0f);
            if (Head != null)
                Head.localRotation = Quaternion.Euler(-move * 5f + Mathf.Sin(time * 0.7f) * 2f, Mathf.Sin(time * 0.37f) * 8f * (1f - move), 0f);

            if (Body != null && hasBase)
            {
                float bob = (Mathf.Abs(Mathf.Sin(phase)) * 0.04f - 0.02f) * move * Height / 1.8f;
                Body.localPosition = bodyBase + Vector3.up * bob;
            }

            if (hurtT > 0f)
            {
                hurtT -= dt;
                transform.localScale = Vector3.one * (1f + hurtT * 0.6f);
                if (hurtT <= 0f) transform.localScale = Vector3.one;
            }
        }
    }

    public static class CharacterBuilder
    {
        static readonly string[] HairColors = { "#2A1A10", "#4A3020", "#8A6A40", "#C8B080", "#1A1A1A", "#7A3A20", "#D0D0D0" };
        static readonly string[] SkinTones = { "#F0D0B0", "#E0B090", "#C89870", "#A07050", "#7A5038", "#F8E0C8" };
        static readonly string[] IrisColors = { "#4A3020", "#2A1A10", "#3A5A7A", "#5A6A40", "#6A6A6A" };

        /// <summary>
        /// If a prefab exists at Resources/{path}, use it instead of the generated body. This is the hook for real art:
        /// drop a rigged model into Resources and set "model" in the JSON.
        /// </summary>
        public static GameObject TryModel(string path, Transform parent)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null) { Debug.LogWarning("[Hollowmark] Model not found at Resources/" + path + ", using the generated body."); return null; }
            var go = Object.Instantiate(prefab, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            return go;
        }

        static Transform Pivot(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        static void AddPart(CharacterRig rig, BodyRegion region, Transform pivot, Vector3 scale, Color color, float muscle, float shoulders = 1f, bool visible = true)
        {
            if (visible) Prim.Shape(BodyMesh.Get(region, 0f, muscle, shoulders), pivot, Vector3.zero, scale, color, region.ToString());
            rig.Parts.Add(new BodyPart { Region = region, Pivot = pivot, Position = Vector3.zero, Scale = scale, Muscle = muscle, Shoulders = shoulders });
        }

        /// <summary>
        /// The shared skeleton and skin for every two-legged character. The head geometry lives under rig.Skull,
        /// which is scaled so that features can be placed in 1.8 m reference units (face front is at z = 0.11).
        /// </summary>
        public static CharacterRig BuildBody(Transform parent, string name, float height, BodyPlan plan, Color skin)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rig = root.AddComponent<CharacterRig>();
            rig.Height = height;

            float s = height / (0.92f * plan.Leg + 0.64f * plan.Torso + 0.248f * plan.Head);
            float b = plan.Bulk, sb = Mathf.Sqrt(b);
            float hipY = 0.92f * plan.Leg * s;
            rig.Body = Pivot(root.transform, "Body", Vector3.zero);

            var legScale = new Vector3(s * b, s * plan.Leg, s * b);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var hip = Pivot(rig.Body, i == 0 ? "LegL" : "LegR", new Vector3(side * 0.095f * s * b, hipY, 0f));
                AddPart(rig, BodyRegion.Thigh, hip, legScale, skin, plan.Muscle);
                var knee = Pivot(hip, "Knee", new Vector3(0f, -0.43f * s * plan.Leg, 0f));
                AddPart(rig, BodyRegion.Shin, knee, legScale, skin, plan.Muscle);
                var ankle = Pivot(knee, "Ankle", new Vector3(0f, -0.42f * s * plan.Leg, 0f));
                AddPart(rig, BodyRegion.Foot, ankle, new Vector3(s * sb, s * plan.Leg, s * sb), skin, plan.Muscle);
                rig.Legs.Add(hip);
                rig.Knees.Add(knee);
            }

            rig.Torso = Pivot(rig.Body, "Torso", new Vector3(0f, hipY, 0f));
            rig.Torso.localRotation = Quaternion.Euler(plan.Hunch, 0f, 0f);
            var trunkScale = new Vector3(s * b, s * plan.Torso, s * b);
            Prim.Shape(BodyMesh.Get("Trunk", 0f, plan.Muscle, plan.Shoulders), rig.Torso, Vector3.zero, trunkScale, skin, "Trunk");
            AddPart(rig, BodyRegion.Pelvis, rig.Torso, trunkScale, skin, plan.Muscle, 1f, false);
            AddPart(rig, BodyRegion.Chest, rig.Torso, trunkScale, skin, plan.Muscle, plan.Shoulders, false);
            AddPart(rig, BodyRegion.Neck, rig.Torso, new Vector3(s * sb, s * plan.Torso, s * sb), skin, plan.Muscle);

            var armScale = new Vector3(s * b, s * plan.Arm, s * b);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var shoulder = Pivot(rig.Torso, i == 0 ? "ArmL" : "ArmR", new Vector3(side * 0.19f * s * b * plan.Shoulders, 0.51f * s * plan.Torso, 0f));
                shoulder.localRotation = Quaternion.Euler(0f, 0f, side * rig.ArmRoll);
                AddPart(rig, BodyRegion.UpperArm, shoulder, armScale, skin, plan.Muscle);
                var elbow = Pivot(shoulder, "Elbow", new Vector3(0f, -0.29f * s * plan.Arm, 0f));
                elbow.localRotation = Quaternion.Euler(-12f, 0f, 0f);
                AddPart(rig, BodyRegion.Forearm, elbow, armScale, skin, plan.Muscle);
                var wrist = Pivot(elbow, "Wrist", new Vector3(0f, -0.25f * s * plan.Arm, 0f));
                AddPart(rig, BodyRegion.Hand, wrist, new Vector3(s * sb, s, s * sb), skin, plan.Muscle);
                if (i == 0) { rig.LeftArm = shoulder; rig.LeftElbow = elbow; }
                else { rig.RightArm = shoulder; rig.RightElbow = elbow; rig.Hand = Pivot(wrist, "Grip", new Vector3(0f, -0.085f * s, 0.01f * s)); }
            }

            rig.Head = Pivot(rig.Torso, "Head", new Vector3(0f, 0.64f * s * plan.Torso, 0f));
            rig.Skull = Pivot(rig.Head, "Skull", Vector3.zero);
            rig.Skull.localScale = Vector3.one * s * plan.Head;
            AddPart(rig, BodyRegion.Head, rig.Skull, Vector3.one, skin, plan.Muscle);
            return rig;
        }

        /// <summary>
        /// A human (player or villager) with a seeded face, hair and build. muscle: 0 slim, 1 muscular, or below 0
        /// to pick an everyday build from the seed. Dress it with rig.Cover or DressVillager.
        /// </summary>
        public static CharacterRig BuildHuman(Transform parent, float height, int seed, out Color skin, bool feminine = false, float muscle = -1f)
        {
            var oldState = Random.state;
            Random.InitState(seed);
            skin = EnumUtil.ParseColor(SkinTones[Random.Range(0, SkinTones.Length)], Color.white);
            var hair = EnumUtil.ParseColor(HairColors[Random.Range(0, HairColors.Length)], Color.black);
            if (muscle < 0f) muscle = Random.Range(0f, 0.6f);
            if (feminine) muscle = Mathf.Min(muscle, 0.15f);
            var plan = new BodyPlan
            {
                Bulk = Random.Range(0.92f, 1.08f) * (feminine ? 0.93f : 1f) * (1f + 0.05f * muscle),
                Shoulders = Random.Range(0.96f, 1.04f) * (feminine ? 0.9f : 1f) * (1f + 0.1f * muscle),
                Head = Random.Range(0.97f, 1.03f),
                Leg = Random.Range(0.97f, 1.03f),
                Muscle = muscle,
            };

            CharacterRig rig = null;
            if (HumanMesh.Available)
            {
                string shape = feminine ? (muscle < 0.08f ? HumanMesh.FemaleSlim : HumanMesh.Female)
                    : muscle < 0.2f ? HumanMesh.Slim : muscle < 0.7f ? HumanMesh.Average : muscle < 1.2f ? HumanMesh.Muscular : HumanMesh.Giant;
                rig = HumanMesh.Build(parent, "HumanRig", shape, height, skin);
            }
            if (rig != null) BuildRealisticFace(rig, hair, feminine);
            else
            {
                rig = BuildBody(parent, "HumanRig", height, plan, skin);
                BuildHumanFace(rig, skin, hair, feminine);
            }

            Random.state = oldState;
            rig.CollectRenderers();
            return rig;
        }

        static void BuildHumanFace(CharacterRig rig, Color skin, Color hair, bool feminine)
        {
            var h = rig.Skull;
            var iris = EnumUtil.ParseColor(IrisColors[Random.Range(0, IrisColors.Length)], Color.black);
            float spread = Random.Range(0.03f, 0.036f);
            for (int i = -1; i <= 1; i += 2)
            {
                Prim.Make(PrimitiveType.Sphere, h, new Vector3(i * spread, 0.122f, 0.093f), new Vector3(0.028f, 0.017f, 0.02f), new Color(0.95f, 0.93f, 0.9f), "EyeWhite");
                var eye = Prim.Make(PrimitiveType.Sphere, h, new Vector3(i * spread, 0.122f, 0.101f), new Vector3(0.012f, 0.012f, 0.006f), iris, "Iris");
                rig.Eyes.Add(eye.GetComponent<Renderer>());
                Prim.Make(PrimitiveType.Sphere, h, new Vector3(i * spread, 0.13f, 0.093f), new Vector3(0.03f, 0.01f, 0.02f), skin * 0.97f, "Eyelid");
                var brow = Prim.Make(PrimitiveType.Sphere, h, new Vector3(i * (spread + 0.003f), 0.146f, 0.1f), new Vector3(0.036f, feminine ? 0.006f : 0.009f, 0.012f), hair, "Brow");
                brow.transform.localRotation = Quaternion.Euler(0f, 0f, -i * Random.Range(2f, 9f));
                Prim.Make(PrimitiveType.Sphere, h, new Vector3(i * 0.078f, 0.115f, -0.005f), new Vector3(0.016f, 0.052f, 0.034f), skin * 0.95f, "Ear");
            }
            var nose = Prim.Shape(BodyMesh.Get("Nose"), h, Vector3.zero, Vector3.one, skin * 0.97f, "Nose");
            nose.transform.localScale = new Vector3(Random.Range(0.85f, 1.25f), 1f, Random.Range(0.95f, 1.08f));
            var lip = feminine ? new Color(0.7f, 0.35f, 0.35f) : Color.Lerp(skin, new Color(0.55f, 0.3f, 0.28f), 0.6f);
            float mouthW = Random.Range(0.036f, 0.046f);
            Prim.Make(PrimitiveType.Sphere, h, new Vector3(0f, 0.061f, 0.103f), new Vector3(mouthW, 0.009f, 0.014f), lip, "UpperLip");
            Prim.Make(PrimitiveType.Sphere, h, new Vector3(0f, 0.051f, 0.102f), new Vector3(mouthW * 0.9f, 0.011f, 0.015f), lip * 1.05f, "LowerLip");

            // 1700s hair: men wear it tied back in a queue, women pin it up in a bun.
            Prim.Shape(BodyMesh.Get("Hair"), h, Vector3.zero, Vector3.one, hair, "Hair");
            Prim.Shape(BodyMesh.Get(feminine ? "Bun" : "Queue"), h, Vector3.zero, Vector3.one, hair, feminine ? "Bun" : "Queue");
        }

        /// <summary>The sculpted head already has a face; this adds eye colour, brows and 1700s hair.</summary>
        static void BuildRealisticFace(CharacterRig rig, Color hair, bool feminine)
        {
            var h = rig.Skull;
            HumanMesh.Eyes(rig, EnumUtil.ParseColor(IrisColors[Random.Range(0, IrisColors.Length)], Color.black), rig.Eyes);
            for (int i = -1; i <= 1; i += 2)
            {
                var brow = Prim.Make(PrimitiveType.Sphere, h, new Vector3(i * 0.034f, 0.147f, 0.106f), new Vector3(0.036f, feminine ? 0.005f : 0.008f, 0.01f), hair, "Brow");
                brow.transform.localRotation = Quaternion.Euler(-12f, 0f, -i * Random.Range(3f, 9f));
            }
            HumanMesh.Hair(rig, hair);
            Prim.Shape(BodyMesh.Get(feminine ? "Bun" : "Queue"), h, Vector3.zero, Vector3.one, hair, feminine ? "Bun" : "Queue");
        }

        /// <summary>Everyday 18th-century clothes: shirt, waistcoat, breeches, stockings and shoes, or a shift and gown.</summary>
        public static void DressVillager(CharacterRig rig, Color main, bool feminine)
        {
            var linen = new Color(0.92f, 0.9f, 0.84f);
            var shoes = new Color(0.12f, 0.09f, 0.07f);
            rig.Cover(linen, 0.006f, "Shirt", BodyRegion.Chest, BodyRegion.UpperArm, BodyRegion.Forearm);
            if (feminine) rig.Cover(main, 0.013f, "Bodice", BodyRegion.Chest, BodyRegion.Pelvis);
            else rig.Cover(main, 0.013f, "Waistcoat", BodyRegion.Chest);
            rig.Cover(shoes, 0.006f, "Shoes", BodyRegion.Foot);
            if (feminine)
            {
                Prim.Shape(BodyMesh.Get("Gown"), rig.Torso, Vector3.zero, rig.TrunkScale, main * 0.85f, "Gown");
                rig.Cover(new Color(0.85f, 0.84f, 0.8f), 0.004f, "Stockings", BodyRegion.Shin);
            }
            else
            {
                rig.Cover(main * 0.7f, 0.008f, "Breeches", BodyRegion.Pelvis, BodyRegion.Thigh);
                rig.Cover(new Color(0.85f, 0.84f, 0.8f), 0.004f, "Stockings", BodyRegion.Shin);
            }
            rig.CollectRenderers();
        }

        static BodyPlan CreaturePlan(BodyShape shape, out float typicalWidth)
        {
            switch (shape)
            {
                case BodyShape.Goblin: typicalWidth = 0.5f; return new BodyPlan { Leg = 0.8f, Arm = 1.2f, Torso = 0.95f, Bulk = 0.85f, Shoulders = 0.95f, Head = 1.6f, Hunch = 18f, Muscle = 0.5f };
                case BodyShape.Imp: typicalWidth = 0.53f; return new BodyPlan { Leg = 0.85f, Arm = 1.1f, Torso = 0.9f, Bulk = 0.75f, Head = 1.5f, Hunch = 8f, Muscle = 0.4f };
                case BodyShape.Brute: typicalWidth = 0.81f; return new BodyPlan { Leg = 0.75f, Arm = 1.25f, Torso = 1.05f, Bulk = 1.6f, Shoulders = 1.35f, Head = 1.15f, Hunch = 22f, Muscle = 1.2f };
                case BodyShape.Giant: typicalWidth = 0.48f; var giant = BodyPlan.Giant(); giant.Hunch = 6f; return giant;
                default: typicalWidth = 0.37f; return new BodyPlan { Arm = 1.05f, Bulk = 0.85f, Hunch = 10f, Muscle = 0f }; // Humanoid: gaunt
            }
        }

        /// <summary>Builds one of the creature body plans with per-individual variation, so no two look alike.</summary>
        public static CharacterRig BuildCreature(Transform parent, BodyShape shape, float height, float width,
            Color skin, Color eyeColor, int seed)
        {
            var oldState = Random.state;
            Random.InitState(seed);

            skin = Prim.Vary(skin, 0.08f);
            var dark = skin * 0.7f; dark.a = 1f;
            CharacterRig rig;
            if (shape == BodyShape.Crawler) rig = BuildCrawler(parent, height, width, skin, dark, eyeColor);
            else
            {
                float typical;
                var plan = CreaturePlan(shape, out typical);
                float build = Mathf.Clamp((width / height) / typical, 0.75f, 1.35f);
                plan.Bulk *= build;
                plan.Muscle *= Mathf.Clamp(build, 0.6f, 1.1f); // thin bosses are wiry, broad ones hulking
                plan.Arm *= Random.Range(0.9f, 1.15f);
                plan.Head *= Random.Range(0.9f, 1.15f);
                plan.Hunch += Random.Range(0f, 10f);
                rig = HumanMesh.Available ? BuildRealisticCreature(parent, shape, height, build, plan.Hunch, skin) : null;
                bool realistic = rig != null;
                if (!realistic) rig = BuildBody(parent, "CreatureRig", height, plan, skin);
                BuildCreatureFace(rig, shape, skin, dark, eyeColor, realistic);
                DressCreature(rig, shape, skin, dark, height);
            }

            Random.state = oldState;
            rig.CollectRenderers();
            return rig;
        }

        static CharacterRig BuildRealisticCreature(Transform parent, BodyShape shape, float height, float build, float hunch, Color skin)
        {
            switch (shape)
            {
                case BodyShape.Goblin: return HumanMesh.Build(parent, "CreatureRig", HumanMesh.Slim, height, skin, Random.Range(1.35f, 1.6f), null, hunch);
                case BodyShape.Imp: return HumanMesh.Build(parent, "CreatureRig", HumanMesh.Slim, height, skin, Random.Range(1.25f, 1.45f), null, hunch);
                case BodyShape.Brute:
                    return HumanMesh.Build(parent, "CreatureRig", HumanMesh.Giant, height, skin, Random.Range(0.95f, 1.1f),
                        new Vector3(1.25f, 1f, 1.2f) * Mathf.Clamp(build, 0.9f, 1.15f), hunch);
                case BodyShape.Giant:
                    return HumanMesh.Build(parent, "CreatureRig", build < 0.85f ? HumanMesh.Muscular : HumanMesh.Giant, height, skin, 0.95f,
                        build > 1.1f ? (Vector3?)new Vector3(build, 1f, build) : null, hunch);
                default: return HumanMesh.Build(parent, "CreatureRig", HumanMesh.Slim, height, skin, 1f, null, hunch);
            }
        }

        static void BuildCreatureFace(CharacterRig rig, BodyShape shape, Color skin, Color dark, Color eyeColor, bool realistic)
        {
            var h = rig.Skull;
            var bone = new Color(0.85f, 0.8f, 0.65f);
            int eyeCount = Random.value < 0.2f ? 3 : (Random.value < 0.08f ? 1 : 2);
            float eyeSize = Random.Range(0.018f, 0.028f) * (eyeCount == 1 ? 1.6f : 1f);
            if (realistic && eyeCount == 2) { HumanMesh.Eyes(rig, eyeColor, rig.Eyes, true, eyeSize / 0.012f); eyeCount = 0; }
            for (int i = 0; i < eyeCount; i++)
            {
                Vector3 pos;
                if (eyeCount == 1) pos = new Vector3(0f, 0.125f, 0.098f);
                else if (eyeCount == 3 && i == 2) pos = new Vector3(0f, 0.16f, 0.1f);
                else pos = new Vector3((i == 0 ? -1f : 1f) * 0.034f, 0.122f, 0.094f);
                Prim.Make(PrimitiveType.Sphere, h, pos - new Vector3(0f, 0f, 0.008f), Vector3.one * eyeSize * 1.4f, dark * 0.4f, "Socket");
                var eye = Prim.Make(PrimitiveType.Sphere, h, pos, Vector3.one * eyeSize, eyeColor, "Eye", false, true);
                rig.Eyes.Add(eye.GetComponent<Renderer>());
            }
            // A heavy brow ridge and a wide dark mouth make every creature read as hostile.
            Prim.Make(PrimitiveType.Sphere, h, new Vector3(0f, 0.146f, realistic ? 0.104f : 0.098f), new Vector3(0.11f, 0.02f, 0.03f), skin * 0.9f, "BrowRidge");
            Prim.Make(PrimitiveType.Sphere, h, new Vector3(0f, 0.055f, realistic ? 0.108f : 0.102f), new Vector3(Random.Range(0.05f, 0.075f), 0.014f, 0.02f), new Color(0.15f, 0.04f, 0.04f), "Mouth");

            switch (shape)
            {
                case BodyShape.Brute:
                    Prim.Shape(BodyMesh.Get("Snout"), h, new Vector3(0f, 0.08f, 0.07f), Vector3.one * Random.Range(0.9f, 1.3f), dark, "Snout");
                    break;
                case BodyShape.Goblin:
                {
                    var nose = Prim.Shape(BodyMesh.Get("Nose"), h, new Vector3(0f, -0.03f, -0.04f), new Vector3(1.4f, 1.4f, 1.45f), skin * 0.9f, "Nose");
                    nose.name = "HookNose";
                    break;
                }
                default:
                    if (!realistic) Prim.Shape(BodyMesh.Get("Nose"), h, Vector3.zero, Vector3.one, skin * 0.9f, "Nose");
                    break;
            }

            if (shape == BodyShape.Brute || shape == BodyShape.Goblin || Random.value < 0.3f)
                for (int i = -1; i <= 1; i += 2)
                {
                    var tusk = Prim.Shape(BodyMesh.Get("Cone"), h, new Vector3(i * 0.022f, 0.048f, 0.104f), new Vector3(0.008f, Random.Range(0.025f, 0.045f), 0.008f), bone, "Tusk");
                    tusk.transform.localRotation = Quaternion.Euler(-10f, 0f, -i * 10f);
                }

            int hornCount = shape == BodyShape.Humanoid ? 0 : Random.Range(0, 3) * 2;
            for (int i = 0; i < hornCount; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float row = i / 2;
                var horn = Prim.Shape(BodyMesh.Get("Cone"), h, new Vector3(side * (0.045f + row * 0.02f), 0.2f - row * 0.04f, -0.01f - row * 0.03f),
                    new Vector3(0.022f, Random.Range(0.06f, 0.14f), 0.022f), bone, "Horn");
                horn.transform.localRotation = Quaternion.Euler(-20f - row * 30f, 0f, -side * Random.Range(25f, 50f));
            }

            if (shape == BodyShape.Goblin || shape == BodyShape.Imp)
            {
                float earLen = Random.Range(0.09f, 0.16f);
                for (int i = -1; i <= 1; i += 2)
                {
                    var ear = Prim.Shape(BodyMesh.Get("Cone"), h, new Vector3(i * 0.074f, 0.12f, -0.01f), new Vector3(0.03f, earLen, 0.01f), skin, "Ear");
                    ear.transform.localRotation = Quaternion.Euler(-15f, 0f, -i * Random.Range(60f, 85f));
                }
            }
            else if (!realistic)
            {
                for (int i = -1; i <= 1; i += 2)
                    Prim.Make(PrimitiveType.Sphere, h, new Vector3(i * 0.078f, 0.115f, -0.005f), new Vector3(0.016f, 0.05f, 0.034f), skin * 0.95f, "Ear");
            }

            if ((shape == BodyShape.Humanoid || shape == BodyShape.Giant) && Random.value < 0.6f)
            {
                var hairColor = Prim.Vary(new Color(0.25f, 0.24f, 0.22f), 0.3f);
                if (realistic) HumanMesh.Hair(rig, hairColor);
                else Prim.Shape(BodyMesh.Get("Hair"), h, Vector3.zero, Vector3.one, hairColor, "Hair");
            }
        }

        static void DressCreature(CharacterRig rig, BodyShape shape, Color skin, Color dark, float height)
        {
            var rag = Prim.Vary(new Color(0.28f, 0.24f, 0.2f), 0.2f);
            switch (shape)
            {
                case BodyShape.Goblin:
                case BodyShape.Brute:
                    rig.Cover(rag, 0.012f, "Loincloth", BodyRegion.Pelvis);
                    break;
                case BodyShape.Humanoid:
                    // The Hollowed still wear what is left of their clothes.
                    rig.Cover(Prim.Vary(new Color(0.45f, 0.43f, 0.4f), 0.2f), 0.007f, "Shirt", BodyRegion.Chest, BodyRegion.UpperArm);
                    rig.Cover(rag, 0.009f, "Breeches", BodyRegion.Pelvis, BodyRegion.Thigh);
                    break;
                case BodyShape.Giant:
                    rig.Cover(dark * 0.8f, 0.01f, "Coat", BodyRegion.Chest, BodyRegion.UpperArm, BodyRegion.Forearm);
                    Prim.Shape(BodyMesh.Get("CoatTail"), rig.Torso, Vector3.zero, rig.TrunkScale * 1.04f, dark * 0.8f, "CoatTail");
                    rig.Cover(rag, 0.009f, "Breeches", BodyRegion.Pelvis, BodyRegion.Thigh);
                    rig.Cover(new Color(0.12f, 0.1f, 0.08f), 0.012f, "Boots", BodyRegion.Shin, BodyRegion.Foot);
                    break;
                case BodyShape.Imp:
                {
                    // A thin tail and leathery wings.
                    float s = height / 1.6f;
                    var tail = Prim.Shape(BodyMesh.Get("Cone"), rig.Torso, new Vector3(0f, -0.05f * s, -0.08f * s), new Vector3(0.03f, 0.55f, 0.03f) * s, dark, "Tail");
                    tail.transform.localRotation = Quaternion.Euler(-125f, 0f, 0f);
                    for (int i = -1; i <= 1; i += 2)
                    {
                        var wing = Prim.Shape(BodyMesh.Get("Cone"), rig.Torso, new Vector3(i * 0.08f * s, 0.45f * s, -0.1f * s), new Vector3(0.14f, 0.6f, 0.012f) * s, dark, "Wing");
                        wing.transform.localRotation = Quaternion.Euler(-25f, i * 25f, -i * 50f);
                    }
                    break;
                }
            }
        }

        static CharacterRig BuildCrawler(Transform parent, float height, float width, Color skin, Color dark, Color eyeColor)
        {
            var root = new GameObject("CreatureRig");
            root.transform.SetParent(parent, false);
            var rig = root.AddComponent<CharacterRig>();
            rig.Height = height;
            rig.ArmRoll = 0f;
            rig.Body = Pivot(root.transform, "Body", Vector3.zero);

            float bodyLen = width * 1.6f;
            Prim.Shape(BodyMesh.Get("Carapace"), rig.Body, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height * 1.1f, bodyLen), skin, "Carapace");
            // Banding across the shell.
            for (int i = 0; i < 3; i++)
                Prim.Shape(BodyMesh.Get("Carapace"), rig.Body, new Vector3(0f, height * 0.52f, bodyLen * (i - 1) * 0.25f), new Vector3(width * 1.02f, height * 1.1f, bodyLen * 0.12f), dark, "Band");

            for (int i = 0; i < 6; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float z = Mathf.Lerp(-bodyLen * 0.3f, bodyLen * 0.3f, (i / 2) / 2f);
                var leg = Pivot(rig.Body, "Leg", new Vector3(side * width * 0.4f, height * 0.5f, z));
                float upper = width * 0.45f;
                var thigh = Prim.Shape(BodyMesh.Get("Cone"), leg, Vector3.zero, new Vector3(0.05f, upper, 0.05f), dark, "LegUpper");
                thigh.transform.localRotation = Quaternion.Euler(0f, 0f, -side * 60f);
                var joint = thigh.transform.localRotation * new Vector3(0f, upper * 0.95f, 0f);
                float lower = (height * 0.5f + joint.y) / 0.94f;
                var shin = Prim.Shape(BodyMesh.Get("Cone"), leg, joint, new Vector3(0.04f, lower, 0.04f), dark, "LegLower");
                shin.transform.localRotation = Quaternion.Euler(0f, 0f, -side * 160f);
                rig.Legs.Add(leg);
            }

            rig.Head = Pivot(rig.Body, "Head", new Vector3(0f, height * 0.5f, bodyLen * 0.5f));
            rig.Skull = rig.Head;
            Prim.Shape(BodyMesh.Get("Carapace"), rig.Head, Vector3.zero, new Vector3(width * 0.45f, height * 0.6f, width * 0.4f), skin * 0.9f, "Skull");
            int eyes = Random.value < 0.3f ? 4 : 2;
            for (int i = 0; i < eyes; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * width * (i < 2 ? 0.1f : 0.06f);
                var eye = Prim.Make(PrimitiveType.Sphere, rig.Head, new Vector3(x, height * (i < 2 ? 0.1f : 0.18f), width * 0.17f),
                    Vector3.one * width * Random.Range(0.05f, 0.08f), eyeColor, "Eye", false, true);
                rig.Eyes.Add(eye.GetComponent<Renderer>());
            }
            rig.RightArm = Pivot(rig.Head, "Mandibles", new Vector3(0f, -height * 0.08f, width * 0.15f));
            for (int i = -1; i <= 1; i += 2)
            {
                var m = Prim.Shape(BodyMesh.Get("Cone"), rig.RightArm, new Vector3(i * 0.06f, 0f, 0f), new Vector3(0.035f, 0.28f, 0.035f) * width, new Color(0.8f, 0.75f, 0.6f), "Mandible");
                m.transform.localRotation = Quaternion.Euler(90f, i * -20f, 0f);
            }
            rig.Hand = rig.RightArm;
            return rig;
        }

        /// <summary>Simple placeholder model for a held weapon, sized from the weapon data.</summary>
        public static GameObject BuildWeapon(WeaponDef def, Transform hand)
        {
            var model = TryModel(def.model, hand);
            if (model != null) return model;

            var root = new GameObject("Weapon_" + def.id);
            root.transform.SetParent(hand, false);
            root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var wood = new Color(0.35f, 0.22f, 0.12f);
            if (def.Kind == WeaponKind.Firearm)
            {
                Prim.Make(PrimitiveType.Cube, root.transform, new Vector3(0f, def.length * 0.35f, 0f), new Vector3(0.06f, def.length * 0.8f, 0.08f), wood, "Stock");
                var barrel = Prim.Make(PrimitiveType.Cylinder, root.transform, new Vector3(0f, def.length * 0.55f, 0.04f), new Vector3(0.035f, def.length * 0.45f, 0.035f), def.Tint, "Barrel");
                if (def.pellets > 1) barrel.transform.localScale = new Vector3(0.08f, def.length * 0.45f, 0.08f);
            }
            else
            {
                bool pole = def.length > 1.6f;
                if (pole)
                {
                    Prim.Make(PrimitiveType.Cylinder, root.transform, new Vector3(0f, def.length * 0.3f, 0f), new Vector3(0.04f, def.length * 0.5f, 0.04f), wood, "Shaft");
                    Prim.Make(PrimitiveType.Cube, root.transform, new Vector3(0f, def.length * 0.8f, 0f), new Vector3(0.22f, 0.3f, 0.02f), def.Tint, "Head");
                }
                else
                {
                    Prim.Make(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.04f, 0.16f, 0.04f), wood, "Grip");
                    Prim.Make(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.14f, 0f), new Vector3(0.18f, 0.03f, 0.05f), new Color(0.7f, 0.6f, 0.3f), "Guard");
                    float bladeW = def.Weight == WeightClass.Great ? 0.09f : def.Weight == WeightClass.Light ? 0.035f : 0.06f;
                    Prim.Make(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.15f + def.length * 0.5f, 0f), new Vector3(bladeW, def.length, 0.012f), def.Tint, "Blade");
                }
            }
            return root;
        }
    }
}
