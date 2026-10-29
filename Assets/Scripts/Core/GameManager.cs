using System.Collections.Generic;
using Hollowmark.Data;
using Hollowmark.Enemies;
using Hollowmark.Player;
using Hollowmark.UI;
using Hollowmark.World;
using UnityEngine;

namespace Hollowmark
{
    /// <summary>
    /// Owns the run: the clock, the village threat meter, which bosses are dead, and switching between
    /// the village hub and the dungeons (each world is built procedurally from data under WorldRoot).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public Transform WorldRoot { get; private set; }
        public Vector3 SpawnPoint { get; private set; }
        public DungeonDef CurrentDungeon { get; private set; }
        public bool InVillage { get { return CurrentDungeon == null; } }

        public float WorldMinutes;
        public float Threat = 30f;    // 0..100. At 100 the bosses' creatures raid the village.
        public int Day { get { return (int)(WorldMinutes / 1440f) + 1; } }
        public float Hour { get { return (WorldMinutes / 60f) % 24f; } }
        public BossAI ActiveBoss { get; set; }
        public VillageRaid Raid { get; set; }
        public bool Paused;

        readonly HashSet<string> defeated = new HashSet<string>();
        Hero hero;
        WeatherSystem weather;

        public void Init()
        {
            Instance = this;
            GameDatabase.Load();

            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.gameObject.AddComponent<AudioListener>();
            }
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;

            // Replace whatever lights the scene had with our own sun.
            foreach (var l in FindObjectsOfTypeAll<Light>()) if (l.type == LightType.Directional) l.gameObject.SetActive(false);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.transform.SetParent(transform, false);

            weather = gameObject.AddComponent<WeatherSystem>();
            weather.Sun = sun;

            WorldMinutes = GameDatabase.Balance.startHour * 60f;
            gameObject.AddComponent<Hud>();
            hero = Hero.Create(cam);
            hero.Stats.OnMessage += Hud.Push;
            EnterVillage();
            Hud.Instance.ShowIntro();
        }

        /// <summary>Scene objects of a type, without the FindObjectsOfType APIs that changed between Unity versions.</summary>
        static System.Collections.Generic.List<T> FindObjectsOfTypeAll<T>() where T : Object
        {
            var result = new System.Collections.Generic.List<T>();
            foreach (var o in Resources.FindObjectsOfTypeAll<T>())
            {
                var c = o as Component;
                if (c != null && c.gameObject.scene.IsValid()) result.Add(o);
            }
            return result;
        }

        void Update()
        {
            if (hero == null || Paused) return;
            float dt = Time.deltaTime;
            float minutes = dt * GameDatabase.Balance.gameMinutesPerSecond;
            WorldMinutes += minutes;
            GrowThreat(minutes);
            hero.Stats.Tick(dt, minutes);

            if (InVillage && Raid == null && Threat >= GameDatabase.Balance.raidThreshold && !hero.Stats.Dead)
                Raid = VillageRaid.Begin(WorldRoot, HighestUnlockedTier());
        }

        void GrowThreat(float minutes)
        {
            if (defeated.Count >= GameDatabase.Dungeons.Count) return; // all bosses dead: the village is safe
            Threat = Mathf.Min(100f, Threat + GameDatabase.Balance.threatPerGameDay * minutes / 1440f);
        }

        /// <summary>Time passing from training and similar actions.</summary>
        public void AdvanceTime(float minutes)
        {
            WorldMinutes += minutes;
            GrowThreat(minutes);
            hero.Stats.Tick(0f, minutes);
        }

        public float HoursUntil(float hour)
        {
            float h = hour - Hour;
            if (h <= 0f) h += 24f;
            return h;
        }

        public void Sleep(float hours, bool inBed)
        {
            WorldMinutes += hours * 60f;
            GrowThreat(hours * 60f);
            hero.Stats.Sleep(hours, inBed);
            Hud.Instance.Fade(string.Format("You sleep for {0:0} hours. Day {1}, {2:00}:00.", hours, Day, Mathf.Floor(Hour)));
            if (Threat > 75f) Hud.Push("You dream of fire. The creatures are gathering against the village (threat " + Threat.ToString("0") + ").");
        }

        public bool IsUnlocked(DungeonDef d) { return string.IsNullOrEmpty(d.unlockAfter) || defeated.Contains(d.unlockAfter); }
        public bool BossDefeated(DungeonDef d) { return defeated.Contains(d.id); }
        public int BossesDefeated { get { return defeated.Count; } }

        public int HighestUnlockedTier()
        {
            int tier = 1;
            foreach (var d in GameDatabase.Dungeons) if (IsUnlocked(d)) tier = Mathf.Max(tier, d.tier);
            return tier;
        }

        Transform NewWorld(string name)
        {
            if (WorldRoot != null) Destroy(WorldRoot.gameObject);
            EnemyAI.All.Clear();
            ActiveBoss = null;
            Raid = null;
            WorldRoot = new GameObject(name).transform;
            return WorldRoot;
        }

        public void EnterVillage()
        {
            CurrentDungeon = null;
            var root = NewWorld("World_Village");
            float yaw;
            SpawnPoint = VillageBuilder.Build(root, out yaw);
            weather.SetWeathers(new[] { "village_fair", "village_drizzle" }, 240f, true);
            hero.Controller.Teleport(SpawnPoint, yaw);
            Hud.Instance.Banner("Hollowmark Village", "Day " + Day + ". Rest, trade and train before the next descent.");
        }

        public void EnterDungeon(DungeonDef d)
        {
            CurrentDungeon = d;
            var root = NewWorld("World_" + d.id);
            float yaw;
            SpawnPoint = DungeonBuilder.Build(d, root, BossDefeated(d), out yaw);
            weather.SetWeathers(d.weathers, d.weatherChangeSeconds, false);
            hero.Controller.Teleport(SpawnPoint, yaw);
            Hud.Instance.Banner(d.name, d.description);
        }

        public void OnBossDefeated(BossAI boss)
        {
            defeated.Add(boss.DungeonId);
            Threat = Mathf.Max(0f, Threat - boss.Boss.threatReduction);
            ActiveBoss = null;
            var portal = DungeonBuilder.MakeExitPortal(WorldRoot, boss.transform.position + boss.transform.forward * 3f);
            portal.name = "VictoryPortal";
            Hud.Instance.Banner(boss.Boss.name + " has fallen!", "The village breathes easier. A way home has opened.");
            if (defeated.Count >= GameDatabase.Dungeons.Count)
                Hud.Push("Every boss is dead. Hollowmark is safe. You have won!");
            else
                foreach (var d in GameDatabase.Dungeons)
                    if (d.unlockAfter == boss.DungeonId) Hud.Push("A new gate has opened in the village: " + d.name + ".");
        }

        public void OnPlayerDied(string cause)
        {
            Hud.Instance.ShowDeath(cause);
        }

        public void Respawn()
        {
            var inv = hero.Stats.Inventory;
            int lost = Mathf.FloorToInt(inv.Gold * GameDatabase.Balance.deathGoldLoss);
            inv.Gold -= lost;
            hero.Stats.Revive();
            WorldMinutes += 8f * 60f;
            EnterVillage();
            Hud.Push("You wake in the village infirmary. The physician took " + lost + " gold.");
        }
    }
}
