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
    /// </summary>
    public static class GameInput
    {
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

        public static bool Key(KeyCode k) { var kb = Keyboard.current; var m = Map(k); return kb != null && m != IKey.None && kb[m].isPressed; }
        public static bool KeyDown(KeyCode k) { var kb = Keyboard.current; var m = Map(k); return kb != null && m != IKey.None && kb[m].wasPressedThisFrame; }
        static UnityEngine.InputSystem.Controls.ButtonControl Button(int b)
        {
            var m = Mouse.current; if (m == null) return null;
            return b == 0 ? m.leftButton : b == 1 ? m.rightButton : m.middleButton;
        }
        public static bool MouseHeld(int b) { var c = Button(b); return c != null && c.isPressed; }
        public static bool MouseDown(int b) { var c = Button(b); return c != null && c.wasPressedThisFrame; }
        public static Vector2 MouseDelta { get { var m = Mouse.current; return m == null ? Vector2.zero : m.delta.ReadValue() * 0.05f; } }
        public static float Scroll { get { var m = Mouse.current; return m == null ? 0f : m.scroll.ReadValue().y / 120f; } }
#else
        public static bool Key(KeyCode k) { return Input.GetKey(k); }
        public static bool KeyDown(KeyCode k) { return Input.GetKeyDown(k); }
        public static bool MouseHeld(int b) { return Input.GetMouseButton(b); }
        public static bool MouseDown(int b) { return Input.GetMouseButtonDown(b); }
        public static Vector2 MouseDelta { get { return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")); } }
        public static float Scroll { get { return Input.mouseScrollDelta.y; } }
#endif

        public static Vector2 Move
        {
            get
            {
                var v = Vector2.zero;
                if (Key(KeyCode.W) || Key(KeyCode.UpArrow)) v.y += 1f;
                if (Key(KeyCode.S) || Key(KeyCode.DownArrow)) v.y -= 1f;
                if (Key(KeyCode.D) || Key(KeyCode.RightArrow)) v.x += 1f;
                if (Key(KeyCode.A) || Key(KeyCode.LeftArrow)) v.x -= 1f;
                return Vector2.ClampMagnitude(v, 1f);
            }
        }
    }
}
