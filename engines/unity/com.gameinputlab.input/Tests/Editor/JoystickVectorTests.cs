using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameInputLab.Tests
{
    /// <summary>
    /// Replays the vectors the app generates from its own code (scripts/joystick-vectors.mjs).
    /// A failure here means Unity and the app no longer feel the same.
    /// </summary>
    public class JoystickVectorTests
    {
        const string FixturePath = "Packages/com.gameinputlab.input/Tests/Editor/Fixtures/joystick-vectors.json";

        // The app computes in double, Unity in float.
        const float ResponseTolerance = 1e-4f;
        // Smoothing snaps onto the target within 0.001, so float and double may snap one frame apart.
        const float SmoothingTolerance = 1e-3f;

        static VectorFile vectors;

        static VectorFile Vectors
        {
            get
            {
                if (vectors != null) return vectors;
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(FixturePath);
                Assert.IsNotNull(asset, $"Missing fixture {FixturePath}. Run `npm run vectors` in the app repo.");
                vectors = JsonUtility.FromJson<VectorFile>(asset.text);
                Assert.AreEqual(1, vectors.version, "Unknown vector file version.");
                return vectors;
            }
        }

        [Test]
        public void FieldLimitsAndDefaultsMatchTheApp()
        {
            Assert.AreEqual(Vectors.fields.Length, JoystickConfig.Ranges.Count, "Field count");
            foreach (FieldDto field in Vectors.fields)
            {
                Assert.IsTrue(JoystickConfig.Ranges.TryGetValue(field.key, out JoystickConfig.FieldRange range), $"Missing field {field.key}");
                Assert.AreEqual(field.min, range.Min, 0f, $"{field.key} min");
                Assert.AreEqual(field.max, range.Max, 0f, $"{field.key} max");
                Assert.AreEqual(field.decimals, range.Decimals, $"{field.key} decimals");
            }
            AssertSameConfig(Vectors.defaults.ToConfig(), new JoystickConfig(), "defaults");
        }

        [Test]
        public void ResponseCurveMatchesTheApp()
        {
            foreach (CurveCase item in Vectors.curve)
            {
                float actual = JoystickResponse.ResponseCurve(item.deflection, item.config.ToConfig());
                Assert.AreEqual(item.expected, actual, ResponseTolerance, $"deflection {item.deflection} with {item.config}");
            }
        }

        [Test]
        public void StickResponseMatchesTheApp()
        {
            foreach (ResponseCase item in Vectors.response)
            {
                Vector2 actual = JoystickResponse.Evaluate(new Vector2(item.offsetX, item.offsetY), item.config.ToConfig());
                string label = $"offset ({item.offsetX}, {item.offsetY}) with {item.config}";
                Assert.AreEqual(item.outputX, actual.x, ResponseTolerance, label + " x");
                Assert.AreEqual(item.outputY, actual.y, ResponseTolerance, label + " y");
            }
        }

        [Test]
        public void SmoothingMatchesTheApp()
        {
            foreach (SmoothingCase item in Vectors.smoothing)
            {
                Vector2 actual = JoystickResponse.Smooth(
                    new Vector2(item.currentX, item.currentY), new Vector2(item.targetX, item.targetY), item.deltaMs, item.smoothingMs);
                string label = $"smooth {item.deltaMs} ms of {item.smoothingMs} ms";
                Assert.AreEqual(item.outputX, actual.x, SmoothingTolerance, label + " x");
                Assert.AreEqual(item.outputY, actual.y, SmoothingTolerance, label + " y");
            }
        }

        [Test]
        public void JsonExportFromTheAppLoads()
        {
            JoystickConfig parsed = JoystickConfig.FromJson(Vectors.exportSample.json);
            AssertSameConfig(Vectors.exportSample.config.ToConfig(), parsed, "export sample");
            AssertSameConfig(parsed, JoystickConfig.FromJson(parsed.ToJson()), "round trip");
        }

        [Test]
        public void TouchScenariosMatchTheApp()
        {
            Assert.IsNotEmpty(Vectors.scenarios);
            foreach (ScenarioDto scenario in Vectors.scenarios)
            {
                var tracker = new StickTracker(scenario.config.ToConfig())
                {
                    Area = new Rect(scenario.area.x, scenario.area.y, scenario.area.width, scenario.area.height),
                    UnitsPerPoint = scenario.unitsPerPoint,
                };
                var touches = new List<StickTouch>();
                for (int index = 0; index < scenario.frames.Length; index++)
                {
                    FrameDto frame = scenario.frames[index];
                    string label = $"\"{scenario.name}\" frame {index}";
                    if (frame.interrupt)
                    {
                        tracker.Interrupt();
                    }
                    else
                    {
                        touches.Clear();
                        foreach (TouchDto touch in frame.touches) touches.Add(touch.ToTouch());
                        tracker.Step(touches, frame.deltaMs);
                    }
                    Assert.AreEqual(frame.fingerId, tracker.FingerId ?? -1, label + " finger");
                    Assert.AreEqual(frame.released, ReasonName(tracker.LastRelease), label + " release");
                    Assert.AreEqual(frame.outputX, tracker.Output.x, SmoothingTolerance, label + " output x");
                    Assert.AreEqual(frame.outputY, tracker.Output.y, SmoothingTolerance, label + " output y");
                }
            }
        }

        static void AssertSameConfig(JoystickConfig expected, JoystickConfig actual, string label)
        {
            Assert.AreEqual(expected.floating, actual.floating, label + " floating");
            Assert.AreEqual(expected.radius, actual.radius, 1e-5f, label + " radius");
            Assert.AreEqual(expected.deadZone, actual.deadZone, 1e-5f, label + " deadZone");
            Assert.AreEqual(expected.outerDeadZone, actual.outerDeadZone, 1e-5f, label + " outerDeadZone");
            Assert.AreEqual(expected.curve, actual.curve, 1e-5f, label + " curve");
            Assert.AreEqual(expected.sensitivity, actual.sensitivity, 1e-5f, label + " sensitivity");
            Assert.AreEqual(expected.smoothing, actual.smoothing, 1e-5f, label + " smoothing");
        }

        static string ReasonName(StickReleaseReason? reason)
        {
            switch (reason)
            {
                case StickReleaseReason.Ended: return "ended";
                case StickReleaseReason.Canceled: return "canceled";
                case StickReleaseReason.Lost: return "lost";
                case StickReleaseReason.Interrupted: return "interrupted";
                default: return "";
            }
        }

        // JsonUtility reads public fields with the same names as the JSON keys.
#pragma warning disable 0649
        [Serializable] class VectorFile
        {
            public int version;
            public ConfigDto defaults;
            public FieldDto[] fields;
            public CurveCase[] curve;
            public ResponseCase[] response;
            public SmoothingCase[] smoothing;
            public ExportSampleDto exportSample;
            public ScenarioDto[] scenarios;
        }

        [Serializable] class ConfigDto
        {
            public string mode;
            public float radius, deadZone, outerDeadZone, curve, sensitivity, smoothing;

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

            public override string ToString() =>
                $"{mode} r{radius} dz{deadZone}/{outerDeadZone} c{curve} s{sensitivity} sm{smoothing}";
        }

        [Serializable] class FieldDto { public string key; public float min, max, step; public int decimals; }
        [Serializable] class CurveCase { public ConfigDto config; public float deflection, expected; }
        [Serializable] class ResponseCase { public ConfigDto config; public float offsetX, offsetY, outputX, outputY; }
        [Serializable] class SmoothingCase { public float currentX, currentY, targetX, targetY, deltaMs, smoothingMs, outputX, outputY; }
        [Serializable] class ExportSampleDto { public string json; public ConfigDto config; }
        [Serializable] class AreaDto { public float x, y, width, height; }

        [Serializable] class ScenarioDto
        {
            public string name;
            public ConfigDto config;
            public AreaDto area;
            public float unitsPerPoint;
            public FrameDto[] frames;
        }

        [Serializable] class FrameDto
        {
            public bool interrupt;
            public float deltaMs;
            public TouchDto[] touches;
            public int fingerId;
            public string released;
            public float outputX, outputY;
        }

        [Serializable] class TouchDto
        {
            public int id;
            public float x, y;
            public string phase;

            public StickTouch ToTouch()
            {
                StickTouchPhase value;
                switch (phase)
                {
                    case "began": value = StickTouchPhase.Began; break;
                    case "moved": value = StickTouchPhase.Moved; break;
                    case "stationary": value = StickTouchPhase.Stationary; break;
                    case "ended": value = StickTouchPhase.Ended; break;
                    case "canceled": value = StickTouchPhase.Canceled; break;
                    default: throw new ArgumentException($"Unknown phase {phase}");
                }
                return new StickTouch(id, new Vector2(x, y), value);
            }
        }
#pragma warning restore 0649
    }
}
