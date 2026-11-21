#include "GILTypes.h"

const TCHAR* GILReleaseName(EGILRelease Release)
{
	switch (Release)
	{
	case EGILRelease::Ended: return TEXT("ended");
	case EGILRelease::Canceled: return TEXT("canceled");
	case EGILRelease::Lost: return TEXT("lost");
	case EGILRelease::Interrupted: return TEXT("interrupted");
	default: return TEXT("");
	}
}

bool GILParsePhase(const FString& Name, EGILTouchPhase& OutPhase)
{
	if (Name == TEXT("began")) { OutPhase = EGILTouchPhase::Began; return true; }
	if (Name == TEXT("moved")) { OutPhase = EGILTouchPhase::Moved; return true; }
	if (Name == TEXT("stationary")) { OutPhase = EGILTouchPhase::Stationary; return true; }
	if (Name == TEXT("ended")) { OutPhase = EGILTouchPhase::Ended; return true; }
	if (Name == TEXT("canceled")) { OutPhase = EGILTouchPhase::Canceled; return true; }
	return false;
}
