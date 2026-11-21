#pragma once

#include "CoreMinimal.h"
#include "GILTypes.h"

class FJsonObject;

/**
 * The reference scene's motion from Joystick Lab: how a character driven by the stick moves, and how the camera
 * follows. Mirrors src/components/sceneMotion.ts. World units, +y up.
 */
struct GAMEINPUTLAB_API FGILSceneParams
{
	/** Units per second at full stick (1 to 10). */
	double MaxSpeed = 4.0;
	/** Units per second squared: how fast the velocity follows the stick (2 to 100). */
	double Acceleration = 20.0;
	/** Degrees per second the character can turn (90 to 1080). */
	double TurnRate = 720.0;
	/** Time constant of the camera follow in milliseconds. 0 locks the camera to the character (0 to 400). */
	double CameraLag = 120.0;

	/** A copy clamped to the app's limits. Mirrors normalizeScene. */
	FGILSceneParams Normalized() const;

	/** Read the app's scene object (keys maxSpeed, acceleration, turnRate, cameraLag). Not clamped. */
	static FGILSceneParams FromObject(const FJsonObject& Data);

	/** Read the "scene" of a JSON export from the app. A file without one gives the defaults. Returns false for invalid JSON. */
	static bool FromJson(const FString& Text, FGILSceneParams& OutParams);
};

/** The character and camera. Positions in world units, heading in radians (y up). */
struct GAMEINPUTLAB_API FGILSceneState
{
	FVector2D Position = FVector2D::ZeroVector;
	FVector2D Velocity = FVector2D::ZeroVector;
	double Heading = GILPi / 2.0;
	FVector2D Camera = FVector2D::ZeroVector;
};

class GAMEINPUTLAB_API FGILSceneMotion
{
public:
	/** Longest frame that still counts, so a stall does not throw the character across the floor. */
	static constexpr double MaxFrameMs = 100.0;

	/** At the origin, facing up, at rest. */
	static FGILSceneState Start() { return FGILSceneState(); }

	/** The shortest signed turn from one angle to another, in (-pi, pi]. Mirrors angleDiff. */
	static double AngleDiff(double From, double To);

	/**
	 * One frame. Output is the stick output (+y up, length 0 to 1). Speed follows power, so half power moves at
	 * half speed. Returns a new state; the one passed in is not changed. Mirrors stepScene.
	 */
	static FGILSceneState Step(const FGILSceneState& State, const FVector2D& Output, double DeltaMs, const FGILSceneParams& Params);
};
