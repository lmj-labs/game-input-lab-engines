// VectorSampleTiltPreset.cs
// Game Input Lab tilt preset "Vector sample tilt".
//
// Needs the com.gameinputlab.input package (Unity 2022.3+). Version 0.2.0 or newer. Package Manager ▸ + ▸ Add package from git URL:
//   https://github.com/lmj-labs/game-input-lab-engines.git?path=/engines/unity/com.gameinputlab.input
// The package runs the same tilt, response and smoothing as the app.
//
// The reading is the direction gravity pulls, in the phone's own axes (any unit, the scale does not matter): x to the right, y to the top edge,
// z out of the screen, so lying face up reads z = -1 (what iOS reports; negate Android's raw accelerometer).
// Check it with Input.acceleration on the phone. If tilting right still moves the wrong way, flip invertX.
//   var tilt = new TiltStick(VectorSampleTiltPreset.Create());
//   tilt.Calibrate(Input.acceleration);   // the pose that means "no tilt"; call it when play starts
//   Vector2 move = tilt.Step(Input.acceleration, Time.deltaTime * 1000f);   // length 0-1, +y up

using GameInputLab;

public static class VectorSampleTiltPreset
{
    public static TiltConfig Create() => new TiltConfig
    {
        orientation = TiltOrientation.TopLeft,
        invertX = false,
        invertY = true,
        maxAngle = 25f,        // degrees
        deadZone = 8f,        // % of maxAngle
        outerDeadZone = 4f,   // % of maxAngle
        curve = 1.4f,
        sensitivity = 1.2f,
        smoothing = 60f,        // ms
    };
}
