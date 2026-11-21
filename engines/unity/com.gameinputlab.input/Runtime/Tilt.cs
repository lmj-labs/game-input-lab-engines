using System;
using UnityEngine;

namespace GameInputLab
{
    /// <summary>Where the phone's top edge points while you play, so left/right and up/down follow the screen.</summary>
    public enum TiltOrientation
    {
        Portrait,
        TopLeft,
        TopRight,
    }

    /// <summary>
    /// Tilt parameters with the same names, units and limits as the Game Input Lab app.
    /// Mirrors TiltConfig in src/sensors/tilt.ts.
    /// </summary>
    [Serializable]
    public sealed class TiltConfig
    {
        public const string JsonFormat = "game-input-lab/tilt";

        public TiltOrientation orientation;
        [Tooltip("Flip left/right after the orientation is applied.")]
        public bool invertX;
        [Tooltip("Flip up/down after the orientation is applied.")]
        public bool invertY;

        [Tooltip("Tilt in degrees from the neutral pose that gives a full push.")]
        [Range(10f, 60f)] public float maxAngle = 30f;
        [Tooltip("Inner dead zone, percent of maxAngle.")]
        [Range(0f, 50f)] public float deadZone = 5f;
        [Tooltip("Outer dead zone, percent of maxAngle.")]
        [Range(0f, 30f)] public float outerDeadZone;
        [Tooltip("Response exponent after the dead zones. 1 is linear, 2 is quadratic.")]
        [Range(0.5f, 3f)] public float curve = 1f;
        [Tooltip("Gain after the curve. Output is clamped to 1.")]
        [Range(0.5f, 2f)] public float sensitivity = 1f;
        [Tooltip("Exponential smoothing time constant in milliseconds. 0 turns smoothing off.")]
        [Range(0f, 300f)] public float smoothing = 40f;

        static float Clamp(float value, float fallback, float min, float max, int decimals)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = fallback;
            return (float)Math.Round(Mathf.Clamp(value, min, max), decimals, MidpointRounding.AwayFromZero);
        }

        /// <summary>A copy with every field clamped to the app's limits and rounded like the app.</summary>
        public TiltConfig Normalized()
        {
            var defaults = new TiltConfig();
            return new TiltConfig
            {
                orientation = Enum.IsDefined(typeof(TiltOrientation), orientation) ? orientation : TiltOrientation.Portrait,
                invertX = invertX,
                invertY = invertY,
                maxAngle = Clamp(maxAngle, defaults.maxAngle, 10f, 60f, 0),
                deadZone = Clamp(deadZone, defaults.deadZone, 0f, 50f, 0),
                outerDeadZone = Clamp(outerDeadZone, defaults.outerDeadZone, 0f, 30f, 0),
                curve = Clamp(curve, defaults.curve, 0.5f, 3f, 1),
                sensitivity = Clamp(sensitivity, defaults.sensitivity, 0.5f, 2f, 2),
                smoothing = Clamp(smoothing, defaults.smoothing, 0f, 300f, 0),
            };
        }

        /// <summary>The joystick model the tilt goes through: a full push at maxAngle degrees.</summary>
        public JoystickConfig AsJoystick() => new JoystickConfig
        {
            radius = maxAngle,
            deadZone = deadZone,
            outerDeadZone = outerDeadZone,
            curve = curve,
            sensitivity = sensitivity,
            smoothing = smoothing,
        };

        /// <summary>Read a JSON export from the app, or a bare config object. Out-of-range values are clamped.</summary>
        public static TiltConfig FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON is empty.", nameof(json));
            var envelope = new Envelope();
            JsonUtility.FromJsonOverwrite(json, envelope);
            ConfigJson data;
            if (string.IsNullOrEmpty(envelope.format))
            {
                data = new ConfigJson();
                JsonUtility.FromJsonOverwrite(json, data);
            }
            else if (envelope.format == JsonFormat)
            {
                data = envelope.config ?? new ConfigJson();
            }
            else
            {
                throw new ArgumentException($"Unsupported format \"{envelope.format}\". Expected \"{JsonFormat}\".", nameof(json));
            }
            return data.ToConfig().Normalized();
        }

        [Serializable]
        sealed class Envelope
        {
            public string format;
            public ConfigJson config = new ConfigJson();
        }

        // Field names and the orientation strings match the app's JSON.
        [Serializable]
        sealed class ConfigJson
        {
            public string orientation = "portrait";
            public bool invertX;
            public bool invertY;
            public float maxAngle = 30f;
            public float deadZone = 5f;
            public float outerDeadZone;
            public float curve = 1f;
            public float sensitivity = 1f;
            public float smoothing = 40f;

            public TiltConfig ToConfig() => new TiltConfig
            {
                orientation = orientation == "topLeft" ? TiltOrientation.TopLeft : orientation == "topRight" ? TiltOrientation.TopRight : TiltOrientation.Portrait,
                invertX = invertX,
                invertY = invertY,
                maxAngle = maxAngle,
                deadZone = deadZone,
                outerDeadZone = outerDeadZone,
                curve = curve,
                sensitivity = sensitivity,
                smoothing = smoothing,
            };
        }
    }

    /// <summary>Tilt in degrees. Roll: right edge down is positive. Pitch: top edge down is positive.</summary>
    public readonly struct TiltAngles
    {
        public readonly float Roll;
        public readonly float Pitch;

        public TiltAngles(float roll, float pitch)
        {
            Roll = roll;
            Pitch = pitch;
        }
    }

    /// <summary>
    /// Tilt as a stick. Each method mirrors the function of the same role in src/sensors/tilt.ts; the shared test
    /// vectors check they agree. The reading is the direction gravity pulls in the phone's own axes (x to the right,
    /// y to the top edge, z out of the screen; lying face up reads z = -1).
    /// </summary>
    public static class Tilt
    {
        /// <summary>The phone's tilt from one reading. Independent of the sign of z. Mirrors gravityAngles.</summary>
        public static TiltAngles Angles(Vector3 gravity)
        {
            float roll = Mathf.Atan2(gravity.x, Mathf.Sqrt(gravity.y * gravity.y + gravity.z * gravity.z)) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(gravity.y, Mathf.Sqrt(gravity.x * gravity.x + gravity.z * gravity.z)) * Mathf.Rad2Deg;
            return new TiltAngles(roll, pitch);
        }

        /// <summary>The tilt away from the neutral pose in degrees, turned to the screen's axes (+y up). Mirrors tiltOffset.</summary>
        public static Vector2 Offset(TiltAngles angles, TiltAngles neutral, TiltConfig config)
        {
            float roll = angles.Roll - neutral.Roll;
            float pitch = angles.Pitch - neutral.Pitch;
            Vector2 screen;
            switch (config.orientation)
            {
                case TiltOrientation.TopLeft: screen = new Vector2(-pitch, roll); break;
                case TiltOrientation.TopRight: screen = new Vector2(pitch, -roll); break;
                default: screen = new Vector2(roll, pitch); break;
            }
            return new Vector2(config.invertX ? -screen.x : screen.x, config.invertY ? -screen.y : screen.y);
        }

        /// <summary>Stick output, length 0-1, +y up, before smoothing. Mirrors tiltTarget.</summary>
        public static Vector2 Target(TiltAngles angles, TiltAngles neutral, TiltConfig config) =>
            JoystickResponse.Evaluate(Offset(angles, neutral, config), config.AsJoystick());
    }

    /// <summary>A tilt stick: remembers the neutral pose and the smoothed output.</summary>
    public sealed class TiltStick
    {
        public TiltConfig Config;
        public TiltAngles Neutral;
        /// <summary>Smoothed output, length 0-1, +y up.</summary>
        public Vector2 Output { get; private set; }

        public TiltStick(TiltConfig config) { Config = config; }

        /// <summary>Take this reading as the pose that means "no tilt".</summary>
        public void Calibrate(Vector3 gravity) { Neutral = Tilt.Angles(gravity); }

        /// <summary>One frame with the latest reading. Returns the smoothed output.</summary>
        public Vector2 Step(Vector3 gravity, float deltaMs)
        {
            Vector2 target = Tilt.Target(Tilt.Angles(gravity), Neutral, Config);
            Output = JoystickResponse.Smooth(Output, target, deltaMs, Config.smoothing);
            return Output;
        }

        public void Reset() { Output = Vector2.zero; }
    }
}
