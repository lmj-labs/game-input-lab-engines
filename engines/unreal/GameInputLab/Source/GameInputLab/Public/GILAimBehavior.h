#pragma once

#include "CoreMinimal.h"

class FJsonObject;

enum class EGILAimRelease : uint8
{
	/** Keep the last aimed direction. */
	Hold,
	/** Turn to the way the player moves. */
	Forward,
};

/**
 * What the right stick does to the aim: where the weapon points after you let go, the push at which it fires, and
 * how much movement slows while you aim. Mirrors src/components/aimBehavior.ts.
 */
struct GAMEINPUTLAB_API FGILAimConfig
{
	EGILAimRelease Release = EGILAimRelease::Hold;
	/** Right stick output, in percent of a full push, at which the weapon fires (20 to 100). */
	double FireThreshold = 40.0;
	/** Percent by which movement slows while aiming (0 to 80). 0 is off. */
	double SlowWhileAiming = 0.0;

	/** A copy clamped to the app's limits. */
	FGILAimConfig Normalized() const;

	/** Read the app's aim object (keys release, fireThreshold, slowWhileAiming). Not clamped. */
	static FGILAimConfig FromObject(const FJsonObject& Data);
};

/** The result of one frame of FGILAimBehavior::Step. */
struct GAMEINPUTLAB_API FGILAimStep
{
	/** Radians, y up (atan2(y, x)). */
	double Heading = 0.0;
	bool bAiming = false;
	bool bFiring = false;
	/** Multiply the movement speed by this. */
	double MoveScale = 1.0;
};

class GAMEINPUTLAB_API FGILAimBehavior
{
public:
	/** Stick output beyond this counts as aiming (and as moving, for the left stick). Mirrors engaged. */
	static constexpr double Engaged = 0.15;

	/**
	 * One frame. Heading is the previous heading; Move and Aim are the two stick outputs (+y up, length 0 to 1).
	 * Mirrors aimStep. The config is used as given: call Normalized() first for untrusted data.
	 */
	static FGILAimStep Step(double Heading, const FVector2D& Move, const FVector2D& Aim, const FGILAimConfig& Config);
};
