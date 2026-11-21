using System;
using UnityEngine;

namespace GameInputLab
{
    /// <summary>
    /// Safe-area placement used by the app's landscape labs. Mirrors src/landscape/layout.ts:
    /// a control's centre is a fraction of the safe area, kept far enough from its edges for the
    /// control to fit. Rects and anchors have y up and the origin at the bottom left, like Unity.
    /// Units are whatever the caller uses; the app and the test vectors use points.
    /// </summary>
    public static class SafeAreaLayout
    {
        /// <summary>The screen minus the notch, rounded corners and home indicator.</summary>
        public static Rect SafeRect(float width, float height, float top, float left, float right, float bottom) =>
            new Rect(left, bottom, Mathf.Max(0f, width - left - right), Mathf.Max(0f, height - top - bottom));

        /// <summary>The radius a stick can really have: it has to fit inside the safe area.</summary>
        public static float StickRadius(float radius, Rect safe) =>
            Mathf.Max(0f, Mathf.Min(radius, Mathf.Min(Mathf.Floor(safe.height / 2f), Mathf.Floor(safe.width / 2f))));

        /// <summary>
        /// Centre of a control. <paramref name="anchor"/> is a fraction of the safe area from its bottom left;
        /// <paramref name="extent"/> is the radius that must stay inside it (stick radius, or half the hit diameter).
        /// </summary>
        public static Vector2 Center(Rect safe, Vector2 anchor, float extent) => new Vector2(
            ClampCentre(safe.x + anchor.x * safe.width, extent, safe.x, safe.width),
            ClampCentre(safe.y + anchor.y * safe.height, extent, safe.y, safe.height));

        static float ClampCentre(float value, float extent, float start, float length)
        {
            float margin = Mathf.Min(extent, length / 2f);
            return Mathf.Max(start + margin, Mathf.Min(start + length - margin, value));
        }

        /// <summary>
        /// Canvas units per point, for sizes that come from the app. A point is a pixel at 160 dpi; the canvas
        /// scale factor turns pixels into canvas units.
        /// </summary>
        public static float UnitsPerPoint(Canvas canvas)
        {
            if (canvas == null) throw new ArgumentNullException(nameof(canvas));
            Canvas root = canvas.rootCanvas;
            float scale = root.scaleFactor > 0f ? root.scaleFactor : 1f;
            return VirtualJoystick.EstimatePixelsPerPoint() / scale;
        }
    }
}
