#pragma once

#include "CoreMinimal.h"

/** Shared types. All positions, outputs and angles use +y up and radians, like the Game Input Lab app and the other engine packages. */

constexpr double GILPi = 3.14159265358979323846;

/** A rectangle. Contains() includes the left and bottom edges and excludes the right and top ones. */
struct GAMEINPUTLAB_API FGILRect
{
	FVector2D Position = FVector2D::ZeroVector;
	FVector2D Size = FVector2D::ZeroVector;

	FGILRect() = default;
	FGILRect(double X, double Y, double Width, double Height) : Position(X, Y), Size(Width, Height) {}

	FVector2D Center() const { return Position + Size * 0.5; }

	bool Contains(const FVector2D& Point) const
	{
		return Point.X >= Position.X && Point.Y >= Position.Y && Point.X < Position.X + Size.X && Point.Y < Position.Y + Size.Y;
	}
};

enum class EGILTouchPhase : uint8
{
	Began,
	Moved,
	Stationary,
	Ended,
	Canceled,
};

/** One finger in one frame. Position is +y up: flip the y of Unreal's screen coordinates (view height minus y). */
struct GAMEINPUTLAB_API FGILTouch
{
	int32 Id = 0;
	FVector2D Position = FVector2D::ZeroVector;
	EGILTouchPhase Phase = EGILTouchPhase::Moved;

	FGILTouch() = default;
	FGILTouch(int32 InId, const FVector2D& InPosition, EGILTouchPhase InPhase) : Id(InId), Position(InPosition), Phase(InPhase) {}
};

/** Why a stick let go. */
enum class EGILRelease : uint8
{
	None,
	Ended,
	Canceled,
	Lost,
	Interrupted,
};

/** "ended", "canceled", "lost", "interrupted", or "" for None. The same words the app and the other packages use. */
GAMEINPUTLAB_API const TCHAR* GILReleaseName(EGILRelease Release);

/** Parse "began", "moved", "stationary", "ended" or "canceled". Returns false for anything else. */
GAMEINPUTLAB_API bool GILParsePhase(const FString& Name, EGILTouchPhase& OutPhase);
