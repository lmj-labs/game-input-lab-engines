// VectorSampleDualLandscapeLayout.cs
// Game Input Lab landscape layout "Vector sample dual" (Dual Stick).
//
// Needs the com.gameinputlab.input package (Unity 2022.3+). Version 0.1.0 or newer. Package Manager ▸ + ▸ Add package from git URL:
//   https://github.com/lmj-labs/game-input-lab-engines.git?path=/engines/unity/com.gameinputlab.input
//
// Scene: a full-screen Canvas ▸ an empty child with SafeAreaPanel (fills Screen.safeArea) ▸ one
// RectTransform per control inside it. Then, for example:
//   var layout = VectorSampleDualLandscapeLayout.Create();
//   float units = SafeAreaLayout.UnitsPerPoint(canvas);
//   layout.Apply(safeArea, "left", leftStickRect, units);
//   layout.Apply(safeArea, "right", rightStickRect, units);
//   leftJoystick.Config = layout.Stick("left");
//   // Every frame: var step = AimBehavior.Step(heading, leftOutput, rightOutput, layout.Aim());
//   // step.Heading is where the weapon points, step.Firing whether it fires, step.MoveScale scales movement.
// Call Apply again when SafeAreaPanel.Changed fires (rotation, new safe area).

using GameInputLab;
using UnityEngine;

public static class VectorSampleDualLandscapeLayout
{
    public static LandscapeLayout Create() => new LandscapeLayout
    {
        lab = "dualStick",
        controls = new[]
        {
            new ControlLayout { id = "left", anchor = new Vector2(0.22f, 0.35f) },
            new ControlLayout { id = "right", anchor = new Vector2(0.78f, 0.35f) },
        },
        sticks = new[]
        {
            new StickLayout { id = "left", config = new JoystickConfig { floating = false, radius = 64f, deadZone = 10f, outerDeadZone = 0f, curve = 1f, sensitivity = 1f, smoothing = 0f } },
            new StickLayout { id = "right", config = new JoystickConfig { floating = false, radius = 72f, deadZone = 8f, outerDeadZone = 0f, curve = 1f, sensitivity = 1.1f, smoothing = 0f } },
        },
        aim = new AimBehaviorConfig { release = "forward", fireThreshold = 55f, slowWhileAiming = 30f },
    };
}
