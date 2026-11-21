#include "GILTilt.h"
#include "GILJoystickResponse.h"
#include "GILTypes.h"
#include "GILLog.h"

namespace
{
	double Field(double Value, double Fallback, double Low, double High, int32 Decimals)
	{
		if (!FMath::IsFinite(Value))
		{
			Value = Fallback;
		}
		const double Scale = FMath::Pow(10.0, static_cast<double>(Decimals));
		return FMath::RoundToDouble(FMath::Clamp(Value, Low, High) * Scale) / Scale;
	}

	EGILTiltOrientation ParseOrientation(const FString& Name)
	{
		if (Name == TEXT("topLeft")) { return EGILTiltOrientation::TopLeft; }
		if (Name == TEXT("topRight")) { return EGILTiltOrientation::TopRight; }
		return EGILTiltOrientation::Portrait;
	}

	bool ReadBool(const FJsonObject& Data, const TCHAR* Key)
	{
		bool Value = false;
		return Data.TryGetBoolField(Key, Value) && Value;
	}

	constexpr double RadToDeg = 180.0 / GILPi;
}

FGILTiltConfig FGILTiltConfig::Normalized() const
{
	const FGILTiltConfig Defaults;
	FGILTiltConfig Result;
	Result.Orientation = Orientation;
	Result.bInvertX = bInvertX;
	Result.bInvertY = bInvertY;
	Result.MaxAngle = Field(MaxAngle, Defaults.MaxAngle, 10.0, 60.0, 0);
	Result.DeadZone = Field(DeadZone, Defaults.DeadZone, 0.0, 50.0, 0);
	Result.OuterDeadZone = Field(OuterDeadZone, Defaults.OuterDeadZone, 0.0, 30.0, 0);
	Result.Curve = Field(Curve, Defaults.Curve, 0.5, 3.0, 1);
	Result.Sensitivity = Field(Sensitivity, Defaults.Sensitivity, 0.5, 2.0, 2);
	Result.Smoothing = Field(Smoothing, Defaults.Smoothing, 0.0, 300.0, 0);
	return Result;
}

FGILJoystickConfig FGILTiltConfig::AsJoystick() const
{
	FGILJoystickConfig Result;
	Result.Radius = MaxAngle;
	Result.DeadZone = DeadZone;
	Result.OuterDeadZone = OuterDeadZone;
	Result.Curve = Curve;
	Result.Sensitivity = Sensitivity;
	Result.Smoothing = Smoothing;
	return Result;
}

FGILTiltConfig FGILTiltConfig::FromObject(const FJsonObject& Data)
{
	FGILTiltConfig Result;
	Result.Orientation = ParseOrientation(GILJson::String(Data, TEXT("orientation"), TEXT("portrait")));
	Result.bInvertX = ReadBool(Data, TEXT("invertX"));
	Result.bInvertY = ReadBool(Data, TEXT("invertY"));
	Result.MaxAngle = GILJson::Number(Data, TEXT("maxAngle"), Result.MaxAngle);
	Result.DeadZone = GILJson::Number(Data, TEXT("deadZone"), Result.DeadZone);
	Result.OuterDeadZone = GILJson::Number(Data, TEXT("outerDeadZone"), Result.OuterDeadZone);
	Result.Curve = GILJson::Number(Data, TEXT("curve"), Result.Curve);
	Result.Sensitivity = GILJson::Number(Data, TEXT("sensitivity"), Result.Sensitivity);
	Result.Smoothing = GILJson::Number(Data, TEXT("smoothing"), Result.Smoothing);
	return Result;
}

bool FGILTiltConfig::FromJson(const FString& Text, FGILTiltConfig& OutConfig)
{
	TSharedPtr<FJsonObject> Root;
	if (!GILJson::ParseObject(Text, Root))
	{
		return false;
	}
	const FJsonObject* Data = Root.Get();
	if (Root->HasField(TEXT("format")))
	{
		const FString Format = GILJson::String(*Root, TEXT("format"));
		if (Format != TEXT("game-input-lab/tilt"))
		{
			UE_LOG(LogGameInputLab, Error, TEXT("Game Input Lab: unsupported format \"%s\". Expected \"game-input-lab/tilt\"."), *Format);
			return false;
		}
		Data = &GILJson::Object(*Root, TEXT("config"));
	}
	OutConfig = FromObject(*Data).Normalized();
	return true;
}

FGILTiltAngles FGILTilt::Angles(const FVector& Gravity)
{
	FGILTiltAngles Result;
	Result.Roll = FMath::Atan2(Gravity.X, FMath::Sqrt(Gravity.Y * Gravity.Y + Gravity.Z * Gravity.Z)) * RadToDeg;
	Result.Pitch = FMath::Atan2(Gravity.Y, FMath::Sqrt(Gravity.X * Gravity.X + Gravity.Z * Gravity.Z)) * RadToDeg;
	return Result;
}

FVector2D FGILTilt::Offset(const FGILTiltAngles& Angles, const FGILTiltAngles& Neutral, const FGILTiltConfig& Config)
{
	const double Roll = Angles.Roll - Neutral.Roll;
	const double Pitch = Angles.Pitch - Neutral.Pitch;
	FVector2D Screen(Roll, Pitch);
	if (Config.Orientation == EGILTiltOrientation::TopLeft)
	{
		Screen = FVector2D(-Pitch, Roll);
	}
	else if (Config.Orientation == EGILTiltOrientation::TopRight)
	{
		Screen = FVector2D(Pitch, -Roll);
	}
	return FVector2D(Config.bInvertX ? -Screen.X : Screen.X, Config.bInvertY ? -Screen.Y : Screen.Y);
}

FVector2D FGILTilt::Target(const FGILTiltAngles& Angles, const FGILTiltAngles& Neutral, const FGILTiltConfig& Config)
{
	return FGILJoystickResponse::Evaluate(Offset(Angles, Neutral, Config), Config.AsJoystick());
}

void FGILTiltStick::Calibrate(const FVector& Gravity)
{
	Neutral = FGILTilt::Angles(Gravity);
}

FVector2D FGILTiltStick::Step(const FVector& Gravity, double DeltaMs)
{
	const FVector2D Target = FGILTilt::Target(FGILTilt::Angles(Gravity), Neutral, Config);
	Output = FGILJoystickResponse::Smooth(Output, Target, DeltaMs, Config.Smoothing);
	return Output;
}
