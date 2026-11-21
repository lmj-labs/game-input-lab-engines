using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameInputLab
{
    /// <summary>One step of a vibration pattern: how long, and how strong (0-100, 0 is a pause).</summary>
    [Serializable]
    public struct HapticStep
    {
        public int durationMs;
        public int intensity;

        public HapticStep(int durationMs, int intensity)
        {
            this.durationMs = durationMs;
            this.intensity = intensity;
        }
    }

    /// <summary>A CoreHaptics event. Times and durations are in seconds, intensity and sharpness 0-1.</summary>
    public struct HapticEvent
    {
        public bool transient;
        public float time;
        public float duration;
        public float intensity;
        public float sharpness;
    }

    /// <summary>
    /// A custom vibration pattern from the app's Haptics Lab. Mirrors src/haptics/pattern.ts: the strength at a time, the
    /// Android waveform, the timings for phones that cannot set the strength, and the iOS events.
    /// </summary>
    public sealed class HapticPattern
    {
        public const string JsonFormat = "game-input-lab/haptic-pattern";
        public const int MaxSteps = 16;
        public const int MaxTotalMs = 5000;
        const int MinStepMs = 10;
        const int MaxStepMs = 1000;
        const int StepMs = 10;
        const int IntensityStep = 5;
        /// <summary>Steps this short or shorter are played as a single tap (iOS transient event).</summary>
        public const int TransientMs = 30;
        public const float IosSharpness = 0.5f;

        public HapticStep[] Steps { get; }

        /// <summary>Play again from the start when the last step ends. The total length is the period, so a trailing pause stays.</summary>
        public bool Loop { get; set; }

        /// <summary>The repeat argument of Android's createWaveform and Vibrator.vibrate(long[], repeat): 0 loops from the start, -1 plays once.</summary>
        public int AndroidRepeat => Loop ? 0 : -1;

        public HapticPattern(params HapticStep[] steps)
        {
            Steps = steps ?? new HapticStep[0];
        }

        public int DurationMs
        {
            get
            {
                int total = 0;
                foreach (HapticStep item in Steps) total += item.durationMs;
                return total;
            }
        }

        static int Snap(int value, int step, int min, int max) => Math.Min(max, Math.Max(min, (int)Math.Floor((double)value / step + 0.5) * step));

        /// <summary>The same limits as the app: 1 to 16 steps, 10-1000 ms in tens, 0-100% in fives, 5 s in all. Never returns an empty pattern.</summary>
        public HapticPattern Normalized()
        {
            var steps = new List<HapticStep>();
            int total = 0;
            for (int i = 0; i < Steps.Length && i < MaxSteps; i++)
            {
                int room = MaxTotalMs - total;
                if (room < MinStepMs) break;
                int duration = Snap(Steps[i].durationMs, StepMs, MinStepMs, MaxStepMs);
                duration = Math.Min(duration, room / StepMs * StepMs);
                steps.Add(new HapticStep(duration, Snap(Steps[i].intensity, IntensityStep, 0, 100)));
                total += duration;
            }
            if (steps.Count == 0) steps.Add(new HapticStep(30, 100));
            return new HapticPattern(steps.ToArray()) { Loop = Loop };
        }

        /// <summary>Strength from 0 to 1 at <paramref name="timeMs"/> after the start. 0 before the start and from the end on.</summary>
        public float IntensityAt(float timeMs)
        {
            if (timeMs < 0f) return 0f;
            float start = 0f;
            foreach (HapticStep item in Steps)
            {
                if (timeMs < start + item.durationMs) return item.intensity / 100f;
                start += item.durationMs;
            }
            return 0f;
        }

        /// <summary>Android 8+ VibrationEffect.createWaveform(timings, amplitudes, <see cref="AndroidRepeat"/>): amplitude 0 is off, 1-255 is the strength.</summary>
        public void AndroidWaveform(out long[] timings, out int[] amplitudes)
        {
            timings = new long[Steps.Length];
            amplitudes = new int[Steps.Length];
            for (int i = 0; i < Steps.Length; i++)
            {
                timings[i] = Steps[i].durationMs;
                amplitudes[i] = Steps[i].intensity <= 0 ? 0 : Math.Min(255, Math.Max(1, (Steps[i].intensity * 255 + 50) / 100));
            }
        }

        /// <summary>For phones that cannot set the strength: off, on, off, on, starting with off. Any strength above 0 is on.</summary>
        public long[] LegacyTimings()
        {
            var timings = new List<long>();
            bool on = false;
            foreach (HapticStep item in Steps)
            {
                bool stepOn = item.intensity > 0;
                if (timings.Count == 0 && stepOn) timings.Add(0);
                if (timings.Count > 0 && stepOn == on) timings[timings.Count - 1] += item.durationMs;
                else timings.Add(item.durationMs);
                on = stepOn;
            }
            return timings.ToArray();
        }

        /// <summary>CoreHaptics events. Pauses become gaps between events.</summary>
        public HapticEvent[] IosEvents()
        {
            var events = new List<HapticEvent>();
            int start = 0;
            foreach (HapticStep item in Steps)
            {
                if (item.intensity > 0)
                {
                    bool transient = item.durationMs <= TransientMs;
                    events.Add(new HapticEvent
                    {
                        transient = transient,
                        time = start / 1000f,
                        duration = transient ? 0f : item.durationMs / 1000f,
                        intensity = item.intensity / 100f,
                        sharpness = IosSharpness,
                    });
                }
                start += item.durationMs;
            }
            return events.ToArray();
        }

        /// <summary>Read a JSON export from the app, or a bare object with a steps list. The result is normalized.</summary>
        public static HapticPattern FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON is empty.", nameof(json));
            var data = new PatternJson();
            JsonUtility.FromJsonOverwrite(json, data);
            if (!string.IsNullOrEmpty(data.format) && data.format != JsonFormat)
                throw new ArgumentException($"Unsupported format \"{data.format}\". Expected \"{JsonFormat}\".", nameof(json));
            return new HapticPattern(data.steps ?? new HapticStep[0]) { Loop = data.loop }.Normalized();
        }

        [Serializable]
        sealed class PatternJson
        {
            public string format;
            public bool loop;
            public HapticStep[] steps;
        }
    }
}
