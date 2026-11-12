using Hollowmark.Player;
using Hollowmark.UI;
using UnityEngine;

namespace Hollowmark.Family
{
    /// <summary>The IMGUI panel you get when you talk to Olmar: his story, adoption, lessons, and whether he comes along.</summary>
    public class FamilyUi : MonoBehaviour
    {
        public static FamilyUi Instance { get; private set; }

        Olmar opener;
        bool open;
        string note;
        Texture2D white;
        GUIStyle label, small, title, button;
        System.Action pending;
        void Later(System.Action a) { pending += a; }

        static Hero H { get { return Hero.Instance; } }
        static Family F { get { return Family.Instance; } }
        static OlmarConfig Cfg { get { return Family.Cfg; } }

        void Awake()
        {
            Instance = this;
            white = new Texture2D(1, 1);
            white.SetPixel(0, 0, Color.white);
            white.Apply();
        }

        public void Open(Olmar o)
        {
            if (H == null || H.Stats.Dead || (Hud.Instance != null && Hud.Instance.AnyScreenOpen) || Hud.SideMenuOpen) return;
            opener = o;
            open = true;
            note = null;
            if (!F.Met) F.Met = true;
            Hud.SideMenuOpen = true;
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            opener = null;
            Hud.SideMenuOpen = false;
            Hud.SideMenuClosedFrame = Time.frameCount;
        }

        void Update()
        {
            if (pending != null) { var p = pending; pending = null; p(); }
            if (!open) return;
            bool walkedAway = opener == null || H == null || Vector3.Distance(opener.transform.position, H.transform.position) > 7f;
            if (GameInput.KeyDown(KeyCode.Escape) || GameInput.KeyDown(KeyCode.Tab) || walkedAway
                || H.Stats.Dead || (Hud.Instance != null && Hud.Instance.AnyScreenOpen))
                Close();
        }

        void OnDisable() { if (open) Hud.SideMenuOpen = false; }

        void Styles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = true };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = 13 };
            title = new GUIStyle(label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            button = new GUIStyle(GUI.skin.button) { fontSize = 15, richText = true };
        }

        void Fill(Rect r, Color c) { var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = old; }

        void OnGUI()
        {
            if (!open || H == null) return;
            if (Hud.Instance != null && Hud.Instance.AnyScreenOpen) return;
            Styles();
            GUI.depth = -1;
            float w, h;
            UiScale.Apply(out w, out h);
            float pw = Mathf.Min(720, w - 40), ph = Mathf.Min(560, h - 40);
            var r = new Rect((w - pw) / 2, (h - ph) / 2, pw, ph);
            Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.45f));
            Fill(r, new Color(0.09f, 0.07f, 0.05f, 0.96f));
            Fill(new Rect(r.x, r.y, r.width, 3), new Color(0.75f, 0.55f, 0.25f));
            GUI.Label(new Rect(r.x, r.y + 8, r.width, 40), F.Adopted ? Cfg.name : "A lonely boy", title);

            GUILayout.BeginArea(new Rect(r.x + 24, r.y + 56, r.width - 48, r.height - 72));
            if (!F.Adopted) Story(); else Status();
            if (!string.IsNullOrEmpty(note)) { GUILayout.Space(6); GUILayout.Label("<i>" + note + "</i>", small); }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close (Esc)", button, GUILayout.Height(30))) Later(Close);
            GUILayout.EndArea();
        }

        void Story()
        {
            GUILayout.Label(Cfg.story, label);
            GUILayout.Space(10);
            GUILayout.Label("His name is <b>" + Cfg.name + "</b>. He is " + F.Age + " years old.", label);
            GUILayout.Space(14);
            if (GUILayout.Button("<b>Adopt him</b> and take him home", button, GUILayout.Height(40)))
                Later(() => { var o = opener; Close(); if (o != null) o.TakeHome(); });
            if (GUILayout.Button("Leave him be for now", button, GUILayout.Height(30))) Later(Close);
        }

        void Status()
        {
            var f = F;
            var gm = GameManager.Instance;
            string weapon = f.Grown ? "a real hunting hanger" : "his father's wooden sword";
            GUILayout.Label(string.Format("Age <b>{0}</b>   Mood <b>{1}</b>   Health <b>{2:0}/{3:0}</b>{4}", f.Age, f.MoodWord,
                Mathf.Max(0f, f.Health), f.MaxHealth, f.Wounded ? "   <color=#ff9080>(hurt, resting)</color>" : ""), label);
            GUILayout.Label("Fights with " + weapon + ".  Lessons: " + f.Lessons + "   Creatures slain: " + f.Kills, small);
            GUILayout.Space(8);
            SkillRow(f.Strength, "hits harder, more health");
            SkillRow(f.Swordplay, "swings faster, misses less");
            SkillRow(f.Smarts, f.Smarts.Level < 3 ? "spots hidden creatures (steps back when hurt at 3)"
                : f.Smarts.Level < 5 ? "spots hidden creatures, steps back when hurt (drags ambushers out at 5)"
                : "spots and drags out hidden creatures, guards you, dodges blows");
            GUILayout.Space(10);

            // Lessons and talks.
            var problem = f.LessonProblem();
            GUI.enabled = problem == null;
            if (GUILayout.Button("<b>Teach him at the dummy</b> (" + Cfg.lessonMinutes.ToString("0") + " min, x" + f.TeacherBonus.ToString("0.0") + " from your own skill)", button, GUILayout.Height(36)))
                Later(() =>
                {
                    f.Teach();
                    if (opener != null) opener.ShowLesson();
                    Hud.Instance.Fade("You spend an hour drilling " + Cfg.name + " at the dummy.");
                    note = "He is getting the hang of it. (" + f.LessonsToday + "/" + Cfg.lessonsPerDay + " lessons today)";
                });
            GUI.enabled = true;
            if (problem != null) GUILayout.Label("<color=#c0b090>" + problem + "</color>", small);

            GUI.enabled = f.CanTalk;
            if (GUILayout.Button("Talk with him (once a day)", button, GUILayout.Height(30)))
                Later(() => { note = f.Talk(); });
            GUI.enabled = true;
            GUILayout.Space(10);

            // Growing up: at 16 he chooses to follow you or guard the house.
            if (!f.Grown)
            {
                float minutesLeft = (Cfg.joinAge - Cfg.startAge) * Cfg.daysPerYear * 1440f - (gm != null ? gm.WorldMinutes : 0f);
                int days = Mathf.Max(1, Mathf.CeilToInt(minutesLeft / 1440f));
                GUILayout.Label("When he is " + Cfg.joinAge + " (in about " + days + " day" + (days == 1 ? "" : "s") + ") he can fight beside you in the dungeons. Until then he stays home.", small);
            }
            else if (gm != null && !gm.InVillage)
            {
                if (GUILayout.Button("Send him home to rest", button, GUILayout.Height(32)))
                    Later(() => { var o = opener; Close(); if (o != null) o.SendHome(); });
            }
            else
            {
                bool along = f.Mode == Family.Stay.Along;
                GUILayout.Label(along ? "He will <b>come with you</b> into the next dungeon." : "He <b>stays home</b> and guards the house. He fights the creatures if they raid the village.", label);
                GUILayout.BeginHorizontal();
                GUI.enabled = !along;
                if (GUILayout.Button("Take him into the dungeons", button, GUILayout.Height(32))) Later(() => f.Mode = Family.Stay.Along);
                GUI.enabled = along;
                if (GUILayout.Button("Leave him home", button, GUILayout.Height(32))) Later(() => f.Mode = Family.Stay.Home);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        void SkillRow(Skill s, string what)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>" + s.Name + " " + s.Level + "</b>", label, GUILayout.Width(150));
            var bar = GUILayoutUtility.GetRect(120, 14, GUILayout.Width(120));
            bar.y += 5;
            Fill(bar, new Color(1, 1, 1, 0.12f));
            Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(s.Xp / s.XpToNext), bar.height), new Color(0.85f, 0.65f, 0.3f));
            GUILayout.Space(10);
            GUILayout.Label(what, small);
            GUILayout.EndHorizontal();
        }
    }
}
