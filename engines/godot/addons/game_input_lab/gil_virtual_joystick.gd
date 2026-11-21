class_name GILVirtualJoystick
extends Control
## On-screen virtual joystick driven by GILStickTracker. Read `value` each frame for movement (length 0 to 1,
## +y up). The Control's rectangle is the touch area: a touch must begin inside it to take the stick, and in fixed
## mode the stick centre is the centre of the rectangle.
##
## Touches come from InputEventScreenTouch and InputEventScreenDrag. To try it with a mouse on desktop, turn on
## Project Settings > Input Devices > Pointing > Emulate Touch From Mouse. A cancelled touch needs Godot 4.2 or
## newer (InputEventScreenTouch.canceled).

const Tracker := preload("gil_stick_tracker.gd")
const SafeArea := preload("gil_safe_area_layout.gd")

## Raised when the stick lets go, with the reason: "ended", "canceled", "lost" or "interrupted".
signal released(reason: String)

## JSON exported from Game Input Lab (the Joystick Lab export). When set, it replaces `config` on _ready.
@export_multiline var preset_json := ""
## Screen pixels per point. 0: estimate from the screen DPI / 160. Set it if the stick feels larger or smaller
## than in the app on a given device.
@export var pixels_per_point_override := 0.0

## The stick parameters. Assigning a new one takes effect at once.
var config := GILJoystickConfig.new():
	set(value):
		config = value if value != null else GILJoystickConfig.new()
		if tracker != null:
			tracker.config = config
var tracker: GILStickTracker

## Smoothed stick output, length 0 to 1, +y up.
var value: Vector2:
	get:
		return tracker.output if tracker != null else Vector2.ZERO

# id -> { "position": Vector2 (screen y down), "phase": String }
var _touches := {}


func _ready() -> void:
	if Engine.is_editor_hint():
		return
	if preset_json != "":
		var loaded := GILJoystickConfig.from_json(preset_json)
		if loaded != null:
			config = loaded
	tracker = Tracker.new(config)
	tracker.released.connect(func(reason: String) -> void: released.emit(reason))
	set_process(true)


func _input(event: InputEvent) -> void:
	if Engine.is_editor_hint():
		return
	if event is InputEventScreenTouch:
		var touch := event as InputEventScreenTouch
		if touch.pressed:
			_touches[touch.index] = { "position": touch.position, "phase": "began" }
		elif _touches.has(touch.index):
			_touches[touch.index]["position"] = touch.position
			_touches[touch.index]["phase"] = "canceled" if touch.canceled else "ended"
	elif event is InputEventScreenDrag:
		var drag := event as InputEventScreenDrag
		if _touches.has(drag.index) and _touches[drag.index]["phase"] != "began":
			_touches[drag.index]["position"] = drag.position
			_touches[drag.index]["phase"] = "moved"
		elif _touches.has(drag.index):
			_touches[drag.index]["position"] = drag.position


func _process(delta: float) -> void:
	if tracker == null:
		return
	var rect := get_global_rect()
	# The tracker works with +y up; the screen has +y down.
	tracker.area = Rect2(rect.position.x, -rect.end.y, rect.size.x, rect.size.y)
	tracker.units_per_point = SafeArea.pixels_per_point(pixels_per_point_override)
	var list := []
	for id in _touches:
		var item: Dictionary = _touches[id]
		list.append({ "id": id, "position": Vector2(item["position"].x, -item["position"].y), "phase": item["phase"] })
	tracker.step(list, delta * 1000.0)
	# Ended and cancelled touches are gone after the frame that reported them; a touch that began is now held.
	for id in _touches.keys():
		var phase: String = _touches[id]["phase"]
		if phase == "ended" or phase == "canceled":
			_touches.erase(id)
		elif phase == "began":
			_touches[id]["phase"] = "stationary"


func _notification(what: int) -> void:
	if tracker == null:
		return
	# Losing focus or pausing releases the stick at once, the same rule as the other engine packages.
	if what == NOTIFICATION_APPLICATION_FOCUS_OUT or what == NOTIFICATION_WM_WINDOW_FOCUS_OUT or what == NOTIFICATION_PAUSED:
		_touches.clear()
		tracker.interrupt()
