class_name GILTiltConfig
extends RefCounted
## Tilt parameters with the same names, units and limits as the Game Input Lab app.
## Mirrors TiltConfig in src/sensors/tilt.ts. JSON keys keep the app's camelCase names.

const JSON_FORMAT := "game-input-lab/tilt"
const ORIENTATIONS := ["portrait", "topLeft", "topRight"]

## Where the phone's top edge points while you play: "portrait", "topLeft" or "topRight".
var orientation := "portrait"
## Flip left/right after the orientation is applied.
var invert_x := false
## Flip up/down after the orientation is applied.
var invert_y := false
## Tilt in degrees from the neutral pose that gives a full push. 10 to 60.
var max_angle := 30.0
## Inner dead zone, percent of max_angle. 0 to 50.
var dead_zone := 5.0
## Outer dead zone, percent of max_angle. 0 to 30.
var outer_dead_zone := 0.0
## Response exponent after the dead zones. 0.5 to 3.
var curve := 1.0
## Gain after the curve. Output is clamped to 1. 0.5 to 2.
var sensitivity := 1.0
## Exponential smoothing time constant in milliseconds. 0 turns smoothing off. 0 to 300.
var smoothing := 40.0


static func _clamp_field(value: float, fallback: float, low: float, high: float, decimals: int) -> float:
	if is_nan(value) or is_inf(value):
		value = fallback
	var scale := pow(10.0, float(decimals))
	return roundf(clampf(value, low, high) * scale) / scale


## A copy with every field clamped to the app's limits and rounded like the app.
func normalized() -> GILTiltConfig:
	var defaults := GILTiltConfig.new()
	var result := GILTiltConfig.new()
	result.orientation = orientation if ORIENTATIONS.has(orientation) else "portrait"
	result.invert_x = invert_x
	result.invert_y = invert_y
	result.max_angle = _clamp_field(max_angle, defaults.max_angle, 10.0, 60.0, 0)
	result.dead_zone = _clamp_field(dead_zone, defaults.dead_zone, 0.0, 50.0, 0)
	result.outer_dead_zone = _clamp_field(outer_dead_zone, defaults.outer_dead_zone, 0.0, 30.0, 0)
	result.curve = _clamp_field(curve, defaults.curve, 0.5, 3.0, 1)
	result.sensitivity = _clamp_field(sensitivity, defaults.sensitivity, 0.5, 2.0, 2)
	result.smoothing = _clamp_field(smoothing, defaults.smoothing, 0.0, 300.0, 0)
	return result


## The joystick model the tilt goes through: a full push at max_angle degrees.
func as_joystick() -> GILJoystickConfig:
	var result := GILJoystickConfig.new()
	result.radius = max_angle
	result.dead_zone = dead_zone
	result.outer_dead_zone = outer_dead_zone
	result.curve = curve
	result.sensitivity = sensitivity
	result.smoothing = smoothing
	return result


## Read the app's config object (keys orientation, invertX, maxAngle, ...). Missing fields keep their defaults. Not clamped.
static func from_dict(data: Dictionary) -> GILTiltConfig:
	var result := GILTiltConfig.new()
	result.orientation = str(data.get("orientation", "portrait"))
	result.invert_x = bool(data.get("invertX", false))
	result.invert_y = bool(data.get("invertY", false))
	result.max_angle = float(data.get("maxAngle", result.max_angle))
	result.dead_zone = float(data.get("deadZone", result.dead_zone))
	result.outer_dead_zone = float(data.get("outerDeadZone", result.outer_dead_zone))
	result.curve = float(data.get("curve", result.curve))
	result.sensitivity = float(data.get("sensitivity", result.sensitivity))
	result.smoothing = float(data.get("smoothing", result.smoothing))
	return result


## Read a JSON export from the app, or a bare config object. Out-of-range values are clamped.
## Returns null (and prints an error) when the text is not valid JSON or has another format.
static func from_json(text: String) -> GILTiltConfig:
	var parsed: Variant = JSON.parse_string(text)
	if not (parsed is Dictionary):
		push_error("Game Input Lab: the JSON is empty or not an object.")
		return null
	var data: Dictionary = parsed
	if data.has("format"):
		if str(data["format"]) != JSON_FORMAT:
			push_error("Game Input Lab: unsupported format \"%s\". Expected \"%s\"." % [data["format"], JSON_FORMAT])
			return null
		var inner: Variant = data.get("config", {})
		data = inner if inner is Dictionary else {}
	return from_dict(data).normalized()
