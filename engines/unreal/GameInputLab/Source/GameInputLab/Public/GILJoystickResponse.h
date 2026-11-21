#pragma once

#include "CoreMinimal.h"
#include "GILJoystickConfig.h"

/** Stick response math. Each function mirrors the function of the same role in src/components/joystickMath.ts; the shared test vectors check they agree. */
class GAMEINPUTLAB_API FGILJoystickResponse
{
public:
	/** Distances closer than this to the target snap onto it, so smoothing settles at rest. */
	static constexpr double SmoothingSnap = 0.001;

	/** Stick deflection (0 to 1) to output strength (0 to 1). Mirrors responseCurve. */
	static double ResponseCurve(double Deflection, const FGILJoystickConfig& Config);

	/** Finger offset from the stick centre in points (+y up) to output with length 0 to 1. The direction is never altered. Mirrors stickResponseUp. */
	static FVector2D Evaluate(const FVector2D& OffsetPoints, const FGILJoystickConfig& Config);

	/** Frame rate independent exponential smoothing. Mirrors smoothStick. */
	static FVector2D Smooth(const FVector2D& Current, const FVector2D& Target, double DeltaMs, double SmoothingMs);
};
