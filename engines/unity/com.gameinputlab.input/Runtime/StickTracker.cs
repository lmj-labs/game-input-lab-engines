using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameInputLab
{
    public enum StickTouchPhase
    {
        Began,
        Moved,
        Stationary,
        Ended,
        Canceled,
    }

    public enum StickReleaseReason
    {
        /// <summary>The finger lifted. Output eases back to zero through smoothing.</summary>
        Ended,
        /// <summary>The system cancelled the touch. Output drops to zero at once.</summary>
        Canceled,
        /// <summary>The tracked finger vanished from the touch list, or its ID began again. Drops at once.</summary>
        Lost,
        /// <summary>The app lost focus or was paused. Drops at once.</summary>
        Interrupted,
    }

    /// <summary>One touch in one frame. Positions use +y up, like Unity screen space.</summary>
    public readonly struct StickTouch
    {
        public readonly int Id;
        public readonly Vector2 Position;
        public readonly StickTouchPhase Phase;

        public StickTouch(int id, Vector2 position, StickTouchPhase phase)
        {
            Id = id;
            Position = position;
            Phase = phase;
        }
    }

    /// <summary>
    /// Which finger owns the stick, and how the stick lets go. Input-system agnostic:
    /// feed it every known touch once per frame. Mirrors src/components/stickTracker.ts.
    /// </summary>
    public sealed class StickTracker
    {
        JoystickConfig config;

        public StickTracker(JoystickConfig config = null)
        {
            Config = config;
        }

        public JoystickConfig Config
        {
            get => config;
            set => config = value ?? new JoystickConfig();
        }

        /// <summary>Touch area in the same space as the touch positions. Only touches that begin inside it are taken.</summary>
        public Rect Area { get; set; }

        /// <summary>Touch-space units per point: 1 for points, pixels per point for screen pixels.</summary>
        public float UnitsPerPoint { get; set; } = 1f;

        public int? FingerId { get; private set; }
        public Vector2 Origin { get; private set; }
        public Vector2 Point { get; private set; }

        /// <summary>Smoothed output, length 0–1, +y up.</summary>
        public Vector2 Output { get; private set; }

        /// <summary>Release caused by the latest Step, Track or Interrupt call, if any.</summary>
        public StickReleaseReason? LastRelease { get; private set; }

        public bool IsPressed => FingerId.HasValue;

        public event Action<StickReleaseReason> Released;

        /// <summary>Unsmoothed output for the current finger position.</summary>
        public Vector2 Target
        {
            get
            {
                if (!FingerId.HasValue) return Vector2.zero;
                float scale = UnitsPerPoint > 0f ? UnitsPerPoint : 1f;
                return JoystickResponse.Evaluate((Point - Origin) / scale, config);
            }
        }

        /// <summary>One frame: track the touches, then advance smoothing.</summary>
        public void Step(IReadOnlyList<StickTouch> touches, float deltaMs)
        {
            Track(touches);
            Advance(deltaMs);
        }

        public void Track(IReadOnlyList<StickTouch> touches)
        {
            LastRelease = null;
            if (FingerId.HasValue)
            {
                int index = IndexOf(touches, FingerId.Value);
                if (index < 0 || touches[index].Phase == StickTouchPhase.Began) Release(StickReleaseReason.Lost);
                else if (touches[index].Phase == StickTouchPhase.Ended) Release(StickReleaseReason.Ended);
                else if (touches[index].Phase == StickTouchPhase.Canceled) Release(StickReleaseReason.Canceled);
                else Point = touches[index].Position;
            }
            if (!FingerId.HasValue && touches != null)
            {
                for (int i = 0; i < touches.Count; i++)
                {
                    StickTouch touch = touches[i];
                    if (touch.Phase != StickTouchPhase.Began || !Area.Contains(touch.Position)) continue;
                    FingerId = touch.Id;
                    Point = touch.Position;
                    Origin = config.floating ? touch.Position : Area.center;
                    break;
                }
            }
        }

        public void Advance(float deltaMs)
        {
            Output = JoystickResponse.Smooth(Output, Target, deltaMs, config.smoothing);
        }

        /// <summary>The app lost focus or was paused: drop the finger and zero the output.</summary>
        public void Interrupt()
        {
            LastRelease = null;
            if (FingerId.HasValue) Release(StickReleaseReason.Interrupted);
            else Output = Vector2.zero;
        }

        /// <summary>Back to idle without raising Released.</summary>
        public void Reset()
        {
            FingerId = null;
            Origin = Point = Output = Vector2.zero;
            LastRelease = null;
        }

        void Release(StickReleaseReason reason)
        {
            FingerId = null;
            LastRelease = reason;
            if (reason != StickReleaseReason.Ended) Output = Vector2.zero;
            Released?.Invoke(reason);
        }

        static int IndexOf(IReadOnlyList<StickTouch> touches, int id)
        {
            if (touches == null) return -1;
            for (int i = 0; i < touches.Count; i++)
            {
                if (touches[i].Id == id) return i;
            }
            return -1;
        }
    }
}
