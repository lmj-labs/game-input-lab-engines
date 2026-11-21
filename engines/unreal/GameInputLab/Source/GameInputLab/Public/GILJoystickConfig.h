#pragma once

#include "CoreMinimal.h"

class FJsonObject;

/** Limits and decimals of one parameter, keyed by the app's field name. */
struct GAMEINPUTLAB_API FGILFieldRange
{
	const TCHAR* Key;
	double Min;
	double Max;
	int32 Decimals;
};

/**
 * Joystick parameters with the same names, units and limits as the Game Input Lab app.
 * Mirrors JoystickConfig in src/components/joystickMath.ts. JSON keys keep the app's camelCase names.
 */
struct GAMEINPUTLAB_API FGILJoystickConfig
{
	/** Fixed: the stick centre is the centre of the touch area. Floating: it is where the finger lands. */
	bool bFloating = false;
	/** Maximum knob travel in points (iOS pt, about one Android dp). 32 to 96. */
	double Radius = 64.0;
	/** Inner dead zone, percent of the radius. 0 to 50. */
	double DeadZone = 10.0;
	/** Outer dead zone, percent of the radius. 0 to 30. */
	double OuterDeadZone = 0.0;
	/** Response exponent after the dead zones. 0.5 to 3. */
	double Curve = 1.0;
	/** Gain after the curve. Output is clamped to 1. 0.5 to 2. */
	double Sensitivity = 1.0;
	/** Exponential smoothing time constant in milliseconds. 0 turns smoothing off. 0 to 300. */
	double Smoothing = 0.0;

	/** The six parameters with their limits. */
	static TConstArrayView<FGILFieldRange> Ranges();

	/** A copy with every field clamped to the app's limits and rounded like the app. */
	FGILJoystickConfig Normalized() const;

	/** Read the app's config object (keys mode, radius, deadZone, ...). Missing fields keep their defaults. Not clamped. */
	static FGILJoystickConfig FromObject(const FJsonObject& Data);

	/**
	 * Read a JSON export from the app, or a bare config object. Out-of-range values are clamped.
	 * Returns false (and logs an error) when the text is not valid JSON or has another format.
	 */
	static bool FromJson(const FString& Text, FGILJoystickConfig& OutConfig);
};
