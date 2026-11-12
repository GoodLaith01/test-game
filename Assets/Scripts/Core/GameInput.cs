using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using IKey = UnityEngine.InputSystem.Key;
#endif

namespace Hollowmark
{
    /// <summary>
    /// Input wrapper. Uses the legacy Input Manager by default; if the project is switched to the
    /// Input System package only, it reads the keyboard and mouse devices directly instead.
    /// On phones and tablets the on-screen touch controls (UI/TouchControls.cs) press "virtual" keys and
    /// mouse buttons here, so the rest of the game never needs to know whether it is played by touch.
    /// </summary>
    public static class GameInput
    {
        // ---------- Virtual input from the touch controls ----------

        static readonly System.Collections.Generic.Dictionary<KeyCode, int> vHeld = new System.Collections.Generic.Dictionary<KeyCode, int>();
        static readonly System.Collections.Generic.Dictionary<KeyCode, int> vDown = new System.Collections.Generic.Dictionary<KeyCode, int>();
        static readonly bool[] vMouseHeld = new bool[3];
        static readonly int[] vMouseDown = { -1, -1, -1 };
        static Vector2 vMove, vLook;
        static float vScroll;
        static int vLookFrame = -1;

        /// <summary>True while the touch controls are in charge: the real mouse is then ignored, because iOS
        /// turns every finger into a fake mouse click (that would make every camera drag an attack).</summary>
        public static bool TouchMode;

        /// <summary>Hold or release a key from a touch button. Call once per frame, before game scripts read input.</summary>
        public static void SetVirtualKey(KeyCode k, bool held)
        {
            bool was = vHeld.ContainsKey(k);
            if (held && !was) { vHeld[k] = Time.frameCount; vDown[k] = Time.frameCount; }
            else if (!held && was) vHeld.Remove(k);
        }
        /// <summary>A one-frame key tap (menus, eat, drink, ...).</summary>
        public static void TapVirtualKey(KeyCode k) { vDown[k] = Time.frameCount; }
        public static void SetVirtualMouse(int b, bool held)
        {
            if (held && !vMouseHeld[b]) vMouseDown[b] = Time.frameCount;
            vMouseHeld[b] = held;
        }
        public static void SetVirtualMove(Vector2 v) { vMove = Vector2.ClampMagnitude(v, 1f); }
        public static void SetVirtualLook(Vector2 lookDelta, float scroll) { vLook = lookDelta; vScroll = scroll; vLookFrame = Time.frameCount; }
        public static void ClearVirtual()
        {
            vHeld.Clear(); vDown.Clear(); vMove = Vector2.zero; vLook = Vector2.zero; vScroll = 0f;
            for (int i = 0; i < 3; i++) { vMouseHeld[i] = false; vMouseDown[i] = -1; }
        }

        static bool VKey(KeyCode k) { return vHeld.ContainsKey(k); }
        static bool VKeyDown(KeyCode k) { int f; return vDown.TryGetValue(k, out f) && f == Time.frameCount; }

        public static bool Key(KeyCode k) { return VKey(k) || RawKey(k); }
        public static bool KeyDown(KeyCode k) { return VKeyDown(k) || RawKeyDown(k); }
        public static bool MouseHeld(int b) { return vMouseHeld[b] || (!TouchMode && RawMouseHeld(b)); }
        public static bool MouseDown(int b) { return vMouseDown[b] == Time.frameCount || (!TouchMode && RawMouseDown(b)); }
        public static Vector2 MouseDelta { get { return TouchMode ? (vLookFrame == Time.frameCount ? vLook : Vector2.zero) : RawMouseDelta; } }
        public static float Scroll { get { return TouchMode ? (vLookFrame == Time.frameCount ? vScroll : 0f) : RawScroll; } }

        // ---------- Keyboard and mouse ----------

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        static IKey Map(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.LeftControl: return IKey.LeftCtrl;
                case KeyCode.Return: return IKey.Enter;
                case KeyCode.Alpha1: return IKey.Digit1;
                case KeyCode.Alpha2: return IKey.Digit2;
                case KeyCode.Alpha3: return IKey.Digit3;
                case KeyCode.Alpha4: return IKey.Digit4;
            }
            IKey key;
            return System.Enum.TryParse(k.ToString(), out key) ? key : IKey.None;
        }

        static bool RawKey(KeyCode k) { var kb = Keyboard.current; var m = Map(k); return kb != null && m != IKey.None && kb[m].isPressed; }
        static bool RawKeyDown(KeyCode k) { var kb = Keyboard.current; var m = Map(k); return kb != null && m != IKey.None && kb[m].wasPressedThisFrame; }
        static UnityEngine.InputSystem.Controls.ButtonControl Button(int b)
        {
            var m = Mouse.current; if (m == null) return null;
            return b == 0 ? m.leftButton : b == 1 ? m.rightButton : m.middleButton;
        }
        static bool RawMouseHeld(int b) { var c = Button(b); return c != null && c.isPressed; }
        static bool RawMouseDown(int b) { var c = Button(b); return c != null && c.wasPressedThisFrame; }
        static Vector2 RawMouseDelta { get { var m = Mouse.current; return m == null ? Vector2.zero : m.delta.ReadValue() * 0.05f; } }
        static float RawScroll { get { var m = Mouse.current; return m == null ? 0f : m.scroll.ReadValue().y / 120f; } }
#else
        static bool RawKey(KeyCode k) { return Input.GetKey(k); }
        static bool RawKeyDown(KeyCode k) { return Input.GetKeyDown(k); }
        static bool RawMouseHeld(int b) { return Input.GetMouseButton(b); }
        static bool RawMouseDown(int b) { return Input.GetMouseButtonDown(b); }
        static Vector2 RawMouseDelta { get { return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")); } }
        static float RawScroll { get { return Input.mouseScrollDelta.y; } }
#endif

        public static Vector2 Move
        {
            get
            {
                var v = vMove;
                if (Key(KeyCode.W) || Key(KeyCode.UpArrow)) v.y += 1f;
                if (Key(KeyCode.S) || Key(KeyCode.DownArrow)) v.y -= 1f;
                if (Key(KeyCode.D) || Key(KeyCode.RightArrow)) v.x += 1f;
                if (Key(KeyCode.A) || Key(KeyCode.LeftArrow)) v.x -= 1f;
                return Vector2.ClampMagnitude(v, 1f);
            }
        }
    }
}
