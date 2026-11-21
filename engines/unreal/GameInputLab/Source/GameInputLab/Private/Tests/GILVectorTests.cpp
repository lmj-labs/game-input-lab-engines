// Replays the test vectors the app generates from its own code (scripts/joystick-vectors.mjs): the same inputs
// must give the same outputs in Unreal and in the app. A failure here means Unreal and the app no longer feel the
// same.
//
// Run from an Unreal project that has this plugin in its Plugins folder (Unreal 5.1 or newer):
//   UnrealEditor-Cmd <Project>.uproject -ExecCmds="Automation RunTests GameInputLab; Quit" -unattended -nullrhi -nosplash
// or open Window > Test Automation and run GameInputLab.Vectors.

#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

#include "GILAimBehavior.h"
#include "GILJoystickConfig.h"
#include "GILJoystickResponse.h"
#include "GILLandscapeLayout.h"
#include "GILLog.h"
#include "GILSafeAreaLayout.h"
#include "GILSceneMotion.h"
#include "GILStickTracker.h"
#include "GILTilt.h"
#include "GILHaptics.h"
#include "Interfaces/IPluginManager.h"
#include "Misc/FileHelper.h"
#include "Misc/Paths.h"

namespace
{
	// Everything runs in 64-bit floats, like the app, so only the last bits can differ between the two math libraries.
	constexpr double Tolerance = 1e-6;
	// Smoothing snaps onto the target within 0.001, so the two may snap one frame apart.
	constexpr double SmoothingTolerance = 1e-3;
	// The scene scripts run for a few hundred frames and smooth the camera.
	constexpr double SceneTolerance = 2e-3;

	using FObj = TSharedPtr<FJsonObject>;
	using FList = TArray<TSharedPtr<FJsonValue>>;

	struct FRun
	{
		FAutomationTestBase& Test;
		int32 Checks = 0;
		int32 Failures = 0;

		explicit FRun(FAutomationTestBase& InTest) : Test(InTest) {}

		void Expect(bool bCondition, const FString& Label)
		{
			++Checks;
			if (!bCondition)
			{
				++Failures;
				Test.AddError(TEXT("FAIL ") + Label);
			}
		}

		void Near(double Expected, double Actual, double Tol, const FString& Label)
		{
			++Checks;
			if (FMath::Abs(Expected - Actual) > Tol)
			{
				++Failures;
				Test.AddError(FString::Printf(TEXT("FAIL %s: expected %g, got %g"), *Label, Expected, Actual));
			}
		}

		void SameConfig(const FGILJoystickConfig& Expected, const FGILJoystickConfig& Actual, const FString& Label)
		{
			Expect(Expected.bFloating == Actual.bFloating, Label + TEXT(" floating"));
			Near(Expected.Radius, Actual.Radius, 1e-9, Label + TEXT(" radius"));
			Near(Expected.DeadZone, Actual.DeadZone, 1e-9, Label + TEXT(" deadZone"));
			Near(Expected.OuterDeadZone, Actual.OuterDeadZone, 1e-9, Label + TEXT(" outerDeadZone"));
			Near(Expected.Curve, Actual.Curve, 1e-9, Label + TEXT(" curve"));
			Near(Expected.Sensitivity, Actual.Sensitivity, 1e-9, Label + TEXT(" sensitivity"));
			Near(Expected.Smoothing, Actual.Smoothing, 1e-9, Label + TEXT(" smoothing"));
		}
	};

	double Num(const FObj& Object, const TCHAR* Key) { return Object->GetNumberField(Key); }
	FString Str(const FObj& Object, const TCHAR* Key) { return Object->GetStringField(Key); }
	const FJsonObject& Sub(const FObj& Object, const TCHAR* Key) { return *Object->GetObjectField(Key); }
	FObj SubPtr(const FObj& Object, const TCHAR* Key) { return Object->GetObjectField(Key); }
	const FList& List(const FObj& Object, const TCHAR* Key) { return Object->GetArrayField(Key); }

	void FieldsAndDefaults(FRun& R, const FObj& V)
	{
		const FList& Fields = List(V, TEXT("fields"));
		const TConstArrayView<FGILFieldRange> Ranges = FGILJoystickConfig::Ranges();
		R.Expect(Fields.Num() == Ranges.Num(), TEXT("field count"));
		for (const TSharedPtr<FJsonValue>& Value : Fields)
		{
			const FObj Field = Value->AsObject();
			const FString Key = Str(Field, TEXT("key"));
			const FGILFieldRange* Range = Ranges.FindByPredicate([&Key](const FGILFieldRange& Item) { return Key == Item.Key; });
			R.Expect(Range != nullptr, TEXT("field ") + Key);
			if (Range == nullptr)
			{
				continue;
			}
			R.Near(Num(Field, TEXT("min")), Range->Min, 0.0, Key + TEXT(" min"));
			R.Near(Num(Field, TEXT("max")), Range->Max, 0.0, Key + TEXT(" max"));
			R.Expect(static_cast<int32>(Num(Field, TEXT("decimals"))) == Range->Decimals, Key + TEXT(" decimals"));
		}
		R.SameConfig(FGILJoystickConfig::FromObject(Sub(V, TEXT("defaults"))), FGILJoystickConfig(), TEXT("defaults"));
	}

	void Response(FRun& R, const FObj& V)
	{
		for (const TSharedPtr<FJsonValue>& Value : List(V, TEXT("curve")))
		{
			const FObj Item = Value->AsObject();
			const FGILJoystickConfig Config = FGILJoystickConfig::FromObject(Sub(Item, TEXT("config")));
			R.Near(Num(Item, TEXT("expected")), FGILJoystickResponse::ResponseCurve(Num(Item, TEXT("deflection")), Config), Tolerance,
				FString::Printf(TEXT("curve at %g"), Num(Item, TEXT("deflection"))));
		}
		for (const TSharedPtr<FJsonValue>& Value : List(V, TEXT("response")))
		{
			const FObj Item = Value->AsObject();
			const FGILJoystickConfig Config = FGILJoystickConfig::FromObject(Sub(Item, TEXT("config")));
			const FVector2D Actual = FGILJoystickResponse::Evaluate(FVector2D(Num(Item, TEXT("offsetX")), Num(Item, TEXT("offsetY"))), Config);
			const FString Label = FString::Printf(TEXT("response at (%g, %g)"), Num(Item, TEXT("offsetX")), Num(Item, TEXT("offsetY")));
			R.Near(Num(Item, TEXT("outputX")), Actual.X, Tolerance, Label + TEXT(" x"));
			R.Near(Num(Item, TEXT("outputY")), Actual.Y, Tolerance, Label + TEXT(" y"));
		}
	}

	void Smoothing(FRun& R, const FObj& V)
	{
		for (const TSharedPtr<FJsonValue>& Value : List(V, TEXT("smoothing")))
		{
			const FObj Item = Value->AsObject();
			const FVector2D Actual = FGILJoystickResponse::Smooth(
				FVector2D(Num(Item, TEXT("currentX")), Num(Item, TEXT("currentY"))), FVector2D(Num(Item, TEXT("targetX")), Num(Item, TEXT("targetY"))),
				Num(Item, TEXT("deltaMs")), Num(Item, TEXT("smoothingMs")));
			const FString Label = FString::Printf(TEXT("smooth %g ms of %g ms"), Num(Item, TEXT("deltaMs")), Num(Item, TEXT("smoothingMs")));
			R.Near(Num(Item, TEXT("outputX")), Actual.X, SmoothingTolerance, Label + TEXT(" x"));
			R.Near(Num(Item, TEXT("outputY")), Actual.Y, SmoothingTolerance, Label + TEXT(" y"));
		}
	}

	void JsonExport(FRun& R, const FObj& V)
	{
		const FObj Sample = SubPtr(V, TEXT("exportSample"));
		FGILJoystickConfig Parsed;
		const bool bParsed = FGILJoystickConfig::FromJson(Str(Sample, TEXT("json")), Parsed);
		R.Expect(bParsed, TEXT("export sample parses"));
		if (bParsed)
		{
			R.SameConfig(FGILJoystickConfig::FromObject(Sub(Sample, TEXT("config"))), Parsed, TEXT("export sample"));
		}
		// A config clamps to the app's limits and rounds like the app.
		FGILJoystickConfig Wild;
		Wild.Radius = 500.0;
		Wild.Curve = 1.26;
		Wild.Sensitivity = -4.0;
		const FGILJoystickConfig Clamped = Wild.Normalized();
		R.Near(96.0, Clamped.Radius, 0.0, TEXT("radius clamps"));
		R.Near(1.3, Clamped.Curve, 1e-9, TEXT("curve rounds to one decimal"));
		R.Near(0.5, Clamped.Sensitivity, 0.0, TEXT("sensitivity clamps"));
		FGILJoystickConfig Ignored;
		R.Expect(!FGILJoystickConfig::FromJson(TEXT("{\"format\":\"something/else\"}"), Ignored), TEXT("another format is refused"));
	}

	void TouchScenarios(FRun& R, const FObj& V)
	{
		const FList& Scenarios = List(V, TEXT("scenarios"));
		R.Expect(Scenarios.Num() > 0, TEXT("touch scenarios exist"));
		for (const TSharedPtr<FJsonValue>& ScenarioValue : Scenarios)
		{
			const FObj Scenario = ScenarioValue->AsObject();
			FGILStickTracker Tracker(FGILJoystickConfig::FromObject(Sub(Scenario, TEXT("config"))));
			const FObj Area = SubPtr(Scenario, TEXT("area"));
			Tracker.Area = FGILRect(Num(Area, TEXT("x")), Num(Area, TEXT("y")), Num(Area, TEXT("width")), Num(Area, TEXT("height")));
			Tracker.UnitsPerPoint = Num(Scenario, TEXT("unitsPerPoint"));
			int32 Index = 0;
			for (const TSharedPtr<FJsonValue>& FrameValue : List(Scenario, TEXT("frames")))
			{
				const FObj Frame = FrameValue->AsObject();
				const FString Label = FString::Printf(TEXT("\"%s\" frame %d"), *Str(Scenario, TEXT("name")), Index);
				if (Frame->GetBoolField(TEXT("interrupt")))
				{
					Tracker.Interrupt();
				}
				else
				{
					TArray<FGILTouch> Touches;
					for (const TSharedPtr<FJsonValue>& TouchValue : List(Frame, TEXT("touches")))
					{
						const FObj Item = TouchValue->AsObject();
						EGILTouchPhase Phase = EGILTouchPhase::Moved;
						R.Expect(GILParsePhase(Str(Item, TEXT("phase")), Phase), Label + TEXT(" phase"));
						Touches.Add(FGILTouch(static_cast<int32>(Num(Item, TEXT("id"))), FVector2D(Num(Item, TEXT("x")), Num(Item, TEXT("y"))), Phase));
					}
					Tracker.Step(Touches, Num(Frame, TEXT("deltaMs")));
				}
				R.Expect(static_cast<int32>(Num(Frame, TEXT("fingerId"))) == Tracker.FingerId, Label + TEXT(" finger"));
				R.Expect(Str(Frame, TEXT("released")) == GILReleaseName(Tracker.LastRelease), Label + TEXT(" release"));
				R.Near(Num(Frame, TEXT("outputX")), Tracker.Output.X, SmoothingTolerance, Label + TEXT(" output x"));
				R.Near(Num(Frame, TEXT("outputY")), Tracker.Output.Y, SmoothingTolerance, Label + TEXT(" output y"));
				++Index;
			}
		}
	}

	void Layout(FRun& R, const FObj& V)
	{
		for (const TSharedPtr<FJsonValue>& ItemValue : List(V, TEXT("layout")))
		{
			const FObj Item = ItemValue->AsObject();
			const FObj Screen = SubPtr(Item, TEXT("screen"));
			const FObj Insets = SubPtr(Item, TEXT("insets"));
			const FGILRect Safe = FGILSafeAreaLayout::SafeRect(Num(Screen, TEXT("width")), Num(Screen, TEXT("height")),
				Num(Insets, TEXT("top")), Num(Insets, TEXT("left")), Num(Insets, TEXT("right")), Num(Insets, TEXT("bottom")));
			const FObj Expected = SubPtr(Item, TEXT("safe"));
			const FString Name = Str(Item, TEXT("name"));
			R.Near(Num(Expected, TEXT("x")), Safe.Position.X, 1e-3, Name + TEXT(" safe x"));
			R.Near(Num(Expected, TEXT("y")), Safe.Position.Y, 1e-3, Name + TEXT(" safe y"));
			R.Near(Num(Expected, TEXT("width")), Safe.Size.X, 1e-3, Name + TEXT(" safe width"));
			R.Near(Num(Expected, TEXT("height")), Safe.Size.Y, 1e-3, Name + TEXT(" safe height"));
			for (const TSharedPtr<FJsonValue>& ControlValue : List(Item, TEXT("controls")))
			{
				const FObj Control = ControlValue->AsObject();
				const FString Label = Name + TEXT(" ") + Str(Control, TEXT("id"));
				const double Extent = Str(Control, TEXT("kind")) == TEXT("stick")
					? FGILSafeAreaLayout::StickRadius(Num(Control, TEXT("size")), Safe)
					: Num(Control, TEXT("size")) / 2.0;
				R.Near(Num(Control, TEXT("extent")), Extent, 1e-3, Label + TEXT(" extent"));
				const FVector2D Centre = FGILSafeAreaLayout::Center(Safe, FVector2D(Num(Control, TEXT("anchorX")), Num(Control, TEXT("anchorY"))), Extent);
				R.Near(Num(Control, TEXT("centerX")), Centre.X, 1e-3, Label + TEXT(" x"));
				R.Near(Num(Control, TEXT("centerY")), Centre.Y, 1e-3, Label + TEXT(" y"));
			}
		}

		const FObj Portrait = SubPtr(V, TEXT("portraitExport"));
		FGILLandscapeLayout PortraitLayout;
		R.Expect(FGILLandscapeLayout::FromJson(Str(Portrait, TEXT("json")), PortraitLayout) && PortraitLayout.Orientation == Str(Portrait, TEXT("orientation")),
			TEXT("portrait export is portrait"));
		FGILLandscapeLayout PlainLayout;
		R.Expect(FGILLandscapeLayout::FromJson(TEXT("{\"format\": \"game-input-lab/landscape-layout\", \"lab\": \"gamepad\"}"), PlainLayout)
			&& PlainLayout.Orientation == TEXT("landscape"), TEXT("a layout without orientation is landscape"));

		const FObj Export = SubPtr(V, TEXT("landscapeExport"));
		FGILLandscapeLayout Parsed;
		const bool bParsed = FGILLandscapeLayout::FromJson(Str(Export, TEXT("json")), Parsed);
		R.Expect(bParsed, TEXT("landscape export parses"));
		if (!bParsed)
		{
			return;
		}
		R.Expect(Parsed.Lab == TEXT("gamepad"), TEXT("landscape lab"));
		R.Expect(Parsed.Orientation == TEXT("landscape"), TEXT("landscape export is landscape"));
		R.Expect(Parsed.Controls.Num() == List(Export, TEXT("controls")).Num(), TEXT("landscape control count"));
		for (const TSharedPtr<FJsonValue>& ControlValue : List(Export, TEXT("controls")))
		{
			const FObj Expected = ControlValue->AsObject();
			const FString Id = Str(Expected, TEXT("id"));
			const FGILControlLayout* Control = Parsed.FindControl(Id);
			R.Expect(Control != nullptr, TEXT("control ") + Id);
			if (Control == nullptr)
			{
				continue;
			}
			double Value = 0.0;
			R.Near(Num(Expected, TEXT("anchorX")), Control->Anchor.X, 1e-9, Id + TEXT(" anchor x"));
			R.Near(Num(Expected, TEXT("anchorY")), Control->Anchor.Y, 1e-9, Id + TEXT(" anchor y"));
			R.Near(Expected->TryGetNumberField(TEXT("visual"), Value) ? Value : 0.0, Control->Visual, 1e-9, Id + TEXT(" visual"));
			R.Near(Expected->TryGetNumberField(TEXT("hit"), Value) ? Value : 0.0, Control->Hit, 1e-9, Id + TEXT(" hit"));
		}
		for (const TSharedPtr<FJsonValue>& StickValue : List(Export, TEXT("sticks")))
		{
			const FObj Expected = StickValue->AsObject();
			const FString Id = Str(Expected, TEXT("id"));
			const FGILJoystickConfig* Stick = Parsed.FindStick(Id);
			R.Expect(Stick != nullptr, TEXT("stick ") + Id);
			if (Stick != nullptr)
			{
				R.SameConfig(FGILJoystickConfig::FromObject(*Expected), *Stick, TEXT("stick ") + Id);
			}
		}
		R.Expect(Parsed.Aim.Release == EGILAimRelease::Hold && Parsed.Aim.FireThreshold == 40.0, TEXT("a layout without aim gets the defaults"));

		// Place: the y flip, the unit scale and the stick ring size.
		const FGILControlLayout* First = Parsed.Controls.Num() > 0 ? &Parsed.Controls[0] : nullptr;
		if (First != nullptr)
		{
			const FGILRect Safe(0.0, 0.0, 800.0, 360.0);
			const FVector2D CentreUp = Parsed.CenterOf(First->Id, Safe);
			const FGILPlacement Placed = Parsed.Place(First->Id, FVector2D(1600.0, 720.0), 2.0);
			R.Near(CentreUp.X * 2.0, Placed.Center.X, 1e-6, TEXT("place x"));
			R.Near(720.0 - CentreUp.Y * 2.0, Placed.Center.Y, 1e-6, TEXT("place y is flipped"));
		}
	}

	void Tilt(FRun& R, const FObj& V)
	{
		const FObj Block = SubPtr(V, TEXT("tilt"));
		const FList& Angles = List(Block, TEXT("angles"));
		R.Expect(Angles.Num() > 0, TEXT("tilt angle cases exist"));
		for (const TSharedPtr<FJsonValue>& Value : Angles)
		{
			const FObj Item = Value->AsObject();
			const FGILTiltAngles Actual = FGILTilt::Angles(FVector(Num(Item, TEXT("x")), Num(Item, TEXT("y")), Num(Item, TEXT("z"))));
			const FString Label = FString::Printf(TEXT("tilt angles (%g, %g, %g)"), Num(Item, TEXT("x")), Num(Item, TEXT("y")), Num(Item, TEXT("z")));
			R.Near(Num(Item, TEXT("roll")), Actual.Roll, 1e-6, Label + TEXT(" roll"));
			R.Near(Num(Item, TEXT("pitch")), Actual.Pitch, 1e-6, Label + TEXT(" pitch"));
		}

		const FList& Targets = List(Block, TEXT("target"));
		R.Expect(Targets.Num() > 0, TEXT("tilt target cases exist"));
		for (const TSharedPtr<FJsonValue>& Value : Targets)
		{
			const FObj Item = Value->AsObject();
			const FGILTiltConfig Config = FGILTiltConfig::FromObject(Sub(Item, TEXT("config")));
			FGILTiltAngles Pose;
			Pose.Roll = Num(Item, TEXT("roll"));
			Pose.Pitch = Num(Item, TEXT("pitch"));
			FGILTiltAngles Neutral;
			Neutral.Roll = Num(Item, TEXT("neutralRoll"));
			Neutral.Pitch = Num(Item, TEXT("neutralPitch"));
			const FVector2D Actual = FGILTilt::Target(Pose, Neutral, Config);
			const FString Label = FString::Printf(TEXT("tilt %s (%g, %g)"), *Str(Item->GetObjectField(TEXT("config")), TEXT("orientation")), Pose.Roll, Pose.Pitch);
			R.Near(Num(Item, TEXT("outputX")), Actual.X, Tolerance, Label + TEXT(" x"));
			R.Near(Num(Item, TEXT("outputY")), Actual.Y, Tolerance, Label + TEXT(" y"));
		}

		const FObj Export = SubPtr(Block, TEXT("export"));
		FGILTiltConfig Parsed;
		const bool bParsed = FGILTiltConfig::FromJson(Str(Export, TEXT("json")), Parsed);
		R.Expect(bParsed, TEXT("tilt export parses"));
		if (bParsed)
		{
			const FGILTiltConfig Expected = FGILTiltConfig::FromObject(Sub(Export, TEXT("config")));
			R.Expect(Parsed.Orientation == Expected.Orientation, TEXT("tilt orientation"));
			R.Expect(Parsed.bInvertX == Expected.bInvertX && Parsed.bInvertY == Expected.bInvertY, TEXT("tilt invert flags"));
			R.Near(Expected.MaxAngle, Parsed.MaxAngle, 1e-9, TEXT("tilt max angle"));
			R.Near(Expected.DeadZone, Parsed.DeadZone, 1e-9, TEXT("tilt dead zone"));
			R.Near(Expected.OuterDeadZone, Parsed.OuterDeadZone, 1e-9, TEXT("tilt outer dead zone"));
			R.Near(Expected.Curve, Parsed.Curve, 1e-9, TEXT("tilt curve"));
			R.Near(Expected.Sensitivity, Parsed.Sensitivity, 1e-9, TEXT("tilt sensitivity"));
			R.Near(Expected.Smoothing, Parsed.Smoothing, 1e-9, TEXT("tilt smoothing"));
		}
		FGILTiltConfig Wild;
		Wild.MaxAngle = 500.0;
		Wild.Curve = 1.26;
		const FGILTiltConfig Clamped = Wild.Normalized();
		R.Near(60.0, Clamped.MaxAngle, 0.0, TEXT("tilt max angle clamps"));
		R.Near(1.3, Clamped.Curve, 1e-9, TEXT("tilt curve rounds to one decimal"));

		// A stick calibrated flat gives half power at half the range.
		FGILTiltConfig Plain;
		Plain.Smoothing = 0.0;
		Plain.DeadZone = 0.0;
		FGILTiltStick Stick(Plain);
		Stick.Calibrate(FVector(0.0, 0.0, -1.0));
		const double Quarter = 15.0 * GILPi / 180.0;
		const FVector2D Half = Stick.Step(FVector(FMath::Sin(Quarter), 0.0, -FMath::Cos(Quarter)), 16.0);
		R.Near(0.5, Half.X, 1e-9, TEXT("tilt stick half power"));
		R.Near(0.0, Half.Y, 1e-9, TEXT("tilt stick no sideways drift"));
	}

	FGILHapticPattern HapticPattern(const FList& Steps, bool bLoop = false)
	{
		FGILHapticPattern Pattern;
		Pattern.bLoop = bLoop;
		for (const TSharedPtr<FJsonValue>& Value : Steps)
		{
			const FObj Item = Value->AsObject();
			FGILHapticStep Step;
			Step.DurationMs = static_cast<int32>(Num(Item, TEXT("durationMs")));
			Step.Intensity = static_cast<int32>(Num(Item, TEXT("intensity")));
			Pattern.Steps.Add(Step);
		}
		return Pattern;
	}

	bool SameInts(const FList& Expected, const TArray<int32>& Actual)
	{
		if (Expected.Num() != Actual.Num())
		{
			return false;
		}
		for (int32 Index = 0; Index < Actual.Num(); ++Index)
		{
			if (static_cast<int32>(Expected[Index]->AsNumber()) != Actual[Index])
			{
				return false;
			}
		}
		return true;
	}

	void Haptics(FRun& R, const FObj& V)
	{
		const FObj Block = SubPtr(V, TEXT("haptics"));
		R.Expect(static_cast<int32>(Num(Block, TEXT("maxSteps"))) == FGILHapticPattern::MaxSteps, TEXT("haptic max steps"));
		R.Expect(static_cast<int32>(Num(Block, TEXT("maxTotalMs"))) == FGILHapticPattern::MaxTotalMs, TEXT("haptic max total"));
		R.Expect(static_cast<int32>(Num(Block, TEXT("transientMs"))) == FGILHapticPattern::TransientMs, TEXT("haptic transient length"));
		R.Near(Num(Block, TEXT("sharpness")), FGILHapticPattern::IosSharpness, 1e-9, TEXT("haptic sharpness"));

		const FList& Patterns = List(Block, TEXT("patterns"));
		R.Expect(Patterns.Num() > 0, TEXT("haptic pattern cases exist"));
		for (const TSharedPtr<FJsonValue>& Value : Patterns)
		{
			const FObj Item = Value->AsObject();
			const FString Id = Str(Item, TEXT("id"));
			const FGILHapticPattern Pattern = HapticPattern(List(Item, TEXT("steps")), Item->GetBoolField(TEXT("loop")));
			R.Expect(Pattern.bLoop == Item->GetBoolField(TEXT("loop")), Id + TEXT(" loop"));
			R.Expect(Pattern.AndroidRepeat() == static_cast<int32>(Num(Item, TEXT("androidRepeat"))), Id + TEXT(" android repeat"));
			R.Expect(Pattern.DurationMs() == static_cast<int32>(Num(Item, TEXT("duration"))), Id + TEXT(" duration"));
			for (const TSharedPtr<FJsonValue>& SampleValue : List(Item, TEXT("samples")))
			{
				const FObj Sample = SampleValue->AsObject();
				R.Near(Num(Sample, TEXT("intensity")), Pattern.IntensityAt(Num(Sample, TEXT("timeMs"))), 1e-9,
					FString::Printf(TEXT("%s at %g ms"), *Id, Num(Sample, TEXT("timeMs"))));
			}
			const FObj Android = SubPtr(Item, TEXT("android"));
			R.Expect(SameInts(List(Android, TEXT("timings")), Pattern.AndroidTimings()), Id + TEXT(" android timings"));
			R.Expect(SameInts(List(Android, TEXT("amplitudes")), Pattern.AndroidAmplitudes()), Id + TEXT(" android amplitudes"));
			R.Expect(SameInts(List(Item, TEXT("legacy")), Pattern.LegacyTimings()), Id + TEXT(" legacy timings"));

			const FList& Expected = List(Item, TEXT("ios"));
			const TArray<FGILHapticEvent> Events = Pattern.IosEvents();
			R.Expect(Expected.Num() == Events.Num(), Id + TEXT(" ios event count"));
			for (int32 Index = 0; Index < Events.Num() && Index < Expected.Num(); ++Index)
			{
				const FObj Want = Expected[Index]->AsObject();
				const FString Label = FString::Printf(TEXT("%s event %d"), *Id, Index);
				R.Expect((Str(Want, TEXT("kind")) == TEXT("transient")) == Events[Index].bTransient, Label + TEXT(" kind"));
				R.Near(Num(Want, TEXT("time")), Events[Index].Time, 1e-9, Label + TEXT(" time"));
				R.Near(Num(Want, TEXT("duration")), Events[Index].Duration, 1e-9, Label + TEXT(" duration"));
				R.Near(Num(Want, TEXT("intensity")), Events[Index].Intensity, 1e-9, Label + TEXT(" intensity"));
				R.Near(Num(Want, TEXT("sharpness")), Events[Index].Sharpness, 1e-9, Label + TEXT(" sharpness"));
			}
		}

		for (const TSharedPtr<FJsonValue>& Value : List(Block, TEXT("normalize")))
		{
			const FObj Item = Value->AsObject();
			const FObj Input = SubPtr(Item, TEXT("input"));
			bool bInputLoop = false;
			Input->TryGetBoolField(TEXT("loop"), bInputLoop);
			const FGILHapticPattern Actual = HapticPattern(List(Input, TEXT("steps")), bInputLoop).Normalized();
			R.Expect(Actual.bLoop == Item->GetBoolField(TEXT("loop")), TEXT("haptic normalize loop"));
			const FList& Expected = List(Item, TEXT("steps"));
			R.Expect(Actual.Steps.Num() == Expected.Num(), TEXT("haptic normalize step count"));
			for (int32 Index = 0; Index < Actual.Steps.Num() && Index < Expected.Num(); ++Index)
			{
				const FObj Want = Expected[Index]->AsObject();
				R.Expect(Actual.Steps[Index].DurationMs == static_cast<int32>(Num(Want, TEXT("durationMs")))
					&& Actual.Steps[Index].Intensity == static_cast<int32>(Num(Want, TEXT("intensity"))),
					FString::Printf(TEXT("haptic normalize step %d"), Index));
			}
		}

		FGILHapticPattern Parsed;
		const bool bParsed = FGILHapticPattern::FromJson(TEXT("{\"format\":\"game-input-lab/haptic-pattern\",\"steps\":[{\"durationMs\":33,\"intensity\":47},{\"durationMs\":80,\"intensity\":0}]}"), Parsed);
		R.Expect(bParsed && Parsed.Steps.Num() == 2 && Parsed.Steps[0].DurationMs == 30 && Parsed.Steps[0].Intensity == 45 && Parsed.Steps[1].DurationMs == 80,
			TEXT("haptic JSON reads back, normalized"));
		R.Expect(bParsed && !Parsed.bLoop, TEXT("haptic JSON without loop does not loop"));
		FGILHapticPattern Looped;
		R.Expect(FGILHapticPattern::FromJson(TEXT("{\"loop\":true,\"steps\":[{\"durationMs\":30,\"intensity\":100}]}"), Looped) && Looped.bLoop && Looped.AndroidRepeat() == 0,
			TEXT("haptic JSON loop reads back"));
		FGILHapticPattern Other;
		R.Expect(!FGILHapticPattern::FromJson(TEXT("{\"format\":\"other\",\"steps\":[]}"), Other), TEXT("haptic JSON of another format is refused"));
		const TArray<uint8> Samples = FGILHapticPattern::FromSteps({ { 30, 100 }, { 20, 0 } }).Amplitudes(100);
		R.Expect(Samples.Num() == 5 && Samples[0] == 255 && Samples[2] == 255 && Samples[3] == 0, TEXT("haptic amplitudes sample the pattern"));
	}

	void Aim(FRun& R, const FObj& V)
	{
		const FList& Cases = List(V, TEXT("aimBehavior"));
		R.Expect(Cases.Num() > 0, TEXT("aim cases exist"));
		for (const TSharedPtr<FJsonValue>& Value : Cases)
		{
			const FObj Item = Value->AsObject();
			const FGILAimConfig Config = FGILAimConfig::FromObject(*Item);
			const FGILAimStep Step = FGILAimBehavior::Step(Num(Item, TEXT("heading")), FVector2D(Num(Item, TEXT("moveX")), Num(Item, TEXT("moveY"))),
				FVector2D(Num(Item, TEXT("aimX")), Num(Item, TEXT("aimY"))), Config);
			const FString Label = Str(Item, TEXT("name"));
			R.Near(Num(Item, TEXT("expectedHeading")), Step.Heading, Tolerance, Label + TEXT(" heading"));
			R.Expect(Item->GetBoolField(TEXT("expectedAiming")) == Step.bAiming, Label + TEXT(" aiming"));
			R.Expect(Item->GetBoolField(TEXT("expectedFiring")) == Step.bFiring, Label + TEXT(" firing"));
			R.Near(Num(Item, TEXT("expectedMoveScale")), Step.MoveScale, Tolerance, Label + TEXT(" move scale"));
		}

		const FObj Dual = SubPtr(V, TEXT("dualStickExport"));
		FGILLandscapeLayout Layout;
		const bool bParsed = FGILLandscapeLayout::FromJson(Str(Dual, TEXT("json")), Layout);
		R.Expect(bParsed && Layout.Lab == TEXT("dualStick"), TEXT("dual stick export parses"));
		if (bParsed)
		{
			const FObj Aim = SubPtr(Dual, TEXT("aim"));
			R.Expect((Str(Aim, TEXT("release")) == TEXT("forward")) == (Layout.Aim.Release == EGILAimRelease::Forward), TEXT("dual stick release"));
			R.Near(Num(Aim, TEXT("fireThreshold")), Layout.Aim.FireThreshold, 1e-9, TEXT("dual stick fire threshold"));
			R.Near(Num(Aim, TEXT("slowWhileAiming")), Layout.Aim.SlowWhileAiming, 1e-9, TEXT("dual stick slow"));
		}
		TSharedPtr<FJsonObject> Wild;
		if (GILJson::ParseObject(TEXT("{\"release\":\"sideways\",\"fireThreshold\":5,\"slowWhileAiming\":99}"), Wild))
		{
			const FGILAimConfig Clamped = FGILAimConfig::FromObject(*Wild).Normalized();
			R.Expect(Clamped.Release == EGILAimRelease::Hold && Clamped.FireThreshold == 20.0 && Clamped.SlowWhileAiming == 80.0, TEXT("aim config clamps"));
		}
	}

	void Scene(FRun& R, const FObj& V)
	{
		const FList& Scenarios = List(V, TEXT("sceneMotion"));
		R.Expect(Scenarios.Num() > 0, TEXT("scene scenarios exist"));
		for (const TSharedPtr<FJsonValue>& ScenarioValue : Scenarios)
		{
			const FObj Scenario = ScenarioValue->AsObject();
			const FGILSceneParams Params = FGILSceneParams::FromObject(Sub(Scenario, TEXT("parameters")));
			FGILSceneState State = FGILSceneMotion::Start();
			int32 Index = 0;
			for (const TSharedPtr<FJsonValue>& FrameValue : List(Scenario, TEXT("frames")))
			{
				const FObj Frame = FrameValue->AsObject();
				State = FGILSceneMotion::Step(State, FVector2D(Num(Frame, TEXT("outputX")), Num(Frame, TEXT("outputY"))), Num(Frame, TEXT("deltaMs")), Params);
				const FString Label = FString::Printf(TEXT("%s frame %d"), *Str(Scenario, TEXT("name")), Index);
				R.Near(Num(Frame, TEXT("posX")), State.Position.X, SceneTolerance, Label + TEXT(" position x"));
				R.Near(Num(Frame, TEXT("posY")), State.Position.Y, SceneTolerance, Label + TEXT(" position y"));
				R.Near(Num(Frame, TEXT("velX")), State.Velocity.X, SceneTolerance, Label + TEXT(" velocity x"));
				R.Near(Num(Frame, TEXT("velY")), State.Velocity.Y, SceneTolerance, Label + TEXT(" velocity y"));
				R.Near(Num(Frame, TEXT("heading")), State.Heading, SceneTolerance, Label + TEXT(" heading"));
				R.Near(Num(Frame, TEXT("camX")), State.Camera.X, SceneTolerance, Label + TEXT(" camera x"));
				R.Near(Num(Frame, TEXT("camY")), State.Camera.Y, SceneTolerance, Label + TEXT(" camera y"));
				++Index;
			}
		}

		const FObj Export = SubPtr(V, TEXT("sceneExport"));
		FGILSceneParams Parsed;
		const bool bParsed = FGILSceneParams::FromJson(Str(Export, TEXT("json")), Parsed);
		R.Expect(bParsed, TEXT("scene export parses"));
		if (bParsed)
		{
			const FObj Scene = SubPtr(Export, TEXT("scene"));
			R.Near(Num(Scene, TEXT("maxSpeed")), Parsed.MaxSpeed, 1e-9, TEXT("scene max speed"));
			R.Near(Num(Scene, TEXT("acceleration")), Parsed.Acceleration, 1e-9, TEXT("scene acceleration"));
			R.Near(Num(Scene, TEXT("turnRate")), Parsed.TurnRate, 1e-9, TEXT("scene turn rate"));
			R.Near(Num(Scene, TEXT("cameraLag")), Parsed.CameraLag, 1e-9, TEXT("scene camera lag"));
		}
		FGILSceneParams Defaults;
		R.Expect(FGILSceneParams::FromJson(TEXT("{\"format\":\"game-input-lab/joystick\",\"config\":{}}"), Defaults), TEXT("a file without a scene parses"));
		R.Near(4.0, Defaults.MaxSpeed, 0.0, TEXT("a file without a scene gives the defaults"));
	}
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FGILVectorTest, "GameInputLab.Vectors", EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)

bool FGILVectorTest::RunTest(const FString& Parameters)
{
	const TSharedPtr<IPlugin> Plugin = IPluginManager::Get().FindPlugin(TEXT("GameInputLab"));
	if (!Plugin.IsValid())
	{
		AddError(TEXT("The GameInputLab plugin is not loaded."));
		return false;
	}
	const FString Path = FPaths::Combine(Plugin->GetBaseDir(), TEXT("Resources/Tests/joystick-vectors.json"));
	FString Text;
	if (!FFileHelper::LoadFileToString(Text, *Path))
	{
		AddError(FString::Printf(TEXT("Cannot read %s. Run `npm run vectors` in the app repo."), *Path));
		return false;
	}
	TSharedPtr<FJsonObject> V;
	if (!GILJson::ParseObject(Text, V))
	{
		AddError(TEXT("The vector file is not valid JSON."));
		return false;
	}

	FRun R(*this);
	R.Expect(static_cast<int32>(V->GetNumberField(TEXT("version"))) == 1, TEXT("vector file version"));
	FieldsAndDefaults(R, V);
	Response(R, V);
	Smoothing(R, V);
	JsonExport(R, V);
	TouchScenarios(R, V);
	Layout(R, V);
	Aim(R, V);
	Scene(R, V);
	Tilt(R, V);
	Haptics(R, V);
	AddInfo(FString::Printf(TEXT("%d checks, %d failed"), R.Checks, R.Failures));
	return R.Failures == 0;
}

#endif // WITH_DEV_AUTOMATION_TESTS
