using System.Collections.Generic;
using Hollowmark.Data;
using UnityEngine;

namespace Hollowmark.Visuals
{
    /// <summary>
    /// A blocky placeholder body made of primitives, with pivots for simple procedural animation.
    /// Every character (player, villagers, enemies, bosses) is built through here, so swapping in
    /// real art only means replacing the builders below with prefab instantiation (see TryModel).
    /// </summary>
    public class CharacterRig : MonoBehaviour
    {
        public Transform Body, Head, RightArm, LeftArm, Hand;
        public readonly List<Transform> Legs = new List<Transform>();
        public readonly List<Renderer> Eyes = new List<Renderer>();
        public readonly List<Renderer> AllRenderers = new List<Renderer>();
        public float Height = 1.8f;

        // Driven by controllers.
        [HideInInspector] public float MoveSpeed;
        [HideInInspector] public bool Dead;

        float phase, attackT = -1f, attackDuration = 0.4f, hurtT;
        Vector3 bodyBase;
        Quaternion baseRotation;
        bool hasBase;

        public void PlayAttack(float duration) { attackT = 0f; attackDuration = Mathf.Max(0.15f, duration); }
        public void PlayHurt() { hurtT = 0.15f; }

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

        void Update()
        {
            if (!hasBase && Body != null) { bodyBase = Body.localPosition; baseRotation = transform.localRotation; hasBase = true; }
            if (Dead)
            {
                // Topple over.
                transform.localRotation = Quaternion.Slerp(transform.localRotation, baseRotation * Quaternion.Euler(85f, 0f, 0f), Time.deltaTime * 4f);
                return;
            }
            if (hasBase) transform.localRotation = Quaternion.Slerp(transform.localRotation, baseRotation, Time.deltaTime * 6f);

            phase += Time.deltaTime * Mathf.Clamp(MoveSpeed, 0f, 10f) * 2.2f;
            float swing = Mathf.Sin(phase) * Mathf.Clamp01(MoveSpeed / 3f) * 35f;
            for (int i = 0; i < Legs.Count; i++)
            {
                if (Legs[i] == null) continue;
                Legs[i].localRotation = Quaternion.Euler((i % 2 == 0 ? swing : -swing), 0f, 0f);
            }
            if (LeftArm != null) LeftArm.localRotation = Quaternion.Euler(-swing * 0.8f, 0f, 0f);

            if (RightArm != null)
            {
                if (attackT >= 0f)
                {
                    attackT += Time.deltaTime;
                    float t = attackT / attackDuration;
                    // Wind up then slash down and across.
                    float pitch = t < 0.35f ? Mathf.Lerp(0f, -150f, t / 0.35f) : Mathf.Lerp(-150f, 40f, (t - 0.35f) / 0.65f);
                    float yaw = t < 0.35f ? 0f : Mathf.Lerp(0f, -50f, (t - 0.35f) / 0.65f);
                    RightArm.localRotation = Quaternion.Euler(pitch, yaw, 0f);
                    if (t >= 1f) attackT = -1f;
                }
                else
                {
                    RightArm.localRotation = Quaternion.Slerp(RightArm.localRotation, Quaternion.Euler(swing * 0.8f - 10f, 0f, 0f), Time.deltaTime * 10f);
                }
            }

            if (Body != null && hasBase)
            {
                float bob = Mathf.Abs(Mathf.Sin(phase)) * 0.04f * Mathf.Clamp01(MoveSpeed / 3f);
                Body.localPosition = bodyBase + Vector3.up * bob;
            }

            if (hurtT > 0f)
            {
                hurtT -= Time.deltaTime;
                transform.localScale = Vector3.one * (1f + hurtT * 0.6f);
                if (hurtT <= 0f) transform.localScale = Vector3.one;
            }
        }
    }

    public static class CharacterBuilder
    {
        static readonly string[] HairColors = { "#2A1A10", "#4A3020", "#8A6A40", "#C8B080", "#1A1A1A", "#7A3A20", "#D0D0D0" };
        static readonly string[] SkinTones = { "#F0D0B0", "#E0B090", "#C89870", "#A07050", "#7A5038", "#F8E0C8" };

        /// <summary>
        /// If a prefab exists at Resources/{path}, use it instead of primitives. This is the hook for real art:
        /// drop rigged models with realistic faces into Resources and set "model" in the JSON.
        /// </summary>
        public static GameObject TryModel(string path, Transform parent)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null) { Debug.LogWarning("[Hollowmark] Model not found at Resources/" + path + ", using placeholder."); return null; }
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

        /// <summary>A human (player or villager). The face is a placeholder: see README for the realistic face pipeline.</summary>
        public static CharacterRig BuildHuman(Transform parent, float height, int seed, out Color skin)
        {
            var oldState = Random.state;
            Random.InitState(seed);
            skin = EnumUtil.ParseColor(SkinTones[Random.Range(0, SkinTones.Length)], Color.white);
            var hair = EnumUtil.ParseColor(HairColors[Random.Range(0, HairColors.Length)], Color.black);

            var root = new GameObject("HumanRig");
            root.transform.SetParent(parent, false);
            var rig = root.AddComponent<CharacterRig>();
            rig.Height = height;
            float s = height / 1.8f;

            float hip = 0.9f * s;
            rig.Body = Pivot(root.transform, "Body", Vector3.zero);
            for (int i = 0; i < 2; i++)
            {
                var leg = Pivot(rig.Body, i == 0 ? "LegL" : "LegR", new Vector3(i == 0 ? -0.12f * s : 0.12f * s, hip, 0f));
                Prim.Make(PrimitiveType.Cube, leg, new Vector3(0f, -0.45f * s, 0f), new Vector3(0.17f, 0.88f, 0.2f) * s, skin, "LegSkin");
                Prim.Make(PrimitiveType.Cube, leg, new Vector3(0f, -0.86f * s, 0.05f * s), new Vector3(0.19f, 0.1f, 0.3f) * s, skin * 0.85f, "Foot");
                rig.Legs.Add(leg);
            }
            Prim.Make(PrimitiveType.Cube, rig.Body, new Vector3(0f, hip + 0.33f * s, 0f), new Vector3(0.46f, 0.66f, 0.26f) * s, skin, "TorsoSkin");

            var shoulderY = hip + 0.62f * s;
            rig.LeftArm = Pivot(rig.Body, "ArmL", new Vector3(-0.31f * s, shoulderY, 0f));
            Prim.Make(PrimitiveType.Cube, rig.LeftArm, new Vector3(0f, -0.32f * s, 0f), new Vector3(0.14f, 0.66f, 0.15f) * s, skin, "ArmSkin");
            rig.RightArm = Pivot(rig.Body, "ArmR", new Vector3(0.31f * s, shoulderY, 0f));
            Prim.Make(PrimitiveType.Cube, rig.RightArm, new Vector3(0f, -0.32f * s, 0f), new Vector3(0.14f, 0.66f, 0.15f) * s, skin, "ArmSkin");
            rig.Hand = Pivot(rig.RightArm, "Hand", new Vector3(0f, -0.66f * s, 0.05f * s));

            rig.Head = Pivot(rig.Body, "Head", new Vector3(0f, shoulderY + 0.17f * s, 0f));
            BuildHumanFace(rig, s, skin, hair);

            Random.state = oldState;
            rig.CollectRenderers();
            return rig;
        }

        static void BuildHumanFace(CharacterRig rig, float s, Color skin, Color hair)
        {
            var h = rig.Head;
            float headW = Random.Range(0.21f, 0.25f) * s, headH = Random.Range(0.26f, 0.3f) * s;
            Prim.Make(PrimitiveType.Sphere, h, new Vector3(0f, 0.04f * s, 0f), new Vector3(headW, headH, 0.25f * s), skin, "Skull");
            Prim.Make(PrimitiveType.Cube, h, new Vector3(0f, -0.06f * s, 0.03f * s), new Vector3(headW * 0.75f, 0.1f * s, 0.18f * s), skin * 0.97f, "Jaw");
            float eyeSpread = Random.Range(0.045f, 0.06f) * s;
            for (int i = -1; i <= 1; i += 2)
            {
                Prim.Make(PrimitiveType.Sphere, h, new Vector3(i * eyeSpread, 0.06f * s, 0.11f * s), new Vector3(0.045f, 0.03f, 0.02f) * s, Color.white, "EyeWhite");
                var iris = Prim.Make(PrimitiveType.Sphere, h, new Vector3(i * eyeSpread, 0.06f * s, 0.12f * s), new Vector3(0.022f, 0.022f, 0.012f) * s, new Color(0.25f, 0.18f, 0.1f), "Iris");
                rig.Eyes.Add(iris.GetComponent<Renderer>());
                Prim.Make(PrimitiveType.Cube, h, new Vector3(i * eyeSpread, 0.095f * s, 0.115f * s), new Vector3(0.05f, 0.01f, 0.01f) * s, hair, "Brow");
                Prim.Make(PrimitiveType.Sphere, h, new Vector3(i * headW * 0.5f, 0.03f * s, 0f), new Vector3(0.03f, 0.06f, 0.04f) * s, skin * 0.95f, "Ear");
            }
            Prim.Make(PrimitiveType.Cube, h, new Vector3(0f, 0.025f * s, 0.125f * s), new Vector3(0.025f, Random.Range(0.045f, 0.065f), 0.035f) * s, skin * 0.92f, "Nose");
            Prim.Make(PrimitiveType.Cube, h, new Vector3(0f, -0.03f * s, 0.115f * s), new Vector3(Random.Range(0.045f, 0.07f), 0.012f, 0.01f) * s, new Color(0.55f, 0.3f, 0.28f), "Mouth");
            // 1700s queue hairstyle: short on top, tied back.
            Prim.Make(PrimitiveType.Sphere, h, new Vector3(0f, 0.1f * s, -0.015f * s), new Vector3(headW * 1.05f, headH * 0.6f, 0.26f * s), hair, "Hair");
            Prim.Make(PrimitiveType.Cube, h, new Vector3(0f, 0.0f, -0.13f * s), new Vector3(0.04f, 0.14f, 0.04f) * s, hair, "Queue");
        }

        /// <summary>Builds one of the five creature body plans with per-individual variation, so no two look alike.</summary>
        public static CharacterRig BuildCreature(Transform parent, BodyShape shape, float height, float width,
            Color skin, Color eyeColor, int seed)
        {
            var oldState = Random.state;
            Random.InitState(seed);

            var root = new GameObject("CreatureRig");
            root.transform.SetParent(parent, false);
            var rig = root.AddComponent<CharacterRig>();
            rig.Height = height;
            rig.Body = Pivot(root.transform, "Body", Vector3.zero);

            // Individual variation.
            skin = Prim.Vary(skin, 0.08f);
            float headScale = Random.Range(0.85f, 1.25f);
            float armScale = Random.Range(0.85f, 1.3f);
            float hunch = Random.Range(0f, 25f);
            int eyeCount = Random.value < 0.2f ? 3 : (Random.value < 0.08f ? 1 : 2);
            int hornCount = Random.Range(0, 4);
            var dark = skin * 0.7f; dark.a = 1f;

            switch (shape)
            {
                case BodyShape.Crawler: BuildCrawler(rig, height, width, skin, dark); break;
                default: BuildBiped(rig, shape, height, width, skin, dark, armScale, hunch); break;
            }

            // Head with face features.
            float headSize = (shape == BodyShape.Brute ? 0.3f : shape == BodyShape.Giant ? 0.18f : 0.32f) * height * headScale;
            if (shape == BodyShape.Humanoid) headSize = 0.14f * height * headScale;
            if (shape == BodyShape.Crawler) headSize = 0.45f * height * headScale;
            Prim.Make(PrimitiveType.Sphere, rig.Head, Vector3.zero, new Vector3(headSize, headSize * Random.Range(0.85f, 1.15f), headSize), skin, "Skull");
            Prim.Make(PrimitiveType.Cube, rig.Head, new Vector3(0f, -headSize * 0.3f, headSize * 0.25f),
                new Vector3(headSize * Random.Range(0.5f, 0.8f), headSize * 0.25f, headSize * 0.5f), dark, "Snout");

            for (int i = 0; i < eyeCount; i++)
            {
                float x = eyeCount == 1 ? 0f : Mathf.Lerp(-0.22f, 0.22f, eyeCount == 2 ? i : i / 2f) * headSize;
                float y = (eyeCount == 3 && i == 1 ? 0.25f : 0.1f) * headSize;
                var eye = Prim.Make(PrimitiveType.Sphere, rig.Head, new Vector3(x, y, headSize * 0.45f),
                    Vector3.one * headSize * Random.Range(0.13f, 0.2f), eyeColor, "Eye", false, true);
                rig.Eyes.Add(eye.GetComponent<Renderer>());
            }
            for (int i = 0; i < hornCount; i++)
            {
                float side = (i % 2 == 0 ? -1f : 1f) * Random.Range(0.15f, 0.35f);
                var horn = Prim.Make(PrimitiveType.Cylinder, rig.Head, new Vector3(side * headSize, headSize * 0.45f, -headSize * 0.05f),
                    new Vector3(0.06f, Random.Range(0.12f, 0.25f), 0.06f) * headSize * 3f, new Color(0.85f, 0.8f, 0.65f), "Horn");
                horn.transform.localRotation = Quaternion.Euler(Random.Range(-30f, 10f), 0f, side * 60f);
            }
            if (shape == BodyShape.Goblin || shape == BodyShape.Imp)
            {
                float earSize = Random.Range(0.25f, 0.5f) * headSize;
                for (int i = -1; i <= 1; i += 2)
                {
                    var ear = Prim.Make(PrimitiveType.Cube, rig.Head, new Vector3(i * headSize * 0.55f, headSize * 0.1f, 0f),
                        new Vector3(earSize, earSize * 0.25f, earSize * 0.15f), skin, "Ear");
                    ear.transform.localRotation = Quaternion.Euler(0f, 0f, i * Random.Range(10f, 35f));
                }
            }

            Random.state = oldState;
            rig.CollectRenderers();
            return rig;
        }

        static void BuildBiped(CharacterRig rig, BodyShape shape, float height, float width, Color skin, Color dark, float armScale, float hunch)
        {
            float legLen = height * (shape == BodyShape.Brute ? 0.3f : shape == BodyShape.Giant ? 0.42f : 0.4f);
            float torsoLen = height * (shape == BodyShape.Brute ? 0.4f : 0.35f);
            float legW = width * (shape == BodyShape.Brute ? 0.32f : 0.24f);

            for (int i = 0; i < 2; i++)
            {
                var leg = Pivot(rig.Body, "Leg", new Vector3((i == 0 ? -1f : 1f) * width * 0.22f, legLen, 0f));
                Prim.Make(PrimitiveType.Cube, leg, new Vector3(0f, -legLen * 0.5f, 0f), new Vector3(legW, legLen, legW * 1.1f), dark, "LegMesh");
                rig.Legs.Add(leg);
            }

            var torso = Pivot(rig.Body, "Torso", new Vector3(0f, legLen, 0f));
            torso.localRotation = Quaternion.Euler(hunch, 0f, 0f);
            Prim.Make(PrimitiveType.Cube, torso, new Vector3(0f, torsoLen * 0.5f, 0f), new Vector3(width * 0.8f, torsoLen, width * 0.5f), skin, "TorsoMesh");
            if (shape == BodyShape.Brute)
                Prim.Make(PrimitiveType.Sphere, torso, new Vector3(0f, torsoLen * 0.35f, width * 0.15f), new Vector3(width * 0.85f, torsoLen * 0.7f, width * 0.55f), skin, "Belly");

            float armLen = torsoLen * 1.1f * armScale;
            rig.LeftArm = Pivot(torso, "ArmL", new Vector3(-width * 0.5f, torsoLen * 0.9f, 0f));
            Prim.Make(PrimitiveType.Cube, rig.LeftArm, new Vector3(0f, -armLen * 0.5f, 0f), new Vector3(legW * 0.8f, armLen, legW * 0.8f), skin, "ArmMesh");
            rig.RightArm = Pivot(torso, "ArmR", new Vector3(width * 0.5f, torsoLen * 0.9f, 0f));
            Prim.Make(PrimitiveType.Cube, rig.RightArm, new Vector3(0f, -armLen * 0.5f, 0f), new Vector3(legW * 0.8f, armLen, legW * 0.8f), skin, "ArmMesh");
            rig.Hand = Pivot(rig.RightArm, "Hand", new Vector3(0f, -armLen, 0.05f));

            float headSize = shape == BodyShape.Giant || shape == BodyShape.Humanoid ? height * 0.14f : height * 0.3f;
            rig.Head = Pivot(torso, "Head", new Vector3(0f, torsoLen + headSize * 0.45f, shape == BodyShape.Goblin ? width * 0.15f : 0f));

            if (shape == BodyShape.Imp)
            {
                // A thin tail and stubby wings.
                var tail = Prim.Make(PrimitiveType.Cylinder, torso, new Vector3(0f, 0.05f, -width * 0.4f), new Vector3(0.05f, height * 0.25f, 0.05f), dark, "Tail");
                tail.transform.localRotation = Quaternion.Euler(-60f, 0f, 0f);
                for (int i = -1; i <= 1; i += 2)
                {
                    var wing = Prim.Make(PrimitiveType.Cube, torso, new Vector3(i * width * 0.5f, torsoLen * 0.8f, -width * 0.3f), new Vector3(width * 0.8f, height * 0.25f, 0.02f), dark, "Wing");
                    wing.transform.localRotation = Quaternion.Euler(0f, i * 40f, i * 20f);
                }
            }
        }

        static void BuildCrawler(CharacterRig rig, float height, float width, Color skin, Color dark)
        {
            float bodyLen = width * 1.6f;
            Prim.Make(PrimitiveType.Sphere, rig.Body, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height * 0.55f, bodyLen), skin, "Carapace");
            for (int i = 0; i < 6; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float z = Mathf.Lerp(-bodyLen * 0.35f, bodyLen * 0.35f, (i / 2) / 2f);
                var leg = Pivot(rig.Body, "Leg", new Vector3(side * width * 0.45f, height * 0.45f, z));
                var mesh = Prim.Make(PrimitiveType.Cube, leg, new Vector3(side * width * 0.25f, -height * 0.22f, 0f), new Vector3(width * 0.55f, 0.06f, 0.07f), dark, "LegMesh");
                mesh.transform.localRotation = Quaternion.Euler(0f, 0f, side * -35f);
                rig.Legs.Add(leg);
            }
            rig.Head = Pivot(rig.Body, "Head", new Vector3(0f, height * 0.5f, bodyLen * 0.5f));
            rig.RightArm = Pivot(rig.Head, "Mandibles", new Vector3(0f, -height * 0.1f, height * 0.15f));
            for (int i = -1; i <= 1; i += 2)
            {
                var m = Prim.Make(PrimitiveType.Cube, rig.RightArm, new Vector3(i * 0.08f, 0f, 0.12f), new Vector3(0.04f, 0.04f, 0.26f), new Color(0.8f, 0.75f, 0.6f), "Mandible");
                m.transform.localRotation = Quaternion.Euler(0f, i * -20f, 0f);
            }
            rig.Hand = rig.RightArm;
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
