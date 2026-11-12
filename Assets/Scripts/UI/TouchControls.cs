using System.Collections.Generic;
using Hollowmark.Data;
using Hollowmark.Player;
using UnityEngine;

namespace Hollowmark.UI
{
    /// <summary>
    /// On-screen controls for phones and tablets (made for iPhone, works on any touch screen).
    ///
    ///  - Left side of the screen: a floating joystick. Put your thumb down anywhere in the lower left and slide.
    ///    Push past the outer ring to sprint.
    ///  - Right side of the screen: drag with one finger to turn the camera, pinch with two fingers to zoom.
    ///  - Bottom right: Attack (or Fire), Block (or Aim), Roll, Jump, Use, Reload (firearms) and your 4 abilities.
    ///  - Top right: Menu, Bag (inventory), Skills, Eat, Drink, Heal. In any menu a Close button takes their place.
    ///
    /// The buttons press "virtual" keys in <see cref="GameInput"/>, so every game script works unchanged.
    /// It turns on by itself on phones, tablets and touch browsers. Press F2 in the Unity editor to try the layout
    /// with the mouse (one finger), or use Unity's Device Simulator for real multi-touch.
    /// </summary>
    [DefaultExecutionOrder(-1000)] // read the fingers before any game script reads input
    public class TouchControls : MonoBehaviour
    {
        public static TouchControls Instance { get; private set; }

        /// <summary>True when the game is being played by touch.</summary>
        public static bool Active { get { return forced || Application.isMobilePlatform || sawTouch; } }
        static bool forced, sawTouch;

        // Tuning (virtual pixels, see UiScale).
        public float StickRadius = 64f;
        public float LookSensitivity = 0.09f;  // camera degrees per virtual pixel, divided by the camera's own sensitivity
        public float PinchZoom = 0.025f;

        enum Role { None, Stick, Look, Button }
        class Track { public int id; public Role role; public string button; public Vector2 pos, last; }
        struct Finger { public int id; public Vector2 pos; public int phase; } // phase: 0 began, 1 held, 2 ended

        enum Kind { HoldKey, HoldMouse, TapKey }
        class Btn
        {
            public string id, label;
            public Vector2 c; public float r;
            public Kind kind; public KeyCode key; public int mouse;
            public bool held, lit;
            public float cooldown; // 0..1 for ability buttons
        }

        readonly Dictionary<int, Track> tracks = new Dictionary<int, Track>();
        readonly List<Finger> fingers = new List<Finger>();
        readonly List<Btn> buttons = new List<Btn>();
        readonly HashSet<KeyCode> heldKeys = new HashSet<KeyCode>();
        static readonly KeyCode[] HoldKeys = { KeyCode.LeftShift, KeyCode.Space, KeyCode.C, KeyCode.E, KeyCode.R };

        Vector2 stickBase, stickKnob;
        bool stickOn, sprinting, wasActive;
        float W, H;
        Texture2D disc, ring;
        GUIStyle labelStyle, smallStyle, bigStyle;

        void Awake()
        {
            Instance = this;
            disc = MakeDisc(false);
            ring = MakeDisc(true);
            if (Application.isMobilePlatform)
            {
                Application.targetFrameRate = 60;                // iOS runs at 30 fps unless asked
                UnityEngine.Screen.sleepTimeout = SleepTimeout.NeverSleep; // don't dim the screen mid-fight
                UnityEngine.Screen.autorotateToPortrait = false;
                UnityEngine.Screen.autorotateToPortraitUpsideDown = false;
                UnityEngine.Screen.autorotateToLandscapeLeft = true;
                UnityEngine.Screen.autorotateToLandscapeRight = true;
                UnityEngine.Screen.orientation = ScreenOrientation.AutoRotation;
#if !(ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER)
                Input.multiTouchEnabled = true;
#endif
            }
        }

        // ---------------------------------------------------------------- Update

        void Update()
        {
            if (Application.isEditor && GameInput.KeyDown(KeyCode.F2)) forced = !forced;
            ReadFingers();
            GameInput.TouchMode = Active;
            if (!Active)
            {
                if (wasActive) { GameInput.ClearVirtual(); tracks.Clear(); }
                wasActive = false;
                return;
            }
            wasActive = true;

            float s = UiScale.Factor;
            W = UnityEngine.Screen.width / s; H = UnityEngine.Screen.height / s;
            var hero = Hero.Instance;
            bool menu = hero == null || hero.InputLocked || hero.Stats.Dead;
            BuildButtons(hero, menu);

            // Fingers: assign each new finger a job, follow it, and forget it when it lifts.
            var tappedNow = new List<Btn>();
            foreach (var f in fingers)
            {
                Track t;
                if (f.phase == 0 || !tracks.TryGetValue(f.id, out t))
                {
                    t = new Track { id = f.id, pos = f.pos, last = f.pos };
                    var b = Hit(f.pos);
                    if (b != null) { t.role = Role.Button; t.button = b.id; tappedNow.Add(b); }
                    else if (menu) t.role = Role.None; // the menus themselves handle the tap
                    else if (f.pos.x < W * 0.42f && f.pos.y > H * 0.3f)
                    {
                        t.role = Role.Stick; stickBase = f.pos;
                    }
                    else t.role = Role.Look;
                    tracks[f.id] = t;
                }
                t.last = t.pos;
                t.pos = f.pos;
                if (f.phase == 2) tracks.Remove(f.id);
            }
            // Drop tracks whose finger vanished without an "ended" (app lost focus, etc).
            var gone = new List<int>();
            foreach (var kv in tracks) if (!fingers.Exists(f => f.id == kv.Key)) gone.Add(kv.Key);
            foreach (var id in gone) tracks.Remove(id);

            // Joystick.
            Vector2 move = Vector2.zero;
            stickOn = false; sprinting = false;
            Vector2 look = Vector2.zero; float zoom = 0f;
            var lookers = new List<Track>();
            foreach (var t in tracks.Values)
            {
                if (menu) break;
                if (t.role == Role.Stick)
                {
                    stickOn = true;
                    var d = t.pos - stickBase;
                    float sprintR = StickRadius * 1.25f;
                    if (d.magnitude > sprintR * 1.3f) { stickBase = t.pos - d.normalized * sprintR * 1.3f; d = t.pos - stickBase; }
                    sprinting = d.magnitude > sprintR;
                    stickKnob = stickBase + Vector2.ClampMagnitude(d, StickRadius);
                    var v = d / StickRadius;
                    move = new Vector2(v.x, -v.y); // GUI y points down, "forward" is up
                    if (move.magnitude < 0.12f) move = Vector2.zero;
                }
                else if (t.role == Role.Look) lookers.Add(t);
            }
            if (lookers.Count == 1)
            {
                var d = lookers[0].pos - lookers[0].last;
                look = new Vector2(d.x, -d.y) * LookSensitivity;
            }
            else if (lookers.Count >= 2)
            {
                float now = Vector2.Distance(lookers[0].pos, lookers[1].pos);
                float before = Vector2.Distance(lookers[0].last, lookers[1].last);
                zoom = (now - before) * PinchZoom;
            }
            GameInput.SetVirtualMove(move);
            GameInput.SetVirtualLook(look, zoom);

            // Buttons.
            var wantKeys = new HashSet<KeyCode>();
            bool attack = false, block = false;
            if (sprinting) wantKeys.Add(KeyCode.LeftShift);
            foreach (var b in buttons)
            {
                b.held = false;
                foreach (var t in tracks.Values) if (t.role == Role.Button && t.button == b.id) b.held = true;
                if (!b.held) continue;
                if (b.kind == Kind.HoldKey) wantKeys.Add(b.key);
                else if (b.kind == Kind.HoldMouse) { if (b.mouse == 0) attack = true; else block = true; }
            }
            foreach (var b in tappedNow) if (b.kind == Kind.TapKey) GameInput.TapVirtualKey(b.key);
            foreach (var k in HoldKeys) GameInput.SetVirtualKey(k, wantKeys.Contains(k));
            GameInput.SetVirtualMouse(0, attack);
            GameInput.SetVirtualMouse(1, block);
        }

        void BuildButtons(Hero hero, bool menu)
        {
            buttons.Clear();
            float L = UiScale.Left, R = UiScale.Right, T = UiScale.Top, B = UiScale.Bottom;
            float topY = T + 30f;
            if (hero == null) return;
            if (menu)
            {
                if (!hero.Stats.Dead) Add("close", "Close", new Vector2(W - R - 34f, topY), 26f, Kind.TapKey, KeyCode.Escape);
                return;
            }

            // Top-right row.
            string[] ids = { "menu", "bag", "skills", "eat", "drink", "heal" };
            string[] labels = { "Menu", "Bag", "Skills", "Eat", "Drink", "Heal" };
            KeyCode[] keys = { KeyCode.Escape, KeyCode.Tab, KeyCode.K, KeyCode.Q, KeyCode.F, KeyCode.H };
            for (int i = 0; i < ids.Length; i++)
                Add(ids[i], labels[i], new Vector2(W - R - 34f - i * 58f, topY), 24f, Kind.TapKey, keys[i]);

            // Action cluster, bottom right.
            var c = new Vector2(W - R - 88f, H - B - 84f);
            bool gun = hero.Combat.Current.Kind == WeaponKind.Firearm;
            Add("attack", gun ? "Fire" : "Attack", c, 54f, Kind.HoldMouse, KeyCode.None, 0);
            Add("block", gun ? "Aim" : "Block", c + new Vector2(-130f, 14f), 40f, Kind.HoldMouse, KeyCode.None, 1);
            Add("roll", "Roll", c + new Vector2(-104f, -96f), 34f, Kind.HoldKey, KeyCode.C);
            Add("jump", "Jump", c + new Vector2(10f, -132f), 34f, Kind.HoldKey, KeyCode.Space);
            var use = Add("use", "Use", c + new Vector2(-238f, 20f), 40f, Kind.HoldKey, KeyCode.E);
            use.lit = hero.Focus != null;
            if (gun) Add("reload", "Reload", c + new Vector2(-215f, -110f), 30f, Kind.HoldKey, KeyCode.R).lit = hero.Stats.Inventory.LoadedRounds <= 0;

            // Abilities (only the slots that hold one).
            var ab = hero.GetComponent<Abilities>();
            if (ab != null)
                for (int i = 0; i < Abilities.SlotCount; i++)
                {
                    var a = ab.Slots[i];
                    if (a == null) continue;
                    var b = Add("ability" + i, ShortName(a.name), c + new Vector2(-250f + i * 64f, -215f), 28f, Kind.TapKey, KeyCode.Alpha1 + i);
                    b.cooldown = Mathf.Clamp01(ab.Remaining(a) / Mathf.Max(0.01f, a.cooldown));
                }
        }

        Btn Add(string id, string label, Vector2 c, float r, Kind kind, KeyCode key, int mouse = -1)
        {
            var b = new Btn { id = id, label = label, c = c, r = r, kind = kind, key = key, mouse = mouse };
            buttons.Add(b);
            return b;
        }

        static string ShortName(string n)
        {
            if (string.IsNullOrEmpty(n)) return "?";
            int sp = n.IndexOf(' ');
            string w = sp > 0 ? n.Substring(0, sp) : n;
            return w.Length > 7 ? w.Substring(0, 7) : w;
        }

        Btn Hit(Vector2 p)
        {
            Btn best = null; float bestD = float.MaxValue;
            foreach (var b in buttons)
            {
                float d = Vector2.Distance(p, b.c);
                if (d <= b.r + 10f && d < bestD) { best = b; bestD = d; } // a little slack around each button
            }
            return best;
        }

        // ---------------------------------------------------------------- Reading fingers

        void ReadFingers()
        {
            fingers.Clear();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var ts = UnityEngine.InputSystem.Touchscreen.current;
            if (ts != null)
                foreach (var t in ts.touches)
                {
                    bool began = t.press.wasPressedThisFrame, ended = t.press.wasReleasedThisFrame;
                    if (!began && !ended && !t.press.isPressed) continue;
                    AddFinger(t.touchId.ReadValue(), t.position.ReadValue(), began ? 0 : ended ? 2 : 1);
                }
            if (fingers.Count > 0) { NoteRealTouch(); return; }
            var m = UnityEngine.InputSystem.Mouse.current;
            if (forced && m != null && (m.leftButton.isPressed || m.leftButton.wasReleasedThisFrame))
                AddFinger(-1, m.position.ReadValue(), m.leftButton.wasPressedThisFrame ? 0 : m.leftButton.wasReleasedThisFrame ? 2 : 1);
#else
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                int phase = t.phase == TouchPhase.Began ? 0 : (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) ? 2 : 1;
                AddFinger(t.fingerId, t.position, phase);
            }
            if (fingers.Count > 0) { NoteRealTouch(); return; }
            // Editor test mode (F2): the mouse acts as one finger.
            if (forced && (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0)))
                AddFinger(-1, Input.mousePosition, Input.GetMouseButtonDown(0) ? 0 : Input.GetMouseButtonUp(0) ? 2 : 1);
#endif
        }

        void AddFinger(int id, Vector2 screenPos, int phase)
        {
            fingers.Add(new Finger { id = id, pos = UiScale.ToGui(screenPos), phase = phase });
        }

        // A phone browser (WebGL) may not report itself as mobile: switch to touch the first time a finger lands.
        static void NoteRealTouch()
        {
            if (!Application.isEditor && Application.platform == RuntimePlatform.WebGLPlayer) sawTouch = true;
        }

        // ---------------------------------------------------------------- Drawing

        void OnGUI()
        {
            if (!Active || Hero.Instance == null) return;
            GUI.depth = -50; // on top of the HUD and the menus
            float w, h;
            UiScale.Apply(out w, out h);
            Styles();

            if (UnityEngine.Screen.height > UnityEngine.Screen.width)
            {
                Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.85f));
                GUI.Label(new Rect(0, h / 2 - 60, w, 120), "Turn your phone sideways\nto play Hollowmark", bigStyle);
                return;
            }

            // Joystick: a faint ghost where your thumb usually rests, the real stick where it actually is.
            var hero = Hero.Instance;
            bool menu = hero.InputLocked || hero.Stats.Dead;
            if (!menu)
            {
                var b = stickOn ? stickBase : new Vector2(UiScale.Left + 120f, h - UiScale.Bottom - 120f);
                var k = stickOn ? stickKnob : b;
                float a = stickOn ? 0.55f : 0.22f;
                Disc(ring, b, StickRadius, sprinting ? new Color(1f, 0.85f, 0.4f, 0.8f) : new Color(1f, 1f, 1f, a));
                Disc(disc, b, StickRadius, new Color(0f, 0f, 0f, a * 0.4f));
                Disc(disc, k, StickRadius * 0.45f, new Color(1f, 1f, 1f, a + 0.15f));
                if (stickOn && sprinting) GUI.Label(new Rect(b.x - 60, b.y - StickRadius - 26, 120, 20), "SPRINT", smallStyle);
            }

            foreach (var btn in buttons)
            {
                Color bg = btn.held ? new Color(1f, 0.85f, 0.45f, 0.75f)
                    : btn.lit ? new Color(0.95f, 0.75f, 0.3f, 0.55f)
                    : new Color(0.08f, 0.06f, 0.05f, 0.5f);
                Disc(disc, btn.c, btn.r, bg);
                Disc(ring, btn.c, btn.r, new Color(1f, 1f, 1f, btn.held ? 0.9f : 0.45f));
                if (btn.cooldown > 0f) Disc(disc, btn.c, btn.r * btn.cooldown, new Color(0f, 0f, 0f, 0.55f));
                var style = btn.r >= 40f ? labelStyle : smallStyle;
                GUI.Label(new Rect(btn.c.x - btn.r, btn.c.y - btn.r, btn.r * 2f, btn.r * 2f), btn.label, style);
            }
        }

        void Disc(Texture2D tex, Vector2 c, float r, Color col)
        {
            var old = GUI.color; GUI.color = col;
            GUI.DrawTexture(new Rect(c.x - r, c.y - r, r * 2f, r * 2f), tex);
            GUI.color = old;
        }

        void Fill(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void Styles()
        {
            if (labelStyle != null) return;
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            labelStyle.normal.textColor = Color.white;
            smallStyle = new GUIStyle(labelStyle) { fontSize = 12 };
            bigStyle = new GUIStyle(labelStyle) { fontSize = 30, wordWrap = true };
        }

        // A soft-edged white circle (or ring) the buttons are tinted from.
        static Texture2D MakeDisc(bool hollow)
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            float c = (n - 1) / 2f, outer = n / 2f - 1f, inner = outer - 5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(outer - d);
                    if (hollow) a *= Mathf.Clamp01(d - inner);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
