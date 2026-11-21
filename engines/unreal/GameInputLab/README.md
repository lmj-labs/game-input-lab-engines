# Game Input Lab for Unreal

C++ reference implementation of the app's joystick, aim and layout rules (Unreal 5.1+, any platform). It runs the same algorithm as the app; an automation test replays the app's test vectors to check it. Plain C++ classes, no Blueprint nodes yet.

## Install

Copy `GameInputLab` into your project's `Plugins` folder, add `"GameInputLab"` to `PublicDependencyModuleNames` in your module's `Build.cs`, and regenerate project files.

## Use

Export a preset or layout from the app (Unreal C++ tab). It writes a header such as `TwinStickJoystickPreset.h` that builds the config:

```cpp
#include "TwinStickJoystickPreset.h"
#include "GILStickTracker.h"

FGILStickTracker Tracker(TwinStickJoystickPreset::Create());
Tracker.Area = FGILRect(0.0, 0.0, 300.0, 300.0);   // touch area, +y up
Tracker.Step(Touches, DeltaMs);                     // TArray<FGILTouch>, once per frame
const FVector2D Move = Tracker.Output;              // length 0 to 1, +y up
```

Positions and outputs use +y up, like the app. Unreal's screen y points down: pass `ViewHeight - Y`. Call `Tracker.Interrupt()` when the app loses focus.

| Class | What it does | App (`src/`) |
|---|---|---|
| `FGILJoystickConfig` | Parameters, limits, JSON | `components/joystickMath.ts` |
| `FGILJoystickResponse` | Response curve, smoothing | `components/joystickMath.ts` |
| `FGILStickTracker` | Finger ownership, cancel, lost ID, interrupt | `components/stickTracker.ts` |
| `FGILAimBehavior` | Weapon heading, fire threshold, slow while aiming | `components/aimBehavior.ts` |
| `FGILSceneMotion` | Reference scene: speed, acceleration, turning, follow camera | `components/sceneMotion.ts` |
| `FGILSafeAreaLayout`, `FGILLandscapeLayout` | Safe-area placement, layout JSON (`Orientation`: "landscape" or "portrait", lock the screen to match), `Place` for a widget | `landscape/layout.ts` |

All numbers are `double`. JSON exports from the app load with `FromJson` on the config, scene and layout types.

## Tilt

`FGILTiltStick` (with `FGILTiltConfig`, `FGILTilt`) turns phone tilt into a stick output. Pass the gravity vector in iOS convention (x right, y toward the top edge, z out of the screen, face up = z -1); negate it if your platform reports the opposite sign. Call `Calibrate` with the pose the player holds. Upright forward/back tilt cannot be measured from gravity alone.

## Haptics

`FGILHapticPattern` is a custom vibration from the Haptics Lab: `IntensityAt`, `AndroidTimings` and `AndroidAmplitudes`, `LegacyTimings`, `IosEvents`, and `Amplitudes(SampleRate)`, the pattern sampled as 0-255 values. `bLoop` says the pattern repeats from the start when it ends (its total length is the period, so end it with a 0% step to leave a gap); pass it as `bLoop` to `PlayHapticEffect`. `AndroidRepeat()` is the matching Android `repeat` argument. It only computes; there is no player. To play it, fill a `UHapticFeedbackEffect_Buffer` (`Amplitudes`, `SampleRate`) and call `PlayHapticEffect`. That path has not been tried on a phone.

## Tests

`Source/GameInputLab/Private/Tests/GILVectorTests.cpp` replays `Resources/Tests/joystick-vectors.json`, which the app repo generates (`npm run vectors`). Do not edit it. With the plugin in a project's `Plugins` folder:

```
UnrealEditor-Cmd <Project>.uproject -ExecCmds="Automation RunTests GameInputLab; Quit" -unattended -nullrhi -nosplash
```

or open Window > Test Automation and run `GameInputLab.Vectors`.
