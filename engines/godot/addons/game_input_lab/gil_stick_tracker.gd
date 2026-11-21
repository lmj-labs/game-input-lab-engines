class_name GILStickTracker
extends RefCounted
## Which finger owns the stick, and how the stick lets go. Input-system agnostic: feed it every known
## touch once per frame. Mirrors src/components/stickTracker.ts.
##
## A touch is a Dictionary: { "id": int, "position": Vector2 (+y up), "phase": String }, where phase is
## "began", "moved", "stationary", "ended" or "canceled". Positions use +y up, the same as the app and the
## other engine packages; GILVirtualJoystick flips Godot's y for you.

const Response := preload("gil_joystick_response.gd")

## Emitted when the stick lets go: "ended", "canceled", "lost" or "interrupted".
signal released(reason: String)

var config := GILJoystickConfig.new()
## Touch area in the same space as the touch positions. Only touches that begin inside it are taken.
## The right and bottom edges are exclusive.
var area := Rect2()
## Touch-space units per point: 1 for points, pixels per point for screen pixels.
var units_per_point := 1.0

## -1 when no finger is tracked.
var finger_id := -1
var origin := Vector2.ZERO
var point := Vector2.ZERO
## Smoothed output, length 0 to 1, +y up.
var output := Vector2.ZERO
## Release caused by the latest step, track or interrupt call, or "" for none.
var last_release := ""


func _init(initial: GILJoystickConfig = null) -> void:
	if initial != null:
		config = initial


func is_pressed() -> bool:
	return finger_id != -1


## Unsmoothed output for the current finger position.
func target() -> Vector2:
	if finger_id == -1:
		return Vector2.ZERO
	var scale := units_per_point if units_per_point > 0.0 else 1.0
	return Response.evaluate((point - origin) / scale, config)


## One frame: track the touches, then advance smoothing.
func step(touches: Array, delta_ms: float) -> void:
	track(touches)
	advance(delta_ms)


func track(touches: Array) -> void:
	last_release = ""
	if finger_id != -1:
		var index := _index_of(touches, finger_id)
		if index < 0 or touches[index]["phase"] == "began":
			_release("lost")
		elif touches[index]["phase"] == "ended":
			_release("ended")
		elif touches[index]["phase"] == "canceled":
			_release("canceled")
		else:
			point = touches[index]["position"]
	if finger_id == -1:
		for touch in touches:
			if touch["phase"] != "began" or not area.has_point(touch["position"]):
				continue
			finger_id = touch["id"]
			point = touch["position"]
			origin = touch["position"] if config.floating else area.get_center()
			break


func advance(delta_ms: float) -> void:
	output = Response.smooth(output, target(), delta_ms, config.smoothing)


## The app lost focus or was paused: drop the finger and zero the output.
func interrupt() -> void:
	last_release = ""
	if finger_id != -1:
		_release("interrupted")
	else:
		output = Vector2.ZERO


## Back to idle without emitting released.
func reset() -> void:
	finger_id = -1
	origin = Vector2.ZERO
	point = Vector2.ZERO
	output = Vector2.ZERO
	last_release = ""


func _release(reason: String) -> void:
	finger_id = -1
	last_release = reason
	if reason != "ended":
		output = Vector2.ZERO
	released.emit(reason)


static func _index_of(touches: Array, id: int) -> int:
	for i in touches.size():
		if touches[i]["id"] == id:
			return i
	return -1
