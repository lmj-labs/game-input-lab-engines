#include "GILJoystickResponse.h"

double FGILJoystickResponse::ResponseCurve(double Deflection, const FGILJoystickConfig& Config)
{
	const double Inner = Config.DeadZone / 100.0;
	const double Outer = 1.0 - Config.OuterDeadZone / 100.0;
	if (Deflection <= Inner)
	{
		return 0.0;
	}
	const double Scaled = FMath::Min(1.0, (Deflection - Inner) / (Outer - Inner));
	return FMath::Min(1.0, FMath::Pow(Scaled, Config.Curve) * Config.Sensitivity);
}

FVector2D FGILJoystickResponse::Evaluate(const FVector2D& OffsetPoints, const FGILJoystickConfig& Config)
{
	const double Length = OffsetPoints.Size();
	if (Config.Radius <= 0.0 || Length == 0.0)
	{
		return FVector2D::ZeroVector;
	}
	const double Strength = ResponseCurve(FMath::Min(1.0, Length / Config.Radius), Config);
	if (Strength == 0.0)
	{
		return FVector2D::ZeroVector;
	}
	return OffsetPoints / Length * Strength;
}

FVector2D FGILJoystickResponse::Smooth(const FVector2D& Current, const FVector2D& Target, double DeltaMs, double SmoothingMs)
{
	if (SmoothingMs <= 0.0)
	{
		return Target;
	}
	if (DeltaMs <= 0.0)
	{
		return Current;
	}
	const double T = 1.0 - FMath::Exp(-DeltaMs / SmoothingMs);
	const FVector2D Next = Current + (Target - Current) * T;
	return (Target - Next).Size() < SmoothingSnap ? Target : Next;
}
