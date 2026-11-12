using UnityEngine;

namespace Hollowmark.UI
{
    /// <summary>
    /// Makes the IMGUI screens readable and tappable on phones. The HUD is laid out in "virtual" pixels.
    /// On a computer one virtual pixel is one screen pixel (nothing changes). On a touch screen the whole UI is
    /// scaled up so the screen is about 600 virtual pixels tall, and the safe-area insets keep buttons and text
    /// out from under the iPhone notch, the rounded corners and the home bar.
    /// </summary>
    public static class UiScale
    {
        public const float TouchVirtualHeight = 600f;

        public static float Factor
        {
            get { return TouchControls.Active ? Mathf.Max(1f, UnityEngine.Screen.height / TouchVirtualHeight) : 1f; }
        }

        /// <summary>Call at the top of OnGUI. Sets the GUI scale and returns the virtual screen size.</summary>
        public static void Apply(out float w, out float h)
        {
            float s = Factor;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            w = UnityEngine.Screen.width / s;
            h = UnityEngine.Screen.height / s;
            if (TouchControls.Active) WidenScrollbars();
        }

        /// <summary>Converts a screen position (pixels, origin bottom left) to virtual GUI space (origin top left).</summary>
        public static Vector2 ToGui(Vector2 screen)
        {
            float s = Factor;
            return new Vector2(screen.x / s, (UnityEngine.Screen.height - screen.y) / s);
        }

        // Safe-area insets in virtual pixels (0 on a computer).
        public static float Left { get { return TouchControls.Active ? UnityEngine.Screen.safeArea.xMin / Factor : 0f; } }
        public static float Right { get { return TouchControls.Active ? (UnityEngine.Screen.width - UnityEngine.Screen.safeArea.xMax) / Factor : 0f; } }
        public static float Top { get { return TouchControls.Active ? (UnityEngine.Screen.height - UnityEngine.Screen.safeArea.yMax) / Factor : 0f; } }
        public static float Bottom { get { return TouchControls.Active ? UnityEngine.Screen.safeArea.yMin / Factor : 0f; } }

        static bool widened;
        // Fingers can't hit IMGUI's thin default scrollbars, so make them thumb-sized.
        static void WidenScrollbars()
        {
            if (widened) return;
            widened = true;
            var sk = GUI.skin;
            sk.verticalScrollbar.fixedWidth = 26f;
            sk.verticalScrollbarThumb.fixedWidth = 26f;
            sk.horizontalScrollbar.fixedHeight = 26f;
            sk.horizontalScrollbarThumb.fixedHeight = 26f;
        }
    }
}
