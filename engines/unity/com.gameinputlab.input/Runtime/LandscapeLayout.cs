using System;
using UnityEngine;

namespace GameInputLab
{
    /// <summary>One control of a landscape layout. Anchor is a fraction of the safe area, y up from the bottom.</summary>
    [Serializable]
    public sealed class ControlLayout
    {
        public string id;
        public Vector2 anchor = new Vector2(0.5f, 0.5f);
        [Tooltip("Visible diameter in points. Buttons only; a stick is as wide as its ring.")]
        public float visual;
        [Tooltip("Touch diameter in points. Buttons only.")]
        public float hit;
    }

    [Serializable]
    public sealed class StickLayout
    {
        public string id;
        public JoystickConfig config = new JoystickConfig();
    }

    /// <summary>
    /// Control positions, button sizes and stick parameters exported by one of the app's landscape labs.
    /// Mirrors src/landscape/layout.ts; <see cref="SafeAreaLayout"/> holds the placement rules.
    /// </summary>
    [Serializable]
    public sealed class LandscapeLayout
    {
        public const string JsonFormat = "game-input-lab/landscape-layout";

        /// <summary>"gamepad" (stick and buttons) or "dualStick".</summary>
        public string lab;
        /// <summary>"landscape" or "portrait": which way the phone is held. The layout was made for that one; lock the screen to match.</summary>
        public string orientation = "landscape";
        public ControlLayout[] controls = new ControlLayout[0];
        public StickLayout[] sticks = new StickLayout[0];
        /// <summary>What the right stick does to the aim. Only the "dualStick" lab exports it; other layouts get the defaults.</summary>
        public AimBehaviorConfig aim = new AimBehaviorConfig();

        /// <summary>The aim behavior, clamped to the app's limits.</summary>
        public AimBehaviorConfig Aim() => (aim ?? new AimBehaviorConfig()).Normalized();

        public ControlLayout Control(string id)
        {
            foreach (ControlLayout control in controls)
            {
                if (control != null && control.id == id) return control;
            }
            return null;
        }

        /// <summary>The stick's parameters, clamped to the app's limits. Null if the layout has no such stick.</summary>
        public JoystickConfig Stick(string id)
        {
            StickLayout stick = FindStick(id);
            return stick?.config.Normalized();
        }

        StickLayout FindStick(string id)
        {
            foreach (StickLayout stick in sticks)
            {
                if (stick != null && stick.id == id) return stick;
            }
            return null;
        }

        /// <summary>
        /// Place and size <paramref name="target"/> inside <paramref name="safeRoot"/> (an object with
        /// <see cref="SafeAreaPanel"/>, or any RectTransform that covers the area the layout was made for).
        /// <paramref name="unitsPerPoint"/> comes from <see cref="SafeAreaLayout.UnitsPerPoint"/>.
        /// A button's optional <paramref name="hitArea"/> takes the touch diameter; it may be a child of
        /// <paramref name="target"/> or a sibling.
        /// </summary>
        public void Apply(RectTransform safeRoot, string id, RectTransform target, float unitsPerPoint, RectTransform hitArea = null)
        {
            if (safeRoot == null) throw new ArgumentNullException(nameof(safeRoot));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (unitsPerPoint <= 0f) throw new ArgumentOutOfRangeException(nameof(unitsPerPoint));
            ControlLayout control = Control(id) ?? throw new ArgumentException($"The layout has no control \"{id}\".", nameof(id));

            Rect safe = safeRoot.rect;
            var safePoints = new Rect(0f, 0f, safe.width / unitsPerPoint, safe.height / unitsPerPoint);
            StickLayout stick = FindStick(id);
            float visual = control.visual;
            float hit = control.hit;
            if (stick != null)
            {
                visual = hit = SafeAreaLayout.StickRadius(stick.config.Normalized().radius, safePoints) * 2f;
            }

            Vector2 centre = SafeAreaLayout.Center(safePoints, control.anchor, hit / 2f) * unitsPerPoint;
            Place(target, centre, visual * unitsPerPoint);
            if (hitArea == null) return;
            Place(hitArea, hitArea.parent == target ? target.sizeDelta / 2f : centre, hit * unitsPerPoint);
        }

        // Anchored to the parent's bottom left, so the position is a plain offset from the safe area's corner.
        static void Place(RectTransform rect, Vector2 centre, float size)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = centre;
            rect.sizeDelta = new Vector2(size, size);
        }

        /// <summary>Read a JSON export from the app. Stick parameters are clamped to the app's limits.</summary>
        public static LandscapeLayout FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON is empty.", nameof(json));
            var data = new LayoutJson();
            JsonUtility.FromJsonOverwrite(json, data);
            if (data.format != JsonFormat)
            {
                throw new ArgumentException($"Unsupported format \"{data.format}\". Expected \"{JsonFormat}\".", nameof(json));
            }

            var layout = new LandscapeLayout { lab = data.lab, orientation = data.orientation == "portrait" ? "portrait" : "landscape" };
            if (data.controls != null)
            {
                layout.controls = new ControlLayout[data.controls.Length];
                for (int index = 0; index < data.controls.Length; index++)
                {
                    ControlJson item = data.controls[index];
                    layout.controls[index] = new ControlLayout
                    {
                        id = item.id,
                        anchor = new Vector2(Mathf.Clamp01(item.anchorX), Mathf.Clamp01(item.anchorY)),
                        visual = item.visual,
                        hit = item.hit,
                    };
                }
            }
            if (data.sticks != null)
            {
                layout.sticks = new StickLayout[data.sticks.Length];
                for (int index = 0; index < data.sticks.Length; index++)
                {
                    StickJson item = data.sticks[index];
                    layout.sticks[index] = new StickLayout { id = item.id, config = item.ToConfig().Normalized() };
                }
            }
            if (data.aim != null) layout.aim = data.aim.Normalized();
            return layout;
        }

        // Field names match the app's JSON; JsonUtility reads public fields with the same names.
        [Serializable]
        sealed class LayoutJson
        {
            public string format;
            public string lab;
            public string orientation;
            public ControlJson[] controls;
            public StickJson[] sticks;
            public AimBehaviorConfig aim;
        }

        [Serializable]
        sealed class ControlJson
        {
            public string id;
            public float anchorX = 0.5f;
            public float anchorY = 0.5f;
            public float visual;
            public float hit;
        }

        [Serializable]
        sealed class StickJson
        {
            public string id;
            public string mode = "fixed";
            public float radius = 64f;
            public float deadZone = 10f;
            public float outerDeadZone;
            public float curve = 1f;
            public float sensitivity = 1f;
            public float smoothing;

            public JoystickConfig ToConfig() => new JoystickConfig
            {
                floating = mode == "floating",
                radius = radius,
                deadZone = deadZone,
                outerDeadZone = outerDeadZone,
                curve = curve,
                sensitivity = sensitivity,
                smoothing = smoothing,
            };
        }
    }
}
