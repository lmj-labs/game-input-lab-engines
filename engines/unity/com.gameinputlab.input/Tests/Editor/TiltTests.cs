using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameInputLab.Tests
{
    /// <summary>Replays the tilt vectors the app generates from src/sensors/tilt.ts.</summary>
    public class TiltTests
    {
        const string FixturePath = "Packages/com.gameinputlab.input/Tests/Editor/Fixtures/joystick-vectors.json";
        // The app computes in double, Unity in float; angles reach 90 degrees.
        const float AngleTolerance = 1e-3f;
        const float Tolerance = 1e-4f;

        static VectorFile Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(FixturePath);
            Assert.IsNotNull(asset, $"Missing fixture {FixturePath}. Run `npm run vectors` in the app repo.");
            return JsonUtility.FromJson<VectorFile>(asset.text);
        }

        static TiltConfig ToConfig(ConfigDto dto) => new TiltConfig
        {
            orientation = dto.orientation == "topLeft" ? TiltOrientation.TopLeft : dto.orientation == "topRight" ? TiltOrientation.TopRight : TiltOrientation.Portrait,
            invertX = dto.invertX,
            invertY = dto.invertY,
            maxAngle = dto.maxAngle,
            deadZone = dto.deadZone,
            outerDeadZone = dto.outerDeadZone,
            curve = dto.curve,
            sensitivity = dto.sensitivity,
            smoothing = dto.smoothing,
        };

        [Test]
        public void AnglesMatchTheApp()
        {
            AngleCase[] cases = Load().tilt.angles;
            Assert.IsNotEmpty(cases, "The fixture has no tilt angle cases.");
            foreach (AngleCase item in cases)
            {
                TiltAngles angles = Tilt.Angles(new Vector3(item.x, item.y, item.z));
                string label = $"({item.x}, {item.y}, {item.z})";
                Assert.AreEqual(item.roll, angles.Roll, AngleTolerance, label + " roll");
                Assert.AreEqual(item.pitch, angles.Pitch, AngleTolerance, label + " pitch");
            }
        }

        [Test]
        public void TargetMatchesTheApp()
        {
            TargetCase[] cases = Load().tilt.target;
            Assert.IsNotEmpty(cases, "The fixture has no tilt target cases.");
            foreach (TargetCase item in cases)
            {
                Vector2 output = Tilt.Target(new TiltAngles(item.roll, item.pitch), new TiltAngles(item.neutralRoll, item.neutralPitch), ToConfig(item.config));
                string label = $"{item.config.orientation} ({item.roll}, {item.pitch})";
                Assert.AreEqual(item.outputX, output.x, Tolerance, label + " x");
                Assert.AreEqual(item.outputY, output.y, Tolerance, label + " y");
            }
        }

        [Test]
        public void JsonAndGeneratedCSharpCarryTheConfig()
        {
            ExportDto export = Load().tilt.export;
            TiltConfig fromJson = TiltConfig.FromJson(export.json);
            AssertSame(ToConfig(export.config), fromJson);
            AssertSame(fromJson, VectorSampleTiltPreset.Create());
            Assert.Throws<System.ArgumentException>(() => TiltConfig.FromJson("{\"format\":\"something/else\"}"));
        }

        [Test]
        public void NormalizedClampsToTheAppLimits()
        {
            TiltConfig clamped = new TiltConfig { maxAngle = 500f, deadZone = -3f, curve = 1.26f, sensitivity = 99f, smoothing = 99.4f }.Normalized();
            Assert.AreEqual(60f, clamped.maxAngle);
            Assert.AreEqual(0f, clamped.deadZone);
            Assert.AreEqual(1.3f, clamped.curve, 1e-6f);
            Assert.AreEqual(2f, clamped.sensitivity);
            Assert.AreEqual(99f, clamped.smoothing);
        }

        [Test]
        public void TiltStickCalibratesAndSmooths()
        {
            var stick = new TiltStick(new TiltConfig { smoothing = 0f, deadZone = 0f });
            var flat = new Vector3(0f, 0f, -1f);
            stick.Calibrate(flat);
            Assert.AreEqual(0f, stick.Step(flat, 16f).magnitude, 1e-6f);
            // 15 degrees to the right of a 30 degree range: half way.
            Vector2 half = stick.Step(new Vector3(Mathf.Sin(15f * Mathf.Deg2Rad), 0f, -Mathf.Cos(15f * Mathf.Deg2Rad)), 16f);
            Assert.AreEqual(0.5f, half.x, 1e-4f);
            Assert.AreEqual(0f, half.y, 1e-4f);
        }

        static void AssertSame(TiltConfig expected, TiltConfig actual)
        {
            Assert.AreEqual(expected.orientation, actual.orientation);
            Assert.AreEqual(expected.invertX, actual.invertX);
            Assert.AreEqual(expected.invertY, actual.invertY);
            Assert.AreEqual(expected.maxAngle, actual.maxAngle, 1e-5f);
            Assert.AreEqual(expected.deadZone, actual.deadZone, 1e-5f);
            Assert.AreEqual(expected.outerDeadZone, actual.outerDeadZone, 1e-5f);
            Assert.AreEqual(expected.curve, actual.curve, 1e-5f);
            Assert.AreEqual(expected.sensitivity, actual.sensitivity, 1e-5f);
            Assert.AreEqual(expected.smoothing, actual.smoothing, 1e-5f);
        }

#pragma warning disable 0649
        [System.Serializable] class VectorFile { public TiltDto tilt; }
        [System.Serializable] class TiltDto { public AngleCase[] angles; public TargetCase[] target; public ExportDto export; }
        [System.Serializable] class AngleCase { public float x, y, z, roll, pitch; }
        [System.Serializable] class ConfigDto
        {
            public string orientation;
            public bool invertX, invertY;
            public float maxAngle, deadZone, outerDeadZone, curve, sensitivity, smoothing;
        }
        [System.Serializable] class TargetCase
        {
            public ConfigDto config;
            public float roll, pitch, neutralRoll, neutralPitch, outputX, outputY;
        }
        [System.Serializable] class ExportDto { public string json; public ConfigDto config; }
#pragma warning restore 0649
    }
}
