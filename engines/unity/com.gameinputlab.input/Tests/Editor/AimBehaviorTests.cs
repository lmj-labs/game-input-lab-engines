using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameInputLab.Tests
{
    /// <summary>Replays the aim behavior vectors the app generates from src/components/aimBehavior.ts.</summary>
    public class AimBehaviorTests
    {
        const string FixturePath = "Packages/com.gameinputlab.input/Tests/Editor/Fixtures/joystick-vectors.json";
        const float Tolerance = 1e-4f;

        static VectorFile Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(FixturePath);
            Assert.IsNotNull(asset, $"Missing fixture {FixturePath}. Run `npm run vectors` in the app repo.");
            return JsonUtility.FromJson<VectorFile>(asset.text);
        }

        [Test]
        public void StepMatchesTheApp()
        {
            AimCase[] cases = Load().aimBehavior;
            Assert.IsNotEmpty(cases, "The fixture has no aim behavior cases.");
            foreach (AimCase item in cases)
            {
                var config = new AimBehaviorConfig { release = item.release, fireThreshold = item.fireThreshold, slowWhileAiming = item.slowWhileAiming };
                AimStep step = AimBehavior.Step(item.heading, new Vector2(item.moveX, item.moveY), new Vector2(item.aimX, item.aimY), config);
                Assert.AreEqual(item.expectedHeading, step.Heading, Tolerance, item.name + " heading");
                Assert.AreEqual(item.expectedAiming, step.Aiming, item.name + " aiming");
                Assert.AreEqual(item.expectedFiring, step.Firing, item.name + " firing");
                Assert.AreEqual(item.expectedMoveScale, step.MoveScale, Tolerance, item.name + " move scale");
            }
        }

        [Test]
        public void NormalizedClampsToTheAppLimits()
        {
            AimBehaviorConfig config = new AimBehaviorConfig { release = "sideways", fireThreshold = 5f, slowWhileAiming = 99f }.Normalized();
            Assert.AreEqual(AimBehaviorConfig.Hold, config.release);
            Assert.AreEqual(20f, config.fireThreshold);
            Assert.AreEqual(80f, config.slowWhileAiming);
        }

        [Test]
        public void DualStickLayoutJsonCarriesTheAimBehavior()
        {
            VectorFile file = Load();
            LandscapeLayout layout = LandscapeLayout.FromJson(file.dualStickExport.json);
            Assert.AreEqual("dualStick", layout.lab);
            AimBehaviorConfig aim = layout.Aim();
            Assert.AreEqual(file.dualStickExport.aim.release, aim.release);
            Assert.AreEqual(file.dualStickExport.aim.fireThreshold, aim.fireThreshold, 1e-5f);
            Assert.AreEqual(file.dualStickExport.aim.slowWhileAiming, aim.slowWhileAiming, 1e-5f);
            // The generated C# builds the same behavior.
            AimBehaviorConfig fromCode = VectorSampleDualLandscapeLayout.Create().Aim();
            Assert.AreEqual(aim.release, fromCode.release);
            Assert.AreEqual(aim.fireThreshold, fromCode.fireThreshold, 1e-5f);
            Assert.AreEqual(aim.slowWhileAiming, fromCode.slowWhileAiming, 1e-5f);
            // A layout without an "aim" key, such as a Stick + Buttons export, gets the defaults.
            AimBehaviorConfig defaults = LandscapeLayout.FromJson(file.landscapeExport.json).Aim();
            Assert.AreEqual(AimBehaviorConfig.Hold, defaults.release);
            Assert.AreEqual(40f, defaults.fireThreshold);
        }

#pragma warning disable 0649
        [System.Serializable] class VectorFile
        {
            public AimCase[] aimBehavior;
            public DualExport dualStickExport;
            public LandscapeExport landscapeExport;
        }

        [System.Serializable] class AimCase
        {
            public string name, release;
            public float fireThreshold, slowWhileAiming, heading, moveX, moveY, aimX, aimY;
            public float expectedHeading, expectedMoveScale;
            public bool expectedAiming, expectedFiring;
        }

        [System.Serializable] class DualExport { public string json; public AimDto aim; }
        [System.Serializable] class AimDto { public string release; public float fireThreshold, slowWhileAiming; }
        [System.Serializable] class LandscapeExport { public string json; }
#pragma warning restore 0649
    }
}
