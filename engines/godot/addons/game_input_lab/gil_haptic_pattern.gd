class_name GILHapticPattern
extends RefCounted
## A custom vibration pattern from the app's Haptics Lab. Mirrors src/haptics/pattern.ts: the strength at a time, the
## Android waveform, the timings for phones that cannot set the strength, and the iOS events. JSON keys keep the app's
## camelCase names.

const JSON_FORMAT := "game-input-lab/haptic-pattern"
const MAX_STEPS := 16
const MAX_TOTAL_MS := 5000
const MIN_STEP_MS := 10
const MAX_STEP_MS := 1000
const STEP_MS := 10
const INTENSITY_STEP := 5
## Steps this short or shorter are played as a single tap (iOS transient event).
const TRANSIENT_MS := 30
const IOS_SHARPNESS := 0.5

## Each step is Vector2i(duration in ms, strength 0 to 100). Strength 0 is a pause.
var steps: Array[Vector2i] = []
## Play again from the start when the last step ends. The total length is the period, so a trailing pause stays.
var loop := false


## From a list of [duration_ms, strength] pairs, as the app's export writes it.
static func from_steps(list: Array, looping := false) -> GILHapticPattern:
	var pattern := GILHapticPattern.new()
	pattern.loop = looping
	for item in list:
		if item is Vector2i:
			pattern.steps.append(item)
		elif item is Array and item.size() >= 2:
			pattern.steps.append(Vector2i(int(item[0]), int(item[1])))
	return pattern


static func _snap(value: int, step: int, low: int, high: int) -> int:
	return clampi(int(floor(float(value) / float(step) + 0.5)) * step, low, high)


## A copy within the app's limits: 1 to 16 steps, 10 to 1000 ms in tens, 0 to 100 in fives, 5 s in all. Never empty.
func normalized() -> GILHapticPattern:
	var result := GILHapticPattern.new()
	result.loop = loop
	var total := 0
	for i in mini(steps.size(), MAX_STEPS):
		var room := MAX_TOTAL_MS - total
		if room < MIN_STEP_MS:
			break
		var duration := _snap(steps[i].x, STEP_MS, MIN_STEP_MS, MAX_STEP_MS)
		duration = mini(duration, room - room % STEP_MS)
		result.steps.append(Vector2i(duration, _snap(steps[i].y, INTENSITY_STEP, 0, 100)))
		total += duration
	if result.steps.is_empty():
		result.steps.append(Vector2i(30, 100))
	return result


func duration_ms() -> int:
	var total := 0
	for item in steps:
		total += item.x
	return total


## Strength from 0 to 1 at time_ms after the start. 0 before the start and from the end on.
func intensity_at(time_ms: float) -> float:
	if time_ms < 0.0:
		return 0.0
	var start := 0.0
	for item in steps:
		if time_ms < start + float(item.x):
			return float(item.y) / 100.0
		start += float(item.x)
	return 0.0


## The repeat argument of Android's createWaveform and Vibrator.vibrate(long[], repeat): 0 loops from the start, -1 plays once.
func android_repeat() -> int:
	return 0 if loop else -1


## Android 8+ VibrationEffect.createWaveform(timings, amplitudes, android_repeat()): {"timings": [...], "amplitudes": [...]}.
## Amplitude 0 is off, 1 to 255 is the strength.
func android_waveform() -> Dictionary:
	var timings: Array[int] = []
	var amplitudes: Array[int] = []
	for item in steps:
		timings.append(item.x)
		amplitudes.append(0 if item.y <= 0 else clampi(int(floor(float(item.y * 255 + 50) / 100.0)), 1, 255))
	return { "timings": timings, "amplitudes": amplitudes }


## For phones that cannot set the strength: off, on, off, on, starting with off. Any strength above 0 is on.
func legacy_timings() -> Array[int]:
	var timings: Array[int] = []
	var on := false
	for item in steps:
		var step_on := item.y > 0
		if timings.is_empty() and step_on:
			timings.append(0)
		if not timings.is_empty() and step_on == on:
			timings[timings.size() - 1] += item.x
		else:
			timings.append(item.x)
		on = step_on
	return timings


## CoreHaptics events: [{"kind": "transient" or "continuous", "time", "duration", "intensity", "sharpness"}], seconds.
func ios_events() -> Array[Dictionary]:
	var events: Array[Dictionary] = []
	var start := 0
	for item in steps:
		if item.y > 0:
			var transient := item.x <= TRANSIENT_MS
			events.append({
				"kind": "transient" if transient else "continuous",
				"time": float(start) / 1000.0,
				"duration": 0.0 if transient else float(item.x) / 1000.0,
				"intensity": float(item.y) / 100.0,
				"sharpness": IOS_SHARPNESS,
			})
		start += item.x
	return events


## Read a JSON export from the app, or a bare object with a steps list. Returns null on a bad file.
static func from_json(text: String) -> GILHapticPattern:
	var parsed: Variant = JSON.parse_string(text)
	if not (parsed is Dictionary):
		push_error("Game Input Lab: the JSON is empty or not an object.")
		return null
	var data: Dictionary = parsed
	if data.has("format") and str(data["format"]) != JSON_FORMAT:
		push_error("Game Input Lab: unsupported format \"%s\". Expected \"%s\"." % [data["format"], JSON_FORMAT])
		return null
	var pattern := GILHapticPattern.new()
	pattern.loop = data.get("loop", false) == true
	var list: Variant = data.get("steps", [])
	if list is Array:
		for item in list:
			if item is Dictionary:
				pattern.steps.append(Vector2i(int(item.get("durationMs", 0)), int(item.get("intensity", 0))))
	return pattern.normalized()
