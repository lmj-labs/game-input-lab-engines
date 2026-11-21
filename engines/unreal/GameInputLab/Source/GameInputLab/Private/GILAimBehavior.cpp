#include "GILAimBehavior.h"
#include "GILLog.h"

FGILAimConfig FGILAimConfig::Normalized() const
{
	FGILAimConfig Result;
	Result.Release = Release == EGILAimRelease::Forward ? EGILAimRelease::Forward : EGILAimRelease::Hold;
	Result.FireThreshold = FMath::Clamp(FMath::RoundToDouble(FireThreshold), 20.0, 100.0);
	Result.SlowWhileAiming = FMath::Clamp(FMath::RoundToDouble(SlowWhileAiming), 0.0, 80.0);
	return Result;
}

FGILAimConfig FGILAimConfig::FromObject(const FJsonObject& Data)
{
	FGILAimConfig Result;
	Result.Release = GILJson::String(Data, TEXT("release"), TEXT("hold")) == TEXT("forward") ? EGILAimRelease::Forward : EGILAimRelease::Hold;
	Result.FireThreshold = GILJson::Number(Data, TEXT("fireThreshold"), Result.FireThreshold);
	Result.SlowWhileAiming = GILJson::Number(Data, TEXT("slowWhileAiming"), Result.SlowWhileAiming);
	return Result;
}

FGILAimStep FGILAimBehavior::Step(double Heading, const FVector2D& Move, const FVector2D& Aim, const FGILAimConfig& Config)
{
	const double Strength = Aim.Size();
	FGILAimStep Result;
	Result.bAiming = Strength > Engaged;
	Result.Heading = Heading;
	if (Result.bAiming)
	{
		Result.Heading = FMath::Atan2(Aim.Y, Aim.X);
	}
	else if (Config.Release == EGILAimRelease::Forward && Move.Size() > Engaged)
	{
		Result.Heading = FMath::Atan2(Move.Y, Move.X);
	}
	Result.bFiring = Result.bAiming && Strength >= Config.FireThreshold / 100.0;
	Result.MoveScale = Result.bAiming ? 1.0 - Config.SlowWhileAiming / 100.0 : 1.0;
	return Result;
}
