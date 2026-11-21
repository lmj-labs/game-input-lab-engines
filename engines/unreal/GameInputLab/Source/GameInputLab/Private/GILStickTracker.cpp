#include "GILStickTracker.h"

FVector2D FGILStickTracker::Target() const
{
	if (FingerId == INDEX_NONE)
	{
		return FVector2D::ZeroVector;
	}
	const double Scale = UnitsPerPoint > 0.0 ? UnitsPerPoint : 1.0;
	return FGILJoystickResponse::Evaluate((Point - Origin) / Scale, Config);
}

void FGILStickTracker::Step(const TArray<FGILTouch>& Touches, double DeltaMs)
{
	Track(Touches);
	Advance(DeltaMs);
}

void FGILStickTracker::Track(const TArray<FGILTouch>& Touches)
{
	LastRelease = EGILRelease::None;
	if (FingerId != INDEX_NONE)
	{
		const int32 Current = FingerId;
		const FGILTouch* Own = Touches.FindByPredicate([Current](const FGILTouch& Touch) { return Touch.Id == Current; });
		if (Own == nullptr || Own->Phase == EGILTouchPhase::Began)
		{
			Release(EGILRelease::Lost);
		}
		else if (Own->Phase == EGILTouchPhase::Ended)
		{
			Release(EGILRelease::Ended);
		}
		else if (Own->Phase == EGILTouchPhase::Canceled)
		{
			Release(EGILRelease::Canceled);
		}
		else
		{
			Point = Own->Position;
		}
	}
	if (FingerId == INDEX_NONE)
	{
		for (const FGILTouch& Touch : Touches)
		{
			if (Touch.Phase != EGILTouchPhase::Began || !Area.Contains(Touch.Position))
			{
				continue;
			}
			FingerId = Touch.Id;
			Point = Touch.Position;
			Origin = Config.bFloating ? Touch.Position : Area.Center();
			break;
		}
	}
}

void FGILStickTracker::Advance(double DeltaMs)
{
	Output = FGILJoystickResponse::Smooth(Output, Target(), DeltaMs, Config.Smoothing);
}

void FGILStickTracker::Interrupt()
{
	LastRelease = EGILRelease::None;
	if (FingerId != INDEX_NONE)
	{
		Release(EGILRelease::Interrupted);
	}
	else
	{
		Output = FVector2D::ZeroVector;
	}
}

void FGILStickTracker::Reset()
{
	FingerId = INDEX_NONE;
	Origin = FVector2D::ZeroVector;
	Point = FVector2D::ZeroVector;
	Output = FVector2D::ZeroVector;
	LastRelease = EGILRelease::None;
}

void FGILStickTracker::Release(EGILRelease Reason)
{
	FingerId = INDEX_NONE;
	LastRelease = Reason;
	if (Reason != EGILRelease::Ended)
	{
		Output = FVector2D::ZeroVector;
	}
	OnReleased.Broadcast(Reason);
}
