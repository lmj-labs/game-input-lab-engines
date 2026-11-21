using System;
using UnityEngine;

namespace GameInputLab
{
    /// <summary>
    /// What the right stick does to the aim: where the weapon points after you let go, the push at which it
    /// fires, and how much movement slows while you aim. Mirrors src/components/aimBehavior.ts.
    /// </summary>
    [Serializable]
    public sealed class AimBehaviorConfig
    {
        public const string Hold = "hold";
        public const string Forward = "forward";

        [Tooltip("\"hold\" keeps the last aimed direction; \"forward\" turns to the way the player moves.")]
        public string release = Hold;
        [Tooltip("Right stick output, in percent of a full push, at which the weapon fires (20-100).")]
        public float fireThreshold = 40f;
        [Tooltip("Percent by which movement slows while aiming (0-80). 0 is off.")]
        public float slowWhileAiming;

        /// <summary>A copy clamped to the app's limits.</summary>
        public AimBehaviorConfig Normalized() => new AimBehaviorConfig
        {
            release = release == Forward ? Forward : Hold,
            fireThreshold = Mathf.Clamp(Mathf.Round(fireThreshold), 20f, 100f),
            slowWhileAiming = Mathf.Clamp(Mathf.Round(slowWhileAiming), 0f, 80f),
        };
    }

    /// <summary>The result of one frame of <see cref="AimBehavior.Step"/>.</summary>
    public readonly struct AimStep
    {
        /// <summary>Radians, y up (Atan2(y, x)).</summary>
        public readonly float Heading;
        public readonly bool Aiming;
        public readonly bool Firing;
        /// <summary>Multiply the movement speed by this.</summary>
        public readonly float MoveScale;

        public AimStep(float heading, bool aiming, bool firing, float moveScale)
        {
            Heading = heading;
            Aiming = aiming;
            Firing = firing;
            MoveScale = moveScale;
        }
    }

    public static class AimBehavior
    {
        /// <summary>Stick output beyond this counts as aiming (and as moving, for the left stick). Mirrors engaged.</summary>
        public const float Engaged = 0.15f;

        /// <summary>
        /// One frame. <paramref name="heading"/> is the previous heading; move and aim are the two stick outputs
        /// (+y up, length 0-1). Mirrors aimStep. The config is used as given: call Normalized first for untrusted data.
        /// </summary>
        public static AimStep Step(float heading, Vector2 move, Vector2 aim, AimBehaviorConfig config)
        {
            float strength = aim.magnitude;
            bool aiming = strength > Engaged;
            float next = heading;
            if (aiming) next = Mathf.Atan2(aim.y, aim.x);
            else if (config.release == AimBehaviorConfig.Forward && move.magnitude > Engaged) next = Mathf.Atan2(move.y, move.x);
            return new AimStep(
                next,
                aiming,
                aiming && strength >= config.fireThreshold / 100f,
                aiming ? 1f - config.slowWhileAiming / 100f : 1f);
        }
    }
}
