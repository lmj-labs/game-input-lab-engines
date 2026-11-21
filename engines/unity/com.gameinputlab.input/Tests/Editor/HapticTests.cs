using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameInputLab.Tests
{
    /// <summary>Replays the vibration vectors the app generates from src/haptics/pattern.ts.</summary>
    public class HapticTests
    {
        const string FixturePath = "Packages/com.gameinputlab.input/Tests/Editor/Fixtures/joystick-vectors.json";
        const float Tolerance = 1e-5f;

        static HapticsDto Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(FixturePath);
            Assert.IsNotNull(asset, $"Missing fixture {FixturePath}. Run `npm run vectors` in the app repo.");
            return JsonUtility.FromJson<VectorFile>(asset.text).haptics;
        }

        static HapticPattern ToPattern(StepDto[] steps, bool loop = false) => new HapticPattern(steps.Select(item => new HapticStep(item.durationMs, item.intensity)).ToArray()) { Loop = loop };

        [Test]
        public void LimitsMatchTheApp()
        {
            HapticsDto data = Load();
            Assert.AreEqual(data.maxSteps, HapticPattern.MaxSteps);
            Assert.AreEqual(data.maxTotalMs, HapticPattern.MaxTotalMs);
            Assert.AreEqual(data.transientMs, HapticPattern.TransientMs);
            Assert.AreEqual(data.sharpness, HapticPattern.IosSharpness, Tolerance);
        }

        [Test]
        public void StrengthAndDurationMatchTheApp()
        {
            foreach (PatternDto item in Load().patterns)
            {
                HapticPattern pattern = ToPattern(item.steps);
                Assert.AreEqual(item.duration, pattern.DurationMs, item.id + " duration");
                foreach (SampleDto sample in item.samples)
                    Assert.AreEqual(sample.intensity, pattern.IntensityAt(sample.timeMs), Tolerance, $"{item.id} at {sample.timeMs} ms");
            }
        }

        [Test]
        public void AndroidWaveformAndLegacyTimingsMatchTheApp()
        {
            foreach (PatternDto item in Load().patterns)
            {
                HapticPattern pattern = ToPattern(item.steps, item.loop);
                Assert.AreEqual(item.androidRepeat, pattern.AndroidRepeat, item.id + " repeat");
                pattern.AndroidWaveform(out long[] timings, out int[] amplitudes);
                CollectionAssert.AreEqual(item.android.timings.Select(value => (long)value).ToArray(), timings, item.id + " timings");
                CollectionAssert.AreEqual(item.android.amplitudes, amplitudes, item.id + " amplitudes");
                CollectionAssert.AreEqual(item.legacy.Select(value => (long)value).ToArray(), pattern.LegacyTimings(), item.id + " legacy");
            }
        }

        [Test]
        public void IosEventsMatchTheApp()
        {
            foreach (PatternDto item in Load().patterns)
            {
                HapticEvent[] events = ToPattern(item.steps).IosEvents();
                Assert.AreEqual(item.ios.Length, events.Length, item.id + " event count");
                for (int i = 0; i < events.Length; i++)
                {
                    string label = $"{item.id} event {i}";
                    Assert.AreEqual(item.ios[i].kind == "transient", events[i].transient, label + " kind");
                    Assert.AreEqual(item.ios[i].time, events[i].time, Tolerance, label + " time");
                    Assert.AreEqual(item.ios[i].duration, events[i].duration, Tolerance, label + " duration");
                    Assert.AreEqual(item.ios[i].intensity, events[i].intensity, Tolerance, label + " intensity");
                    Assert.AreEqual(item.ios[i].sharpness, events[i].sharpness, Tolerance, label + " sharpness");
                }
            }
        }

        [Test]
        public void NormalizedClampsLikeTheApp()
        {
            foreach (NormalizeDto item in Load().normalize)
            {
                HapticPattern normalized = ToPattern(item.input.steps, item.input.loop).Normalized();
                HapticStep[] actual = normalized.Steps;
                Assert.AreEqual(item.loop, normalized.Loop, "loop");
                Assert.AreEqual(item.steps.Length, actual.Length, "step count");
                for (int i = 0; i < actual.Length; i++)
                {
                    Assert.AreEqual(item.steps[i].durationMs, actual[i].durationMs, $"step {i} duration");
                    Assert.AreEqual(item.steps[i].intensity, actual[i].intensity, $"step {i} intensity");
                }
            }
        }

        [Test]
        public void JsonExportReadsBack()
        {
            const string json = "{\"format\":\"game-input-lab/haptic-pattern\",\"version\":1,\"name\":\"x\",\"steps\":[{\"durationMs\":30,\"intensity\":100},{\"durationMs\":80,\"intensity\":0},{\"durationMs\":33,\"intensity\":47}]}";
            HapticStep[] steps = HapticPattern.FromJson(json).Steps;
            Assert.AreEqual(3, steps.Length);
            Assert.AreEqual(30, steps[2].durationMs);
            Assert.AreEqual(45, steps[2].intensity);
            Assert.IsFalse(HapticPattern.FromJson(json).Loop);
            Assert.IsTrue(HapticPattern.FromJson("{\"loop\":true,\"steps\":[{\"durationMs\":30,\"intensity\":100}]}").Loop);
            Assert.Throws<System.ArgumentException>(() => HapticPattern.FromJson("{\"format\":\"other\",\"steps\":[]}"));
        }

        [System.Serializable] class VectorFile { public HapticsDto haptics; }
        [System.Serializable] class HapticsDto
        {
            public int maxSteps, maxTotalMs, transientMs;
            public float sharpness;
            public PatternDto[] patterns;
            public NormalizeDto[] normalize;
        }
        [System.Serializable] class StepDto { public int durationMs, intensity; }
        [System.Serializable] class SampleDto { public float timeMs, intensity; }
        [System.Serializable] class AndroidDto { public long[] timings; public int[] amplitudes; }
        [System.Serializable] class EventDto { public string kind; public float time, duration, intensity, sharpness; }
        [System.Serializable] class PatternDto
        {
            public string id;
            public StepDto[] steps;
            public bool loop;
            public int androidRepeat;
            public int duration;
            public SampleDto[] samples;
            public AndroidDto android;
            public long[] legacy;
            public EventDto[] ios;
        }
        [System.Serializable] class InputDto { public StepDto[] steps; public bool loop; }
        [System.Serializable] class NormalizeDto { public InputDto input; public StepDto[] steps; public bool loop; }
    }
}
