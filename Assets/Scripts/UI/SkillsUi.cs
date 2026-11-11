using System.Linq;
using Hollowmark.Data;
using Hollowmark.Player;
using UnityEngine;

namespace Hollowmark.UI
{
    /// <summary>
    /// The RPG screens, drawn with IMGUI like the rest of the HUD:
    /// the skills screen (K) with the character sheet, practice skills, the three skill trees and the ability keys,
    /// plus the level bar and the 1-4 ability bar shown during play.
    /// </summary>
    public class SkillsUi : MonoBehaviour
    {
        public static SkillsUi Instance { get; private set; }
        public bool IsOpen { get; private set; }

        Vector2 scrollLeft, scrollTrees;
        Texture2D white;
        GUIStyle label, small, tiny, title, button, centered, boxTitle;

        System.Action pending;
        void Later(System.Action a) { pending += a; }

        static Hero H { get { return Hero.Instance; } }

        void Awake()
        {
            Instance = this;
            white = new Texture2D(1, 1);
            white.SetPixel(0, 0, Color.white);
            white.Apply();
        }

        void OnDisable() { if (IsOpen) Hud.SideMenuOpen = false; }

        void Open()
        {
            IsOpen = true;
            Hud.SideMenuOpen = true;
        }

        void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Hud.SideMenuOpen = false;
            Hud.SideMenuClosedFrame = Time.frameCount;
        }

        void Update()
        {
            if (pending != null) { var p = pending; pending = null; p(); }
            if (H == null) return;
            bool hudScreen = Hud.Instance != null && Hud.Instance.AnyScreenOpen;
            if (IsOpen)
            {
                if (GameInput.KeyDown(KeyCode.K) || GameInput.KeyDown(KeyCode.Escape) || GameInput.KeyDown(KeyCode.Tab) || H.Stats.Dead || hudScreen)
                    Close();
            }
            else if (GameInput.KeyDown(KeyCode.K) && !hudScreen && !Hud.SideMenuOpen && Hud.SideMenuClosedFrame != Time.frameCount && !H.Stats.Dead)
                Open();
        }

        void Styles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = true };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = 12 };
            tiny = new GUIStyle(label) { fontSize = 11 };
            title = new GUIStyle(label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            centered = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            boxTitle = new GUIStyle(label) { fontSize = 13, fontStyle = FontStyle.Bold };
            button = new GUIStyle(GUI.skin.button) { fontSize = 13, richText = true };
        }

        void Fill(Rect r, Color c) { var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = old; }

        void Bar(Rect r, float frac, Color c)
        {
            Fill(r, new Color(0f, 0f, 0f, 0.65f));
            Fill(new Rect(r.x + 1, r.y + 1, (r.width - 2) * Mathf.Clamp01(frac), r.height - 2), c);
        }

        void OnGUI()
        {
            if (H == null) return;
            Styles();
            GUI.depth = -1;
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            bool hudScreen = Hud.Instance != null && Hud.Instance.AnyScreenOpen;
            if (hudScreen) return;
            if (!IsOpen)
            {
                if (!Hud.SideMenuOpen) { LevelBar(); AbilityBar(w, h); }
                return;
            }
            SkillsScreen(w, h);
        }

        // ---------- During play ----------

        void LevelBar()
        {
            var p = H.Stats.Progress;
            GUI.Label(new Rect(16, 72, 70, 20), "<b>Level " + p.Level + "</b>", small);
            Bar(new Rect(86, 77, 130, 10), p.MaxLevel ? 1f : p.Xp / p.XpToNext, new Color(0.62f, 0.45f, 0.95f));
            string pts = p.Points > 0 ? "  <color=#ffd040>" + p.Points + " skill point" + (p.Points == 1 ? "" : "s") + ": press K</color>" : "  K skills";
            GUI.Label(new Rect(222, 72, 360, 20), (p.MaxLevel ? "MAX" : string.Format("{0:0}/{1:0} XP", p.Xp, p.XpToNext)) + pts, small);
            if (Time.time < p.WarCryUntil) GUI.Label(new Rect(16, 92, 400, 20), string.Format("<color=#ff8060>Battle Cry {0:0}s</color>", p.WarCryUntil - Time.time), small);
            else if (Time.time < p.DeadEyeUntil) GUI.Label(new Rect(16, 92, 400, 20), string.Format("<color=#ffd040>Dead-Eye ready ({0:0}s)</color>", p.DeadEyeUntil - Time.time), small);
        }

        void AbilityBar(float w, float h)
        {
            var ab = H.GetComponent<Abilities>();
            if (ab == null || ab.Slots.All(s => s == null)) return;
            const float size = 62, gap = 6;
            float x0 = w / 2 - (size * Abilities.SlotCount + gap * (Abilities.SlotCount - 1)) / 2, y = h - size - 14;
            for (int i = 0; i < Abilities.SlotCount; i++)
            {
                var a = ab.Slots[i];
                var r = new Rect(x0 + i * (size + gap), y, size, size);
                Fill(r, new Color(0.08f, 0.06f, 0.05f, 0.8f));
                if (a != null)
                {
                    var tree = TreeOf(a);
                    Fill(new Rect(r.x, r.y, r.width, 3), tree != null ? tree.Tint : Color.white);
                    float left = ab.Remaining(a);
                    if (left > 0f) Fill(new Rect(r.x, r.y + 3, r.width, (r.height - 3) * left / Mathf.Max(0.01f, a.cooldown)), new Color(0f, 0f, 0f, 0.6f));
                    bool tired = H.Stats.Stamina < a.stamina * 0.5f;
                    GUI.Label(new Rect(r.x + 2, r.y + 12, r.width - 4, 34), (tired ? "<color=#ff9080>" : "") + a.name + (tired ? "</color>" : ""), centered);
                    if (left > 0f) GUI.Label(new Rect(r.x, r.y + 40, r.width, 18), Mathf.CeilToInt(left) + "s", centered);
                }
                GUI.Label(new Rect(r.x + 4, r.y + 2, 20, 16), "<b>" + (i + 1) + "</b>", tiny);
            }
        }

        static SkillTreeDef TreeOf(AbilityDef a)
        {
            var perk = SkillDatabase.Perks.FirstOrDefault(p => p.ability == a.id);
            return perk != null ? perk.Tree : null;
        }

        // ---------- The skills screen ----------

        void SkillsScreen(float w, float h)
        {
            var s = H.Stats;
            var p = s.Progress;
            var r = new Rect((w - Mathf.Min(1240, w - 30)) / 2, (h - Mathf.Min(700, h - 30)) / 2, Mathf.Min(1240, w - 30), Mathf.Min(700, h - 30));
            Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.5f));
            Fill(r, new Color(0.08f, 0.06f, 0.05f, 0.97f));
            Fill(new Rect(r.x, r.y, r.width, 3), new Color(0.62f, 0.45f, 0.95f));
            GUI.Label(new Rect(r.x, r.y + 8, r.width, 36), "Skills", title);

            // Header: level, XP and points.
            GUI.Label(new Rect(r.x + 20, r.y + 14, 300, 24), "<b>Level " + p.Level + "</b>" + (p.MaxLevel ? "  (max)" : ""), label);
            Bar(new Rect(r.x + 20, r.y + 40, 260, 12), p.MaxLevel ? 1f : p.Xp / p.XpToNext, new Color(0.62f, 0.45f, 0.95f));
            GUI.Label(new Rect(r.x + 288, r.y + 35, 200, 20), p.MaxLevel ? "" : string.Format("{0:0} / {1:0} XP", p.Xp, p.XpToNext), small);
            GUI.Label(new Rect(r.xMax - 330, r.y + 14, 200, 24), p.Points > 0 ? "<color=#ffd040><b>" + p.Points + " skill point" + (p.Points == 1 ? "" : "s") + "</b></color>" : "No skill points", label);
            if (GUI.Button(new Rect(r.xMax - 120, r.y + 12, 100, 28), "Close (K)", button)) Later(Close);
            bool village = GameManager.Instance != null && GameManager.Instance.InVillage;
            if (p.Spent > 0)
            {
                int cost = p.RespecCost;
                GUI.enabled = village && s.Inventory.Gold >= cost;
                if (GUI.Button(new Rect(r.xMax - 330, r.y + 40, 200, 22), village ? "Forget all perks (" + cost + " gold)" : "Forget perks: in the village", button))
                    Later(() =>
                    {
                        if (s.Inventory.Gold < cost || !GameManager.Instance.InVillage) return;
                        s.Inventory.Gold -= cost;
                        s.Inventory.NotifyChanged();
                        p.Respec();
                        Hud.Push("The village hermit helps you forget. Your skill points are back.");
                    });
                GUI.enabled = true;
            }

            float top = r.y + 70, bottom = r.yMax - 14;
            float leftW = 300;
            CharacterSheet(new Rect(r.x + 14, top, leftW, bottom - top));
            Trees(new Rect(r.x + 24 + leftW, top, r.width - leftW - 38, bottom - top));
        }

        void CharacterSheet(Rect area)
        {
            var s = H.Stats;
            var p = s.Progress;
            Fill(area, new Color(1f, 1f, 1f, 0.03f));
            GUILayout.BeginArea(new Rect(area.x + 8, area.y + 6, area.width - 16, area.height - 12));
            scrollLeft = GUILayout.BeginScrollView(scrollLeft);

            GUILayout.Label("<b>Practice skills</b>  <size=11>(they grow by doing)</size>", label);
            foreach (var sk in s.AllSkills)
            {
                GUILayout.Label(string.Format("<b>{0}</b> {1}   <color=#aaaaaa>{2:0}/{3:0}</color>", sk.Name, sk.Level, sk.Xp, sk.XpToNext), small);
                var br = GUILayoutUtility.GetRect(1, 7, GUILayout.ExpandWidth(true));
                Bar(br, sk.Xp / sk.XpToNext, new Color(0.85f, 0.7f, 0.35f));
                GUILayout.Label("<color=#bbbbbb>" + SkillHint(sk) + "</color>", tiny);
            }

            GUILayout.Space(8);
            GUILayout.Label("<b>Character</b>", label);
            Row("Max health", s.MaxHealth.ToString("0"));
            Row("Max stamina", s.MaxStamina.ToString("0"));
            Row("Melee damage", Pct((1f + 0.07f * (s.Swordsmanship.Level - 1)) * p.MeleeDamageMult));
            Row("Firearm damage", Pct(p.FirearmDamageMult));
            Row("Critical chance", string.Format("{0:0}% melee, {1:0}% shots", p.CritChance(true) * 100f, p.CritChance(false) * 100f));
            Row("Critical damage", "x" + p.CritDamage.ToString("0.00"));
            Row("Swing speed", Pct((1f + 0.04f * (s.Swordsmanship.Level - 1)) * p.AttackSpeed));
            Row("Reload time", Pct(p.ReloadMult, false));
            if (p.LifeSteal > 0f) Row("Life steal", (p.LifeSteal * 100f).ToString("0") + "%");
            if (p.ExecuteBonus > 0f) Row("Vs wounded", "+" + (p.ExecuteBonus * 100f).ToString("0") + "%");
            Row("Blocked hits", (p.BlockFactor * 100f).ToString("0") + "% gets through");
            if (p.HungerMult < 1f) Row("Hunger and thirst", Pct(p.HungerMult, false));
            if (p.TemperatureMult < 1f) Row("Cold and heat", Pct(p.TemperatureMult, false));
            if (p.InsanityMult < 1f) Row("Insanity", Pct(p.InsanityMult, false));
            if (p.HasLastStand) Row("Last Stand", p.LastStandReady ? "<color=#90ff90>ready</color>" : "<color=#ff9080>used (sleep)</color>");

            GUILayout.Space(8);
            GUILayout.Label("<b>Abilities</b>  <size=11>(keys 1-4)</size>", label);
            var ab = H.GetComponent<Abilities>();
            var known = p.KnownAbilities.ToList();
            if (known.Count == 0) GUILayout.Label("<color=#aaaaaa>Learn Whirlwind, Dead-Eye or Second Wind (tier II) to get your first ability.</color>", tiny);
            foreach (var a in known)
            {
                int slot = ab != null ? System.Array.IndexOf(ab.Slots, a) : -1;
                GUILayout.Label(string.Format("<b>{0}</b>{1}  <color=#aaaaaa>{2} stamina, {3}s</color>", a.name, slot >= 0 ? "  [" + (slot + 1) + "]" : "", a.stamina, a.cooldown), small);
                GUILayout.Label("<color=#bbbbbb>" + a.description + "</color>", tiny);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Key:", tiny, GUILayout.Width(30));
                for (int i = 0; i < Abilities.SlotCount; i++)
                {
                    int k = i;
                    var shown = slot == i ? "<b>[" + (i + 1) + "]</b>" : (i + 1).ToString();
                    if (GUILayout.Button(shown, button, GUILayout.Width(34)) && ab != null) Later(() => ab.Assign(k, a));
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            GUILayout.Label("<color=#aaaaaa>Earn XP by killing creatures and bosses, finishing quests, beating raids and training. " +
                "Each level gives a skill point and more health and stamina; every " + SkillDatabase.Config.bonusPointEvery + " levels give a bonus point.</color>", tiny);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static string SkillHint(Skill sk)
        {
            switch (sk.Name)
            {
                case "Strength": return "Heavier weapons, harder heavy hits, more health. Train on the weights.";
                case "Endurance": return "More stamina and health. Gates Survivor perks. Weights and fighting.";
                case "Swordsmanship": return "Faster, harder melee. Gates Blade perks. Dummies and melee hits.";
                case "Marksmanship": return "Firearm damage, reload and aim. Gates Powder perks. Land shots.";
                case "Athletics": return "Cheaper sprint, roll and jump, a bit faster. Sprint and roll.";
            }
            return "";
        }

        void Row(string name, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, small, GUILayout.Width(120));
            GUILayout.Label("<b>" + value + "</b>", small);
            GUILayout.EndHorizontal();
        }

        static string Pct(float mult, bool higherIsBetter = true)
        {
            float d = (mult - 1f) * 100f;
            if (Mathf.Abs(d) < 0.5f) return "normal";
            bool good = higherIsBetter ? d > 0 : d < 0;
            return string.Format("<color={0}>{1}{2:0}%</color>", good ? "#90ff90" : "#ff9080", d > 0 ? "+" : "", d);
        }

        static readonly string[] TierNames = { "I", "II", "III", "IV" };

        void Trees(Rect area)
        {
            var s = H.Stats;
            var p = s.Progress;
            var trees = SkillDatabase.Trees;
            if (trees.Count == 0) { GUI.Label(area, "No skill trees found (Resources/Data/skills.json).", label); return; }
            float colW = (area.width - 24f - (trees.Count - 1) * 10f) / trees.Count;
            int tierCount = Mathf.Max(1, SkillDatabase.Config.tierPoints.Length);

            GUILayout.BeginArea(area);
            scrollTrees = GUILayout.BeginScrollView(scrollTrees);
            GUILayout.BeginHorizontal();
            for (int t = 0; t < trees.Count; t++)
            {
                var tree = trees[t];
                int spent = p.PointsIn(tree.id);
                var skill = s.SkillByName(tree.skill);
                GUILayout.BeginVertical(GUILayout.Width(colW));
                Fill(GUILayoutUtility.GetRect(colW, 3, GUILayout.Width(colW)), tree.Tint);
                GUILayout.Label(string.Format("<b><color=#{0}>{1}</color></b>  <size=11>{2} points spent</size>", ColorUtility.ToHtmlStringRGB(tree.Tint), tree.name, spent), label);
                GUILayout.Label("<color=#bbbbbb>" + tree.description + "</color>", tiny);
                GUILayout.Space(2);

                for (int tier = 0; tier < tierCount; tier++)
                {
                    var perks = SkillDatabase.Perks.Where(x => x.tree == tree.id && x.tier == tier).ToList();
                    if (perks.Count == 0) continue;
                    int needPts = Progression.TierPoints(tier), needSkill = Progression.TierSkill(tier);
                    bool open = spent >= needPts && (skill == null || skill.Level >= needSkill);
                    string req = "";
                    if (needPts > 0) req += needPts + " pts";
                    if (needSkill > 1 && skill != null) req += (req.Length > 0 ? ", " : "") + skill.Name + " " + needSkill;
                    GUILayout.Label(string.Format("<size=11><color={0}>Tier {1}{2}</color></size>", open ? "#e0d0a0" : "#888888",
                        tier < TierNames.Length ? TierNames[tier] : (tier + 1).ToString(), req.Length > 0 ? "  (needs " + req + ")" : ""), tiny);
                    foreach (var perk in perks) PerkBox(perk, tree);
                }
                GUILayout.EndVertical();
                if (t < trees.Count - 1) GUILayout.Space(10);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void PerkBox(PerkDef perk, SkillTreeDef tree)
        {
            var p = H.Stats.Progress;
            int rank = p.Rank(perk.id);
            string blocker = p.Blocker(perk);
            bool mastered = rank >= perk.maxRank;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            string pips = new string('●', rank) + new string('○', perk.maxRank - rank);
            string nameCol = rank > 0 ? "#" + ColorUtility.ToHtmlStringRGB(tree.Tint) : blocker == null ? "#ffffff" : "#999999";
            GUILayout.Label(string.Format("<color={0}>{1}</color>{2}  <color=#d0c080>{3}</color>", nameCol, perk.name,
                string.IsNullOrEmpty(perk.ability) ? "" : " <size=10>[ability]</size>", pips), boxTitle);
            GUILayout.FlexibleSpace();
            if (blocker == null)
            {
                if (GUILayout.Button(rank > 0 ? "+ Rank" : "Learn", button, GUILayout.Width(64))) Later(() => p.Learn(perk));
            }
            else if (!mastered && blocker != "No skill points") GUILayout.Label("<color=#ff9080>locked</color>", tiny, GUILayout.Width(40));
            GUILayout.EndHorizontal();
            GUILayout.Label("<color=#cccccc>" + perk.description + "</color>", tiny);
            if (blocker != null && !mastered && blocker != "No skill points") GUILayout.Label("<color=#ff9080>" + blocker + "</color>", tiny);
            GUILayout.EndVertical();
        }
    }
}
