#pragma once

#include "CoreMinimal.h"
#include "GILAimBehavior.h"
#include "GILJoystickConfig.h"
#include "GILTypes.h"

/** One control of a landscape layout. Visual and Hit are button diameters in points; a stick is as wide as its ring (see FGILLandscapeLayout::Place). */
struct GAMEINPUTLAB_API FGILControlLayout
{
	FString Id;
	/** Fraction of the safe area from its bottom left (y up). */
	FVector2D Anchor = FVector2D(0.5, 0.5);
	double Visual = 0.0;
	double Hit = 0.0;
};

/** Where to put a control: Center is in the caller's units, y down, from the safe area's top left. */
struct GAMEINPUTLAB_API FGILPlacement
{
	FVector2D Center = FVector2D::ZeroVector;
	/** Visible diameter in the caller's units. */
	double Visual = 0.0;
	/** Touch diameter in the caller's units. */
	double Hit = 0.0;
};

/**
 * Control positions, button sizes and stick parameters exported by one of the app's landscape labs.
 * Mirrors src/landscape/layout.ts and the export in src/export/landscapeExport.ts. FGILSafeAreaLayout holds the
 * placement rules.
 */
struct GAMEINPUTLAB_API FGILLandscapeLayout
{
	/** "gamepad" (stick and buttons) or "dualStick". */
	FString Lab;
	/** "landscape" or "portrait": which way the phone is held. The layout was made for that one; lock the screen to match. */
	FString Orientation = TEXT("landscape");
	TArray<FGILControlLayout> Controls;
	/** Stick id ("left", "right") to its parameters. */
	TMap<FString, FGILJoystickConfig> Sticks;
	/** Only the "dualStick" lab exports it; other layouts get the defaults. */
	FGILAimConfig Aim;

	/**
	 * Read a JSON export from the app. Stick parameters and aim behavior are clamped to the app's limits.
	 * Returns false (and logs an error) when the text is not valid or has another format.
	 */
	static bool FromJson(const FString& Text, FGILLandscapeLayout& OutLayout);

	/** The control with this id, or nullptr. */
	const FGILControlLayout* FindControl(const FString& Id) const;

	/** The stick's parameters, or nullptr if the layout has no such stick. */
	const FGILJoystickConfig* FindStick(const FString& Id) const;

	/** Centre of a control in a safe area given in points (y up, origin bottom left). Sticks use their fitted radius. Zero for an unknown id. */
	FVector2D CenterOf(const FString& Id, const FGILRect& SafeRect) const;

	/**
	 * Placement of a control inside a safe area of SafeSize caller's units (for example Slate units).
	 * UnitsPerPoint is how many of those units make one point. The layout's y up is flipped here, so Center
	 * counts from the safe area's top left. A stick's Visual and Hit are its ring diameter.
	 */
	FGILPlacement Place(const FString& Id, const FVector2D& SafeSize, double UnitsPerPoint = 1.0) const;
};
