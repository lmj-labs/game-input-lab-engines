#include "GILSafeAreaLayout.h"

namespace
{
	double ClampCentre(double Value, double Extent, double Start, double Length)
	{
		const double Margin = FMath::Min(Extent, Length / 2.0);
		return FMath::Max(Start + Margin, FMath::Min(Start + Length - Margin, Value));
	}
}

FGILRect FGILSafeAreaLayout::SafeRect(double Width, double Height, double Top, double Left, double Right, double Bottom)
{
	return FGILRect(Left, Bottom, FMath::Max(0.0, Width - Left - Right), FMath::Max(0.0, Height - Top - Bottom));
}

double FGILSafeAreaLayout::StickRadius(double Radius, const FGILRect& Safe)
{
	return FMath::Max(0.0, FMath::Min(Radius, FMath::Min(FMath::FloorToDouble(Safe.Size.Y / 2.0), FMath::FloorToDouble(Safe.Size.X / 2.0))));
}

FVector2D FGILSafeAreaLayout::Center(const FGILRect& Safe, const FVector2D& Anchor, double Extent)
{
	return FVector2D(
		ClampCentre(Safe.Position.X + Anchor.X * Safe.Size.X, Extent, Safe.Position.X, Safe.Size.X),
		ClampCentre(Safe.Position.Y + Anchor.Y * Safe.Size.Y, Extent, Safe.Position.Y, Safe.Size.Y));
}
