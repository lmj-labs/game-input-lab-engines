using UnityEngine;

namespace GameInputLab
{
    /// <summary>
    /// Stick response math. Each method mirrors the function of the same role in
    /// src/components/joystickMath.ts; the shared test vectors check they agree.
    /// </summary>
    public static class JoystickResponse
    {
        /// <summary>Distances closer than this to the target snap onto it, so smoothing settles at rest.</summary>
        public const float SmoothingSnap = 0.001f;

        /// <summary>Stick deflection (0–1) to output strength (0–1). Mirrors responseCurve.</summary>
        public static float ResponseCurve(float deflection, JoystickConfig config)
        {
            float inner = config.deadZone / 100f;
            float outer = 1f - config.outerDeadZone / 100f;
            if (deflection <= inner) return 0f;
            float scaled = Mathf.Min(1f, (deflection - inner) / (outer - inner));
            return Mathf.Min(1f, Mathf.Pow(scaled, config.curve) * config.sensitivity);
        }

        /// <summary>
        /// Finger offset from the stick centre in points (+y up) to output with length 0–1.
        /// The direction is never altered. Mirrors stickResponseUp.
        /// </summary>
        public static Vector2 Evaluate(Vector2 offsetPoints, JoystickConfig config)
        {
            float length = offsetPoints.magnitude;
            if (config.radius <= 0f || length == 0f) return Vector2.zero;
            float strength = ResponseCurve(Mathf.Min(1f, length / config.radius), config);
            if (strength == 0f) return Vector2.zero;
            return offsetPoints / length * strength;
        }

        /// <summary>Frame-rate independent exponential smoothing. Mirrors smoothStick.</summary>
        public static Vector2 Smooth(Vector2 current, Vector2 target, float deltaMs, float smoothingMs)
        {
            if (smoothingMs <= 0f) return target;
            if (deltaMs <= 0f) return current;
            float t = 1f - Mathf.Exp(-deltaMs / smoothingMs);
            Vector2 next = current + (target - current) * t;
            return (target - next).magnitude < SmoothingSnap ? target : next;
        }
    }
}
