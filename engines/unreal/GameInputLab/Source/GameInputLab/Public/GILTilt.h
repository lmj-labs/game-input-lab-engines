#pragma once

#include "CoreMinimal.h"
#include "GILJoystickConfig.h"

class FJsonObject;

/** Where the phone's top edge points while you play, so left/right and up/down follow the screen. */
enum class EGILTiltOrientation : uint8
{
	Portrait,
	TopLeft,
	TopRight,
};

/**
 * Tilt parameters with the same names, units and limits as the Game Input Lab app.
 * Mirrors TiltConfig in src/sensors/tilt.ts.
 */
struct GAMEINPUTLAB_API FGILTiltConfig
{
	EGILTiltOrientation Orientation = EGILTiltOrientation::Portrait;
	/** Flip left/right after the orientation is applied. */
	bool bInvertX = false;
	/** Flip up/down after the orientation is applied. */
	bool bInvertY = false;
	/** Tilt in degrees from the neutral pose that gives a full push. 10 to 60. */
	double MaxAngle = 30.0;
	/** Inner dead zone, percent of MaxAngle. 0 to 50. */
	double DeadZone = 5.0;
	/** Outer dead zone, percent of MaxAngle. 0 to 30. */
	double OuterDeadZone = 0.0;
	/** Response exponent after the dead zones. 0.5 to 3. */
	double Curve = 1.0;
	/** Gain after the curve. Output is clamped to 1. 0.5 to 2. */
	double Sensitivity = 1.0;
	/** Exponential smoothing time constant in milliseconds. 0 turns smoothing off. 0 to 300. */
	double Smoothing = 40.0;

	/** A copy with every field clamped to the app's limits and rounded like the app. */
	FGILTiltConfig Normalized() const;

	/** The joystick model the tilt goes through: a full push at MaxAngle degrees. */
	FGILJoystickConfig AsJoystick() const;

	/** Read the app's config object (keys orientation, invertX, maxAngle, ...). Missing fields keep their defaults. Not clamped. */
	static FGILTiltConfig FromObject(const FJsonObject& Data);

	/** Read a JSON export from the app, or a bare config object. Out-of-range values are clamped. Returns false (and logs an error) for invalid JSON or another format. */
	static bool FromJson(const FString& Text, FGILTiltConfig& OutConfig);
};

/** Tilt in degrees. Roll: right edge down is positive. Pitch: top edge down is positive. */
struct GAMEINPUTLAB_API FGILTiltAngles
{
	double Roll = 0.0;
	double Pitch = 0.0;
};

/**
 * Tilt as a stick. Each function mirrors the function of the same role in src/sensors/tilt.ts; the shared test
 * vectors check they agree. The reading is the direction gravity pulls in the phone's own axes: x to the right,
 * y to the top edge, z out of the screen (lying face up reads z = -1).
 */
class GAMEINPUTLAB_API FGILTilt
{
public:
	/** The phone's tilt from one reading. Independent of the sign of z. Mirrors gravityAngles. */
	static FGILTiltAngles Angles(const FVector& Gravity);

	/** The tilt away from the neutral pose in degrees, turned to the screen's axes (+y up). Mirrors tiltOffset. */
	static FVector2D Offset(const FGILTiltAngles& Angles, const FGILTiltAngles& Neutral, const FGILTiltConfig& Config);

	/** Stick output, length 0 to 1, +y up, before smoothing. Mirrors tiltTarget. */
	static FVector2D Target(const FGILTiltAngles& Angles, const FGILTiltAngles& Neutral, const FGILTiltConfig& Config);
};

/** A tilt stick: remembers the neutral pose and the smoothed output. */
class GAMEINPUTLAB_API FGILTiltStick
{
public:
	FGILTiltStick() = default;
	explicit FGILTiltStick(const FGILTiltConfig& InConfig) : Config(InConfig) {}

	FGILTiltConfig Config;
	FGILTiltAngles Neutral;
	/** Smoothed output, length 0 to 1, +y up. */
	FVector2D Output = FVector2D::ZeroVector;

	/** Take this reading as the pose that means "no tilt". */
	void Calibrate(const FVector& Gravity);

	/** One frame with the latest reading. Returns the smoothed output. */
	FVector2D Step(const FVector& Gravity, double DeltaMs);

	void Reset() { Output = FVector2D::ZeroVector; }
};
