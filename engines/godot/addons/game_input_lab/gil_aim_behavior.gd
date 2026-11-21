class_name GILAimBehavior
extends RefCounted
## What the right stick does to the aim: where the weapon points after you let go, the push at which it
## fires, and how much movement slows while you aim. Mirrors src/components/aimBehavior.ts.

const HOLD := "hold"
const FORWARD := "forward"
## Stick output beyond this counts as aiming (and as moving, for the left stick). Mirrors engaged.
const ENGAGED := 0.15


class Config:
	extends RefCounted
	## "hold" keeps the last aimed direction; "forward" turns to the way the player moves.
	var release := "hold"
	## Right stick output, in percent of a full push, at which the weapon fires (20 to 100).
	var fire_threshold := 40.0
	## Percent by which movement slows while aiming (0 to 80). 0 is off.
	var slow_while_aiming := 0.0

	static func from_dict(data: Dictionary) -> Config:
		var result := Config.new()
		result.release = str(data.get("release", "hold"))
		result.fire_threshold = float(data.get("fireThreshold", result.fire_threshold))
		result.slow_while_aiming = float(data.get("slowWhileAiming", result.slow_while_aiming))
		return result

	## A copy clamped to the app's limits.
	func normalized() -> Config:
		var result := Config.new()
		result.release = "forward" if release == "forward" else "hold"
		result.fire_threshold = clampf(roundf(fire_threshold), 20.0, 100.0)
		result.slow_while_aiming = clampf(roundf(slow_while_aiming), 0.0, 80.0)
		return result


## The result of one frame of step().
class Step:
	extends RefCounted
	## Radians, y up (atan2(y, x)).
	var heading := 0.0
	var aiming := false
	var firing := false
	## Multiply the movement speed by this.
	var move_scale := 1.0


## One frame. `heading` is the previous heading; `move` and `aim` are the two stick outputs (+y up, length 0 to 1).
## Mirrors aimStep. The config is used as given: call normalized() first for untrusted data.
static func step(heading: float, move: Vector2, aim: Vector2, config: Config) -> Step:
	var strength := aim.length()
	var result := Step.new()
	result.aiming = strength > ENGAGED
	result.heading = heading
	if result.aiming:
		result.heading = atan2(aim.y, aim.x)
	elif config.release == FORWARD and move.length() > ENGAGED:
		result.heading = atan2(move.y, move.x)
	result.firing = result.aiming and strength >= config.fire_threshold / 100.0
	result.move_scale = 1.0 - config.slow_while_aiming / 100.0 if result.aiming else 1.0
	return result
