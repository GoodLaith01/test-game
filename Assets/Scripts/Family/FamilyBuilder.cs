using Hollowmark.Visuals;
using UnityEngine;

namespace Hollowmark.Family
{
    /// <summary>Adds the village children, Olmar, his practice dummy by the training yard and his cot in your house.</summary>
    public static class FamilyBuilder
    {
        static readonly Color Timber = new Color(0.4f, 0.28f, 0.16f);
        static readonly Color Straw = new Color(0.62f, 0.5f, 0.26f);

        static readonly Vector3 PlayCentre = new Vector3(-7f, 0f, -6f);   // the green beside the well
        static readonly Vector3 Shelter = new Vector3(18f, 0f, -18f);      // a cottage the children run into

        public static void Build(Transform root)
        {
            Dummy(root, Olmar.DummyPos);
            Cot(root, Olmar.CotSpot + new Vector3(0.8f, 0f, 0.6f));

            var kids = Family.Config.kids;
            for (int i = 0; i < kids.Length; i++)
            {
                var pos = Layers.ClearSpot(PlayCentre, new Vector2(6f, 5f), 0.3f, 1.3f, 30);
                var kid = Kid.Spawn(root, kids[i], pos, PlayCentre, Shelter);
                kid.It = i == 0;
            }

            Olmar.SpawnInVillage(root);
        }

        static void Dummy(Transform root, Vector3 pos)
        {
            // Smaller than the grown-ups' dummies, with a straw "face" painted on.
            var dummy = new GameObject("OlmarDummy");
            dummy.transform.SetParent(root, false);
            dummy.transform.localPosition = pos;
            var body = new GameObject("Body").transform;
            body.SetParent(dummy.transform, false);
            Prim.Box(body, new Vector3(0f, 0.7f, 0f), new Vector3(0.12f, 1.4f, 0.12f), Timber, false);
            Prim.Make(PrimitiveType.Cylinder, body, new Vector3(0f, 1.0f, 0f), new Vector3(0.4f, 0.35f, 0.4f), Straw, "Straw");
            Prim.Box(body, new Vector3(0f, 1.1f, 0f), new Vector3(0.9f, 0.1f, 0.1f), Timber, false, "Arms");
            Prim.Make(PrimitiveType.Sphere, body, new Vector3(0f, 1.52f, 0f), Vector3.one * 0.28f, Straw, "Head");
            Prim.Box(body, new Vector3(0f, 1.55f, 0.13f), new Vector3(0.12f, 0.03f, 0.02f), new Color(0.2f, 0.12f, 0.08f), false, "Face");
            var col = dummy.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.8f, 0f);
            col.height = 1.6f;
            col.radius = 0.3f;
            dummy.AddComponent<OlmarDummy>();
            Prim.Label(dummy.transform, new Vector3(0f, 2.1f, 0f), "Olmar's Dummy", Color.white, 0.05f);
        }

        static void Cot(Transform root, Vector3 pos)
        {
            var cot = new GameObject("OlmarCot").transform;
            cot.SetParent(root, false);
            cot.localPosition = pos;
            Prim.Box(cot, new Vector3(0f, 0.2f, 0f), new Vector3(0.9f, 0.4f, 1.8f), Timber, false, "Frame");
            Prim.Box(cot, new Vector3(0f, 0.45f, 0f), new Vector3(0.8f, 0.1f, 1.7f), new Color(0.7f, 0.62f, 0.45f), false, "StrawMattress");
            Prim.Box(cot, new Vector3(0f, 0.52f, -0.2f), new Vector3(0.82f, 0.04f, 1.1f), new Color(0.35f, 0.25f, 0.4f), false, "Blanket");
        }
    }
}
