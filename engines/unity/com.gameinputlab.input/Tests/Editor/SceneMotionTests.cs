using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameInputLab.Tests
{
    /// <summary>Replays the reference scene vectors the app generates from src/components/sceneMotion.ts.</summary>
    public class SceneMotionTests
    {
        const string FixturePath = "Packages/com.gameinputlab.input/Tests/Editor/Fixtures/joystick-vectors.json";
        // The app computes in double, Unity in float, over up to a few hundred frames.
        const float Tolerance = 2e-3f;

        static VectorFile Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(FixturePath);
            Assert.IsNotNull(asset, $"Missing fixture {FixturePath}. Run `npm run vectors` in the app repo.");
            return JsonUtility.FromJson<VectorFile>(asset.text);
        }

        [Test]
        public void StepMatchesTheApp()
        {
            SceneScenario[] scenarios = Load().sceneMotion;
            Assert.IsNotEmpty(scenarios, "The fixture has no scene scenarios.");
            foreach (SceneScenario scenario in scenarios)
            {
                var parameters = new SceneParams
                {
                    maxSpeed = scenario.parameters.maxSpeed,
                    acceleration = scenario.parameters.acceleration,
                    turnRate = scenario.parameters.turnRate,
                    cameraLag = scenario.parameters.cameraLag,
                };
                SceneState state = SceneState.Start;
                for (int index = 0; index < scenario.frames.Length; index++)
                {
                    SceneFrame frame = scenario.frames[index];
                    state = SceneMotion.Step(state, new Vector2(frame.outputX, frame.outputY), frame.deltaMs, parameters);
                    string label = $"{scenario.name} frame {index}";
                    Assert.AreEqual(frame.posX, state.Position.x, Tolerance, label + " position x");
                    Assert.AreEqual(frame.posY, state.Position.y, Tolerance, label + " position y");
                    Assert.AreEqual(frame.velX, state.Velocity.x, Tolerance, label + " velocity x");
                    Assert.AreEqual(frame.velY, state.Velocity.y, Tolerance, label + " velocity y");
                    Assert.AreEqual(frame.heading, state.Heading, Tolerance, label + " heading");
                    Assert.AreEqual(frame.camX, state.Camera.x, Tolerance, label + " camera x");
                    Assert.AreEqual(frame.camY, state.Camera.y, Tolerance, label + " camera y");
                }
            }
        }

        [Test]
        public void NormalizedClampsToTheAppLimits()
        {
            SceneParams clamped = new SceneParams { maxSpeed = 99f, acceleration = 0f, turnRate = 5000f, cameraLag = -3f }.Normalized();
            Assert.AreEqual(10f, clamped.maxSpeed);
            Assert.AreEqual(2f, clamped.acceleration);
            Assert.AreEqual(1080f, clamped.turnRate);
            Assert.AreEqual(0f, clamped.cameraLag);
        }

        [Test]
        public void JsonAndGeneratedCSharpCarryTheScene()
        {
            ExportDto export = Load().sceneExport;
            SceneParams fromJson = SceneParams.FromJson(export.json);
            Assert.AreEqual(export.scene.maxSpeed, fromJson.maxSpeed, 1e-5f);
            Assert.AreEqual(export.scene.acceleration, fromJson.acceleration, 1e-5f);
            Assert.AreEqual(export.scene.turnRate, fromJson.turnRate, 1e-5f);
            Assert.AreEqual(export.scene.cameraLag, fromJson.cameraLag, 1e-5f);
            SceneParams fromCode = VectorSampleSceneJoystickPreset.CreateScene();
            Assert.AreEqual(fromJson.maxSpeed, fromCode.maxSpeed, 1e-5f);
            Assert.AreEqual(fromJson.acceleration, fromCode.acceleration, 1e-5f);
            Assert.AreEqual(fromJson.turnRate, fromCode.turnRate, 1e-5f);
            Assert.AreEqual(fromJson.cameraLag, fromCode.cameraLag, 1e-5f);
            // The stick itself still reads from the same file.
            Assert.IsNotNull(JoystickConfig.FromJson(export.json));
            // A file without a scene gives the defaults.
            Assert.AreEqual(4f, SceneParams.FromJson("{\"format\":\"game-input-lab/joystick\",\"version\":1,\"config\":{}}").maxSpeed);
        }

#pragma warning disable 0649
        [System.Serializable] class VectorFile
        {
            public SceneScenario[] sceneMotion;
            public ExportDto sceneExport;
        }

        [System.Serializable] class SceneScenario
        {
            public string name;
            public ParamsDto parameters;
            public SceneFrame[] frames;
        }

        [System.Serializable] class ParamsDto { public float maxSpeed, acceleration, turnRate, cameraLag; }

        [System.Serializable] class SceneFrame
        {
            public float outputX, outputY, deltaMs;
            public float posX, posY, velX, velY, heading, camX, camY;
        }

        [System.Serializable] class ExportDto { public string json; public ParamsDto scene; }
#pragma warning restore 0649
    }
}
