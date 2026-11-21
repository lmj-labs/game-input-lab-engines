using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameInputLab
{
    /// <summary>
    /// Joystick parameters with the same names, units and limits as the Game Input Lab app.
    /// Mirrors JoystickConfig in src/components/joystickMath.ts.
    /// </summary>
    [Serializable]
    public sealed class JoystickConfig
    {
        public const string JsonFormat = "game-input-lab/joystick";

        [Tooltip("Fixed: the stick centre is the centre of the touch area. Floating: it is where the finger lands.")]
        public bool floating;

        [Tooltip("Maximum knob travel in points (iOS pt, about one Android dp).")]
        [Range(32f, 96f)] public float radius = 64f;

        [Tooltip("Inner dead zone, percent of the radius. Input below it outputs zero.")]
        [Range(0f, 50f)] public float deadZone = 10f;

        [Tooltip("Outer dead zone, percent of the radius. Input in this edge band outputs full strength.")]
        [Range(0f, 30f)] public float outerDeadZone;

        [Tooltip("Response exponent after the dead zones. 1 is linear, 2 is quadratic.")]
        [Range(0.5f, 3f)] public float curve = 1f;

        [Tooltip("Gain after the curve. Output is clamped to 1.")]
        [Range(0.5f, 2f)] public float sensitivity = 1f;

        [Tooltip("Exponential smoothing time constant in milliseconds. 0 turns smoothing off.")]
        [Range(0f, 300f)] public float smoothing;

        public readonly struct FieldRange
        {
            public readonly float Min;
            public readonly float Max;
            public readonly int Decimals;

            public FieldRange(float min, float max, int decimals)
            {
                Min = min;
                Max = max;
                Decimals = decimals;
            }

            public float Clamp(float value, float fallback)
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) value = fallback;
                value = Mathf.Clamp(value, Min, Max);
                return (float)Math.Round(value, Decimals, MidpointRounding.AwayFromZero);
            }
        }

        /// <summary>Limits and rounding per field, keyed by the app's field names.</summary>
        public static readonly IReadOnlyDictionary<string, FieldRange> Ranges = new Dictionary<string, FieldRange>
        {
            { "radius", new FieldRange(32f, 96f, 0) },
            { "deadZone", new FieldRange(0f, 50f, 0) },
            { "outerDeadZone", new FieldRange(0f, 30f, 0) },
            { "curve", new FieldRange(0.5f, 3f, 1) },
            { "sensitivity", new FieldRange(0.5f, 2f, 2) },
            { "smoothing", new FieldRange(0f, 300f, 0) },
        };

        public JoystickConfig Clone() => (JoystickConfig)MemberwiseClone();

        /// <summary>A copy with every field clamped to the app's limits and rounded like the app.</summary>
        public JoystickConfig Normalized()
        {
            var defaults = new JoystickConfig();
            return new JoystickConfig
            {
                floating = floating,
                radius = Ranges["radius"].Clamp(radius, defaults.radius),
                deadZone = Ranges["deadZone"].Clamp(deadZone, defaults.deadZone),
                outerDeadZone = Ranges["outerDeadZone"].Clamp(outerDeadZone, defaults.outerDeadZone),
                curve = Ranges["curve"].Clamp(curve, defaults.curve),
                sensitivity = Ranges["sensitivity"].Clamp(sensitivity, defaults.sensitivity),
                smoothing = Ranges["smoothing"].Clamp(smoothing, defaults.smoothing),
            };
        }

        /// <summary>
        /// Read a JSON export from the app, or a bare config object.
        /// Missing fields keep their defaults; out-of-range values are clamped.
        /// </summary>
        public static JoystickConfig FromJson(string json)
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

        public string ToJson(bool pretty = true) => JsonUtility.ToJson(ConfigJson.From(this), pretty);

        [Serializable]
        sealed class Envelope
        {
            public string format;
            public ConfigJson config = new ConfigJson();
        }

        // Field names and the "mode" string match the app's JSON.
        [Serializable]
        sealed class ConfigJson
        {
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

            public static ConfigJson From(JoystickConfig config) => new ConfigJson
            {
                mode = config.floating ? "floating" : "fixed",
                radius = config.radius,
                deadZone = config.deadZone,
                outerDeadZone = config.outerDeadZone,
                curve = config.curve,
                sensitivity = config.sensitivity,
                smoothing = config.smoothing,
            };
        }
    }
}
