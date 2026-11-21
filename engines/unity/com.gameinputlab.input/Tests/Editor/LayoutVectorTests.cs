using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameInputLab.Tests
{
    /// <summary>
    /// Replays the landscape layout vectors the app generates from its own code (scripts/joystick-vectors.mjs):
    /// the same layout must put every control in the same place, and the app's export must load unchanged.
    /// </summary>
    public class LayoutVectorTests
    {
        const string FixturePath = "Packages/com.gameinputlab.input/Tests/Editor/Fixtures/joystick-vectors.json";
        // The app computes in double, Unity in float.
        const float Tolerance = 1e-3f;

        static VectorFile Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(FixturePath);
            Assert.IsNotNull(asset, $"Missing fixture {FixturePath}. Run `npm run vectors` in the app repo.");
            var file = JsonUtility.FromJson<VectorFile>(asset.text);
            Assert.AreEqual(1, file.version, "Unknown vector file version.");
            return file;
        }

        [Test]
        public void SafeAreaPlacementMatchesTheApp()
        {
            VectorFile file = Load();
            Assert.IsNotEmpty(file.layout);
            foreach (LayoutCase item in file.layout)
            {
                Rect safe = SafeAreaLayout.SafeRect(item.screen.width, item.screen.height, item.insets.top, item.insets.left, item.insets.right, item.insets.bottom);
                Assert.AreEqual(item.safe.x, safe.x, Tolerance, item.name + " safe x");
                Assert.AreEqual(item.safe.y, safe.y, Tolerance, item.name + " safe y");
                Assert.AreEqual(item.safe.width, safe.width, Tolerance, item.name + " safe width");
                Assert.AreEqual(item.safe.height, safe.height, Tolerance, item.name + " safe height");
                foreach (PlacedDto control in item.controls)
                {
                    string label = $"{item.name} {control.id}";
                    float extent = control.kind == "stick" ? SafeAreaLayout.StickRadius(control.size, safe) : control.size / 2f;
                    Assert.AreEqual(control.extent, extent, Tolerance, label + " extent");
                    Vector2 centre = SafeAreaLayout.Center(safe, new Vector2(control.anchorX, control.anchorY), extent);
                    Assert.AreEqual(control.centerX, centre.x, Tolerance, label + " x");
                    Assert.AreEqual(control.centerY, centre.y, Tolerance, label + " y");
                }
            }
        }

        [Test]
        public void LayoutJsonFromTheAppLoads()
        {
            ExportDto sample = Load().landscapeExport;
            LandscapeLayout layout = LandscapeLayout.FromJson(sample.json);
            Assert.AreEqual("gamepad", layout.lab);
            Assert.AreEqual(sample.controls.Length, layout.controls.Length);
            foreach (ControlDto expected in sample.controls)
            {
                ControlLayout control = layout.Control(expected.id);
                Assert.IsNotNull(control, expected.id);
                Assert.AreEqual(expected.anchorX, control.anchor.x, 1e-5f, expected.id + " anchor x");
                Assert.AreEqual(expected.anchorY, control.anchor.y, 1e-5f, expected.id + " anchor y");
                Assert.AreEqual(expected.visual, control.visual, 1e-5f, expected.id + " visual");
                Assert.AreEqual(expected.hit, control.hit, 1e-5f, expected.id + " hit");
            }
            Assert.AreEqual(sample.sticks.Length, layout.sticks.Length);
            foreach (StickDto expected in sample.sticks)
            {
                JoystickConfig stick = layout.Stick(expected.id);
                Assert.IsNotNull(stick, expected.id);
                Assert.AreEqual(expected.mode == "floating", stick.floating, expected.id + " mode");
                Assert.AreEqual(expected.radius, stick.radius, 1e-5f, expected.id + " radius");
                Assert.AreEqual(expected.deadZone, stick.deadZone, 1e-5f, expected.id + " deadZone");
                Assert.AreEqual(expected.outerDeadZone, stick.outerDeadZone, 1e-5f, expected.id + " outerDeadZone");
                Assert.AreEqual(expected.curve, stick.curve, 1e-5f, expected.id + " curve");
                Assert.AreEqual(expected.sensitivity, stick.sensitivity, 1e-5f, expected.id + " sensitivity");
                Assert.AreEqual(expected.smoothing, stick.smoothing, 1e-5f, expected.id + " smoothing");
            }
        }

        [Test]
        public void GeneratedCSharpMatchesTheJson()
        {
            // VectorSampleLandscapeLayout is written by `npm run vectors` from the same layout as the JSON sample.
            LandscapeLayout fromJson = LandscapeLayout.FromJson(Load().landscapeExport.json);
            LandscapeLayout fromCode = VectorSampleLandscapeLayout.Create();
            Assert.AreEqual(fromJson.lab, fromCode.lab);
            Assert.AreEqual(fromJson.controls.Length, fromCode.controls.Length);
            foreach (ControlLayout expected in fromJson.controls)
            {
                ControlLayout actual = fromCode.Control(expected.id);
                Assert.IsNotNull(actual, expected.id);
                Assert.AreEqual(expected.anchor.x, actual.anchor.x, 1e-5f, expected.id + " anchor x");
                Assert.AreEqual(expected.anchor.y, actual.anchor.y, 1e-5f, expected.id + " anchor y");
                Assert.AreEqual(expected.visual, actual.visual, 1e-5f, expected.id + " visual");
                Assert.AreEqual(expected.hit, actual.hit, 1e-5f, expected.id + " hit");
            }
            foreach (StickLayout expected in fromJson.sticks)
            {
                JoystickConfig actual = fromCode.Stick(expected.id);
                Assert.IsNotNull(actual, expected.id);
                Assert.AreEqual(expected.config.floating, actual.floating, expected.id + " floating");
                Assert.AreEqual(expected.config.radius, actual.radius, 1e-5f, expected.id + " radius");
                Assert.AreEqual(expected.config.deadZone, actual.deadZone, 1e-5f, expected.id + " deadZone");
                Assert.AreEqual(expected.config.curve, actual.curve, 1e-5f, expected.id + " curve");
                Assert.AreEqual(expected.config.smoothing, actual.smoothing, 1e-5f, expected.id + " smoothing");
            }
        }

        [Test]
        public void OrientationIsReadFromTheJson()
        {
            VectorFile file = Load();
            Assert.AreEqual("landscape", LandscapeLayout.FromJson(file.landscapeExport.json).orientation);
            Assert.AreEqual(file.portraitExport.orientation, LandscapeLayout.FromJson(file.portraitExport.json).orientation);
            Assert.AreEqual("portrait", file.portraitExport.orientation);
            // Older exports have no orientation, and anything unknown counts as landscape.
            string format = "\"format\": \"game-input-lab/landscape-layout\"";
            Assert.AreEqual("landscape", LandscapeLayout.FromJson("{" + format + ", \"lab\": \"gamepad\"}").orientation);
            Assert.AreEqual("landscape", LandscapeLayout.FromJson("{" + format + ", \"orientation\": \"sideways\"}").orientation);
        }

        [Test]
        public void ApplyPlacesAndSizesControlsInsideTheSafeArea()
        {
            var root = new GameObject("SafeRoot", typeof(RectTransform));
            try
            {
                var safeRoot = (RectTransform)root.transform;
                safeRoot.sizeDelta = new Vector2(1500f, 738f); // 750 x 369 points at 2 units per point
                RectTransform stickRect = Child(safeRoot, "Stick");
                RectTransform buttonRect = Child(safeRoot, "Jump");
                RectTransform hitRect = Child(buttonRect, "Hit");
                var layout = new LandscapeLayout
                {
                    controls = new[]
                    {
                        new ControlLayout { id = "left", anchor = new Vector2(0.22f, 0.35f) },
                        new ControlLayout { id = "A", anchor = new Vector2(1f, 0f), visual = 56f, hit = 72f },
                    },
                    sticks = new[] { new StickLayout { id = "left", config = new JoystickConfig { radius = 64f } } },
                };

                layout.Apply(safeRoot, "left", stickRect, 2f);
                layout.Apply(safeRoot, "A", buttonRect, 2f, hitRect);

                Assert.AreEqual(256f, stickRect.sizeDelta.x, Tolerance); // ring: 2 x 64 points x 2
                Assert.AreEqual(0.22f * 1500f, stickRect.anchoredPosition.x, Tolerance);
                Assert.AreEqual(0.35f * 738f, stickRect.anchoredPosition.y, Tolerance);
                Assert.AreEqual(112f, buttonRect.sizeDelta.x, Tolerance);  // visible 56 points
                Assert.AreEqual(144f, hitRect.sizeDelta.x, Tolerance);     // hit 72 points
                // Anchored to the bottom right corner, so it is pulled in by half its hit diameter.
                Assert.AreEqual(1500f - 72f, buttonRect.anchoredPosition.x, Tolerance);
                Assert.AreEqual(72f, buttonRect.anchoredPosition.y, Tolerance);
                Assert.AreEqual(buttonRect.sizeDelta.x / 2f, hitRect.anchoredPosition.x, Tolerance);
                Assert.Throws<ArgumentException>(() => layout.Apply(safeRoot, "missing", stickRect, 2f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static RectTransform Child(RectTransform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        // JsonUtility reads public fields with the same names as the JSON keys.
#pragma warning disable 0649
        [Serializable] class VectorFile
        {
            public int version;
            public LayoutCase[] layout;
            public ExportDto landscapeExport;
            public PortraitDto portraitExport;
        }

        [Serializable] class PortraitDto { public string json; public string orientation; }

        [Serializable] class LayoutCase
        {
            public string name;
            public SizeDto screen;
            public InsetsDto insets;
            public RectDto safe;
            public PlacedDto[] controls;
        }

        [Serializable] class SizeDto { public float width, height; }
        [Serializable] class InsetsDto { public float top, left, right, bottom; }
        [Serializable] class RectDto { public float x, y, width, height; }

        [Serializable] class PlacedDto
        {
            public string id;
            public string kind;
            public float anchorX, anchorY;
            public float size, extent, centerX, centerY;
        }

        [Serializable] class ExportDto
        {
            public string json;
            public ControlDto[] controls;
            public StickDto[] sticks;
        }

        [Serializable] class ControlDto { public string id; public float anchorX, anchorY, visual, hit; }

        [Serializable] class StickDto
        {
            public string id;
            public string mode;
            public float radius, deadZone, outerDeadZone, curve, sensitivity, smoothing;
        }
#pragma warning restore 0649
    }
}
