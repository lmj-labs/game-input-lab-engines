#include "GILLandscapeLayout.h"
#include "GILLog.h"
#include "GILSafeAreaLayout.h"

bool FGILLandscapeLayout::FromJson(const FString& Text, FGILLandscapeLayout& OutLayout)
{
	TSharedPtr<FJsonObject> Root;
	if (!GILJson::ParseObject(Text, Root))
	{
		return false;
	}
	const FString Format = GILJson::String(*Root, TEXT("format"));
	if (Format != TEXT("game-input-lab/landscape-layout"))
	{
		UE_LOG(LogGameInputLab, Error, TEXT("Game Input Lab: unsupported format \"%s\". Expected \"game-input-lab/landscape-layout\"."), *Format);
		return false;
	}
	FGILLandscapeLayout Layout;
	Layout.Lab = GILJson::String(*Root, TEXT("lab"));
	Layout.Orientation = GILJson::String(*Root, TEXT("orientation")) == TEXT("portrait") ? TEXT("portrait") : TEXT("landscape");

	const TArray<TSharedPtr<FJsonValue>>* Controls = nullptr;
	if (Root->TryGetArrayField(TEXT("controls"), Controls))
	{
		for (const TSharedPtr<FJsonValue>& Value : *Controls)
		{
			const TSharedPtr<FJsonObject> Item = Value.IsValid() ? Value->AsObject() : nullptr;
			if (!Item.IsValid())
			{
				continue;
			}
			FGILControlLayout Control;
			Control.Id = GILJson::String(*Item, TEXT("id"));
			Control.Anchor = FVector2D(
				FMath::Clamp(GILJson::Number(*Item, TEXT("anchorX"), 0.5), 0.0, 1.0),
				FMath::Clamp(GILJson::Number(*Item, TEXT("anchorY"), 0.5), 0.0, 1.0));
			Control.Visual = GILJson::Number(*Item, TEXT("visual"), 0.0);
			Control.Hit = GILJson::Number(*Item, TEXT("hit"), 0.0);
			Layout.Controls.Add(Control);
		}
	}
	const TArray<TSharedPtr<FJsonValue>>* Sticks = nullptr;
	if (Root->TryGetArrayField(TEXT("sticks"), Sticks))
	{
		for (const TSharedPtr<FJsonValue>& Value : *Sticks)
		{
			const TSharedPtr<FJsonObject> Item = Value.IsValid() ? Value->AsObject() : nullptr;
			if (Item.IsValid())
			{
				Layout.Sticks.Add(GILJson::String(*Item, TEXT("id")), FGILJoystickConfig::FromObject(*Item).Normalized());
			}
		}
	}
	Layout.Aim = FGILAimConfig::FromObject(GILJson::Object(*Root, TEXT("aim"))).Normalized();
	OutLayout = MoveTemp(Layout);
	return true;
}

const FGILControlLayout* FGILLandscapeLayout::FindControl(const FString& Id) const
{
	return Controls.FindByPredicate([&Id](const FGILControlLayout& Control) { return Control.Id == Id; });
}

const FGILJoystickConfig* FGILLandscapeLayout::FindStick(const FString& Id) const
{
	return Sticks.Find(Id);
}

FVector2D FGILLandscapeLayout::CenterOf(const FString& Id, const FGILRect& SafeRect) const
{
	const FGILControlLayout* Control = FindControl(Id);
	if (Control == nullptr)
	{
		UE_LOG(LogGameInputLab, Error, TEXT("Game Input Lab: the layout has no control \"%s\"."), *Id);
		return FVector2D::ZeroVector;
	}
	double Extent = Control->Hit / 2.0;
	if (const FGILJoystickConfig* Stick = FindStick(Id))
	{
		Extent = FGILSafeAreaLayout::StickRadius(Stick->Radius, SafeRect);
	}
	return FGILSafeAreaLayout::Center(SafeRect, Control->Anchor, Extent);
}

FGILPlacement FGILLandscapeLayout::Place(const FString& Id, const FVector2D& SafeSize, double UnitsPerPoint) const
{
	FGILPlacement Result;
	const FGILControlLayout* Control = FindControl(Id);
	if (Control == nullptr || UnitsPerPoint <= 0.0)
	{
		UE_LOG(LogGameInputLab, Error, TEXT("Game Input Lab: cannot place control \"%s\"."), *Id);
		return Result;
	}
	const FGILRect SafePoints(0.0, 0.0, SafeSize.X / UnitsPerPoint, SafeSize.Y / UnitsPerPoint);
	double Visual = Control->Visual;
	double Hit = Control->Hit;
	if (const FGILJoystickConfig* Stick = FindStick(Id))
	{
		Visual = FGILSafeAreaLayout::StickRadius(Stick->Radius, SafePoints) * 2.0;
		Hit = Visual;
	}
	const FVector2D CenterUp = FGILSafeAreaLayout::Center(SafePoints, Control->Anchor, Hit / 2.0) * UnitsPerPoint;
	Result.Center = FVector2D(CenterUp.X, SafeSize.Y - CenterUp.Y);
	Result.Visual = Visual * UnitsPerPoint;
	Result.Hit = Hit * UnitsPerPoint;
	return Result;
}
