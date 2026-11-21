#pragma once

#include "CoreMinimal.h"
#include "GILJoystickConfig.h"
#include "GILJoystickResponse.h"
#include "GILTypes.h"

DECLARE_MULTICAST_DELEGATE_OneParam(FGILStickReleased, EGILRelease);

/**
 * Which finger owns the stick, and how the stick lets go. Input-system agnostic: feed it every known touch once
 * per frame. Mirrors src/components/stickTracker.ts. Touch positions are +y up (see FGILTouch).
 */
class GAMEINPUTLAB_API FGILStickTracker
{
public:
	FGILStickTracker() = default;
	explicit FGILStickTracker(const FGILJoystickConfig& InConfig) : Config(InConfig) {}

	FGILJoystickConfig Config;
	/** Touch area in the same space as the touch positions. Only touches that begin inside it are taken. */
	FGILRect Area;
	/** Touch-space units per point: 1 for points, pixels per point for screen pixels. */
	double UnitsPerPoint = 1.0;

	/** INDEX_NONE when no finger is tracked. */
	int32 FingerId = INDEX_NONE;
	FVector2D Origin = FVector2D::ZeroVector;
	FVector2D Point = FVector2D::ZeroVector;
	/** Smoothed output, length 0 to 1, +y up. */
	FVector2D Output = FVector2D::ZeroVector;
	/** Release caused by the latest Step, Track or Interrupt call, or None. */
	EGILRelease LastRelease = EGILRelease::None;

	/** Broadcast when the stick lets go. */
	FGILStickReleased OnReleased;

	bool IsPressed() const { return FingerId != INDEX_NONE; }

	/** Unsmoothed output for the current finger position. */
	FVector2D Target() const;

	/** One frame: track the touches, then advance smoothing. */
	void Step(const TArray<FGILTouch>& Touches, double DeltaMs);

	void Track(const TArray<FGILTouch>& Touches);
	void Advance(double DeltaMs);

	/** The app lost focus or was paused: drop the finger and zero the output. */
	void Interrupt();

	/** Back to idle without broadcasting OnReleased. */
	void Reset();

private:
	void Release(EGILRelease Reason);
};
