using System;
using Hollowmark.Player;
using UnityEngine;

namespace Hollowmark.Family
{
    [Serializable]
    public class OlmarConfig
    {
        public string name = "Olmar";
        public int startAge = 13;
        public int joinAge = 16;
        public float daysPerYear = 4f;          // game days for Olmar to grow one year older
        public float lessonMinutes = 60f;
        public int lessonsPerDay = 2;
        public float trainFromHour = 7f, trainToHour = 20f;
        public float selfPracticeXpPerHour = 6f; // what he learns alone on his dummy
        public float baseHealth = 60f, healthPerStrength = 14f;
        public float baseDamage = 5f, damagePerStrength = 2.5f, damagePerSkill = 1.5f;
        public float woodenSwordMultiplier = 0.5f;
        public string grownWeapon = "hanger";
        public float woundedHours = 8f;
        public string story;
        public string[] sadLines, happyLines, grownLines, talks;
    }

    [Serializable]
    public class KidDef
    {
        public string name;
        public bool feminine;
        public int seed;
        public float height = 1.3f;
    }

    [Serializable]
    public class FamilyConfig
    {
        public OlmarConfig olmar = new OlmarConfig();
        public KidDef[] kids = new KidDef[0];
        public string[] kidLines = new string[0];
        public string[] kidLinesOlmarHome = new string[0];
    }

    /// <summary>
    /// Olmar's story and growth: whether you adopted him, his age, what he has learned and whether he comes
    /// into the dungeons. Like the Homestead it lives outside the world objects, so it survives the village
    /// being rebuilt every time the hero comes home.
    /// </summary>
    public class Family
    {
        static Family instance;
        public static Family Instance { get { if (instance == null) instance = new Family(); return instance; } }
        public static void Reset() { instance = null; config = null; }

        static FamilyConfig config;
        public static FamilyConfig Config
        {
            get
            {
                if (config != null) return config;
                var asset = Resources.Load<TextAsset>("Data/family");
                try { config = asset != null ? JsonUtility.FromJson<FamilyConfig>(asset.text) : null; }
                catch (Exception e) { Debug.LogError("[Hollowmark] Could not read family.json: " + e.Message); }
                if (config == null) config = new FamilyConfig();
                if (config.olmar == null) config.olmar = new OlmarConfig();
                return config;
            }
        }
        public static OlmarConfig Cfg { get { return Config.olmar; } }

        public enum Stay { Home, Along }

        public bool Met;           // has heard his story
        public bool Adopted;
        public Stay Mode = Stay.Home;
        public float Mood = 10f;   // 0..100: sad when you find him, happier with a home, lessons and talks
        public readonly Skill Strength = new Skill("Strength");
        public readonly Skill Swordplay = new Skill("Swordplay");
        public readonly Skill Smarts = new Skill("Smarts");
        public float Health = -1f;
        public float WoundedUntil = -1f;      // world minutes
        public int LessonDay = -1, LessonsToday, TalkDay = -1;
        public int Lessons, Kills;
        int announcedAge = -1;

        static GameManager GM { get { return GameManager.Instance; } }

        public int Age
        {
            get
            {
                if (GM == null) return Cfg.startAge;
                return Cfg.startAge + Mathf.FloorToInt(GM.WorldMinutes / 1440f / Mathf.Max(0.1f, Cfg.daysPerYear));
            }
        }

        public bool Grown { get { return Age >= Cfg.joinAge; } }
        public bool Wounded { get { return GM != null && GM.WorldMinutes < WoundedUntil; } }
        public float MaxHealth { get { return Cfg.baseHealth + Cfg.healthPerStrength * (Strength.Level - 1); } }

        /// <summary>A real sword once he is grown, his father's wooden one before that.</summary>
        public float Damage
        {
            get
            {
                float d = Cfg.baseDamage + Cfg.damagePerStrength * Strength.Level + Cfg.damagePerSkill * Swordplay.Level;
                return Grown ? d : d * Cfg.woodenSwordMultiplier;
            }
        }

        public float AttackInterval { get { return Mathf.Lerp(1.4f, 0.55f, Mathf.Clamp01((Swordplay.Level - 1) / 9f)); } }
        public float HitChance { get { return Mathf.Clamp(0.6f + 0.04f * Swordplay.Level, 0f, 0.97f); } }
        public float DodgeChance { get { return Mathf.Clamp(0.03f * Smarts.Level, 0f, 0.35f); } }
        public float WarnRange { get { return 4f + 1.5f * Smarts.Level; } }   // spots hidden creatures this far away
        public bool KnowsToRetreat { get { return Smarts.Level >= 3; } }
        public bool PointsOutAmbushes { get { return Smarts.Level >= 5; } } // drags hiding creatures into the open

        public string MoodWord
        {
            get { return Mood < 25f ? "sad" : Mood < 50f ? "quiet" : Mood < 75f ? "content" : "happy"; }
        }

        public void HealFull() { Health = MaxHealth; }

        public void Adopt()
        {
            Met = true;
            Adopted = true;
            Mood = Mathf.Max(Mood, 35f);
            HealFull();
            announcedAge = Age;
        }

        /// <summary>Called by Olmar every frame; announces birthdays.</summary>
        public void Tick()
        {
            if (Health < 0f) HealFull();
            if (!Adopted) return;
            int age = Age;
            if (announcedAge < 0) announcedAge = age;
            if (age <= announcedAge) return;
            announcedAge = age;
            if (age == Cfg.joinAge)
                UI.Hud.Instance.Banner(Cfg.name + " is " + age + "!",
                    "He is old enough to fight. Talk to him to take him into the dungeons, or leave him to guard the house.");
            else
                UI.Hud.Push("It is " + Cfg.name + "'s birthday. He is " + age + " now.");
            HealFull();
        }

        public bool Train(Skill s, float xp)
        {
            if (!s.Add(xp)) return false;
            UI.Hud.Push(Cfg.name + "'s " + s.Name + " rose to " + s.Level + "!");
            if (s == Strength) Health = Mathf.Min(MaxHealth, Health + Cfg.healthPerStrength);
            return true;
        }

        /// <summary>
        /// How much a lesson from the hero is worth. He learns faster from a skilled teacher.
        /// </summary>
        public float TeacherBonus
        {
            get
            {
                var h = Hero.Instance;
                return h == null ? 1f : 1f + 0.2f * (h.Stats.Swordsmanship.Level - 1) + 0.05f * (h.Stats.Strength.Level - 1);
            }
        }

        public string LessonProblem()
        {
            if (!Adopted) return "He is not yours to teach yet.";
            if (Wounded) return "He is hurt. Let him rest first.";
            if (GM == null || !GM.InVillage) return "Lessons happen in the training yard at home.";
            if (GM.Raid != null) return "Not now, the village is under attack!";
            float h = GM.Hour;
            if (h < Cfg.trainFromHour || h >= Cfg.trainToHour) return "It's too dark to train. Teach him in the morning.";
            if (LessonDay == GM.Day && LessonsToday >= Cfg.lessonsPerDay) return "He is worn out from today's lessons. Try again tomorrow.";
            if (Hero.Instance != null && Hero.Instance.Stats.Stamina < 15f) return "You are too tired to teach. Rest first.";
            return null;
        }

        /// <summary>One lesson at the dummy: time passes, he learns swordplay, strength and good sense.</summary>
        public void Teach()
        {
            if (LessonProblem() != null) return;
            if (LessonDay != GM.Day) { LessonDay = GM.Day; LessonsToday = 0; }
            LessonsToday++;
            Lessons++;
            var hero = Hero.Instance;
            float bonus = TeacherBonus;
            GM.AdvanceTime(Cfg.lessonMinutes);
            hero.Stats.Stamina = Mathf.Max(0f, hero.Stats.Stamina - 15f);
            Train(Swordplay, 40f * bonus);
            Train(Strength, 22f * bonus);
            Train(Smarts, 18f * bonus);
            hero.Stats.TrainSkill(hero.Stats.Swordsmanship, 3f); // explaining a cut sharpens your own
            Mood = Mathf.Min(100f, Mood + 8f);
        }

        public bool CanTalk { get { return Adopted && GM != null && TalkDay != GM.Day; } }

        /// <summary>A talk once a day: cheers him up and teaches him to think in a fight.</summary>
        public string Talk()
        {
            if (!CanTalk) return null;
            TalkDay = GM.Day;
            Mood = Mathf.Min(100f, Mood + 12f);
            Train(Smarts, 25f * TeacherBonus);
            var t = Cfg.talks;
            return t != null && t.Length > 0 ? t[UnityEngine.Random.Range(0, t.Length)] : "You talk with Olmar for a while.";
        }

        public string RandomLine()
        {
            var lines = !Adopted ? Cfg.sadLines : Grown ? Cfg.grownLines : Cfg.happyLines;
            if (lines == null || lines.Length == 0) return "...";
            return lines[UnityEngine.Random.Range(0, lines.Length)];
        }

        public void Wound()
        {
            if (GM != null) WoundedUntil = GM.WorldMinutes + Cfg.woundedHours * 60f;
            Health = 1f;
            Mood = Mathf.Max(0f, Mood - 10f);
        }
    }
}
