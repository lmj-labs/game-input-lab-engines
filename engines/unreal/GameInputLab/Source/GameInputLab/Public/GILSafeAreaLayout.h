#pragma once

#include "CoreMinimal.h"
#include "GILTypes.h"

/**
 * Safe-area placement used by the app's landscape labs. Mirrors src/landscape/layout.ts: a control's centre is a
 * fraction of the safe area, kept far enough from its edges for the control to fit. Rects and anchors have y up
 * and the origin at the bottom left, like the app's exports. Units are whatever the caller uses; the app and the
 * test vectors use points.
 */
class GAMEINPUTLAB_API FGILSafeAreaLayout
{
public:
	/** The screen minus the notch, rounded corners and home indicator. */
	static FGILRect SafeRect(double Width, double Height, double Top, double Left, double Right, double Bottom);

	/** The radius a stick can really have: it has to fit inside the safe area. */
	static double StickRadius(double Radius, const FGILRect& Safe);

	/** Centre of a control. Anchor is a fraction of the safe area from its bottom left; Extent is the radius that must stay inside it. */
	static FVector2D Center(const FGILRect& Safe, const FVector2D& Anchor, double Extent);
};
