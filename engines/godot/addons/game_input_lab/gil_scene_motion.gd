class_name GILSceneMotion
extends RefCounted
## The reference scene's motion from Joystick Lab: how a character driven by the stick moves, and how the camera
## follows. Mirrors src/components/sceneMotion.ts. World units, +y up.

const Response := preload("gil_joystick_response.gd")
const Aim := preload("gil_aim_behavior.gd")

## Longest frame that still counts, so a stall does not throw the character across the floor.
const MAX_FRAME_MS := 100.0


class Params:
	extends RefCounted
	## Units per second at full stick (1 to 10).
	var max_speed := 4.0
	## Units per second squared: how fast the velocity follows the stick (2 to 100).
	var acceleration := 20.0
	## Degrees per second the character can turn (90 to 1080).
	var turn_rate := 720.0
	## Time constant of the camera follow in milliseconds. 0 locks the camera to the character (0 to 400).
	var camera_lag := 120.0

	## Read the app's scene object (keys maxSpeed, acceleration, turnRate, cameraLag). Not clamped.
	static func from_dict(data: Dictionary) -> Params:
		var result := Params.new()
		result.max_speed = float(data.get("maxSpeed", result.max_speed))
		result.acceleration = float(data.get("acceleration", result.acceleration))
		result.turn_rate = float(data.get("turnRate", result.turn_rate))
		result.camera_lag = float(data.get("cameraLag", result.camera_lag))
		return result

	## A copy clamped to the app's limits. Mirrors normalizeScene.
	func normalized() -> Params:
		var result := Params.new()
		result.max_speed = clampf(roundf(max_speed * 10.0) / 10.0, 1.0, 10.0)
		result.acceleration = clampf(roundf(acceleration), 2.0, 100.0)
		result.turn_rate = clampf(roundf(turn_rate), 90.0, 1080.0)
		result.camera_lag = clampf(roundf(camera_lag), 0.0, 400.0)
		return result

	## Read the "scene" of a JSON export from the app. A file without one gives the defaults.
	static func from_json(text: String) -> Params:
		var parsed: Variant = JSON.parse_string(text)
		if not (parsed is Dictionary):
			push_error("Game Input Lab: the JSON is empty or not an object.")
			return null
		var scene: Variant = (parsed as Dictionary).get("scene", {})
		return from_dict(scene if scene is Dictionary else {}).normalized()


## The character and camera. Positions in world units, heading in radians (y up).
class State:
	extends RefCounted
	var position := Vector2.ZERO
	var velocity := Vector2.ZERO
	var heading := PI / 2.0
	var camera := Vector2.ZERO


## At the origin, facing up, at rest.
static func start() -> State:
	return State.new()


## The shortest signed turn from one angle to another, in (-PI, PI]. Mirrors angleDiff.
static func angle_diff(from: float, to: float) -> float:
	var diff := fmod(to - from, 2.0 * PI)
	if diff > PI:
		return diff - 2.0 * PI
	if diff <= -PI:
		return diff + 2.0 * PI
	return diff


## One frame. `output` is the stick output (+y up, length 0 to 1). Speed follows power, so half power moves at
## half speed. Returns a new State; the one passed in is not changed. Mirrors stepScene.
static func step(state: State, output: Vector2, delta_ms: float, params: Params) -> State:
	if not (delta_ms > 0.0):
		return state
	var frame_ms := minf(MAX_FRAME_MS, delta_ms)
	var dt := frame_ms / 1000.0

	var target := output * params.max_speed
	var delta := target - state.velocity
	var gap := delta.length()
	var reach := params.acceleration * dt
	var velocity := target if gap <= reach else state.velocity + delta / gap * reach

	var heading := state.heading
	if output.length() > Aim.ENGAGED:
		var turn := angle_diff(heading, atan2(output.y, output.x))
		var most := deg_to_rad(params.turn_rate) * dt
		heading += clampf(turn, -most, most)

	var result := State.new()
	result.velocity = velocity
	result.heading = heading
	result.position = state.position + velocity * dt
	result.camera = Response.smooth(state.camera, result.position, frame_ms, params.camera_lag)
	return result
