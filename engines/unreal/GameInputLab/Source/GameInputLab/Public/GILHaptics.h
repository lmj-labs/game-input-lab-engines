#pragma once

#include "CoreMinimal.h"

/** One step of a vibration pattern: how long, and how strong (0-100, 0 is a pause). */
struct GAMEINPUTLAB_API FGILHapticStep
{
	int32 DurationMs = 0;
	int32 Intensity = 0;
};

/** A CoreHaptics event. Times and durations are in seconds, intensity and sharpness 0-1. */
struct GAMEINPUTLAB_API FGILHapticEvent
{
	bool bTransient = false;
	double Time = 0.0;
	double Duration = 0.0;
	double Intensity = 0.0;
	double Sharpness = 0.0;
};

/**
 * A custom vibration pattern from the app's Haptics Lab. Mirrors src/haptics/pattern.ts: the strength at a time, the
 * Android waveform, the timings for phones that cannot set the strength, and the iOS events. It only computes; playing
 * it is up to the platform (see the plugin README).
 */
struct GAMEINPUTLAB_API FGILHapticPattern
{
	static constexpr int32 MaxSteps = 16;
	static constexpr int32 MaxTotalMs = 5000;
	/** Steps this short or shorter are played as a single tap (iOS transient event). */
	static constexpr int32 TransientMs = 30;
	static constexpr double IosSharpness = 0.5;

	TArray<FGILHapticStep> Steps;

	static FGILHapticPattern FromSteps(const TArray<FGILHapticStep>& InSteps);

	/** A copy within the app's limits: 1 to 16 steps, 10-1000 ms in tens, 0-100 in fives, 5 s in all. Never empty. */
	FGILHapticPattern Normalized() const;

	int32 DurationMs() const;

	/** Strength from 0 to 1 at TimeMs after the start. 0 before the start and from the end on. */
	double IntensityAt(double TimeMs) const;

	/** Android 8+ VibrationEffect.createWaveform(timings, amplitudes, -1). Amplitude 0 is off, 1-255 is the strength. */
	TArray<int32> AndroidTimings() const;
	TArray<int32> AndroidAmplitudes() const;

	/** For phones that cannot set the strength: off, on, off, on, starting with off. Any strength above 0 is on. */
	TArray<int32> LegacyTimings() const;

	/** CoreHaptics events. Pauses become gaps between events. */
	TArray<FGILHapticEvent> IosEvents() const;

	/** The pattern sampled as 0-255 amplitudes, SampleRate samples per second, for UHapticFeedbackEffect_Buffer. */
	TArray<uint8> Amplitudes(int32 SampleRate) const;

	/** Read a JSON export from the app, or a bare object with a steps list. The result is normalized. Returns false (and logs an error) for invalid JSON or another format. */
	static bool FromJson(const FString& Text, FGILHapticPattern& OutPattern);
};
