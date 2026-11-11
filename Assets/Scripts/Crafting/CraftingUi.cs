using System.Collections.Generic;
using System.Linq;
using Hollowmark.Data;
using Hollowmark.Player;
using Hollowmark.UI;
using UnityEngine;

namespace Hollowmark.Crafting
{
    /// <summary>
    /// IMGUI panels for the hearth (cooking), the homestead plots (building), the quest-giving villagers and
    /// the two crafting merchants. Also draws active buffs, poison and the quest tracker in the top-right corner.
    /// </summary>
    public class CraftingUi : MonoBehaviour
    {
        public static CraftingUi Instance { get; private set; }

        public enum Panel { None, Cook, Build, Quest, Larder, Peddler }
        Panel panel = Panel.None;
        Component opener;
        QuestGiver giver;
        Vector2 scroll;

        Texture2D white;
        GUIStyle label, small, title, button, wrap;

        System.Action pending;
        void Later(System.Action a) { pending += a; }

        static Hero H { get { return Hero.Instance; } }
        static Homestead Home { get { return Homestead.Instance; } }

        void Awake()
        {
            Instance = this;
            white = new Texture2D(1, 1);
            white.SetPixel(0, 0, Color.white);
            white.Apply();
        }

        public void Open(Panel p, Component from)
        {
            if (H == null || H.Stats.Dead || (Hud.Instance != null && Hud.Instance.AnyScreenOpen)) return;
            panel = p;
            opener = from;
            scroll = Vector2.zero;
            Hud.SideMenuOpen = true;
        }

        public void OpenQuest(QuestGiver g) { giver = g; Open(Panel.Quest, g); }

        public void Close()
        {
            if (panel == Panel.None) return;
            panel = Panel.None;
            opener = null;
            giver = null;
            Hud.SideMenuOpen = false;
            Hud.SideMenuClosedFrame = Time.frameCount;
        }

        void Update()
        {
            if (pending != null) { var p = pending; pending = null; p(); }
            if (panel == Panel.None) return;
            bool walkedAway = opener == null || H == null
                || Vector3.Distance(opener.transform.position, H.transform.position) > 7f;
            if (GameInput.KeyDown(KeyCode.Escape) || GameInput.KeyDown(KeyCode.Tab) || walkedAway
                || H.Stats.Dead || (Hud.Instance != null && Hud.Instance.AnyScreenOpen))
                Close();
        }

        void OnDisable() { Hud.SideMenuOpen = false; }

        void Styles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = true };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = 12 };
            wrap = new GUIStyle(small) { fontSize = 13 };
            title = new GUIStyle(label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            button = new GUIStyle(GUI.skin.button) { fontSize = 14, richText = true };
        }

        void Fill(Rect r, Color c) { var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = old; }

        void OnGUI()
        {
            if (H == null) return;
            Styles();
            GUI.depth = -1; // above the main HUD
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            bool hudScreen = Hud.Instance != null && Hud.Instance.AnyScreenOpen;
            if (!hudScreen) Corner(w);

            if (panel == Panel.None || hudScreen) return;
            var r = new Rect((w - Mathf.Min(860, w - 40)) / 2, (h - Mathf.Min(620, h - 40)) / 2, Mathf.Min(860, w - 40), Mathf.Min(620, h - 40));
            Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.45f));
            Fill(r, new Color(0.09f, 0.07f, 0.05f, 0.96f));
            Fill(new Rect(r.x, r.y, r.width, 3), new Color(0.75f, 0.55f, 0.25f));
            GUI.Label(new Rect(r.x, r.y + 8, r.width, 40), Heading(), title);

            GUILayout.BeginArea(new Rect(r.x + 20, r.y + 52, r.width - 40, r.height - 70));
            GUILayout.BeginHorizontal();
            GUILayout.Label("<color=#ffd040>" + H.Stats.Inventory.Gold + " gold</color>", label, GUILayout.Width(160));
            GUILayout.FlexibleSpace();
            if (panel == Panel.Cook && GUILayout.Button("Building plans", button, GUILayout.Width(130))) Later(() => panel = Panel.Build);
            if (panel == Panel.Build && GUILayout.Button("Recipes", button, GUILayout.Width(130))) Later(() => panel = Panel.Cook);
            if (GUILayout.Button("Close (Esc)", button, GUILayout.Width(110))) Later(Close);
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            scroll = GUILayout.BeginScrollView(scroll);
            switch (panel)
            {
                case Panel.Cook: CookPanel(); break;
                case Panel.Build: BuildPanel(); break;
                case Panel.Quest: QuestPanel(); break;
                case Panel.Larder: LarderPanel(); break;
                case Panel.Peddler: PeddlerPanel(); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        string Heading()
        {
            var cfg = CraftingDatabase.Config;
            switch (panel)
            {
                case Panel.Cook: return "Cooking";
                case Panel.Build: return "Homestead Building Plans";
                case Panel.Quest: return giver != null ? giver.GiverName : "Villager";
                case Panel.Larder: return cfg.larderName + "'s Larder & Lumber";
                case Panel.Peddler: return cfg.peddlerName + "'s Fine Goods";
            }
            return "";
        }

        // ---------- Text helpers ----------

        static string Need(ItemStack[] stacks)
        {
            if (stacks == null || stacks.Length == 0) return "";
            var inv = H.Stats.Inventory;
            return string.Join(", ", stacks.Select(s =>
            {
                var def = GameDatabase.Item(s.id);
                int have = inv.Count(s.id);
                return string.Format("<color={0}>{1} {2}/{3}</color>", have >= s.count ? "#90ff90" : "#ff9080", def != null ? def.name : s.id, have, s.count);
            }).ToArray());
        }

        public static string Effects(ItemDef i)
        {
            if (i == null) return "";
            var parts = new List<string>();
            if (i.hunger != 0) parts.Add("food " + i.hunger.ToString("+0;-0"));
            if (i.thirst != 0) parts.Add("water " + i.thirst.ToString("+0;-0"));
            if (i.health != 0) parts.Add("health " + i.health.ToString("+0;-0"));
            if (i.stamina != 0) parts.Add("stamina " + i.stamina.ToString("+0;-0"));
            if (i.warmth > 0) parts.Add("warming");
            var b = CraftingDatabase.Buff(i.buff);
            if (b != null) parts.Add(string.Format("<color=#{0}>{1}: {2}, {3} min</color>", ColorUtility.ToHtmlStringRGB(b.Tint), b.name, b.description, Mathf.RoundToInt(b.seconds / 60f)));
            return string.Join(", ", parts.ToArray());
        }

        static string BuildingEffects(BlueprintDef b)
        {
            var parts = new List<string>();
            if (b.maxHealth > 0) parts.Add("+" + b.maxHealth + " max health");
            if (b.maxStamina > 0) parts.Add("+" + b.maxStamina + " max stamina");
            if (b.poisonResist > 0) parts.Add("-" + Mathf.RoundToInt(b.poisonResist * 100f) + "% poison");
            var buff = CraftingDatabase.Buff(b.useBuff);
            if (buff != null) parts.Add("use: " + buff.name + " (" + buff.description + "), every " + b.cooldownHours + "h");
            if (b.dailyItems != null && b.dailyItems.Length > 0) parts.Add("daily: " + string.Join(", ", b.dailyItems.Select(CraftingDatabase.StackName).ToArray()));
            if (CraftingDatabase.Recipes.Any(r => r.station == b.id)) parts.Add("unlocks " + string.Join(", ", CraftingDatabase.Recipes.Where(r => r.station == b.id).Select(r => r.Name).ToArray()));
            return string.Join("; ", parts.ToArray());
        }

        void Divider() { GUILayout.Space(4); var r = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true)); Fill(r, new Color(1, 1, 1, 0.1f)); GUILayout.Space(4); }

        // ---------- Panels ----------

        void CookPanel()
        {
            GUILayout.Label("Cooked meals give buffs: healing, stamina, extra damage, protection, or a ward against poison. Use them from your inventory (Tab).", wrap);
            Divider();
            foreach (var r in CraftingDatabase.Recipes.OrderBy(x => Home.KnownRecipes.Contains(x.id) ? 0 : 1))
            {
                var result = r.Result;
                if (result == null) continue;
                bool known = Home.KnownRecipes.Contains(r.id);
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                if (known)
                {
                    GUILayout.Label("<b>" + r.Name + "</b>" + (r.resultCount > 1 ? " x" + r.resultCount : "") + "   <size=12>" + Effects(result) + "</size>", label);
                    string station = string.IsNullOrEmpty(r.station) ? "" : "   " + StationText(r);
                    GUILayout.Label("Needs: " + Need(r.ingredients) + station + "   <color=#aaaaaa>(" + r.minutes.ToString("0") + " min)</color>", small);
                }
                else
                {
                    GUILayout.Label("<color=#999999><b>" + r.Name + "</b>  (recipe unknown)</color>", label);
                    GUILayout.Label("<color=#999999>Earn it from a villager's quest, or buy it from " + CraftingDatabase.Config.peddlerName + ".</color>", small);
                }
                GUILayout.EndVertical();
                if (known)
                {
                    GUI.enabled = Home.CookProblem(r) == null;
                    if (GUILayout.Button("Cook", button, GUILayout.Width(90), GUILayout.Height(34))) { var rr = r; Later(() => Home.Cook(rr)); }
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
                Divider();
            }
        }

        string StationText(RecipeDef r)
        {
            var b = CraftingDatabase.Blueprint(r.station);
            string name = b != null ? b.name : r.station;
            return Home.HasStation(r) ? "<color=#90ff90>" + name + "</color>" : "<color=#ff9080>needs a " + name + "</color>";
        }

        void BuildPanel()
        {
            GUILayout.Label("Each plot on your homestead holds one building. Buildings give lasting bonuses, daily goods, or a buff you can use. " +
                CraftingDatabase.Config.larderName + " sells timber, stone, nails and rope.", wrap);
            Divider();
            foreach (var b in CraftingDatabase.Blueprints)
            {
                bool built = Home.Built.Contains(b.id), known = Home.KnownBlueprints.Contains(b.id);
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                string status = built ? "  <color=#90ff90>(built)</color>" : known ? "" : "  <color=#999999>(blueprint needed)</color>";
                GUILayout.Label("<b>" + b.name + "</b>" + status, label);
                GUILayout.Label(b.description, small);
                GUILayout.Label("<color=#c0d8ff>" + BuildingEffects(b) + "</color>", small);
                if (!built)
                {
                    if (known)
                        GUILayout.Label("Needs: " + Need(b.materials) + ", <color=" + (H.Stats.Inventory.Gold >= b.gold ? "#90ff90" : "#ff9080") + ">" + b.gold + " gold</color> for labour   <color=#aaaaaa>(" + b.buildHours + " hours)</color>", small);
                    else
                        GUILayout.Label("<color=#999999>Earn the blueprint from a villager's quest" + (b.price > 0 ? ", or buy it from " + CraftingDatabase.Config.peddlerName : "") + ".</color>", small);
                }
                GUILayout.EndVertical();
                if (!built && known)
                {
                    GUI.enabled = Home.BuildProblem(b) == null;
                    if (GUILayout.Button("Build", button, GUILayout.Width(90), GUILayout.Height(34))) { var bb = b; Later(() => { if (Home.Build(bb)) Close(); }); }
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
                Divider();
            }
        }

        void QuestPanel()
        {
            if (giver == null) return;
            Homestead.QuestState state;
            var q = Home.CurrentQuest(giver.GiverName, out state);
            switch (state)
            {
                case Homestead.QuestState.AllDone:
                case Homestead.QuestState.None:
                    GUILayout.Label("\"Bless you. You've done more for this village than anyone. I've nothing more to ask.\"", label);
                    return;
                case Homestead.QuestState.Locked:
                    GUILayout.Label("\"Nothing for you just now. Come back once you've helped a few more folk around the village.\"", label);
                    var before = CraftingDatabase.Quest(q.after);
                    if (before != null) GUILayout.Label("<color=#aaaaaa>Finish \"" + before.title + "\" (" + before.giver + ") first.</color>", small);
                    return;
            }

            GUILayout.Label("<b><size=18>" + q.title + "</size></b>", label);
            GUILayout.Space(4);
            GUILayout.Label("\"" + q.text + "\"", label);
            GUILayout.Space(8);
            GUILayout.Label("<b>Task</b>", label);
            foreach (var line in Home.Progress(q)) GUILayout.Label("  " + line, label);
            GUILayout.Space(6);
            GUILayout.Label("<b>Reward</b>  <color=#ffd040>" + Home.Rewards(q) + "</color>", label);
            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            if (state == Homestead.QuestState.Available)
            {
                if (GUILayout.Button("Accept", button, GUILayout.Width(140), GUILayout.Height(36))) { var qq = q; Later(() => Home.Accept(qq)); }
            }
            else
            {
                GUI.enabled = state == Homestead.QuestState.Ready;
                if (GUILayout.Button(q.deliver != null && q.deliver.Length > 0 ? "Hand over" : "Report back", button, GUILayout.Width(140), GUILayout.Height(36)))
                {
                    var qq = q;
                    Later(() => Home.HandIn(qq));
                }
                GUI.enabled = true;
                if (state == Homestead.QuestState.Active) GUILayout.Label("<color=#aaaaaa>Come back when it's done.</color>", small);
            }
            GUILayout.EndHorizontal();
        }

        void LarderPanel()
        {
            GUILayout.Label("\"Fresh from the farms, and timber from the sawpit. Fair prices, mind.\"", wrap);
            Divider();
            var items = GameDatabase.Items.Values.Where(i => i.vendor == "larder").ToList();
            Section("Ingredients", items.Where(i => i.Kind == ItemKind.Ingredient), 1f);
            Section("Building materials", items.Where(i => i.Kind == ItemKind.Material), 1f);
        }

        void PeddlerPanel()
        {
            var cfg = CraftingDatabase.Config;
            GUILayout.Label("\"Finest dishes in the county, ready to eat. Recipes and plans for a discerning hero. Quality costs, friend.\"", wrap);
            Divider();
            Section("Ready-made meals (" + cfg.peddlerMarkup.ToString("0") + "x what they cost to cook)", GameDatabase.Items.Values.Where(i => i.vendor == "peddler"), cfg.peddlerMarkup);

            GUILayout.Label("<b>Recipes</b>", label);
            bool any = false;
            foreach (var r in CraftingDatabase.Recipes.Where(x => x.price > 0 && !Home.KnownRecipes.Contains(x.id)))
            {
                any = true;
                var rr = r;
                Buy("Recipe: <b>" + r.Name + "</b>   <size=11>" + Effects(r.Result) + "</size>", r.price, 1, () => Home.LearnRecipe(rr.id));
            }
            if (!any) GUILayout.Label("<color=#999999>You know every recipe I sell.</color>", small);
            Divider();

            GUILayout.Label("<b>Building plans</b>", label);
            any = false;
            foreach (var b in CraftingDatabase.Blueprints.Where(x => x.price > 0 && !Home.KnownBlueprints.Contains(x.id)))
            {
                any = true;
                var bb = b;
                Buy("Blueprint: <b>" + b.name + "</b>   <size=11>" + BuildingEffects(b) + "</size>", b.price, 1, () => Home.LearnBlueprint(bb.id));
            }
            if (!any) GUILayout.Label("<color=#999999>You have every plan I sell.</color>", small);
        }

        void Section(string heading, IEnumerable<ItemDef> items, float markup)
        {
            GUILayout.Label("<b>" + heading + "</b>", label);
            foreach (var i in items.OrderBy(x => x.price))
            {
                int price = Mathf.Max(1, Mathf.RoundToInt(i.price * markup));
                var ii = i;
                string desc = i.Kind == ItemKind.Ingredient || i.Kind == ItemKind.Material ? i.description : Effects(i);
                Buy("<b>" + i.name + "</b>  <color=#aaaaaa>(have " + H.Stats.Inventory.Count(i.id) + ")</color>   <size=11>" + desc + "</size>",
                    price, markup > 1f ? 1 : 5, () => H.Stats.Inventory.AddItem(ii.id));
            }
            Divider();
        }

        /// <summary>A row with a Buy button, and a "x5" button for cheap goods.</summary>
        void Buy(string text, int price, int bulk, System.Action give)
        {
            var inv = H.Stats.Inventory;
            GUILayout.BeginHorizontal();
            GUILayout.Label(text, small);
            GUI.enabled = inv.Gold >= price;
            if (GUILayout.Button("Buy " + price, button, GUILayout.Width(100)))
                Later(() => { if (inv.Gold < price) return; inv.Gold -= price; give(); inv.NotifyChanged(); });
            GUI.enabled = inv.Gold >= price * bulk;
            if (bulk > 1 && GUILayout.Button("x" + bulk + " for " + price * bulk, button, GUILayout.Width(110)))
                Later(() => { if (inv.Gold < price * bulk) return; inv.Gold -= price * bulk; for (int n = 0; n < bulk; n++) give(); inv.NotifyChanged(); });
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        // ---------- Corner overlay: poison, buffs, quests ----------

        void Corner(float w)
        {
            var s = H.Stats;
            float x = w - 290, y = 12;
            if (s.Poison > 0f)
            {
                Fill(new Rect(x, y, 270, 18), new Color(0f, 0f, 0f, 0.6f));
                Fill(new Rect(x + 2, y + 2, 266 * Mathf.Clamp01(s.Poison / 100f), 14), new Color(0.35f, 0.75f, 0.2f));
                GUI.Label(new Rect(x + 6, y - 1, 280, 20), "<b>POISONED</b>  " + s.Poison.ToString("0"), small);
                y += 22;
            }
            foreach (var a in s.Buffs.Active)
            {
                int secs = Mathf.CeilToInt(a.Remaining);
                GUI.Label(new Rect(x, y, 280, 20), string.Format("<color=#{0}>{1}</color>  {2}:{3:00}  <color=#aaaaaa><size=11>{4}</size></color>",
                    ColorUtility.ToHtmlStringRGB(a.Def.Tint), a.Def.name, secs / 60, secs % 60, a.Def.description), small);
                y += 18;
            }
            if (Home.AcceptedQuests.Count > 0)
            {
                y += 6;
                GUI.Label(new Rect(x, y, 280, 20), "<b>Quests</b>", small);
                y += 18;
                foreach (var id in Home.AcceptedQuests.ToList())
                {
                    var q = CraftingDatabase.Quest(id);
                    if (q == null) continue;
                    bool ready = Home.CanHandIn(q);
                    GUI.Label(new Rect(x, y, 280, 20), (ready ? "<color=#90ff90>READY: " : "<color=#ffe0a0>") + q.title + "</color> <color=#aaaaaa>(" + q.giver + ")</color>", small);
                    y += 16;
                    foreach (var line in Home.Progress(q))
                    {
                        GUI.Label(new Rect(x + 12, y, 270, 20), "<size=11>" + line + "</size>", small);
                        y += 15;
                    }
                }
            }
        }
    }
}
