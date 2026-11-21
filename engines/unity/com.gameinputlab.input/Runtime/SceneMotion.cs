using System;
using UnityEngine;

namespace GameInputLab
{
    /// <summary>
    /// The reference scene's parameters from Joystick Lab: how a character driven by the stick moves, and how the
    /// camera follows. Mirrors src/components/sceneMotion.ts.
    /// </summary>
    [Serializable]
    public sealed class SceneParams
    {
        [Tooltip("Units per second at full stick (1-10).")]
        public float maxSpeed = 4f;
        [Tooltip("Units per second squared: how fast the velocity follows the stick (2-100).")]
        public float acceleration = 20f;
        [Tooltip("Degrees per second the character can turn (90-1080).")]
        public float turnRate = 720f;
        [Tooltip("Time constant of the camera follow in milliseconds. 0 locks the camera to the character (0-400).")]
        public float cameraLag = 120f;

        /// <summary>A copy clamped to the app's limits. Mirrors normalizeScene.</summary>
        public SceneParams Normalized() => new SceneParams
        {
            maxSpeed = Mathf.Clamp(Mathf.Round(maxSpeed * 10f) / 10f, 1f, 10f),
            acceleration = Mathf.Clamp(Mathf.Round(acceleration), 2f, 100f),
            turnRate = Mathf.Clamp(Mathf.Round(turnRate), 90f, 1080f),
            cameraLag = Mathf.Clamp(Mathf.Round(cameraLag), 0f, 400f),
        };

        /// <summary>Read the "scene" of a JSON export from the app. A file without one gives the defaults.</summary>
        public static SceneParams FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON is empty.", nameof(json));
            var envelope = new Envelope();
            JsonUtility.FromJsonOverwrite(json, envelope);
            if (!string.IsNullOrEmpty(envelope.format) && envelope.format != JoystickConfig.JsonFormat)
            {
                throw new ArgumentException($"Unsupported format \"{envelope.format}\". Expected \"{JoystickConfig.JsonFormat}\".", nameof(json));
            }
            return (envelope.scene ?? new SceneParams()).Normalized();
        }

        [Serializable]
        sealed class Envelope
        {
            public string format;
            public SceneParams scene = new SceneParams();
        }
    }

    /// <summary>The character and camera in the reference scene. World units, +y up.</summary>
    public readonly struct SceneState
    {
        public readonly Vector2 Position;
        public readonly Vector2 Velocity;
        /// <summary>Radians, y up.</summary>
        public readonly float Heading;
        public readonly Vector2 Camera;

        public SceneState(Vector2 position, Vector2 velocity, float heading, Vector2 camera)
        {
            Position = position;
            Velocity = velocity;
            Heading = heading;
            Camera = camera;
        }

        /// <summary>At the origin, facing up, at rest.</summary>
        public static SceneState Start => new SceneState(Vector2.zero, Vector2.zero, Mathf.PI / 2f, Vector2.zero);
    }

    public static class SceneMotion
    {
        /// <summary>Longest frame that still counts, so a stall does not throw the character across the floor.</summary>
        public const float MaxFrameMs = 100f;

        /// <summary>The shortest signed turn from one angle to another, in (-π, π]. Mirrors angleDiff.</summary>
        public static float AngleDiff(float from, float to)
        {
            float diff = (to - from) % (2f * Mathf.PI);
            if (diff > Mathf.PI) return diff - 2f * Mathf.PI;
            if (diff <= -Mathf.PI) return diff + 2f * Mathf.PI;
            return diff;
        }

        /// <summary>
        /// One frame. <paramref name="output"/> is the stick output (+y up, length 0-1). Speed follows power, so half
        /// power moves at half speed. Mirrors stepScene.
        /// </summary>
        public static SceneState Step(SceneState state, Vector2 output, float deltaMs, SceneParams parameters)
        {
            if (!(deltaMs > 0f)) return state;
            float frameMs = Mathf.Min(MaxFrameMs, deltaMs);
            float dt = frameMs / 1000f;

            Vector2 target = output * parameters.maxSpeed;
            Vector2 delta = target - state.Velocity;
            float gap = delta.magnitude;
            float reach = parameters.acceleration * dt;
            Vector2 velocity = gap <= reach ? target : state.Velocity + delta / gap * reach;

            float heading = state.Heading;
            if (output.magnitude > AimBehavior.Engaged)
            {
                float turn = AngleDiff(heading, Mathf.Atan2(output.y, output.x));
                float most = parameters.turnRate * Mathf.Deg2Rad * dt;
                heading += Mathf.Clamp(turn, -most, most);
            }

            Vector2 position = state.Position + velocity * dt;
            Vector2 camera = JoystickResponse.Smooth(state.Camera, position, frameMs, parameters.cameraLag);
            return new SceneState(position, velocity, heading, camera);
        }
    }
}
