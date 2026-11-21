class_name GILHapticPlayer
extends Node
## Plays a GILHapticPattern with Input.vibrate_handheld, one call per step. From Godot 4.3 each step gets its own strength;
## before that every step with a strength above 0 vibrates at full strength. Godot cannot cut a vibration that has
## started, so stop() only keeps the later steps from starting. A pattern with loop set starts again from the first step
## when the last one ends, until stop().

var _steps: Array[Vector2i] = []
var _loop := false
var _index := -1
var _elapsed_ms := 0.0
var _next_at_ms := 0.0


func _ready() -> void:
	set_process(false)


func play(pattern: GILHapticPattern) -> void:
	stop()
	var normalized := pattern.normalized()
	_steps = normalized.steps
	_loop = normalized.loop
	set_process(true)


func stop() -> void:
	_steps = []
	_loop = false
	_index = -1
	_elapsed_ms = 0.0
	_next_at_ms = 0.0
	set_process(false)


func is_playing() -> bool:
	return not _steps.is_empty()


func _process(delta: float) -> void:
	_elapsed_ms += delta * 1000.0
	while _index + 1 < _steps.size() and _elapsed_ms >= _next_at_ms:
		_index += 1
		var step := _steps[_index]
		if step.y > 0:
			_vibrate(step.x, float(step.y) / 100.0)
		_next_at_ms += float(step.x)
	if _index + 1 >= _steps.size() and _elapsed_ms >= _next_at_ms:
		if _loop:
			# Start the next round, keeping the time already spent in it so the period does not drift.
			_elapsed_ms -= _next_at_ms
			_next_at_ms = 0.0
			_index = -1
		else:
			stop()


func _vibrate(duration_ms: int, strength: float) -> void:
	if int(Engine.get_version_info()["hex"]) >= 0x040300:
		Input.vibrate_handheld(duration_ms, strength)
	else:
		Input.vibrate_handheld(duration_ms)
