#include "GILHaptics.h"
#include "GILLog.h"

namespace
{
	constexpr int32 MinStepMs = 10;
	constexpr int32 MaxStepMs = 1000;
	constexpr int32 StepMs = 10;
	constexpr int32 IntensityStep = 5;

	/** Round half up to a multiple of Step, then clamp. */
	int32 Snap(int32 Value, int32 Step, int32 Low, int32 High)
	{
		const int32 Rounded = FMath::FloorToInt(static_cast<double>(Value) / static_cast<double>(Step) + 0.5);
		return FMath::Clamp(Rounded * Step, Low, High);
	}
}

FGILHapticPattern FGILHapticPattern::FromSteps(const TArray<FGILHapticStep>& InSteps)
{
	FGILHapticPattern Result;
	Result.Steps = InSteps;
	return Result;
}

FGILHapticPattern FGILHapticPattern::Normalized() const
{
	FGILHapticPattern Result;
	int32 Total = 0;
	for (int32 Index = 0; Index < Steps.Num() && Index < MaxSteps; ++Index)
	{
		const int32 Room = MaxTotalMs - Total;
		if (Room < MinStepMs)
		{
			break;
		}
		int32 Duration = Snap(Steps[Index].DurationMs, StepMs, MinStepMs, MaxStepMs);
		Duration = FMath::Min(Duration, Room - Room % StepMs);
		FGILHapticStep Item;
		Item.DurationMs = Duration;
		Item.Intensity = Snap(Steps[Index].Intensity, IntensityStep, 0, 100);
		Result.Steps.Add(Item);
		Total += Duration;
	}
	if (Result.Steps.Num() == 0)
	{
		FGILHapticStep Tap;
		Tap.DurationMs = 30;
		Tap.Intensity = 100;
		Result.Steps.Add(Tap);
	}
	return Result;
}

int32 FGILHapticPattern::DurationMs() const
{
	int32 Total = 0;
	for (const FGILHapticStep& Item : Steps)
	{
		Total += Item.DurationMs;
	}
	return Total;
}

double FGILHapticPattern::IntensityAt(double TimeMs) const
{
	if (TimeMs < 0.0)
	{
		return 0.0;
	}
	double Start = 0.0;
	for (const FGILHapticStep& Item : Steps)
	{
		if (TimeMs < Start + static_cast<double>(Item.DurationMs))
		{
			return static_cast<double>(Item.Intensity) / 100.0;
		}
		Start += static_cast<double>(Item.DurationMs);
	}
	return 0.0;
}

TArray<int32> FGILHapticPattern::AndroidTimings() const
{
	TArray<int32> Timings;
	for (const FGILHapticStep& Item : Steps)
	{
		Timings.Add(Item.DurationMs);
	}
	return Timings;
}

TArray<int32> FGILHapticPattern::AndroidAmplitudes() const
{
	TArray<int32> Amplitudes;
	for (const FGILHapticStep& Item : Steps)
	{
		Amplitudes.Add(Item.Intensity <= 0 ? 0 : FMath::Clamp((Item.Intensity * 255 + 50) / 100, 1, 255));
	}
	return Amplitudes;
}

TArray<int32> FGILHapticPattern::LegacyTimings() const
{
	TArray<int32> Timings;
	bool bOn = false;
	for (const FGILHapticStep& Item : Steps)
	{
		const bool bStepOn = Item.Intensity > 0;
		if (Timings.Num() == 0 && bStepOn)
		{
			Timings.Add(0);
		}
		if (Timings.Num() > 0 && bStepOn == bOn)
		{
			Timings[Timings.Num() - 1] += Item.DurationMs;
		}
		else
		{
			Timings.Add(Item.DurationMs);
		}
		bOn = bStepOn;
	}
	return Timings;
}

TArray<FGILHapticEvent> FGILHapticPattern::IosEvents() const
{
	TArray<FGILHapticEvent> Events;
	int32 Start = 0;
	for (const FGILHapticStep& Item : Steps)
	{
		if (Item.Intensity > 0)
		{
			FGILHapticEvent Event;
			Event.bTransient = Item.DurationMs <= TransientMs;
			Event.Time = static_cast<double>(Start) / 1000.0;
			Event.Duration = Event.bTransient ? 0.0 : static_cast<double>(Item.DurationMs) / 1000.0;
			Event.Intensity = static_cast<double>(Item.Intensity) / 100.0;
			Event.Sharpness = IosSharpness;
			Events.Add(Event);
		}
		Start += Item.DurationMs;
	}
	return Events;
}

TArray<uint8> FGILHapticPattern::Amplitudes(int32 SampleRate) const
{
	TArray<uint8> Result;
	if (SampleRate <= 0)
	{
		return Result;
	}
	const int32 Count = (DurationMs() * SampleRate + 999) / 1000;
	for (int32 Index = 0; Index < Count; ++Index)
	{
		const double TimeMs = static_cast<double>(Index) * 1000.0 / static_cast<double>(SampleRate);
		Result.Add(static_cast<uint8>(FMath::Clamp(FMath::RoundToInt(IntensityAt(TimeMs) * 255.0), 0, 255)));
	}
	return Result;
}

bool FGILHapticPattern::FromJson(const FString& Text, FGILHapticPattern& OutPattern)
{
	TSharedPtr<FJsonObject> Root;
	if (!GILJson::ParseObject(Text, Root))
	{
		return false;
	}
	if (Root->HasField(TEXT("format")))
	{
		const FString Format = GILJson::String(*Root, TEXT("format"));
		if (Format != TEXT("game-input-lab/haptic-pattern"))
		{
			UE_LOG(LogGameInputLab, Error, TEXT("Game Input Lab: unsupported format \"%s\". Expected \"game-input-lab/haptic-pattern\"."), *Format);
			return false;
		}
	}
	FGILHapticPattern Parsed;
	const TArray<TSharedPtr<FJsonValue>>* List = nullptr;
	if (Root->TryGetArrayField(TEXT("steps"), List) && List != nullptr)
	{
		for (const TSharedPtr<FJsonValue>& Value : *List)
		{
			const TSharedPtr<FJsonObject> Item = Value.IsValid() ? Value->AsObject() : nullptr;
			if (Item.IsValid())
			{
				FGILHapticStep Step;
				Step.DurationMs = static_cast<int32>(GILJson::Number(*Item, TEXT("durationMs"), 0.0));
				Step.Intensity = static_cast<int32>(GILJson::Number(*Item, TEXT("intensity"), 0.0));
				Parsed.Steps.Add(Step);
			}
		}
	}
	OutPattern = Parsed.Normalized();
	return true;
}
