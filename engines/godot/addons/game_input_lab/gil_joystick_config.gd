class_name GILJoystickConfig
extends RefCounted
## Joystick parameters with the same names, units and limits as the Game Input Lab app.
## Mirrors JoystickConfig in src/components/joystickMath.ts. JSON keys keep the app's camelCase names.

const JSON_FORMAT := "game-input-lab/joystick"

## Fixed: the stick centre is the centre of the touch area. Floating: it is where the finger lands.
var floating := false
## Maximum knob travel in points (iOS pt, about one Android dp). 32 to 96.
var radius := 64.0
## Inner dead zone, percent of the radius. 0 to 50.
var dead_zone := 10.0
## Outer dead zone, percent of the radius. 0 to 30.
var outer_dead_zone := 0.0
## Response exponent after the dead zones. 0.5 to 3.
var curve := 1.0
## Gain after the curve. Output is clamped to 1. 0.5 to 2.
var sensitivity := 1.0
## Exponential smoothing time constant in milliseconds. 0 turns smoothing off. 0 to 300.
var smoothing := 0.0

## Limits and decimals per field, keyed by the app's field names: [min, max, decimals].
const RANGES := {
	"radius": [32.0, 96.0, 0],
	"deadZone": [0.0, 50.0, 0],
	"outerDeadZone": [0.0, 30.0, 0],
	"curve": [0.5, 3.0, 1],
	"sensitivity": [0.5, 2.0, 2],
	"smoothing": [0.0, 300.0, 0],
}


static func _clamp_field(key: String, value: float, fallback: float) -> float:
	if is_nan(value) or is_inf(value):
		value = fallback
	var spec: Array = RANGES[key]
	value = clampf(value, spec[0], spec[1])
	var scale := pow(10.0, float(spec[2]))
	return roundf(value * scale) / scale


func duplicate_config() -> GILJoystickConfig:
	var copy := GILJoystickConfig.new()
	copy.floating = floating
	copy.radius = radius
	copy.dead_zone = dead_zone
	copy.outer_dead_zone = outer_dead_zone
	copy.curve = curve
	copy.sensitivity = sensitivity
	copy.smoothing = smoothing
	return copy


## A copy with every field clamped to the app's limits and rounded like the app.
func normalized() -> GILJoystickConfig:
	var defaults := GILJoystickConfig.new()
	var result := GILJoystickConfig.new()
	result.floating = floating
	result.radius = _clamp_field("radius", radius, defaults.radius)
	result.dead_zone = _clamp_field("deadZone", dead_zone, defaults.dead_zone)
	result.outer_dead_zone = _clamp_field("outerDeadZone", outer_dead_zone, defaults.outer_dead_zone)
	result.curve = _clamp_field("curve", curve, defaults.curve)
	result.sensitivity = _clamp_field("sensitivity", sensitivity, defaults.sensitivity)
	result.smoothing = _clamp_field("smoothing", smoothing, defaults.smoothing)
	return result


## Read the app's config object (keys mode, radius, deadZone, ...). Missing fields keep their defaults. Not clamped.
static func from_dict(data: Dictionary) -> GILJoystickConfig:
	var result := GILJoystickConfig.new()
	result.floating = str(data.get("mode", "fixed")) == "floating"
	result.radius = float(data.get("radius", result.radius))
	result.dead_zone = float(data.get("deadZone", result.dead_zone))
	result.outer_dead_zone = float(data.get("outerDeadZone", result.outer_dead_zone))
	result.curve = float(data.get("curve", result.curve))
	result.sensitivity = float(data.get("sensitivity", result.sensitivity))
	result.smoothing = float(data.get("smoothing", result.smoothing))
	return result


func to_dict() -> Dictionary:
	return {
		"mode": "floating" if floating else "fixed",
		"radius": radius,
		"deadZone": dead_zone,
		"outerDeadZone": outer_dead_zone,
		"curve": curve,
		"sensitivity": sensitivity,
		"smoothing": smoothing,
	}


## Read a JSON export from the app, or a bare config object. Out-of-range values are clamped.
## Returns null (and prints an error) when the text is not valid JSON or has another format.
static func from_json(text: String) -> GILJoystickConfig:
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
