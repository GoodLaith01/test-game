using System.Collections.Generic;
using System.Linq;
using Hollowmark.Data;
using Hollowmark.Player;
using Hollowmark.World;
using UnityEngine;

namespace Hollowmark.UI
{
    /// <summary>
    /// The whole UI, drawn with IMGUI so it needs no prefabs: survival bars, messages, prompts,
    /// boss bar, screen effects (hurt, cold, heat, insanity), and the inventory, shop, help and death screens.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public static Hud Instance { get; private set; }

        class Msg { public string Text; public float Time; }
        static readonly List<Msg> messages = new List<Msg>();

        enum Screen { None, Intro, Inventory, Shop, Help, Death }
        Screen screen = Screen.None;
        ShopStall shop;
        bool shopSelling;
        string deathCause;
        Vector2 scrollA, scrollB, scrollC;

        string bannerTitle, bannerSub;
        float bannerUntil;
        string fadeText;
        float fadeUntil;

        Texture2D white;
        GUIStyle label, small, title, box, button, centered;
        float lastHealth;
        float hurtFlash;

        void Awake()
        {
            Instance = this;
            white = new Texture2D(1, 1);
            white.SetPixel(0, 0, Color.white);
            white.Apply();
        }

        public static void Push(string text)
        {
            messages.Add(new Msg { Text = text, Time = Time.time });
            if (messages.Count > 7) messages.RemoveAt(0);
        }

        public void Banner(string t, string sub) { bannerTitle = t; bannerSub = sub; bannerUntil = Time.time + 5f; }
        public void Fade(string text) { fadeText = text; fadeUntil = Time.time + 2.5f; }
        public void ShowIntro() { screen = Screen.Intro; }
        public void ShowDeath(string cause) { deathCause = cause; screen = Screen.Death; }
        public void OpenShop(ShopStall s) { shop = s; shopSelling = false; screen = Screen.Shop; }

        Hero H { get { return Hero.Instance; } }

        void Update()
        {
            if (H == null) return;
            if (pending != null) { var p = pending; pending = null; p(); }
            if (GameInput.KeyDown(KeyCode.Tab) || GameInput.KeyDown(KeyCode.I))
                screen = screen == Screen.Inventory ? Screen.None : (screen == Screen.None ? Screen.Inventory : screen);
            if (GameInput.KeyDown(KeyCode.F1)) screen = screen == Screen.Help ? Screen.None : Screen.Help;
            if (GameInput.KeyDown(KeyCode.Escape) && screen != Screen.Death)
                screen = screen == Screen.None ? Screen.Help : Screen.None;

            bool menu = screen != Screen.None;
            H.InputLocked = menu;
            GameManager.Instance.Paused = menu && screen != Screen.Shop && screen != Screen.Inventory;
            Time.timeScale = GameManager.Instance.Paused ? 0f : 1f;
            Cursor.lockState = menu ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = menu;

            // Quick-use keys.
            if (!menu)
            {
                var inv = H.Stats.Inventory;
                if (GameInput.KeyDown(KeyCode.Q)) Quick(inv.BestOf(ItemKind.Food, i => i.hunger - Mathf.Max(0f, i.insanity) * 3f), "No food! Buy some at the market or search crates.");
                if (GameInput.KeyDown(KeyCode.F)) Quick(inv.BestOf(ItemKind.Drink, i => i.thirst), "No water! Find a flask or visit the well.");
                if (GameInput.KeyDown(KeyCode.H)) Quick(inv.BestOf(ItemKind.Medicine, i => i.health), "No medicine!");
            }

            if (H.Stats.Health < lastHealth - 0.5f) hurtFlash = 1f;
            lastHealth = H.Stats.Health;
            hurtFlash = Mathf.MoveTowards(hurtFlash, 0f, Time.unscaledDeltaTime * 2f);
        }

        // IMGUI draws each frame in several passes; changing the lists mid-frame breaks the layout,
        // so button actions run at the start of the next Update instead.
        System.Action pending;
        void Later(System.Action a) { pending += a; }

        void Quick(ItemDef item, string missing)
        {
            if (item == null) { Push(missing); return; }
            H.Stats.Consume(item);
        }

        void Styles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = 12 };
            title = new GUIStyle(label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            centered = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            box = new GUIStyle(GUI.skin.box) { fontSize = 15, richText = true, alignment = TextAnchor.UpperLeft, wordWrap = true };
            box.normal.textColor = Color.white;
            button = new GUIStyle(GUI.skin.button) { fontSize = 14, richText = true };
        }

        void Rect(Rect r, Color c) { var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = old; }

        void Bar(float x, float y, string name, float value, float max, Color c, string extra = null)
        {
            Rect(new Rect(x, y, 220, 18), new Color(0f, 0f, 0f, 0.6f));
            Rect(new Rect(x + 2, y + 2, 216 * Mathf.Clamp01(value / max), 14), c);
            GUI.Label(new Rect(x + 6, y - 1, 300, 20), string.Format("{0}  {1:0}/{2:0} {3}", name, value, max, extra ?? ""), small);
        }

        void OnGUI()
        {
            if (H == null) return;
            Styles();
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            var s = H.Stats;
            var gm = GameManager.Instance;

            ScreenEffects(w, h, s);

            // Survival bars, bottom left.
            float bx = 16, by = h - 150;
            Bar(bx, by, "Health", s.Health, s.MaxHealth, new Color(0.75f, 0.12f, 0.1f));
            Bar(bx, by + 22, "Stamina", s.Stamina, s.MaxStamina, new Color(0.25f, 0.65f, 0.25f));
            Bar(bx, by + 44, "Hunger", s.Hunger, 100f, new Color(0.8f, 0.55f, 0.2f), s.Hunger <= 0f ? "STARVING" : s.Hunger < 25f ? "hungry" : "");
            Bar(bx, by + 66, "Thirst", s.Thirst, 100f, new Color(0.25f, 0.5f, 0.85f), s.Thirst <= 0f ? "PARCHED" : s.Thirst < 25f ? "thirsty" : "");
            Bar(bx, by + 88, "Sanity", 100f - s.Insanity, 100f, new Color(0.55f, 0.3f, 0.7f), string.Format("(awake {0:0}h)", s.HoursAwake));
            string temp = s.Cold ? "<color=#80c0ff>COLD</color>" : s.Hot ? "<color=#ff9050>HOT</color>" : "comfortable";
            GUI.Label(new Rect(bx, by + 110, 420, 22), string.Format("Body {0:0.0}°C ({1})   Air {2:0}°C   Insulation {3:0%}", s.BodyTemp, temp, s.EnvironmentTemp, s.Inventory.Insulation), small);

            // World info, top left.
            var weather = WeatherSystem.Instance;
            string where = gm.InVillage ? "Hollowmark Village" : gm.CurrentDungeon.name;
            int hour = Mathf.FloorToInt(gm.Hour), minute = Mathf.FloorToInt((gm.Hour - hour) * 60f);
            GUI.Label(new Rect(16, 10, 600, 22), string.Format("<b>{0}</b>   Day {1}, {2:00}:{3:00}   {4}", where, gm.Day, hour, minute,
                weather != null && weather.Current != null ? weather.Current.name : ""), label);
            GUI.Label(new Rect(16, 32, 600, 22), string.Format("<color=#ffd040>{0} gold</color>   Bosses slain {1}/{2}", s.Inventory.Gold, gm.BossesDefeated, GameDatabase.Dungeons.Count), label);
            Rect(new Rect(16, 58, 200, 12), new Color(0f, 0f, 0f, 0.6f));
            Rect(new Rect(17, 59, 198 * gm.Threat / 100f, 10), Color.Lerp(new Color(0.9f, 0.7f, 0.2f), new Color(0.9f, 0.1f, 0.1f), gm.Threat / 100f));
            GUI.Label(new Rect(222, 52, 300, 22), "Village threat " + gm.Threat.ToString("0") + (gm.Raid != null ? "  <color=#ff4040>RAID!</color>" : ""), small);

            // Weapon and armor, bottom right.
            var weapon = H.Combat.Current;
            float rx = w - 330, ry = h - 170;
            string ammo = weapon.Kind == WeaponKind.Firearm
                ? string.Format("  {0}  (powder & shot: {1})", s.Inventory.LoadedRounds > 0 ? "<color=#80ff80>LOADED</color>" : "<color=#ff8080>empty, R to reload</color>", s.Inventory.Count(PlayerCombat.AmmoId))
                : "";
            GUI.Label(new Rect(rx, ry, 330, 22), "<b>" + weapon.name + "</b>" + ammo, label);
            if (H.Combat.Reloading)
            {
                Rect(new Rect(rx, ry + 22, 200, 8), new Color(0f, 0f, 0f, 0.6f));
                Rect(new Rect(rx, ry + 22, 200 * H.Combat.ReloadProgress, 8), new Color(0.9f, 0.8f, 0.4f));
            }
            if (s.Inventory.InUnderwear)
            {
                float pulse = 0.6f + Mathf.Sin(Time.unscaledTime * 6f) * 0.4f;
                GUI.Label(new Rect(rx, ry + 34, 330, 24), string.Format("<color=#ff{0:x2}{0:x2}><b>UNDERWEAR MODE: DEFENSE 0</b></color>", (int)(pulse * 200)), label);
            }
            else
            {
                GUI.Label(new Rect(rx, ry + 34, 330, 22), "Defense " + s.Inventory.Defense.ToString("0"), small);
                int line = 0;
                foreach (var kv in s.Inventory.Worn.OrderBy(k => k.Key))
                {
                    var a = kv.Value;
                    string col = a.Condition > 0.5f ? "#c0ffc0" : a.Condition > 0.2f ? "#ffe080" : "#ff8080";
                    GUI.Label(new Rect(rx, ry + 52 + line * 17, 330, 20), string.Format("{0}: {1} <color={2}>{3:0%}</color>", kv.Key, a.Def.name, col, a.Condition), small);
                    line++;
                }
            }
            GUI.Label(new Rect(rx, h - 26, 330, 22), "Tab inventory   Q eat   F drink   H heal   F1 help", small);

            // Crosshair and prompt.
            if (screen == Screen.None)
            {
                Rect(new Rect(w / 2 - 1, h / 2 - 8, 2, 16), new Color(1, 1, 1, 0.6f));
                Rect(new Rect(w / 2 - 8, h / 2 - 1, 16, 2), new Color(1, 1, 1, 0.6f));
                if (H.Focus != null)
                {
                    Rect(new Rect(w / 2 - 260, h * 0.66f, 520, 30), new Color(0f, 0f, 0f, 0.55f));
                    GUI.Label(new Rect(w / 2 - 260, h * 0.66f, 520, 30), "[E] " + H.Focus.Prompt(H), centered);
                }
            }

            // Boss health.
            var boss = gm.ActiveBoss;
            if (boss != null && boss.IsAlive && Vector3.Distance(boss.transform.position, H.transform.position) < 35f)
            {
                Rect(new Rect(w / 2 - 300, 20, 600, 20), new Color(0f, 0f, 0f, 0.7f));
                Rect(new Rect(w / 2 - 298, 22, 596 * boss.Health / boss.MaxHealth, 16), new Color(0.6f, 0.05f, 0.05f));
                GUI.Label(new Rect(w / 2 - 300, 40, 600, 24), boss.Boss.name + ", " + boss.Boss.title, centered);
            }

            // Messages.
            float my = h - 330;
            foreach (var m in messages)
            {
                float age = Time.time - m.Time;
                if (age > 9f) continue;
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01(9f - age));
                GUI.Label(new Rect(16, my, 640, 22), m.Text, label);
                GUI.color = old;
                my += 21;
            }

            if (Time.time < bannerUntil && screen == Screen.None)
            {
                GUI.Label(new Rect(0, h * 0.18f, w, 50), bannerTitle, title);
                GUI.Label(new Rect(w / 2 - 350, h * 0.18f + 50, 700, 50), bannerSub, centered);
            }

            if (Time.time < fadeUntil)
            {
                float a = Mathf.Clamp01(fadeUntil - Time.time);
                Rect(new Rect(0, 0, w, h), new Color(0, 0, 0, a));
                var old = GUI.color; GUI.color = new Color(1, 1, 1, a);
                GUI.Label(new Rect(0, h / 2 - 20, w, 40), fadeText, centered);
                GUI.color = old;
            }

            switch (screen)
            {
                case Screen.Intro: Intro(w, h); break;
                case Screen.Help: Help(w, h); break;
                case Screen.Inventory: InventoryScreen(w, h); break;
                case Screen.Shop: ShopScreen(w, h); break;
                case Screen.Death: Death(w, h); break;
            }
        }

        void ScreenEffects(float w, float h, PlayerStats s)
        {
            if (hurtFlash > 0f) Edges(w, h, new Color(0.7f, 0f, 0f, hurtFlash * 0.5f), 120f);
            if (s.Health < s.MaxHealth * 0.25f) Edges(w, h, new Color(0.5f, 0f, 0f, 0.25f + Mathf.Sin(Time.time * 4f) * 0.1f), 160f);
            if (s.Cold) Edges(w, h, new Color(0.6f, 0.8f, 1f, Mathf.Clamp01((35.8f - s.BodyTemp) * 0.4f + 0.15f)), 140f);
            if (s.Hot) Edges(w, h, new Color(1f, 0.5f, 0.1f, Mathf.Clamp01((s.BodyTemp - 38.3f) * 0.4f + 0.12f)), 140f);
            if (s.Insanity > 30f)
            {
                float k = s.MindPenalty;
                Rect(new Rect(0, 0, w, h), new Color(0.3f, 0f, 0.35f, k * (0.15f + Mathf.Sin(Time.time * 1.3f) * 0.07f)));
                Edges(w, h, new Color(0.05f, 0f, 0.05f, k * 0.6f), 200f * k + 40f);
            }
        }

        void Edges(float w, float h, Color c, float size)
        {
            for (int i = 0; i < 4; i++)
            {
                float f = 1f - i / 4f;
                var cc = new Color(c.r, c.g, c.b, c.a * f * 0.5f);
                float d = size * (i + 1) / 4f;
                Rect(new Rect(0, 0, w, d), cc);
                Rect(new Rect(0, h - d, w, d), cc);
                Rect(new Rect(0, 0, d, h), cc);
                Rect(new Rect(w - d, 0, d, h), cc);
            }
        }

        Rect Panel(float w, float h, float pw, float ph, string heading)
        {
            var r = new Rect((w - pw) / 2, (h - ph) / 2, pw, ph);
            Rect(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.45f));
            Rect(r, new Color(0.08f, 0.07f, 0.06f, 0.95f));
            GUI.Label(new Rect(r.x, r.y + 8, r.width, 40), heading, title);
            return r;
        }

        const string Controls =
            "<b>WASD</b> move   <b>Shift</b> sprint   <b>Space</b> jump   <b>C</b> dodge roll   <b>Mouse</b> look\n" +
            "<b>Left click</b> attack / fire   <b>Right click</b> block (melee) or aim (firearm)   <b>R</b> reload\n" +
            "<b>E</b> interact (hold E on weights)   <b>Tab</b> inventory   <b>Q</b> eat   <b>F</b> drink   <b>H</b> bandage   <b>Esc</b> pause";

        void Intro(float w, float h)
        {
            var r = Panel(w, h, 760, 470, "Hollowmark, 1740");
            GUI.Label(new Rect(r.x + 30, r.y + 60, r.width - 60, 260),
                "Six dungeons ring your village, and in each one a boss sends its creatures against Hollowmark. " +
                "Some of those creatures were people once, who gave up and joined them.\n\n" +
                "Descend, kill what you find, and take their weapons and armor. Eat, drink and stay warm. " +
                "When your armor breaks you will be fighting in your underwear. " +
                "Sleep in your bed or in a dungeon tent, or the sleeplessness will take your mind.\n\n" +
                "Train at the yard to swing bigger weapons. Return home whenever you like to trade and heal.\n\n" + Controls, box);
            if (GUI.Button(new Rect(r.x + r.width / 2 - 100, r.yMax - 60, 200, 40), "Begin", button)) screen = Screen.None;
        }

        void Help(float w, float h)
        {
            var r = Panel(w, h, 760, 380, "Paused");
            GUI.Label(new Rect(r.x + 30, r.y + 60, r.width - 60, 200), Controls + "\n\n" +
                "Survive: hunger, thirst, body temperature and sleep all matter. Armor keeps out the cold and heat. " +
                "Tents in dungeon camps let you sleep safely when no monsters are near.", box);
            if (GUI.Button(new Rect(r.x + r.width / 2 - 100, r.yMax - 60, 200, 40), "Resume", button)) screen = Screen.None;
        }

        void Death(float w, float h)
        {
            var r = Panel(w, h, 560, 240, "You have fallen");
            GUI.Label(new Rect(r.x + 20, r.y + 70, r.width - 40, 60), "Killed by " + deathCause + "." +
                (H.Stats.Inventory.InUnderwear ? "\nIn your underwear, no less." : ""), centered);
            if (GUI.Button(new Rect(r.x + r.width / 2 - 120, r.yMax - 70, 240, 44), "Wake in the village", button))
            {
                screen = Screen.None;
                GameManager.Instance.Respawn();
            }
        }

        void InventoryScreen(float w, float h)
        {
            var r = Panel(w, h, Mathf.Min(1100, w - 40), Mathf.Min(620, h - 40), "Inventory");
            var s = H.Stats;
            var inv = s.Inventory;
            float colW = (r.width - 60) / 3f;
            float top = r.y + 60, bottom = r.yMax - 20;

            // Weapons.
            GUILayout.BeginArea(new Rect(r.x + 15, top, colW, bottom - top));
            GUILayout.Label("<b>Weapons</b>  (Strength " + s.Strength.Level + ")", label);
            scrollA = GUILayout.BeginScrollView(scrollA);
            foreach (var wpn in inv.Weapons.Distinct().ToList())
            {
                int n = inv.Weapons.Count(x => x == wpn);
                bool eq = inv.Equipped == wpn;
                string req = H.CanWield(wpn) ? "" : " <color=#ff8080>needs STR " + wpn.requiredStrength + "</color>";
                GUILayout.Label(string.Format("{0}{1}{2}\n<size=11>{3}, dmg {4}, {5}{6}</size>", eq ? "► " : "", wpn.name, n > 1 ? " x" + n : "",
                    wpn.weightClass, wpn.damage, wpn.Kind == WeaponKind.Firearm ? "firearm" : "reach " + wpn.range, req), small);
                if (!eq && GUILayout.Button("Equip", button, GUILayout.Width(90))) Later(() => H.Equip(wpn));
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // Armor.
            GUILayout.BeginArea(new Rect(r.x + 30 + colW, top, colW, bottom - top));
            GUILayout.Label("<b>Armor</b>  defense " + inv.Defense.ToString("0") + (inv.InUnderwear ? "  <color=#ff8080>(underwear!)</color>" : ""), label);
            scrollB = GUILayout.BeginScrollView(scrollB);
            foreach (ArmorSlot slot in System.Enum.GetValues(typeof(ArmorSlot)))
            {
                ArmorInstance a;
                if (inv.Worn.TryGetValue(slot, out a))
                {
                    GUILayout.Label(string.Format("{0}: <b>{1}</b> def {2}, {3:0%}", slot, a.Def.name, a.Def.defense, a.Condition), small);
                    if (GUILayout.Button("Take off", button, GUILayout.Width(90))) { var sl = slot; Later(() => inv.TakeOff(sl)); }
                }
                else GUILayout.Label(slot + ": <color=#999999>nothing</color>", small);
            }
            GUILayout.Space(8);
            GUILayout.Label("<b>Carried</b>", small);
            foreach (var a in inv.Armor.ToList())
            {
                GUILayout.Label(string.Format("{0} ({1}) def {2}, warmth {3:0%}, {4:0%}", a.Def.name, a.Def.slot, a.Def.defense, a.Def.insulation, a.Condition), small);
                if (GUILayout.Button("Wear", button, GUILayout.Width(90))) Later(() => inv.Wear(a));
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // Items and skills.
            GUILayout.BeginArea(new Rect(r.x + 45 + colW * 2, top, colW, bottom - top));
            GUILayout.Label("<b>Supplies</b>", label);
            scrollC = GUILayout.BeginScrollView(scrollC);
            foreach (var kv in inv.Items.ToList())
            {
                var def = GameDatabase.Item(kv.Key);
                if (def == null) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(def.name + " x" + kv.Value, small);
                if (def.Kind != ItemKind.Ammo && GUILayout.Button("Use", button, GUILayout.Width(60))) Later(() => s.Consume(def));
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(10);
            GUILayout.Label("<b>Skills</b>", label);
            foreach (var line in s.SkillSummary()) GUILayout.Label(line, small);
            GUILayout.Label("Train Swordsmanship on the dummies and Strength/Endurance on the weights in the village yard.", small);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void ShopScreen(float w, float h)
        {
            var r = Panel(w, h, Mathf.Min(820, w - 40), Mathf.Min(600, h - 40), shop.Keeper);
            var inv = H.Stats.Inventory;
            GUILayout.BeginArea(new Rect(r.x + 20, r.y + 55, r.width - 40, r.height - 75));
            GUILayout.BeginHorizontal();
            GUILayout.Label("<color=#ffd040>" + inv.Gold + " gold</color>", label, GUILayout.Width(140));
            if (GUILayout.Button(shopSelling ? "Buy" : "Sell", button, GUILayout.Width(100))) Later(() => shopSelling = !shopSelling);
            if (shop.Kind == ShopKind.Armor && GUILayout.Button("Repair worn armor (" + RepairCost() + " gold)", button)) Later(Repair);
            if (GUILayout.Button("Close", button, GUILayout.Width(100))) Later(() => screen = Screen.None);
            GUILayout.EndHorizontal();
            scrollA = GUILayout.BeginScrollView(scrollA);
            if (shopSelling) SellList(); else BuyList();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void BuyList()
        {
            var inv = H.Stats.Inventory;
            int tier = GameManager.Instance.HighestUnlockedTier() + 1;
            switch (shop.Kind)
            {
                case ShopKind.Weapons:
                    foreach (var wpn in GameDatabase.Weapons.Values.Where(x => x.sold && x.tier <= tier).OrderBy(x => x.price))
                        Row(string.Format("<b>{0}</b>  {1}, dmg {2}, STR {3}\n<size=11>{4}</size>", wpn.name, wpn.weightClass, wpn.damage, wpn.requiredStrength, wpn.description),
                            wpn.price, () => inv.AddWeapon(wpn));
                    break;
                case ShopKind.Armor:
                    foreach (var a in GameDatabase.Armor.Values.Where(x => x.sold && x.tier <= tier).OrderBy(x => x.slot).ThenBy(x => x.price))
                        Row(string.Format("<b>{0}</b>  {1}, def {2}, durability {3}, warmth {4:0%}\n<size=11>{5}</size>", a.name, a.slot, a.defense, a.durability, a.insulation, a.description),
                            a.price, () => inv.AddArmor(a));
                    break;
                default:
                    foreach (var i in GameDatabase.Items.Values.Where(x => x.sold).OrderBy(x => x.kind).ThenBy(x => x.price))
                    {
                        int count = i.Kind == ItemKind.Ammo ? 5 : 1;
                        Row(string.Format("<b>{0}</b>{1}  {2}", i.name, count > 1 ? " x" + count : "", Effects(i)), i.price * count, () => inv.AddItem(i.id, count));
                    }
                    break;
            }
        }

        static string Effects(ItemDef i)
        {
            var parts = new List<string>();
            if (i.hunger != 0) parts.Add("food " + i.hunger.ToString("+0;-0"));
            if (i.thirst != 0) parts.Add("water " + i.thirst.ToString("+0;-0"));
            if (i.health != 0) parts.Add("health " + i.health.ToString("+0;-0"));
            if (i.stamina != 0) parts.Add("stamina " + i.stamina.ToString("+0;-0"));
            if (i.insanity != 0) parts.Add("insanity " + i.insanity.ToString("+0;-0"));
            if (i.warmth != 0) parts.Add(i.warmth > 0 ? "warming" : "cooling");
            if (i.Kind == ItemKind.Ammo) parts.Add("for firearms");
            return string.Join(", ", parts.ToArray());
        }

        void Row(string text, int price, System.Action buy)
        {
            var inv = H.Stats.Inventory;
            GUILayout.BeginHorizontal();
            GUILayout.Label(text, small);
            GUI.enabled = inv.Gold >= price;
            if (GUILayout.Button("Buy " + price, button, GUILayout.Width(100)))
                Later(() => { inv.Gold -= price; buy(); inv.NotifyChanged(); });
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        void SellList()
        {
            var inv = H.Stats.Inventory;
            foreach (var wpn in inv.Weapons.ToList())
            {
                if (wpn == inv.Equipped && inv.Weapons.Count(x => x == wpn) == 1) continue;
                int price = Mathf.Max(1, wpn.price * 2 / 5);
                GUILayout.BeginHorizontal();
                GUILayout.Label(wpn.name, small);
                if (GUILayout.Button("Sell " + price, button, GUILayout.Width(100))) Later(() => { inv.Weapons.Remove(wpn); inv.Gold += price; inv.NotifyChanged(); });
                GUILayout.EndHorizontal();
            }
            foreach (var a in inv.Armor.ToList())
            {
                int price = Mathf.Max(1, Mathf.RoundToInt(a.Def.price * 0.4f * a.Condition));
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.Format("{0} ({1:0%})", a.Def.name, a.Condition), small);
                if (GUILayout.Button("Sell " + price, button, GUILayout.Width(100))) Later(() => { inv.Armor.Remove(a); inv.Gold += price; inv.NotifyChanged(); });
                GUILayout.EndHorizontal();
            }
            foreach (var kv in inv.Items.ToList())
            {
                var def = GameDatabase.Item(kv.Key);
                if (def == null) continue;
                int price = Mathf.Max(1, def.price / 2);
                GUILayout.BeginHorizontal();
                GUILayout.Label(def.name + " x" + kv.Value, small);
                if (GUILayout.Button("Sell 1 for " + price, button, GUILayout.Width(120))) Later(() => { inv.TakeItem(def.id); inv.Gold += price; });
                GUILayout.EndHorizontal();
            }
        }

        int RepairCost()
        {
            float cost = 0f;
            foreach (var a in H.Stats.Inventory.Worn.Values) cost += (1f - a.Condition) * a.Def.price * 0.5f;
            return Mathf.CeilToInt(cost);
        }

        void Repair()
        {
            var inv = H.Stats.Inventory;
            int cost = RepairCost();
            if (cost <= 0) { Push("Your armor is in good order."); return; }
            if (inv.Gold < cost) { Push("Not enough gold for repairs."); return; }
            inv.Gold -= cost;
            foreach (var a in inv.Worn.Values) a.Durability = a.Def.durability;
            inv.NotifyChanged();
            Push("Briggs hammers your armor back into shape.");
        }
    }
}
