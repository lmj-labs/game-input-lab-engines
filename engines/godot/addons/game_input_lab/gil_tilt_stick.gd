class_name GILTiltStick
extends RefCounted
## A tilt stick: remembers the neutral pose and the smoothed output. Feed it the gravity reading described in GILTilt.

const Response := preload("gil_joystick_response.gd")
const Tilt := preload("gil_tilt.gd")

var config := GILTiltConfig.new()
## The pose that means "no tilt": Vector2(roll, pitch) in degrees.
var neutral := Vector2.ZERO
## Smoothed output, length 0 to 1, +y up.
var output := Vector2.ZERO


func _init(initial: GILTiltConfig = null) -> void:
	if initial != null:
		config = initial


## Take this reading as the pose that means "no tilt".
func calibrate(gravity: Vector3) -> void:
	neutral = Tilt.angles(gravity)


## One frame with the latest reading. Returns the smoothed output.
func step(gravity: Vector3, delta_ms: float) -> Vector2:
	var target := Tilt.target(Tilt.angles(gravity), neutral, config)
	output = Response.smooth(output, target, delta_ms, config.smoothing)
	return output


func reset() -> void:
	output = Vector2.ZERO
