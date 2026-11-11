using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hollowmark.Visuals
{
    /// <summary>
    /// Art helpers: Unity primitives and generated meshes with flat-coloured materials.
    /// Works with the Built-in pipeline (Standard shader) and falls back to URP/HDRP Lit if a pipeline is active.
    /// </summary>
    public static class Prim
    {
        static readonly Dictionary<int, Material> cache = new Dictionary<int, Material>();
        static Shader lit, unlit, particle;
        static Font font;

        public static Shader Lit
        {
            get
            {
                if (lit == null)
                {
                    if (GraphicsSettings.currentRenderPipeline != null)
                        lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("HDRP/Lit");
                    if (lit == null) lit = Shader.Find("Standard");
                    if (lit == null) lit = Shader.Find("Diffuse");
                }
                return lit;
            }
        }

        public static Shader Unlit
        {
            get
            {
                if (unlit == null)
                {
                    if (GraphicsSettings.currentRenderPipeline != null) unlit = Shader.Find("Universal Render Pipeline/Unlit");
                    if (unlit == null) unlit = Shader.Find("Unlit/Color");
                    if (unlit == null) unlit = Lit;
                }
                return unlit;
            }
        }

        public static Shader ParticleShader
        {
            get
            {
                if (particle == null)
                {
                    if (GraphicsSettings.currentRenderPipeline != null) particle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                    if (particle == null) particle = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
                    if (particle == null) particle = Shader.Find("Particles/Standard Unlit");
                    if (particle == null) particle = Shader.Find("Sprites/Default");
                }
                return particle;
            }
        }

        /// <summary>A soft, alpha-blended particle material in the given colour, for either render pipeline.</summary>
        public static Material ParticleMat(Color c)
        {
            var m = new Material(ParticleShader) { name = "Particle" };
            foreach (var prop in new[] { "_MainTex", "_BaseMap" }) if (m.HasProperty(prop)) m.SetTexture(prop, Surfaces.SoftDot);
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", c * 0.5f);
            SetColor(m, c);
            if (m.HasProperty("_Surface"))
            {
                // URP particles: transparent, alpha blended, no depth writes.
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            return m;
        }

        public static Font DefaultFont
        {
            get
            {
                if (font == null)
                {
                    // The built-in font was renamed in Unity 2022.2.
                    try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                    if (font == null) { try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
                }
                return font;
            }
        }

        /// <summary>A textured material for the given surface (see Surfaces). Glowing materials stay flat.</summary>
        public static Material Mat(Color c, Surface surface)
        {
            if (surface == Surface.None) return Mat(c);
            return Surfaces.Get(c, surface);
        }

        public static Material Mat(Color c, bool glow = false)
        {
            int key = c.GetHashCode() ^ (glow ? 0x5bd1e995 : 0);
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(glow ? Unlit : Lit);
            SetColor(m, c);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            cache[key] = m;
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        /// <summary>Creates a coloured primitive. Colliders are removed unless asked for, so visuals never block movement.</summary>
        public static GameObject Make(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color,
            string name = null, bool collider = false, bool glow = false)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var r = go.GetComponent<Renderer>();
            if (glow) { r.sharedMaterial = Mat(color, true); r.shadowCastingMode = ShadowCastingMode.Off; }
            else Texture(go, color, Surfaces.Guess(name ?? go.name, color));
            if (!collider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col); // immediate, so it can never block the frame it was built in
            }
            return go;
        }

        /// <summary>
        /// Gives an object a textured material, with UVs in metres so the texture is the same size on every object.
        /// Tiny pieces (eyes, buttons) keep their own UVs and get the plain surface.
        /// </summary>
        public static void Texture(GameObject go, Color color, Surface surface)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var scale = go.transform.lossyScale;
            float size = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            var mf = go.GetComponent<MeshFilter>();
            if (size < 0.12f && surface != Surface.Metal && surface != Surface.Gold && surface != Surface.Wet) surface = Surface.Plain;
            else if (mf != null && mf.sharedMesh != null)
            {
                var m = Surfaces.MetricCopy(mf.sharedMesh, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                if (m != null) mf.sharedMesh = m;
            }
            r.sharedMaterial = Mat(color, surface);
        }

        /// <summary>Like Make, but with a generated mesh (see BodyMesh) instead of a primitive.</summary>
        public static GameObject Shape(Mesh mesh, Transform parent, Vector3 localPos, Vector3 localScale, Color color, string name, bool glow = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            if (glow) { r.sharedMaterial = Mat(color, true); r.shadowCastingMode = ShadowCastingMode.Off; }
            else
            {
                Surfaces.EnsureUV(mesh);
                r.sharedMaterial = Mat(color, Surfaces.ForGarment(name, color));
            }
            return go;
        }

        public static GameObject Box(Transform parent, Vector3 pos, Vector3 size, Color c, bool collider = true, string name = "Block")
        {
            return Make(PrimitiveType.Cube, parent, pos, size, c, name, collider);
        }

        public static TextMesh Label(Transform parent, Vector3 localPos, string text, Color color, float size = 0.25f)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.color = color;
            tm.characterSize = size;
            tm.fontSize = 48;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            var f = DefaultFont;
            if (f != null)
            {
                tm.font = f;
                go.GetComponent<MeshRenderer>().sharedMaterial = f.material;
            }
            go.AddComponent<Billboard>();
            return tm;
        }

        public static Light PointLight(Transform parent, Vector3 pos, Color c, float range, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        public static Color Vary(Color c, float amount)
        {
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            h = Mathf.Repeat(h + Random.Range(-amount, amount) * 0.15f, 1f);
            s = Mathf.Clamp01(s + Random.Range(-amount, amount));
            v = Mathf.Clamp01(v + Random.Range(-amount, amount));
            return Color.HSVToRGB(h, s, v);
        }
    }

    /// <summary>Keeps world-space labels facing the camera.</summary>
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
