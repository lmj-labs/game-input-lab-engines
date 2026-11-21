#include "GILJoystickConfig.h"
#include "GILLog.h"

namespace
{
	const FGILFieldRange GFields[] = {
		{ TEXT("radius"), 32.0, 96.0, 0 },
		{ TEXT("deadZone"), 0.0, 50.0, 0 },
		{ TEXT("outerDeadZone"), 0.0, 30.0, 0 },
		{ TEXT("curve"), 0.5, 3.0, 1 },
		{ TEXT("sensitivity"), 0.5, 2.0, 2 },
		{ TEXT("smoothing"), 0.0, 300.0, 0 },
	};

	/** Clamp to the field's limits and round to its decimals, like the app. A value that is not a number becomes the fallback. */
	double ClampField(int32 Index, double Value, double Fallback)
	{
		if (!FMath::IsFinite(Value))
		{
			Value = Fallback;
		}
		const FGILFieldRange& Range = GFields[Index];
		Value = FMath::Clamp(Value, Range.Min, Range.Max);
		const double Scale = FMath::Pow(10.0, static_cast<double>(Range.Decimals));
		return FMath::RoundToDouble(Value * Scale) / Scale;
	}
}

TConstArrayView<FGILFieldRange> FGILJoystickConfig::Ranges()
{
	return MakeArrayView(GFields);
}

FGILJoystickConfig FGILJoystickConfig::Normalized() const
{
	const FGILJoystickConfig Defaults;
	FGILJoystickConfig Result;
	Result.bFloating = bFloating;
	Result.Radius = ClampField(0, Radius, Defaults.Radius);
	Result.DeadZone = ClampField(1, DeadZone, Defaults.DeadZone);
	Result.OuterDeadZone = ClampField(2, OuterDeadZone, Defaults.OuterDeadZone);
	Result.Curve = ClampField(3, Curve, Defaults.Curve);
	Result.Sensitivity = ClampField(4, Sensitivity, Defaults.Sensitivity);
	Result.Smoothing = ClampField(5, Smoothing, Defaults.Smoothing);
	return Result;
}

FGILJoystickConfig FGILJoystickConfig::FromObject(const FJsonObject& Data)
{
	FGILJoystickConfig Result;
	Result.bFloating = GILJson::String(Data, TEXT("mode"), TEXT("fixed")) == TEXT("floating");
	Result.Radius = GILJson::Number(Data, TEXT("radius"), Result.Radius);
	Result.DeadZone = GILJson::Number(Data, TEXT("deadZone"), Result.DeadZone);
	Result.OuterDeadZone = GILJson::Number(Data, TEXT("outerDeadZone"), Result.OuterDeadZone);
	Result.Curve = GILJson::Number(Data, TEXT("curve"), Result.Curve);
	Result.Sensitivity = GILJson::Number(Data, TEXT("sensitivity"), Result.Sensitivity);
	Result.Smoothing = GILJson::Number(Data, TEXT("smoothing"), Result.Smoothing);
	return Result;
}

bool FGILJoystickConfig::FromJson(const FString& Text, FGILJoystickConfig& OutConfig)
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
		if (Format != TEXT("game-input-lab/joystick"))
		{
			UE_LOG(LogGameInputLab, Error, TEXT("Game Input Lab: unsupported format \"%s\". Expected \"game-input-lab/joystick\"."), *Format);
			return false;
		}
		Data = &GILJson::Object(*Root, TEXT("config"));
	}
	OutConfig = FromObject(*Data).Normalized();
	return true;
}
