// VectorSampleSceneJoystickPreset.cs
// Game Input Lab preset "Vector sample scene".
//
// Needs the com.gameinputlab.input package (Unity 2022.3+). Version 0.1.0 or newer. Package Manager ▸ + ▸ Add package from git URL:
//   https://github.com/lmj-labs/game-input-lab-engines.git?path=/engines/unity/com.gameinputlab.input
// The package runs the same response, smoothing and touch handling as the app.
//
// Use it on a VirtualJoystick:   joystick.Config = VectorSampleSceneJoystickPreset.Create();
// Or in your own input code:     var tracker = new StickTracker(VectorSampleSceneJoystickPreset.Create());
// The reference scene (Joystick Lab) the stick was tuned in:
//   var state = SceneState.Start;
//   state = SceneMotion.Step(state, stickOutput, deltaMs, VectorSampleSceneJoystickPreset.CreateScene());
//   // state.Position, state.Heading and state.Camera drive the character and the camera.

using GameInputLab;

public static class VectorSampleSceneJoystickPreset
{
    public static JoystickConfig Create() => new JoystickConfig
    {
        floating = false,
        radius = 96f,          // points
        deadZone = 0f,       // % of radius
        outerDeadZone = 30f,  // % of radius
        curve = 1.6f,
        sensitivity = 0.75f,
        smoothing = 0f,       // ms
    };

    public static SceneParams CreateScene() => new SceneParams
    {
        maxSpeed = 5.5f,        // units per second
        acceleration = 30f,  // units per second squared
        turnRate = 540f,        // degrees per second
        cameraLag = 180f,       // ms
    };
}
