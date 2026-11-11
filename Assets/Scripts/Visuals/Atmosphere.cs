using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Hollowmark.Visuals
{
    /// <summary>
    /// Look-and-feel settings applied once at start: sharper, longer shadows, more lights per object, a real sky
    /// with a sun, three-colour ambient light, HDR, anti-aliasing, and post-processing (bloom, filmic tone mapping,
    /// colour grading and a vignette). Works with the Built-in pipeline and with URP.
    /// </summary>
    public static class Atmosphere
    {
        public static Material Sky { get; private set; }
        static bool urp;

        public static void Setup(Camera cam, Light sun)
        {
            urp = GraphicsSettings.currentRenderPipeline != null;

            // Quality: these only change what the current quality level allows, so they are safe on any machine.
            QualitySettings.shadowDistance = 80f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 8);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.softParticles = true;
            if (!urp) QualitySettings.antiAliasing = 4;

            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.fieldOfView = 60f;

            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.35f;
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.reflectionIntensity = 0.55f;

            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                Sky = new Material(skyShader) { name = "HollowmarkSky" };
                SetIf(Sky, "_SunSize", 0.035f);
                SetIf(Sky, "_SunSizeConvergence", 6f);
                SetIf(Sky, "_AtmosphereThickness", 1.1f);
                if (Sky.HasProperty("_SkyTint")) Sky.SetColor("_SkyTint", new Color(0.52f, 0.56f, 0.62f));
                if (Sky.HasProperty("_GroundColor")) Sky.SetColor("_GroundColor", new Color(0.33f, 0.31f, 0.28f));
                SetIf(Sky, "_Exposure", 1.15f);
                RenderSettings.skybox = Sky;
            }

            if (urp) SetupUrpPost(cam);
            else cam.gameObject.AddComponent<PostFx>();
        }

        static void SetIf(Material m, string prop, float v) { if (m.HasProperty(prop)) m.SetFloat(prop, v); }

        /// <summary>Called by the weather every frame: the sky follows the weather's tint and gets darker at night.</summary>
        public static void UpdateSky(Color fog, float daylight, bool outdoors)
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (!outdoors || Sky == null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = RenderSettings.fogColor;
                return;
            }
            cam.clearFlags = CameraClearFlags.Skybox;
            if (RenderSettings.skybox != Sky) RenderSettings.skybox = Sky;
            if (Sky.HasProperty("_SkyTint")) Sky.SetColor("_SkyTint", Color.Lerp(new Color(0.5f, 0.55f, 0.62f), fog, 0.45f));
            SetIf(Sky, "_Exposure", Mathf.Lerp(0.12f, 1.15f, daylight));
            SetIf(Sky, "_AtmosphereThickness", Mathf.Lerp(1.1f, 1.6f, Mathf.Clamp01(RenderSettings.fogDensity * 25f)));
        }

        /// <summary>Three-colour ambient light: sky from above, a softer horizon, and dim bounce light from the ground.</summary>
        public static void SetAmbient(Color ambient, Color ground)
        {
            RenderSettings.ambientSkyColor = ambient * 1.1f;
            RenderSettings.ambientEquatorColor = ambient * 0.8f;
            RenderSettings.ambientGroundColor = Color.Lerp(ambient, ground, 0.5f) * 0.45f;
        }

        // ---------- URP post-processing, set up by reflection so the game compiles without the URP package ----------

        static void SetupUrpPost(Camera cam)
        {
            try
            {
                var camData = FindType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData", "Unity.RenderPipelines.Universal.Runtime");
                if (camData != null)
                {
                    var data = cam.GetComponent(camData) ?? cam.gameObject.AddComponent(camData);
                    SetProp(data, "renderPostProcessing", true);
                    var aaProp = camData.GetProperty("antialiasing");
                    if (aaProp != null) aaProp.SetValue(data, Enum.ToObject(aaProp.PropertyType, 2), null); // SMAA
                }

                var volumeType = FindType("UnityEngine.Rendering.Volume", "Unity.RenderPipelines.Core.Runtime");
                var profileType = FindType("UnityEngine.Rendering.VolumeProfile", "Unity.RenderPipelines.Core.Runtime");
                if (volumeType == null || profileType == null) return;
                var go = new GameObject("PostProcessing");
                Object.DontDestroyOnLoad(go);
                var volume = go.AddComponent(volumeType);
                SetProp(volume, "isGlobal", true);
                SetProp(volume, "priority", 10f);
                var profile = ScriptableObject.CreateInstance(profileType);
                SetProp(volume, "profile", profile);

                var add = profileType.GetMethod("Add", new[] { typeof(Type), typeof(bool) });
                Func<string, object> component = name =>
                {
                    var t = FindType("UnityEngine.Rendering.Universal." + name, "Unity.RenderPipelines.Universal.Runtime");
                    return t == null || add == null ? null : add.Invoke(profile, new object[] { t, false });
                };

                var tonemap = component("Tonemapping");
                Param(tonemap, "mode", 2); // ACES
                var bloom = component("Bloom");
                Param(bloom, "intensity", 0.6f); Param(bloom, "threshold", 1.0f); Param(bloom, "scatter", 0.65f);
                var grade = component("ColorAdjustments");
                Param(grade, "postExposure", 0.25f); Param(grade, "contrast", 12f); Param(grade, "saturation", -6f);
                Param(grade, "colorFilter", new Color(1f, 0.97f, 0.92f));
                var vignette = component("Vignette");
                Param(vignette, "intensity", 0.3f); Param(vignette, "smoothness", 0.45f);
                var lift = component("LiftGammaGain");
                Param(lift, "lift", new Vector4(0.98f, 1f, 1.04f, 0f));
                Param(lift, "gain", new Vector4(1.03f, 1f, 0.96f, 0f));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Hollowmark] URP post-processing could not be set up: " + e.Message);
            }
        }

        static Type FindType(string name, string assembly)
        {
            var t = Type.GetType(name + ", " + assembly);
            if (t != null) return t;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = a.GetType(name);
                if (t != null) return t;
            }
            return null;
        }

        static void SetProp(object target, string name, object value)
        {
            if (target == null) return;
            var type = target.GetType();
            var p = type.GetProperty(name);
            if (p != null && p.CanWrite) { p.SetValue(target, Convert(value, p.PropertyType), null); return; }
            var f = type.GetField(name);
            if (f != null) f.SetValue(target, Convert(value, f.FieldType));
        }

        static object Convert(object value, Type to)
        {
            if (value == null || to.IsInstanceOfType(value)) return value;
            if (to.IsEnum) return Enum.ToObject(to, value);
            return System.Convert.ChangeType(value, to);
        }

        /// <summary>Sets a volume parameter (e.g. bloom.intensity) and marks it as overridden.</summary>
        static void Param(object component, string field, object value)
        {
            if (component == null) return;
            var f = component.GetType().GetField(field);
            if (f == null) return;
            var param = f.GetValue(component);
            if (param == null) return;
            var pt = param.GetType();
            var valueProp = pt.GetProperty("value");
            if (valueProp != null) valueProp.SetValue(param, Convert(value, valueProp.PropertyType), null);
            var overrideProp = pt.GetProperty("overrideState");
            if (overrideProp != null) overrideProp.SetValue(param, true, null);
        }
    }

    /// <summary>
    /// Built-in pipeline post-processing: soft bloom on bright things (lamps, glowing eyes, embers), a filmic tone
    /// curve when the camera renders in HDR, a warm period colour grade and a vignette. Turns itself off if the
    /// shader is missing or not supported, so the picture is never broken.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PostFx : MonoBehaviour
    {
        public float BloomIntensity = 0.55f, BloomThreshold = 0.85f, Exposure = 1.1f, Vignette = 0.32f, Saturation = 0.94f, Contrast = 1.08f;
        public Color Grade = new Color(1.03f, 1f, 0.94f);
        Material mat;
        readonly RenderTexture[] chain = new RenderTexture[6];

        void Start()
        {
            var shader = Shader.Find("Hidden/Hollowmark/PostFx");
            if (shader == null || !shader.isSupported) { enabled = false; return; }
            mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnDestroy() { if (mat != null) Destroy(mat); }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (mat == null) { Graphics.Blit(src, dst); return; }
            bool hdr = src.format == RenderTextureFormat.ARGBHalf || src.format == RenderTextureFormat.ARGBFloat
                || src.format == RenderTextureFormat.DefaultHDR || src.format == RenderTextureFormat.RGB111110Float;
            mat.SetFloat("_Threshold", hdr ? Mathf.Max(1f, BloomThreshold) : Mathf.Max(0.93f, BloomThreshold));
            mat.SetFloat("_Bloom", BloomIntensity);
            mat.SetFloat("_Exposure", Exposure);
            mat.SetFloat("_Tonemap", hdr ? 1f : 0f);
            mat.SetFloat("_Vignette", Vignette);
            mat.SetFloat("_Saturation", Saturation);
            mat.SetFloat("_Contrast", Contrast);
            mat.SetColor("_Grade", Grade);

            // Bloom: threshold and halve, blur down a chain of smaller textures, then add them back up.
            int w = src.width / 2, h = src.height / 2, levels = 0;
            var fmt = hdr ? src.format : RenderTextureFormat.Default;
            for (int i = 0; i < chain.Length && w >= 8 && h >= 8; i++, w /= 2, h /= 2)
            {
                chain[i] = RenderTexture.GetTemporary(w, h, 0, fmt);
                chain[i].filterMode = FilterMode.Bilinear;
                Graphics.Blit(i == 0 ? src : chain[i - 1], chain[i], mat, i == 0 ? 0 : 1);
                levels++;
            }
            for (int i = levels - 1; i > 0; i--) Graphics.Blit(chain[i], chain[i - 1], mat, 2);
            mat.SetTexture("_BloomTex", levels > 0 ? chain[0] : (Texture)Texture2D.blackTexture);
            Graphics.Blit(src, dst, mat, 3);
            for (int i = 0; i < levels; i++) { RenderTexture.ReleaseTemporary(chain[i]); chain[i] = null; }
        }
    }

    /// <summary>Makes a light flicker like a flame.</summary>
    public class Flicker : MonoBehaviour
    {
        public float Amount = 0.18f, Speed = 7f;
        Light l;
        float baseIntensity, seed;

        void Start()
        {
            l = GetComponent<Light>();
            if (l == null) { enabled = false; return; }
            baseIntensity = l.intensity;
            seed = UnityEngine.Random.value * 100f;
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * Speed) - 0.5f;
            l.intensity = baseIntensity * (1f + n * 2f * Amount);
        }
    }

    /// <summary>A small flame made of glowing layered cones that sway and flicker (no particle shaders needed).</summary>
    public class Flame : MonoBehaviour
    {
        Transform outer, inner;
        Vector3 outerScale, innerScale;
        float seed;

        public static Flame Create(Transform parent, Vector3 localPos, float size, Color color)
        {
            var go = new GameObject("FlameFx");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var f = go.AddComponent<Flame>();
            var cone = BodyMesh.Get("Cone");
            f.outer = Prim.Shape(cone, go.transform, Vector3.zero, new Vector3(0.5f, 1.2f, 0.5f) * size, color * 1.6f, "Flame", true).transform;
            f.inner = Prim.Shape(cone, go.transform, new Vector3(0f, 0.02f, 0f) * size, new Vector3(0.28f, 0.75f, 0.28f) * size,
                Color.Lerp(color, new Color(1f, 0.95f, 0.7f), 0.7f) * 2f, "FlameCore", true).transform;
            f.outerScale = f.outer.localScale;
            f.innerScale = f.inner.localScale;
            f.seed = UnityEngine.Random.value * 50f;
            return f;
        }

        void Update()
        {
            float t = Time.time * 9f;
            float a = Mathf.PerlinNoise(seed, t), b = Mathf.PerlinNoise(seed + 7f, t * 1.3f);
            outer.localScale = new Vector3(outerScale.x * (0.85f + 0.3f * b), outerScale.y * (0.75f + 0.5f * a), outerScale.z * (0.85f + 0.3f * b));
            inner.localScale = innerScale * (0.8f + 0.4f * b);
            outer.localRotation = Quaternion.Euler((a - 0.5f) * 14f, 0f, (b - 0.5f) * 14f);
        }
    }
}
