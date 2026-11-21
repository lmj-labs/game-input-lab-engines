class_name GILTilt
extends RefCounted
## Tilt as a stick. Each function mirrors the function of the same role in src/sensors/tilt.ts; the shared test
## vectors check they agree. The reading is the direction gravity pulls in the phone's own axes: x to the right,
## y to the top edge, z out of the screen (lying face up reads z = -1).

const Response := preload("gil_joystick_response.gd")


## The phone's tilt in degrees from one reading: Vector2(roll, pitch). Roll is positive with the right edge down,
## pitch with the top edge down. Independent of the sign of z. Mirrors gravityAngles.
static func angles(gravity: Vector3) -> Vector2:
	var roll := rad_to_deg(atan2(gravity.x, sqrt(gravity.y * gravity.y + gravity.z * gravity.z)))
	var pitch := rad_to_deg(atan2(gravity.y, sqrt(gravity.x * gravity.x + gravity.z * gravity.z)))
	return Vector2(roll, pitch)


## The tilt away from the neutral pose in degrees, turned to the screen's axes (+y up). Mirrors tiltOffset.
static func offset(tilt_angles: Vector2, neutral: Vector2, config: GILTiltConfig) -> Vector2:
	var roll := tilt_angles.x - neutral.x
	var pitch := tilt_angles.y - neutral.y
	var screen := Vector2(roll, pitch)
	if config.orientation == "topLeft":
		screen = Vector2(-pitch, roll)
	elif config.orientation == "topRight":
		screen = Vector2(pitch, -roll)
	return Vector2(-screen.x if config.invert_x else screen.x, -screen.y if config.invert_y else screen.y)


## Stick output, length 0 to 1, +y up, before smoothing. Mirrors tiltTarget.
static func target(tilt_angles: Vector2, neutral: Vector2, config: GILTiltConfig) -> Vector2:
	return Response.evaluate(offset(tilt_angles, neutral, config), config.as_joystick())
