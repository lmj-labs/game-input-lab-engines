class_name GILJoystickResponse
extends RefCounted
## Stick response math. Each function mirrors the function of the same role in src/components/joystickMath.ts;
## the shared test vectors check they agree.

## Distances closer than this to the target snap onto it, so smoothing settles at rest.
const SMOOTHING_SNAP := 0.001


## Stick deflection (0 to 1) to output strength (0 to 1). Mirrors responseCurve.
static func response_curve(deflection: float, config: GILJoystickConfig) -> float:
	var inner := config.dead_zone / 100.0
	var outer := 1.0 - config.outer_dead_zone / 100.0
	if deflection <= inner:
		return 0.0
	var scaled := minf(1.0, (deflection - inner) / (outer - inner))
	return minf(1.0, pow(scaled, config.curve) * config.sensitivity)


## Finger offset from the stick centre in points (+y up) to output with length 0 to 1.
## The direction is never altered. Mirrors stickResponseUp.
static func evaluate(offset_points: Vector2, config: GILJoystickConfig) -> Vector2:
	var length := offset_points.length()
	if config.radius <= 0.0 or length == 0.0:
		return Vector2.ZERO
	var strength := response_curve(minf(1.0, length / config.radius), config)
	if strength == 0.0:
		return Vector2.ZERO
	return offset_points / length * strength


## Frame rate independent exponential smoothing. Mirrors smoothStick.
static func smooth(current: Vector2, target: Vector2, delta_ms: float, smoothing_ms: float) -> Vector2:
	if smoothing_ms <= 0.0:
		return target
	if delta_ms <= 0.0:
		return current
	var t := 1.0 - exp(-delta_ms / smoothing_ms)
	var next := current + (target - current) * t
	return target if (target - next).length() < SMOOTHING_SNAP else next
