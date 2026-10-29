using System;
using UnityEngine;

// All gameplay content (weapons, armor, items, enemies, bosses, weather, dungeons, balance)
// is defined in JSON under Assets/Resources/Data. These classes mirror that JSON.
// To add content, add an entry to the JSON file; no code changes are needed.
namespace Hollowmark.Data
{
    public enum WeaponKind { Melee, Firearm }
    public enum WeightClass { Light, Medium, Heavy, Great }
    public enum ArmorSlot { Head, Chest, Hands, Legs }
    public enum ItemKind { Food, Drink, Medicine, Ammo, Tonic }
    public enum StealthKind { None, Shadow, Burrow }
    public enum BodyShape { Goblin, Humanoid, Crawler, Imp, Brute, Giant }
    public enum ParticleKind { None, Rain, Snow, Ash, Embers, Spores, Dust }
    public enum ShopKind { Weapons, Armor, Provisions }

    static class EnumUtil
    {
        public static T Parse<T>(string value, T fallback) where T : struct
        {
            T result;
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out result)) return result;
            return fallback;
        }

        public static Color ParseColor(string hex, Color fallback)
        {
            Color c;
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out c)) return c;
            return fallback;
        }
    }

    [Serializable]
    public class WeaponDef
    {
        public string id;
        public string name;
        public string description;
        public string kind = "Melee";
        public string weightClass = "Medium";
        public float damage = 10f;
        public float attackTime = 0.8f;      // seconds between attacks
        public float range = 2f;             // melee reach or firearm max range
        public float arc = 90f;              // melee swing arc in degrees
        public float staminaCost = 10f;
        public int requiredStrength = 1;     // strength level needed to wield
        public int tier = 1;                 // 1..5, controls where it drops / is sold
        public int price = 50;
        public bool sold = true;             // offered by the village weaponsmith
        public float reloadTime = 0f;        // firearms only
        public int pellets = 1;              // blunderbuss fires several
        public float spread = 0f;            // degrees
        public int ammoPerShot = 0;
        public float length = 0.9f;          // placeholder visual length in metres
        public string color = "#B0B0B0";
        public bool twoHanded = false;
        public float dropWeight = 1f;
        public string model;                 // optional Resources path to a real model prefab

        [NonSerialized] public WeaponKind Kind;
        [NonSerialized] public WeightClass Weight;
        [NonSerialized] public Color Tint;

        public void Init()
        {
            Kind = EnumUtil.Parse(kind, WeaponKind.Melee);
            Weight = EnumUtil.Parse(weightClass, WeightClass.Medium);
            Tint = EnumUtil.ParseColor(color, Color.gray);
        }
    }

    [Serializable]
    public class ArmorDef
    {
        public string id;
        public string name;
        public string description;
        public string slot = "Chest";
        public float defense = 5f;
        public float durability = 50f;
        public float insulation = 0.1f;      // 0..1, how much it shields from cold/heat
        public float weight = 1f;            // slows movement and stamina regen
        public int tier = 1;
        public int price = 40;
        public bool sold = true;
        public bool monsterArmor = false;    // taken from enemies
        public string color = "#6B4F2A";
        public float dropWeight = 1f;
        public string model;

        [NonSerialized] public ArmorSlot Slot;
        [NonSerialized] public Color Tint;

        public void Init()
        {
            Slot = EnumUtil.Parse(slot, ArmorSlot.Chest);
            Tint = EnumUtil.ParseColor(color, Color.gray);
        }
    }

    [Serializable]
    public class ItemDef
    {
        public string id;
        public string name;
        public string description;
        public string kind = "Food";
        public float hunger;     // + restores hunger meter
        public float thirst;
        public float health;
        public float stamina;
        public float insanity;   // negative calms the mind
        public float warmth;     // + warms, - cools body temperature
        public int price = 5;
        public bool sold = true;
        public float dropWeight = 1f;
        public string color = "#C8A060";

        [NonSerialized] public ItemKind Kind;
        [NonSerialized] public Color Tint;

        public void Init()
        {
            Kind = EnumUtil.Parse(kind, ItemKind.Food);
            Tint = EnumUtil.ParseColor(color, Color.yellow);
        }
    }

    [Serializable]
    public class EnemyDef
    {
        public string id;
        public string name;
        public string description;
        public string bodyShape = "Goblin";
        public float height = 1.2f;
        public float width = 0.6f;
        public float moveSpeed = 4.5f;
        public float health = 40f;
        public float damage = 8f;
        public float attackTime = 1.0f;
        public float attackRange = 1.6f;
        public float sightRange = 14f;
        public float stamina = 120f;         // enemies outlast humans: they rest less often
        public string stealth = "None";
        public float revealDistance = 4f;    // hidden enemies show themselves this close
        public float ambushMultiplier = 1.5f;
        public bool ranged = false;
        public float projectileSpeed = 14f;
        public float preferredDistance = 0f; // ranged enemies keep this distance
        public string strangeWeapon;         // flavour name for their weapon
        public string[] skinColors;
        public string eyeColor = "#FFD040";
        public int goldMin = 2;
        public int goldMax = 8;
        public float weaponDropChance = 0.3f;
        public float armorDropChance = 0.3f;
        public float itemDropChance = 0.5f;
        public string[] weaponDrops;         // empty = any weapon of the dungeon tier
        public string[] armorDrops;          // the armor this creature wears
        public string[] itemDrops;
        public string model;

        [NonSerialized] public StealthKind Stealth;
        [NonSerialized] public BodyShape Shape;

        public void Init()
        {
            Stealth = EnumUtil.Parse(stealth, StealthKind.None);
            Shape = EnumUtil.Parse(bodyShape, BodyShape.Goblin);
        }
    }

    [Serializable]
    public class BossDef
    {
        public string id;
        public string name;
        public string title;
        public string description;
        public string bodyShape = "Giant";
        public float height = 3.2f;
        public float width = 1.4f;
        public float health = 600f;
        public float damage = 25f;
        public float attackTime = 1.6f;
        public float attackRange = 3.2f;
        public float moveSpeed = 3.2f;
        public float slamRadius = 5f;
        public float slamDamage = 35f;
        public float slamCooldown = 9f;
        public string summonEnemyId;
        public int summonCount = 2;
        public float enrageThreshold = 0.35f;
        public string color = "#5A3A3A";
        public string eyeColor = "#FF3020";
        public string weaponName;
        public int rewardGold = 150;
        public string guaranteedWeapon;
        public string guaranteedArmor;
        public float threatReduction = 50f;
        public string model;

        [NonSerialized] public BodyShape Shape;
        [NonSerialized] public Color Tint;

        public void Init()
        {
            Shape = EnumUtil.Parse(bodyShape, BodyShape.Giant);
            Tint = EnumUtil.ParseColor(color, Color.gray);
        }
    }

    [Serializable]
    public class WeatherDef
    {
        public string id;
        public string name;
        public string message;
        public float temperature = 15f;      // degrees Celsius
        public float fogDensity = 0.02f;
        public string fogColor = "#808080";
        public string ambientColor = "#404040";
        public string lightColor = "#FFF4E0";
        public float lightIntensity = 1f;
        public string particles = "None";
        public float particleRate = 200f;
        public float wind = 0f;
        public bool lightning = false;
        public float staminaRegenMultiplier = 1f;
        public float insanityMultiplier = 1f;
        public float weight = 1f;            // chance to be picked when the weather changes

        [NonSerialized] public ParticleKind Particles;
        [NonSerialized] public Color Fog, Ambient, Light;

        public void Init()
        {
            Particles = EnumUtil.Parse(particles, ParticleKind.None);
            Fog = EnumUtil.ParseColor(fogColor, Color.gray);
            Ambient = EnumUtil.ParseColor(ambientColor, Color.gray);
            Light = EnumUtil.ParseColor(lightColor, Color.white);
        }
    }

    [Serializable]
    public class DungeonDef
    {
        public string id;
        public string name;
        public string description;
        public int tier = 1;
        public int roomCount = 6;
        public float roomSize = 16f;
        public float wallHeight = 5f;
        public string floorColor = "#3A3A32";
        public string wallColor = "#55554A";
        public string accentColor = "#FF9930";
        public string gateColor = "#706050";
        public string[] enemies;
        public int enemiesPerRoomMin = 2;
        public int enemiesPerRoomMax = 4;
        public string bossId;
        public string[] weathers;
        public float weatherChangeSeconds = 150f;
        public int campEveryRooms = 3;       // a tent camp appears every N rooms
        public string[] supplyItems;
        public string unlockAfter;           // dungeon id whose boss must be dead first

        [NonSerialized] public Color Floor, Wall, Accent, Gate;

        public void Init()
        {
            Floor = EnumUtil.ParseColor(floorColor, Color.gray);
            Wall = EnumUtil.ParseColor(wallColor, Color.gray);
            Accent = EnumUtil.ParseColor(accentColor, Color.yellow);
            Gate = EnumUtil.ParseColor(gateColor, Color.gray);
        }
    }

    [Serializable]
    public class BalanceConfig
    {
        public float gameMinutesPerSecond = 2f;   // 1 real second = 2 in-game minutes
        public float startHour = 8f;
        public int startGold = 120;
        public string startWeapon = "hanger";
        public string[] startArmor;
        public string[] startItems;

        public float baseHealth = 100f;
        public float baseStamina = 100f;
        public float staminaRegenPerSecond = 18f;
        public float sprintStaminaPerSecond = 14f;
        public float rollStaminaCost = 22f;
        public float blockStaminaPerHit = 12f;
        public float walkSpeed = 4.2f;
        public float sprintSpeed = 7f;

        public float hungerPerGameHour = 3.5f;    // meters are 0..100
        public float thirstPerGameHour = 5f;
        public float starvingDamagePerSecond = 0.8f;
        public float dehydratedDamagePerSecond = 1.2f;

        public float hoursAwakeBeforeInsanity = 16f;
        public float insanityPerGameHour = 6f;
        public float insanityDamagePerSecond = 1.5f; // once the mind breaks at 100
        public float sleepInsanityRecoveryPerHour = 12f;

        public float comfortableMin = 8f;          // degrees C the body tolerates unprotected
        public float comfortableMax = 28f;
        public float temperatureDamagePerSecond = 0.6f;

        public float underwearDamageMultiplier = 1.6f;
        public float armorWearPerDamage = 0.6f;

        public float threatPerGameDay = 20f;
        public float raidThreshold = 100f;
        public float threatAfterRaid = 40f;
        public float deathGoldLoss = 0.5f;
    }

    [Serializable] public class WeaponList { public WeaponDef[] items; }
    [Serializable] public class ArmorList { public ArmorDef[] items; }
    [Serializable] public class ItemList { public ItemDef[] items; }
    [Serializable] public class EnemyList { public EnemyDef[] items; }
    [Serializable] public class BossList { public BossDef[] items; }
    [Serializable] public class WeatherList { public WeatherDef[] items; }
    [Serializable] public class DungeonList { public DungeonDef[] items; }
}
