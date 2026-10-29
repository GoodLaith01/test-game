using System.Collections.Generic;
using Hollowmark.Data;
using Hollowmark.Player;
using Hollowmark.Visuals;
using UnityEngine;

namespace Hollowmark.World
{
    /// <summary>
    /// Drives fog, light, particles (rain, snow, ash, embers, spores), wind and lightning from WeatherDef data,
    /// and changes the weather every so often. Its temperature feeds the hero's body temperature.
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }

        public WeatherDef Current { get; private set; }
        public float Temperature { get { return Current != null ? Current.temperature + nightChill : 18f; } }
        public float InsanityMultiplier { get { return Current != null ? Current.insanityMultiplier : 1f; } }
        public float StaminaRegenMultiplier { get { return Current != null ? Current.staminaRegenMultiplier : 1f; } }

        public Light Sun;
        public bool Outdoors;     // the village has a day/night cycle; dungeons do not

        readonly List<WeatherDef> options = new List<WeatherDef>();
        float changeEvery = 150f, nextChange, nextLightning, nightChill;
        ParticleSystem particles;
        Color fogTarget, ambientTarget, lightTarget;
        float fogDensityTarget, lightIntensityTarget;

        void Awake()
        {
            Instance = this;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            BuildParticles();
        }

        public void SetWeathers(string[] ids, float changeSeconds, bool outdoors)
        {
            options.Clear();
            if (ids != null) foreach (var id in ids) { var w = GameDatabase.Weather(id); if (w != null) options.Add(w); }
            changeEvery = Mathf.Max(20f, changeSeconds);
            Outdoors = outdoors;
            Current = null;
            Pick(true);
        }

        void Pick(bool instant)
        {
            if (options.Count == 0) return;
            WeatherDef next = GameDatabase.PickWeighted(options, w => w.weight);
            bool changed = next != Current;
            Current = next;
            nextChange = Time.time + changeEvery * Random.Range(0.7f, 1.3f);
            fogTarget = next.Fog;
            ambientTarget = next.Ambient;
            lightTarget = next.Light;
            fogDensityTarget = next.fogDensity;
            lightIntensityTarget = next.lightIntensity;
            ConfigureParticles(next);
            if (instant)
            {
                RenderSettings.fogColor = fogTarget;
                RenderSettings.fogDensity = fogDensityTarget;
                RenderSettings.ambientLight = ambientTarget;
                if (Sun != null) { Sun.color = lightTarget; Sun.intensity = lightIntensityTarget; }
            }
            if (changed && !string.IsNullOrEmpty(next.message) && Hero.Instance != null)
                Hero.Instance.Say(next.name + ": " + next.message + " (" + next.temperature.ToString("0") + "°C)");
        }

        void Update()
        {
            if (Current == null) return;
            if (Time.time > nextChange) Pick(false);

            // Day/night only matters outdoors.
            float dayLight = 1f;
            nightChill = 0f;
            if (Outdoors && GameManager.Instance != null)
            {
                float hour = GameManager.Instance.Hour;
                float sunHeight = Mathf.Sin((hour - 6f) / 24f * Mathf.PI * 2f); // -1..1, peaks at noon
                dayLight = Mathf.Clamp01(sunHeight * 1.5f + 0.35f);
                nightChill = -6f * (1f - dayLight);
                if (Sun != null) Sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(-10f, 170f, (hour - 5f) / 15f), -30f, 0f);
            }

            float k = Time.deltaTime * 0.3f;
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, fogTarget * Mathf.Lerp(0.25f, 1f, dayLight), k);
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, fogDensityTarget, k);
            RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, ambientTarget * Mathf.Lerp(0.3f, 1f, dayLight), k);
            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = RenderSettings.fogColor; }

            if (Sun != null)
            {
                Sun.color = Color.Lerp(Sun.color, lightTarget, k);
                float target = lightIntensityTarget * dayLight;
                if (Current.lightning && Time.time > nextLightning)
                {
                    nextLightning = Time.time + Random.Range(6f, 18f);
                    Sun.intensity = 3f;
                    if (Hero.Instance != null && Random.value < 0.3f) Hero.Instance.Cam.Shake(0.6f);
                }
                Sun.intensity = Mathf.Lerp(Sun.intensity, target, Time.deltaTime * 4f);
            }

            if (particles != null && Hero.Instance != null)
                {
                // Falling weather starts overhead; floating weather (embers, spores, dust) around the hero.
                bool falls = Current.Particles == ParticleKind.Rain || Current.Particles == ParticleKind.Snow || Current.Particles == ParticleKind.Ash;
                particles.transform.position = Hero.Instance.transform.position + Vector3.up * (falls ? 8f : 0.5f);
            }
        }

        void BuildParticles()
        {
            var go = new GameObject("WeatherParticles");
            go.transform.SetParent(transform, false);
            particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.maxParticles = 4000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 1f, 30f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            var shader = Prim.ParticleShader;
            if (shader != null) r.sharedMaterial = new Material(shader);
        }

        void ConfigureParticles(WeatherDef w)
        {
            if (particles == null) return;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (w.Particles == ParticleKind.None) return;

            var main = particles.main;
            var emission = particles.emission;
            emission.rateOverTime = w.particleRate;
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            var r = particles.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;

            Color c = Color.white; float size = 0.06f, speed = 0f, life = 3f; Vector3 drift = Vector3.zero;
            switch (w.Particles)
            {
                case ParticleKind.Rain: c = new Color(0.7f, 0.75f, 0.85f, 0.6f); size = 0.04f; speed = 14f; life = 1f;
                    r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; break;
                case ParticleKind.Snow: c = Color.white; size = 0.08f; speed = 1.5f; life = 6f; break;
                case ParticleKind.Ash: c = new Color(0.15f, 0.12f, 0.12f); size = 0.07f; speed = 1f; life = 7f; break;
                case ParticleKind.Embers: c = new Color(1f, 0.5f, 0.1f); size = 0.05f; speed = -1.5f; life = 4f; break;
                case ParticleKind.Spores: c = new Color(0.6f, 0.9f, 0.5f, 0.7f); size = 0.05f; speed = 0.2f; life = 8f; break;
                case ParticleKind.Dust: c = new Color(0.7f, 0.65f, 0.55f, 0.5f); size = 0.03f; speed = 0.1f; life = 8f; break;
            }
            drift = new Vector3(w.wind, 0f, w.wind * 0.3f);
            main.startColor = c;
            main.startSize = size;
            main.startSpeed = 0f;
            main.startLifetime = life;
            velocity.x = new ParticleSystem.MinMaxCurve(drift.x - 0.3f, drift.x + 0.3f);
            velocity.y = new ParticleSystem.MinMaxCurve(-speed - 0.3f, -speed + 0.3f);
            velocity.z = new ParticleSystem.MinMaxCurve(drift.z - 0.3f, drift.z + 0.3f);
            particles.Play();
        }
    }
}
