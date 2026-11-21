class_name GILSafeAreaLayout
extends RefCounted
## Safe-area placement used by the app's landscape labs. Mirrors src/landscape/layout.ts: a control's centre is a
## fraction of the safe area, kept far enough from its edges for the control to fit. Rects and anchors have y up
## and the origin at the bottom left, like the app's exports. Units are whatever the caller uses; the app and the
## test vectors use points.


## The screen minus the notch, rounded corners and home indicator.
static func safe_rect(width: float, height: float, top: float, left: float, right: float, bottom: float) -> Rect2:
	return Rect2(left, bottom, maxf(0.0, width - left - right), maxf(0.0, height - top - bottom))


## The radius a stick can really have: it has to fit inside the safe area.
static func stick_radius(radius: float, safe: Rect2) -> float:
	return maxf(0.0, minf(radius, minf(floorf(safe.size.y / 2.0), floorf(safe.size.x / 2.0))))


## Centre of a control. `anchor` is a fraction of the safe area from its bottom left; `extent` is the radius that
## must stay inside it (stick radius, or half the hit diameter).
static func center(safe: Rect2, anchor: Vector2, extent: float) -> Vector2:
	return Vector2(
		_clamp_centre(safe.position.x + anchor.x * safe.size.x, extent, safe.position.x, safe.size.x),
		_clamp_centre(safe.position.y + anchor.y * safe.size.y, extent, safe.position.y, safe.size.y))


static func _clamp_centre(value: float, extent: float, start: float, length: float) -> float:
	var margin := minf(extent, length / 2.0)
	return maxf(start + margin, minf(start + length - margin, value))


## Screen pixels per point. A point is a pixel at 160 dpi. Pass `override` (above 0) when a device reports a
## misleading DPI.
static func pixels_per_point(override := 0.0) -> float:
	if override > 0.0:
		return override
	var dpi := DisplayServer.screen_get_dpi()
	return dpi / 160.0 if dpi > 0 else 1.0
