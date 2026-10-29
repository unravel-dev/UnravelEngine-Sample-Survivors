using System;
using System.Globalization;
using Unravel.Core;

namespace Survivors
{
    /// <summary>
    /// Sizes the documents for the screen: the body font-size (every rem follows), the ".safe" layers inside the
    /// safe area, and the layout classes of the body (touch, phone, short, compact, stick-right). Desktops and
    /// tablets fit the 1920 x 1080 design; touch screens also get at least a physical rem size
    /// (<see cref="TOUCH_REM_MILLIMETRES"/>), and phones (short side under <see cref="PHONE_SHORT_SIDE_INCHES"/>)
    /// the phone layouts. The player's Interface size multiplies the result within the limits of the layout the
    /// screen gets. Main thread.
    /// </summary>
    internal sealed class UILayout
    {
        /// <summary>The design resolution: 1rem is 10 design pixels on a 1920 x 1080 screen.</summary>
        private const float DESIGN_WIDTH = 1920.0f;
        private const float DESIGN_HEIGHT = 1080.0f;
        /// <summary>Dp of one rem at a UI scale of 1.</summary>
        private const float REM_DP = 10.0f;
        /// <summary>Touch screens whose short side is below this many inches are phones (phone layouts).</summary>
        private const float PHONE_SHORT_SIDE_INCHES = 3.6f;
        /// <summary>Size of one rem on a touch screen, millimetres: text of 2rem reads at 2 mm.</summary>
        private const float TOUCH_REM_MILLIMETRES = 1.0f;
        private const float MILLIMETRES_PER_INCH = 25.4f;
        private const float MIN_SCALE = 0.45f;
        private const float MAX_SCALE = 4.0f;
        /// <summary>Smallest layout a phone may get (rem): the phone layouts are built for about 110 x 53.</summary>
        private const float PHONE_MIN_WIDTH_REM = 100.0f;
        private const float PHONE_MIN_HEIGHT_REM = 48.0f;
        /// <summary>Smallest layout other screens may get (rem): the compact / short layouts.</summary>
        private const float MIN_WIDTH_REM = 144.0f;
        private const float MIN_HEIGHT_REM = 80.0f;
        /// <summary>Screens shorter than this many rem get the short layouts.</summary>
        private const float SHORT_SCREEN_REM = 100.0f;
        /// <summary>Screens narrower than this many rem get the compact layouts.</summary>
        private const float COMPACT_SCREEN_REM = 172.0f;
        private const string SAFE_SELECTOR = ".safe";

        /// <summary>What the screen offers one document, measured in the document's own pixels.</summary>
        private struct Metrics
        {
            public float pixelsPerDp;
            /// <summary>Screen pixels to document pixels (the editor's Game view may differ from the screen).</summary>
            public float toDocument;
            /// <summary>Safe-area insets, document pixels.</summary>
            public float left;
            public float top;
            public float right;
            public float bottom;
            /// <summary>The safe area's size, dp.</summary>
            public float widthDp;
            public float heightDp;
            public float dpi;
            public bool hasDpi;
            public bool isTouch;
            public bool isPhone;
        }

        private float uiScale = 1.0f;
        private string lastLog;

        /// <summary>Dp per rem in the current layout (10 x the UI scale).</summary>
        public float remDp => REM_DP * uiScale;

        /// <summary>Whether a screen's document changed size or density since its last layout (a context sized after
        /// the document loaded, a window that settles its size a few frames late, a dp ratio the UI system sets after
        /// the first layout).</summary>
        public bool IsStale(UIScreen screen)
        {
            UIElement root = screen.GetRoot();
            if (root == null)
            {
                return false;
            }
            Rect body = root.rect;
            UIDocument document = screen.GetDocument();
            float pixelsPerDp = document != null ? document.pixelsPerDp : screen.layoutPixelsPerDp;
            bool isResized = body.width != screen.layoutSize.x || body.height != screen.layoutSize.y;
            return body.width > 0.0f && (isResized || pixelsPerDp != screen.layoutPixelsPerDp);
        }

        /// <summary>Scales a screen's document and fits its ".safe" layers to the safe area.</summary>
        public void Apply(UIScreen screen, GameSettings settings)
        {
            UIDocument document = screen.GetDocument();
            UIElement root = screen.GetRoot();
            if (document == null || root == null)
            {
                return;
            }
            Rect body = root.rect;
            screen.layoutSize = new Vector2(body.width, body.height);
            screen.layoutPixelsPerDp = document.pixelsPerDp;
            Metrics metrics = Measure(body, document.pixelsPerDp);
            uiScale = ComputeScale(metrics, settings.uiScale);
            WriteScale(root, metrics);
            WriteClasses(root, metrics, settings.joystickOnRight);
            LogLayout(metrics);
        }

        private static Metrics Measure(Rect body, float documentPixelsPerDp)
        {
            var metrics = new Metrics { pixelsPerDp = documentPixelsPerDp > 0.0f ? documentPixelsPerDp : 1.0f };
            float documentWidth = body.width > 0.0f ? body.width : Screen.width;
            float documentHeight = body.height > 0.0f ? body.height : Screen.height;
            metrics.toDocument = Screen.width > 0 ? documentWidth / Screen.width : 1.0f;
            Rect safe = Screen.safeArea;
            if (safe.width > 0.0f && safe.height > 0.0f)
            {
                metrics.left = MathF.Max(0.0f, safe.x) * metrics.toDocument;
                metrics.top = MathF.Max(0.0f, safe.y) * metrics.toDocument;
                metrics.right = MathF.Max(0.0f, Screen.width - (safe.x + safe.width)) * metrics.toDocument;
                metrics.bottom = MathF.Max(0.0f, Screen.height - (safe.y + safe.height)) * metrics.toDocument;
            }
            metrics.widthDp = MathF.Max(1.0f, documentWidth - metrics.left - metrics.right) / metrics.pixelsPerDp;
            metrics.heightDp = MathF.Max(1.0f, documentHeight - metrics.top - metrics.bottom) / metrics.pixelsPerDp;
            metrics.dpi = Screen.dpi;
            metrics.hasDpi = metrics.dpi > 0.0f && float.IsFinite(metrics.dpi);
            metrics.isTouch = Input.touchSupported;
            float shortSideInches = metrics.hasDpi ? MathF.Min(Screen.width, Screen.height) / metrics.dpi : float.MaxValue;
            metrics.isPhone = metrics.isTouch && shortSideInches < PHONE_SHORT_SIDE_INCHES;
            return metrics;
        }

        /// <summary>Fit to the design, at least the physical rem size on touch screens, times the player's scale, at
        /// most what keeps the smallest layout of the screen's kind.</summary>
        private static float ComputeScale(in Metrics metrics, float playerScale)
        {
            float scale = MathF.Min(metrics.widthDp / DESIGN_WIDTH, metrics.heightDp / DESIGN_HEIGHT);
            if (metrics.isTouch && metrics.hasDpi)
            {
                float remPixels = TOUCH_REM_MILLIMETRES * metrics.dpi * metrics.toDocument / MILLIMETRES_PER_INCH;
                scale = MathF.Max(scale, remPixels / (REM_DP * metrics.pixelsPerDp));
            }
            scale *= playerScale;
            float minWidthRem = metrics.isPhone ? PHONE_MIN_WIDTH_REM : MIN_WIDTH_REM;
            float minHeightRem = metrics.isPhone ? PHONE_MIN_HEIGHT_REM : MIN_HEIGHT_REM;
            float largest = MathF.Min(metrics.widthDp / (REM_DP * minWidthRem), metrics.heightDp / (REM_DP * minHeightRem));
            scale = MathF.Min(scale, MathF.Max(largest, MIN_SCALE));
            return Math.Clamp(float.IsFinite(scale) && scale > 0.0f ? scale : 1.0f, MIN_SCALE, MAX_SCALE);
        }

        private void WriteScale(UIElement root, in Metrics metrics)
        {
            root.style["font-size"] = FormatDp(REM_DP * uiScale, "0.###");
            foreach (UIElement layer in root.QueryAll(SAFE_SELECTOR))
            {
                layer.style.left = metrics.left / metrics.pixelsPerDp;
                layer.style.top = metrics.top / metrics.pixelsPerDp;
                layer.style["right"] = FormatDp(metrics.right / metrics.pixelsPerDp, "0.##");
                layer.style["bottom"] = FormatDp(metrics.bottom / metrics.pixelsPerDp, "0.##");
            }
        }

        private void WriteClasses(UIElement root, in Metrics metrics, bool joystickOnRight)
        {
            root.SetClass("touch", metrics.isTouch);
            root.SetClass("phone", metrics.isPhone);
            root.SetClass("compact", !metrics.isPhone && GetWidthRem(metrics) < COMPACT_SCREEN_REM);
            root.SetClass("short", !metrics.isPhone && GetHeightRem(metrics) < SHORT_SCREEN_REM);
            root.SetClass("stick-right", joystickOnRight);
        }

        /// <summary>One trace line whenever the layout every document gets changes.</summary>
        private void LogLayout(in Metrics metrics)
        {
            string line = $"[UI] layout: screen {Screen.width}x{Screen.height} px/dp {metrics.pixelsPerDp:0.##} dpi {metrics.dpi:0} " +
                          $"touch {metrics.isTouch} phone {metrics.isPhone} scale {uiScale:0.###} " +
                          $"rem {GetWidthRem(metrics):0.#}x{GetHeightRem(metrics):0.#}";
            if (line == lastLog)
            {
                return;
            }
            lastLog = line;
            Log.Trace(line);
        }

        private float GetWidthRem(in Metrics metrics) => metrics.widthDp / (REM_DP * uiScale);

        private float GetHeightRem(in Metrics metrics) => metrics.heightDp / (REM_DP * uiScale);

        private static string FormatDp(float value, string format) => value.ToString(format, CultureInfo.InvariantCulture) + "dp";
    }
}
