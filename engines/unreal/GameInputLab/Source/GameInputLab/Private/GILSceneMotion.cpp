#include "GILSceneMotion.h"
#include "GILAimBehavior.h"
#include "GILJoystickResponse.h"
#include "GILLog.h"

FGILSceneParams FGILSceneParams::Normalized() const
{
	FGILSceneParams Result;
	Result.MaxSpeed = FMath::Clamp(FMath::RoundToDouble(MaxSpeed * 10.0) / 10.0, 1.0, 10.0);
	Result.Acceleration = FMath::Clamp(FMath::RoundToDouble(Acceleration), 2.0, 100.0);
	Result.TurnRate = FMath::Clamp(FMath::RoundToDouble(TurnRate), 90.0, 1080.0);
	Result.CameraLag = FMath::Clamp(FMath::RoundToDouble(CameraLag), 0.0, 400.0);
	return Result;
}

FGILSceneParams FGILSceneParams::FromObject(const FJsonObject& Data)
{
	FGILSceneParams Result;
	Result.MaxSpeed = GILJson::Number(Data, TEXT("maxSpeed"), Result.MaxSpeed);
	Result.Acceleration = GILJson::Number(Data, TEXT("acceleration"), Result.Acceleration);
	Result.TurnRate = GILJson::Number(Data, TEXT("turnRate"), Result.TurnRate);
	Result.CameraLag = GILJson::Number(Data, TEXT("cameraLag"), Result.CameraLag);
	return Result;
}

bool FGILSceneParams::FromJson(const FString& Text, FGILSceneParams& OutParams)
{
	TSharedPtr<FJsonObject> Root;
	if (!GILJson::ParseObject(Text, Root))
	{
		return false;
	}
	OutParams = FromObject(GILJson::Object(*Root, TEXT("scene"))).Normalized();
	return true;
}

double FGILSceneMotion::AngleDiff(double From, double To)
{
	const double Diff = FMath::Fmod(To - From, 2.0 * GILPi);
	if (Diff > GILPi)
	{
		return Diff - 2.0 * GILPi;
	}
	if (Diff <= -GILPi)
	{
		return Diff + 2.0 * GILPi;
	}
	return Diff;
}

FGILSceneState FGILSceneMotion::Step(const FGILSceneState& State, const FVector2D& Output, double DeltaMs, const FGILSceneParams& Params)
{
	if (!(DeltaMs > 0.0))
	{
		return State;
	}
	const double FrameMs = FMath::Min(MaxFrameMs, DeltaMs);
	const double Dt = FrameMs / 1000.0;

	const FVector2D Target = Output * Params.MaxSpeed;
	const FVector2D Delta = Target - State.Velocity;
	const double Gap = Delta.Size();
	const double Reach = Params.Acceleration * Dt;
	const FVector2D Velocity = Gap <= Reach ? Target : State.Velocity + Delta / Gap * Reach;

	double Heading = State.Heading;
	if (Output.Size() > FGILAimBehavior::Engaged)
	{
		const double Turn = AngleDiff(Heading, FMath::Atan2(Output.Y, Output.X));
		const double Most = Params.TurnRate * GILPi / 180.0 * Dt;
		Heading += FMath::Clamp(Turn, -Most, Most);
	}

	FGILSceneState Result;
	Result.Velocity = Velocity;
	Result.Heading = Heading;
	Result.Position = State.Position + Velocity * Dt;
	Result.Camera = FGILJoystickResponse::Smooth(State.Camera, Result.Position, FrameMs, Params.CameraLag);
	return Result;
}
