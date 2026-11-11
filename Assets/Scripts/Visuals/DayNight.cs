using UnityEngine;
using UnityEngine.Rendering;

namespace Hollowmark.Visuals
{
    /// <summary>
    /// The outdoor day/night cycle. From the hour of the day it sets the sky (sun, moon and stars), the sun's
    /// and moon's light and shadows, the fog and the ambient light, so the whole atmosphere moves through
    /// night, a misty golden dawn, a clear day, a red sunset and a blue moonlit night. The current weather
    /// is mixed in: overcast skies are greyer, mistier and cast softer shadows.
    /// </summary>
    public static class DayNight
    {
        /// <summary>0 in full daylight, 1 in the dead of night. Lamps use it to glow brighter after dark.</summary>
        public static float Night { get; private set; }
        /// <summary>How much colder the night is, in degrees (negative).</summary>
        public static float Chill { get; private set; }

        static Light moon;
        static Material sky;
        static bool skyTried;

        struct Key
        {
            public float Hour;
            public Color Zenith, Horizon, Sun, Ambient;
            public float SunPower, MoonPower, Mist, Stars;
            public Key(float hour, Color zenith, Color horizon, Color sun, float sunPower, Color ambient, float moonPower, float mist, float stars)
            { Hour = hour; Zenith = zenith; Horizon = horizon; Sun = sun; SunPower = sunPower; Ambient = ambient; MoonPower = moonPower; Mist = mist; Stars = stars; }
        }

        static Color C(float r, float g, float b) { return new Color(r, g, b); }

        // The look of the sky through one day. Mist multiplies the fog (morning mist, clear afternoons).
        static readonly Key[] Keys =
        {
            new Key(0f,    C(0.01f, 0.016f, 0.045f), C(0.04f, 0.06f, 0.11f), C(0.5f, 0.6f, 0.9f),  0f,    C(0.09f, 0.11f, 0.2f),  0.3f,  1.2f, 1f),
            new Key(4.3f,  C(0.015f, 0.02f, 0.06f),  C(0.06f, 0.07f, 0.13f), C(0.5f, 0.6f, 0.9f),  0f,    C(0.1f, 0.12f, 0.21f),  0.28f, 1.5f, 0.9f),
            new Key(5.4f,  C(0.12f, 0.14f, 0.3f),    C(0.55f, 0.35f, 0.32f), C(1f, 0.45f, 0.25f),  0.15f, C(0.25f, 0.22f, 0.28f), 0.08f, 2.4f, 0.25f),
            new Key(6.3f,  C(0.28f, 0.38f, 0.62f),   C(1f, 0.62f, 0.38f),    C(1f, 0.58f, 0.32f),  0.65f, C(0.45f, 0.4f, 0.38f),  0f,    2.2f, 0f),
            new Key(8f,    C(0.25f, 0.45f, 0.82f),   C(0.72f, 0.78f, 0.86f), C(1f, 0.88f, 0.72f),  1.05f, C(0.5f, 0.55f, 0.62f),  0f,    1.2f, 0f),
            new Key(12f,   C(0.17f, 0.38f, 0.82f),   C(0.62f, 0.74f, 0.9f),  C(1f, 0.97f, 0.92f),  1.3f,  C(0.55f, 0.6f, 0.68f),  0f,    0.8f, 0f),
            new Key(16f,   C(0.2f, 0.4f, 0.8f),      C(0.72f, 0.75f, 0.82f), C(1f, 0.88f, 0.7f),   1.15f, C(0.55f, 0.55f, 0.58f), 0f,    0.85f, 0f),
            new Key(18.2f, C(0.25f, 0.28f, 0.5f),    C(1f, 0.5f, 0.25f),     C(1f, 0.48f, 0.2f),   0.6f,  C(0.45f, 0.35f, 0.32f), 0f,    1.1f, 0f),
            new Key(19.2f, C(0.08f, 0.08f, 0.22f),   C(0.45f, 0.22f, 0.25f), C(0.9f, 0.35f, 0.25f), 0.08f, C(0.2f, 0.17f, 0.25f), 0.1f,  1.2f, 0.35f),
            new Key(20.5f, C(0.015f, 0.022f, 0.07f), C(0.07f, 0.08f, 0.15f), C(0.5f, 0.6f, 0.9f),  0f,    C(0.1f, 0.12f, 0.21f),  0.3f,  1.2f, 1f),
            new Key(24f,   C(0.01f, 0.016f, 0.045f), C(0.04f, 0.06f, 0.11f), C(0.5f, 0.6f, 0.9f),  0f,    C(0.09f, 0.11f, 0.2f),  0.3f,  1.2f, 1f),
        };

        static Key Sample(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            for (int i = 0; i + 1 < Keys.Length; i++)
            {
                var a = Keys[i]; var b = Keys[i + 1];
                if (hour < a.Hour || hour > b.Hour) continue;
                float t = Mathf.SmoothStep(0f, 1f, (hour - a.Hour) / (b.Hour - a.Hour));
                return new Key(hour, Color.Lerp(a.Zenith, b.Zenith, t), Color.Lerp(a.Horizon, b.Horizon, t), Color.Lerp(a.Sun, b.Sun, t),
                    Mathf.Lerp(a.SunPower, b.SunPower, t), Color.Lerp(a.Ambient, b.Ambient, t), Mathf.Lerp(a.MoonPower, b.MoonPower, t),
                    Mathf.Lerp(a.Mist, b.Mist, t), Mathf.Lerp(a.Stars, b.Stars, t));
            }
            return Keys[0];
        }

        /// <summary>Direction towards the sun: it rises in the east, crosses the southern sky and sets in the west.</summary>
        public static Vector3 SunDirection(float hour)
        {
            float t = (hour - 6f) / 24f * Mathf.PI * 2f;            // 0 at sunrise, pi/2 at noon
            float elevation = Mathf.Sin(t) * 62f * Mathf.Deg2Rad;
            float azimuth = (-90f + (hour - 6f) / 12f * 180f) * Mathf.Deg2Rad; // east -> south -> west
            return new Vector3(Mathf.Cos(elevation) * Mathf.Sin(azimuth), Mathf.Sin(elevation), -Mathf.Cos(elevation) * Mathf.Cos(azimuth)).normalized;
        }

        static Material Sky
        {
            get
            {
                if (sky != null || skyTried) return sky;
                skyTried = true;
                var shader = Shader.Find("Hollowmark/Sky");
                if (shader != null && shader.isSupported) sky = new Material(shader) { name = "HollowmarkDaySky" };
                return sky;
            }
        }

        /// <summary>
        /// Updates everything for the given hour. weatherFog/weatherDensity/weatherLight come from the current
        /// weather; cloud (0 clear .. 1 overcast) is worked out from how foggy and dim that weather is.
        /// </summary>
        public static void Apply(float hour, Light sun, Color weatherFog, float weatherDensity, Color weatherLight, float weatherLightPower, Color weatherAmbient, float dt)
        {
            var key = Sample(hour);
            float cloud = Mathf.Clamp01((weatherDensity - 0.004f) / 0.012f) * 0.8f + Mathf.Clamp01((1.15f - weatherLightPower) / 0.6f) * 0.2f;

            // Overcast skies lose their colour towards the weather's own grey.
            var grey = Color.Lerp(weatherFog, new Color(0.5f, 0.52f, 0.55f), 0.3f);
            float lum = key.Horizon.grayscale;
            var zenith = Color.Lerp(key.Zenith, grey * Mathf.Max(0.05f, key.Zenith.grayscale * 1.6f), cloud * 0.75f);
            var horizon = Color.Lerp(key.Horizon, grey * Mathf.Max(0.05f, lum * 1.2f), cloud * 0.7f);
            Night = Mathf.Clamp01(1f - key.SunPower / 0.6f);
            Chill = -7f * Night;

            // Sun and moon.
            var sunDir = SunDirection(hour);
            var moonDir = (-sunDir + new Vector3(0.2f, 0.25f, 0.15f)).normalized;
            if (moonDir.y < 0.15f) moonDir = new Vector3(moonDir.x, 0.15f, moonDir.z).normalized;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.LookRotation(-sunDir);
                var sunColor = Color.Lerp(key.Sun, weatherLight, 0.25f + cloud * 0.4f);
                float power = key.SunPower * Mathf.Lerp(1f, 0.45f, cloud) * Mathf.Clamp01(sunDir.y * 6f + 0.4f);
                sun.color = Color.Lerp(sun.color, sunColor, dt * 2f);
                sun.intensity = Mathf.Lerp(sun.intensity, power, dt * 2f);
                // Clear skies cast crisp dark shadows, cloudy ones soft pale ones; long low-sun shadows fade a little.
                sun.shadowStrength = Mathf.Lerp(0.92f, 0.45f, cloud) * Mathf.Clamp01(sunDir.y * 4f + 0.2f);
                sun.shadows = sunDir.y > -0.02f ? LightShadows.Soft : LightShadows.None;
            }
            var m = Moon(sun);
            if (m != null)
            {
                m.transform.rotation = Quaternion.LookRotation(-moonDir);
                m.intensity = key.MoonPower * Mathf.Lerp(1f, 0.35f, cloud);
                m.color = new Color(0.62f, 0.72f, 1f);
                bool moonShadows = m.intensity > 0.05f && (sun == null || sun.shadows == LightShadows.None);
                m.shadows = moonShadows ? LightShadows.Soft : LightShadows.None;
                m.shadowStrength = 0.55f * (1f - cloud * 0.6f);
                m.enabled = m.intensity > 0.005f;
            }

            // Fog takes the colour of the horizon, thick at dawn, thin on clear afternoons.
            var fogColor = Color.Lerp(horizon, weatherFog * Mathf.Max(0.08f, lum * 1.3f), 0.25f + cloud * 0.4f);
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, fogColor, dt * 1.5f);
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, weatherDensity * key.Mist, dt * 0.5f);

            // Ambient: the sky's colour from above, the horizon's from the sides, dim earth bounce from below.
            var amb = Color.Lerp(key.Ambient, weatherAmbient * Mathf.Max(0.2f, key.Ambient.grayscale * 1.8f), 0.3f + cloud * 0.3f);
            RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, Color.Lerp(amb, zenith, 0.25f) * 1.1f, dt * 2f);
            RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, Color.Lerp(amb, horizon, 0.3f) * 0.85f, dt * 2f);
            RenderSettings.ambientGroundColor = Color.Lerp(RenderSettings.ambientGroundColor, amb * new Color(0.55f, 0.5f, 0.42f), dt * 2f);

            // The sky itself.
            var cam = Camera.main;
            var mat = Sky;
            if (mat != null)
            {
                if (RenderSettings.skybox != mat) RenderSettings.skybox = mat;
                mat.SetColor("_Zenith", zenith);
                mat.SetColor("_Horizon", horizon);
                mat.SetColor("_Ground", Color.Lerp(horizon, new Color(0.18f, 0.17f, 0.15f), 0.6f) * Mathf.Lerp(1f, 0.25f, Night));
                mat.SetVector("_SunDir", sunDir);
                mat.SetColor("_SunColor", key.Sun * (1f - cloud * 0.85f) * Mathf.Clamp01(sunDir.y * 8f + 0.6f));
                mat.SetVector("_MoonDir", moonDir);
                mat.SetColor("_MoonColor", new Color(0.85f, 0.88f, 0.95f) * key.MoonPower / 0.3f * (1f - cloud * 0.8f));
                mat.SetFloat("_Stars", key.Stars * (1f - cloud));
                mat.SetFloat("_Time2", Time.time);
                if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
            }
            else Atmosphere.UpdateSky(RenderSettings.fogColor, 1f - Night, true);

            // Reflections on metal and wet stone follow the sky every few seconds.
            if (Time.time > nextReflection) { nextReflection = Time.time + 4f; DynamicGI.UpdateEnvironment(); }
            BlobShadow.SetStrength(Mathf.Lerp(0.28f, 0.5f, Mathf.Max(Night, cloud * 0.6f)));
        }

        static float nextReflection;

        /// <summary>Indoors / underground: no moon, no sky.</summary>
        public static void Underground()
        {
            Night = 0f; Chill = 0f;
            if (moon != null) moon.enabled = false;
            BlobShadow.SetStrength(0.55f);
        }

        static Light Moon(Light sun)
        {
            if (moon != null) return moon;
            if (sun == null) return null;
            var go = new GameObject("Moon");
            go.transform.SetParent(sun.transform.parent, false);
            moon = go.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.shadowBias = 0.05f;
            moon.shadowNormalBias = 0.4f;
            return moon;
        }
    }

    /// <summary>
    /// Real shadows from torches, lamps and fires, but only for the few lights nearest the hero, so it stays fast.
    /// </summary>
    public class ShadowLights : MonoBehaviour
    {
        public int Count = 3;
        public float Range = 16f;
        Light[] lights = new Light[0];
        Transform root;
        float next;

        void Update()
        {
            if (Time.time < next) return;
            next = Time.time + 0.35f;
            var gm = GameManager.Instance;
            var cam = Camera.main;
            if (gm == null || gm.WorldRoot == null || cam == null) return;
            if (root != gm.WorldRoot) { root = gm.WorldRoot; lights = root.GetComponentsInChildren<Light>(true); }
            var focus = Player.Hero.Instance != null ? Player.Hero.Instance.transform.position : cam.transform.position;

            // Pick the nearest bright point lights.
            var best = new Light[Count];
            var bestD = new float[Count];
            for (int i = 0; i < Count; i++) bestD[i] = float.MaxValue;
            foreach (var l in lights)
            {
                if (l == null || l.type != LightType.Point || !l.isActiveAndEnabled || l.intensity < 0.4f) continue;
                float d = (l.transform.position - focus).sqrMagnitude;
                if (d > Range * Range) continue;
                for (int i = 0; i < Count; i++)
                    if (d < bestD[i])
                    {
                        for (int j = Count - 1; j > i; j--) { best[j] = best[j - 1]; bestD[j] = bestD[j - 1]; }
                        best[i] = l; bestD[i] = d;
                        break;
                    }
            }
            foreach (var l in lights)
            {
                if (l == null || l.type != LightType.Point) continue;
                bool on = System.Array.IndexOf(best, l) >= 0;
                l.shadows = on ? LightShadows.Soft : LightShadows.None;
                if (on) { l.shadowStrength = 0.8f; l.shadowBias = 0.05f; l.shadowNormalBias = 0.6f; }
            }
        }
    }

    /// <summary>A soft dark patch under a character's feet, so people and creatures sit on the ground even in the dark.</summary>
    public class BlobShadow : MonoBehaviour
    {
        static Material mat;
        static Mesh quad;
        Renderer r;
        CharacterRig rig;

        static float strength = 0.5f;

        /// <summary>Darker in dungeons and at night (when there is no sun shadow), lighter in daylight.</summary>
        public static void SetStrength(float a)
        {
            if (Mathf.Abs(a - strength) < 0.01f) return;
            strength = a;
            if (mat != null) Prim.SetColor(mat, new Color(0f, 0f, 0f, a));
            if (mat != null && mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", new Color(0f, 0f, 0f, a * 0.5f));
        }

        public static void Add(CharacterRig rig, float radius)
        {
            if (rig == null || rig.GetComponentInChildren<BlobShadow>() != null) return;
            if (mat == null)
            {
                if (Prim.ParticleShader == null) return;
                mat = Prim.ParticleMat(new Color(0f, 0f, 0f, strength));
                mat.name = "BlobShadow";
                mat.renderQueue = 2999; // after the ground, before other transparent things
            }
            if (quad == null)
            {
                quad = new Mesh { name = "BlobQuad" };
                quad.vertices = new[] { new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f), new Vector3(1f, 0f, 1f), new Vector3(-1f, 0f, 1f) };
                quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
                quad.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                quad.RecalculateBounds();
            }
            var go = new GameObject("BlobShadow");
            go.transform.SetParent(rig.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.025f, 0f);
            go.transform.localScale = Vector3.one * radius;
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var b = go.AddComponent<BlobShadow>();
            b.r = mr;
            b.rig = rig;
            rig.AllRenderers.Add(mr); // hidden with the body when a creature hides in the shadows
        }

        void LateUpdate()
        {
            if (rig != null && rig.Dead && r.enabled) r.enabled = false;
            // Stay flat on the ground even when the body leans or bobs.
            transform.rotation = Quaternion.Euler(0f, transform.parent.eulerAngles.y, 0f);
        }
    }
}
