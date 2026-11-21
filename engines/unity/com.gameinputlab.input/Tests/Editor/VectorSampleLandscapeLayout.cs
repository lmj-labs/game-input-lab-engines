// VectorSampleLandscapeLayout.cs
// Game Input Lab landscape layout "Vector sample" (Stick + Buttons).
//
// Needs the com.gameinputlab.input package (Unity 2022.3+). Version 0.2.0 or newer. Package Manager ▸ + ▸ Add package from git URL:
//   https://github.com/lmj-labs/game-input-lab-engines.git?path=/engines/unity/com.gameinputlab.input
//
// Scene: a full-screen Canvas ▸ an empty child with SafeAreaPanel (fills Screen.safeArea) ▸ one
// RectTransform per control inside it. Then, for example:
//   var layout = VectorSampleLandscapeLayout.Create();
//   float units = SafeAreaLayout.UnitsPerPoint(canvas);
//   layout.Apply(safeArea, "left", leftStickRect, units);
//   layout.Apply(safeArea, "A", buttonARect, units, buttonAHitRect);   // hit area is optional
//   ... the same for "B"
//   leftJoystick.Config = layout.Stick("left");
// Call Apply again when SafeAreaPanel.Changed fires (rotation, new safe area).

using GameInputLab;
using UnityEngine;

public static class VectorSampleLandscapeLayout
{
    public static LandscapeLayout Create() => new LandscapeLayout
    {
        lab = "gamepad",
        controls = new[]
        {
            new ControlLayout { id = "left", anchor = new Vector2(0.22f, 0.35f) },
            new ControlLayout { id = "A", anchor = new Vector2(0.7f, 0.25f), visual = 60f, hit = 80f },
            new ControlLayout { id = "B", anchor = new Vector2(0.86f, 0.52f), visual = 56f, hit = 72f },
        },
        sticks = new[]
        {
            new StickLayout { id = "left", config = new JoystickConfig { floating = false, radius = 80f, deadZone = 12f, outerDeadZone = 5f, curve = 1.4f, sensitivity = 1.2f, smoothing = 0f } },
        },
    };
}
