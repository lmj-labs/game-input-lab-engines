extends SceneTree
## Replays the test vectors the app generates from its own code (scripts/joystick-vectors.mjs): the same inputs
## must give the same outputs in Godot and in the app. A failure here means Godot and the app no longer feel
## the same.
##
## Run from the repository root (Godot 4.2 or newer). The first time, let Godot register the addon's classes:
##   godot --headless --path engines/godot --import
##   godot --headless --path engines/godot -s tests/run_vectors.gd
## The exit code is 1 when anything fails.

const FIXTURE := "res://tests/fixtures/joystick-vectors.json"
const Tracker := preload("res://addons/game_input_lab/gil_stick_tracker.gd")
const Response := preload("res://addons/game_input_lab/gil_joystick_response.gd")
const SafeArea := preload("res://addons/game_input_lab/gil_safe_area_layout.gd")
const Layout := preload("res://addons/game_input_lab/gil_landscape_layout.gd")
const Aim := preload("res://addons/game_input_lab/gil_aim_behavior.gd")
const Scene := preload("res://addons/game_input_lab/gil_scene_motion.gd")
const Tilt := preload("res://addons/game_input_lab/gil_tilt.gd")
const TiltStick := preload("res://addons/game_input_lab/gil_tilt_stick.gd")
const HapticPattern := preload("res://addons/game_input_lab/gil_haptic_pattern.gd")

# Vector2 holds 32-bit floats in Godot; the app computes in 64-bit.
const TOLERANCE := 1e-4
# Smoothing snaps onto the target within 0.001, so the two may snap one frame apart.
const SMOOTHING_TOLERANCE := 1e-3
# The scene scripts run for a few hundred frames.
const SCENE_TOLERANCE := 2e-3

var checks := 0
var failures := 0


func _init() -> void:
	var text := FileAccess.get_file_as_string(FIXTURE)
	var parsed: Variant = JSON.parse_string(text)
	if not (parsed is Dictionary):
		printerr("Cannot read %s. Run `npm run vectors` in the app repo." % FIXTURE)
		quit(1)
		return
	var v: Dictionary = parsed
	_expect(int(v["version"]) == 1, "vector file version")
	_fields_and_defaults(v)
	_response(v)
	_smoothing(v)
	_json_export(v)
	_touch_scenarios(v)
	_layout(v)
	_aim(v)
	_scene(v)
	_tilt(v)
	_haptics(v)
	print("%d checks, %d failed" % [checks, failures])
	quit(1 if failures > 0 else 0)


func _expect(condition: bool, label: String) -> void:
	checks += 1
	if not condition:
		failures += 1
		printerr("FAIL " + label)


func _near(expected: float, actual: float, tolerance: float, label: String) -> void:
	checks += 1
	if absf(expected - actual) > tolerance:
		failures += 1
		printerr("FAIL %s: expected %s, got %s" % [label, expected, actual])


func _same_config(expected: GILJoystickConfig, actual: GILJoystickConfig, label: String) -> void:
	_expect(expected.floating == actual.floating, label + " floating")
	_near(expected.radius, actual.radius, 1e-6, label + " radius")
	_near(expected.dead_zone, actual.dead_zone, 1e-6, label + " deadZone")
	_near(expected.outer_dead_zone, actual.outer_dead_zone, 1e-6, label + " outerDeadZone")
	_near(expected.curve, actual.curve, 1e-6, label + " curve")
	_near(expected.sensitivity, actual.sensitivity, 1e-6, label + " sensitivity")
	_near(expected.smoothing, actual.smoothing, 1e-6, label + " smoothing")


func _fields_and_defaults(v: Dictionary) -> void:
	var fields: Array = v["fields"]
	_expect(fields.size() == GILJoystickConfig.RANGES.size(), "field count")
	for field in fields:
		var key: String = field["key"]
		_expect(GILJoystickConfig.RANGES.has(key), "field " + key)
		if not GILJoystickConfig.RANGES.has(key):
			continue
		var spec: Array = GILJoystickConfig.RANGES[key]
		_near(float(field["min"]), float(spec[0]), 0.0, key + " min")
		_near(float(field["max"]), float(spec[1]), 0.0, key + " max")
		_expect(int(field["decimals"]) == int(spec[2]), key + " decimals")
	_same_config(GILJoystickConfig.from_dict(v["defaults"]), GILJoystickConfig.new(), "defaults")


func _response(v: Dictionary) -> void:
	for item in v["curve"]:
		var config := GILJoystickConfig.from_dict(item["config"])
		_near(float(item["expected"]), Response.response_curve(float(item["deflection"]), config), TOLERANCE,
			"curve at %s" % item["deflection"])
	for item in v["response"]:
		var config := GILJoystickConfig.from_dict(item["config"])
		var actual := Response.evaluate(Vector2(float(item["offsetX"]), float(item["offsetY"])), config)
		var label := "response at (%s, %s)" % [item["offsetX"], item["offsetY"]]
		_near(float(item["outputX"]), actual.x, TOLERANCE, label + " x")
		_near(float(item["outputY"]), actual.y, TOLERANCE, label + " y")


func _smoothing(v: Dictionary) -> void:
	for item in v["smoothing"]:
		var actual := Response.smooth(
			Vector2(float(item["currentX"]), float(item["currentY"])), Vector2(float(item["targetX"]), float(item["targetY"])),
			float(item["deltaMs"]), float(item["smoothingMs"]))
		var label := "smooth %s ms of %s ms" % [item["deltaMs"], item["smoothingMs"]]
		_near(float(item["outputX"]), actual.x, SMOOTHING_TOLERANCE, label + " x")
		_near(float(item["outputY"]), actual.y, SMOOTHING_TOLERANCE, label + " y")


func _json_export(v: Dictionary) -> void:
	var sample: Dictionary = v["exportSample"]
	var parsed := GILJoystickConfig.from_json(sample["json"])
	_expect(parsed != null, "export sample parses")
	if parsed != null:
		_same_config(GILJoystickConfig.from_dict(sample["config"]), parsed, "export sample")
	# A config clamps to the app's limits and rounds like the app.
	var wild := GILJoystickConfig.new()
	wild.radius = 500.0
	wild.curve = 1.26
	wild.sensitivity = -4.0
	var clamped := wild.normalized()
	_near(96.0, clamped.radius, 0.0, "radius clamps")
	_near(1.3, clamped.curve, 1e-9, "curve rounds to one decimal")
	_near(0.5, clamped.sensitivity, 0.0, "sensitivity clamps")


func _touch_scenarios(v: Dictionary) -> void:
	var scenarios: Array = v["scenarios"]
	_expect(scenarios.size() > 0, "touch scenarios exist")
	for scenario in scenarios:
		var tracker := Tracker.new(GILJoystickConfig.from_dict(scenario["config"]))
		var area: Dictionary = scenario["area"]
		tracker.area = Rect2(float(area["x"]), float(area["y"]), float(area["width"]), float(area["height"]))
		tracker.units_per_point = float(scenario["unitsPerPoint"])
		var index := 0
		for frame in scenario["frames"]:
			var label := "\"%s\" frame %d" % [scenario["name"], index]
			if frame["interrupt"]:
				tracker.interrupt()
			else:
				var touches := []
				for touch in frame["touches"]:
					touches.append({ "id": int(touch["id"]), "position": Vector2(float(touch["x"]), float(touch["y"])), "phase": touch["phase"] })
				tracker.step(touches, float(frame["deltaMs"]))
			_expect(int(frame["fingerId"]) == tracker.finger_id, label + " finger")
			_expect(str(frame["released"]) == tracker.last_release, label + " release")
			_near(float(frame["outputX"]), tracker.output.x, SMOOTHING_TOLERANCE, label + " output x")
			_near(float(frame["outputY"]), tracker.output.y, SMOOTHING_TOLERANCE, label + " output y")
			index += 1


func _layout(v: Dictionary) -> void:
	for item in v["layout"]:
		var screen: Dictionary = item["screen"]
		var insets: Dictionary = item["insets"]
		var safe := SafeArea.safe_rect(float(screen["width"]), float(screen["height"]),
			float(insets["top"]), float(insets["left"]), float(insets["right"]), float(insets["bottom"]))
		var expected: Dictionary = item["safe"]
		_near(float(expected["x"]), safe.position.x, 1e-3, item["name"] + " safe x")
		_near(float(expected["y"]), safe.position.y, 1e-3, item["name"] + " safe y")
		_near(float(expected["width"]), safe.size.x, 1e-3, item["name"] + " safe width")
		_near(float(expected["height"]), safe.size.y, 1e-3, item["name"] + " safe height")
		for control in item["controls"]:
			var label := "%s %s" % [item["name"], control["id"]]
			var extent := SafeArea.stick_radius(float(control["size"]), safe) if control["kind"] == "stick" else float(control["size"]) / 2.0
			_near(float(control["extent"]), extent, 1e-3, label + " extent")
			var centre := SafeArea.center(safe, Vector2(float(control["anchorX"]), float(control["anchorY"])), extent)
			_near(float(control["centerX"]), centre.x, 1e-3, label + " x")
			_near(float(control["centerY"]), centre.y, 1e-3, label + " y")

	var export: Dictionary = v["landscapeExport"]
	var layout := Layout.from_json(export["json"])
	_expect(layout != null, "landscape export parses")
	if layout != null:
		_expect(layout.lab == "gamepad", "landscape lab")
		_expect(layout.controls.size() == export["controls"].size(), "landscape control count")
		for expected in export["controls"]:
			var control := layout.control(expected["id"])
			_expect(not control.is_empty(), "control " + str(expected["id"]))
			if control.is_empty():
				continue
			_near(float(expected["anchorX"]), control["anchor"].x, 1e-5, expected["id"] + " anchor x")
			_near(float(expected["anchorY"]), control["anchor"].y, 1e-5, expected["id"] + " anchor y")
			_near(float(expected.get("visual", 0.0)), control["visual"], 1e-5, expected["id"] + " visual")
			_near(float(expected.get("hit", 0.0)), control["hit"], 1e-5, expected["id"] + " hit")
		for expected in export["sticks"]:
			var stick := layout.stick(expected["id"])
			_expect(stick != null, "stick " + str(expected["id"]))
			if stick != null:
				_same_config(GILJoystickConfig.from_dict(expected), stick, "stick " + str(expected["id"]))
		var defaults := layout.aim
		_expect(defaults.release == "hold" and defaults.fire_threshold == 40.0, "a layout without aim gets the defaults")


func _aim(v: Dictionary) -> void:
	var cases: Array = v["aimBehavior"]
	_expect(cases.size() > 0, "aim cases exist")
	for item in cases:
		var config := Aim.Config.from_dict(item)
		var step := Aim.step(float(item["heading"]), Vector2(float(item["moveX"]), float(item["moveY"])),
			Vector2(float(item["aimX"]), float(item["aimY"])), config)
		var label: String = item["name"]
		_near(float(item["expectedHeading"]), step.heading, TOLERANCE, label + " heading")
		_expect(bool(item["expectedAiming"]) == step.aiming, label + " aiming")
		_expect(bool(item["expectedFiring"]) == step.firing, label + " firing")
		_near(float(item["expectedMoveScale"]), step.move_scale, TOLERANCE, label + " move scale")

	var dual: Dictionary = v["dualStickExport"]
	var layout := Layout.from_json(dual["json"])
	_expect(layout != null and layout.lab == "dualStick", "dual stick export parses")
	if layout != null:
		var aim: Dictionary = dual["aim"]
		_expect(layout.aim.release == str(aim["release"]), "dual stick release")
		_near(float(aim["fireThreshold"]), layout.aim.fire_threshold, 1e-5, "dual stick fire threshold")
		_near(float(aim["slowWhileAiming"]), layout.aim.slow_while_aiming, 1e-5, "dual stick slow")
	var clamped := Aim.Config.from_dict({ "release": "sideways", "fireThreshold": 5, "slowWhileAiming": 99 }).normalized()
	_expect(clamped.release == "hold" and clamped.fire_threshold == 20.0 and clamped.slow_while_aiming == 80.0, "aim config clamps")


func _scene(v: Dictionary) -> void:
	var scenarios: Array = v["sceneMotion"]
	_expect(scenarios.size() > 0, "scene scenarios exist")
	for scenario in scenarios:
		var params := Scene.Params.from_dict(scenario["parameters"])
		var state := Scene.start()
		var index := 0
		for frame in scenario["frames"]:
			state = Scene.step(state, Vector2(float(frame["outputX"]), float(frame["outputY"])), float(frame["deltaMs"]), params)
			var label := "%s frame %d" % [scenario["name"], index]
			_near(float(frame["posX"]), state.position.x, SCENE_TOLERANCE, label + " position x")
			_near(float(frame["posY"]), state.position.y, SCENE_TOLERANCE, label + " position y")
			_near(float(frame["velX"]), state.velocity.x, SCENE_TOLERANCE, label + " velocity x")
			_near(float(frame["velY"]), state.velocity.y, SCENE_TOLERANCE, label + " velocity y")
			_near(float(frame["heading"]), state.heading, SCENE_TOLERANCE, label + " heading")
			_near(float(frame["camX"]), state.camera.x, SCENE_TOLERANCE, label + " camera x")
			_near(float(frame["camY"]), state.camera.y, SCENE_TOLERANCE, label + " camera y")
			index += 1

	var export: Dictionary = v["sceneExport"]
	var from_json := Scene.Params.from_json(export["json"])
	_expect(from_json != null, "scene export parses")
	if from_json != null:
		var scene: Dictionary = export["scene"]
		_near(float(scene["maxSpeed"]), from_json.max_speed, 1e-5, "scene max speed")
		_near(float(scene["acceleration"]), from_json.acceleration, 1e-5, "scene acceleration")
		_near(float(scene["turnRate"]), from_json.turn_rate, 1e-5, "scene turn rate")
		_near(float(scene["cameraLag"]), from_json.camera_lag, 1e-5, "scene camera lag")
	_near(4.0, Scene.Params.from_json("{\"format\":\"game-input-lab/joystick\",\"config\":{}}").max_speed, 0.0, "a file without a scene gives the defaults")


func _tilt(v: Dictionary) -> void:
	var tilt: Dictionary = v["tilt"]
	var angles: Array = tilt["angles"]
	_expect(angles.size() > 0, "tilt angle cases exist")
	for item in angles:
		var actual := Tilt.angles(Vector3(float(item["x"]), float(item["y"]), float(item["z"])))
		var label := "tilt angles (%s, %s, %s)" % [item["x"], item["y"], item["z"]]
		_near(float(item["roll"]), actual.x, 1e-3, label + " roll")
		_near(float(item["pitch"]), actual.y, 1e-3, label + " pitch")

	var targets: Array = tilt["target"]
	_expect(targets.size() > 0, "tilt target cases exist")
	for item in targets:
		var config := GILTiltConfig.from_dict(item["config"])
		var actual := Tilt.target(Vector2(float(item["roll"]), float(item["pitch"])),
			Vector2(float(item["neutralRoll"]), float(item["neutralPitch"])), config)
		var label := "tilt %s (%s, %s)" % [item["config"]["orientation"], item["roll"], item["pitch"]]
		_near(float(item["outputX"]), actual.x, 2e-4, label + " x")
		_near(float(item["outputY"]), actual.y, 2e-4, label + " y")

	var export: Dictionary = tilt["export"]
	var parsed := GILTiltConfig.from_json(export["json"])
	_expect(parsed != null, "tilt export parses")
	if parsed != null:
		var expected := GILTiltConfig.from_dict(export["config"])
		_expect(parsed.orientation == expected.orientation, "tilt orientation")
		_expect(parsed.invert_x == expected.invert_x and parsed.invert_y == expected.invert_y, "tilt invert flags")
		_near(expected.max_angle, parsed.max_angle, 1e-5, "tilt max angle")
		_near(expected.dead_zone, parsed.dead_zone, 1e-5, "tilt dead zone")
		_near(expected.outer_dead_zone, parsed.outer_dead_zone, 1e-5, "tilt outer dead zone")
		_near(expected.curve, parsed.curve, 1e-5, "tilt curve")
		_near(expected.sensitivity, parsed.sensitivity, 1e-5, "tilt sensitivity")
		_near(expected.smoothing, parsed.smoothing, 1e-5, "tilt smoothing")
	var wild := GILTiltConfig.new()
	wild.max_angle = 500.0
	wild.orientation = "sideways"
	wild.curve = 1.26
	var clamped := wild.normalized()
	_near(60.0, clamped.max_angle, 0.0, "tilt max angle clamps")
	_expect(clamped.orientation == "portrait", "tilt orientation falls back")
	_near(1.3, clamped.curve, 1e-6, "tilt curve rounds to one decimal")
	# A stick calibrated flat gives half power at half the range.
	var stick := TiltStick.new(GILTiltConfig.from_dict({ "smoothing": 0, "deadZone": 0 }))
	stick.calibrate(Vector3(0.0, 0.0, -1.0))
	var half := stick.step(Vector3(sin(deg_to_rad(15.0)), 0.0, -cos(deg_to_rad(15.0))), 16.0)
	_near(0.5, half.x, 1e-4, "tilt stick half power")
	_near(0.0, half.y, 1e-4, "tilt stick no sideways drift")


func _haptics(v: Dictionary) -> void:
	var haptics: Dictionary = v["haptics"]
	_expect(int(haptics["maxSteps"]) == HapticPattern.MAX_STEPS, "haptic max steps")
	_expect(int(haptics["maxTotalMs"]) == HapticPattern.MAX_TOTAL_MS, "haptic max total")
	_expect(int(haptics["transientMs"]) == HapticPattern.TRANSIENT_MS, "haptic transient length")
	_near(float(haptics["sharpness"]), HapticPattern.IOS_SHARPNESS, 1e-6, "haptic sharpness")
	var patterns: Array = haptics["patterns"]
	_expect(patterns.size() > 0, "haptic pattern cases exist")
	for item in patterns:
		var id := str(item["id"])
		var pattern := HapticPattern.from_steps(_pairs(item["steps"]))
		_expect(pattern.duration_ms() == int(item["duration"]), id + " duration")
		for sample in item["samples"]:
			_near(float(sample["intensity"]), pattern.intensity_at(float(sample["timeMs"])), 1e-6, "%s at %s ms" % [id, sample["timeMs"]])
		var android := pattern.android_waveform()
		_expect(_ints(item["android"]["timings"]) == _ints(android["timings"]), id + " android timings")
		_expect(_ints(item["android"]["amplitudes"]) == _ints(android["amplitudes"]), id + " android amplitudes")
		_expect(_ints(item["legacy"]) == _ints(pattern.legacy_timings()), id + " legacy timings")
		var events := pattern.ios_events()
		_expect(events.size() == item["ios"].size(), id + " ios event count")
		for i in mini(events.size(), item["ios"].size()):
			var want: Dictionary = item["ios"][i]
			var label := "%s event %d" % [id, i]
			_expect(str(want["kind"]) == events[i]["kind"], label + " kind")
			_near(float(want["time"]), events[i]["time"], 1e-6, label + " time")
			_near(float(want["duration"]), events[i]["duration"], 1e-6, label + " duration")
			_near(float(want["intensity"]), events[i]["intensity"], 1e-6, label + " intensity")
			_near(float(want["sharpness"]), events[i]["sharpness"], 1e-6, label + " sharpness")
	for item in haptics["normalize"]:
		var actual := HapticPattern.from_steps(_pairs(item["input"]["steps"])).normalized().steps
		var expected: Array = item["steps"]
		_expect(actual.size() == expected.size(), "haptic normalize step count")
		for i in mini(actual.size(), expected.size()):
			_expect(actual[i].x == int(expected[i]["durationMs"]) and actual[i].y == int(expected[i]["intensity"]), "haptic normalize step %d" % i)
	var from_json := HapticPattern.from_json('{"format":"game-input-lab/haptic-pattern","steps":[{"durationMs":33,"intensity":47},{"durationMs":80,"intensity":0}]}')
	_expect(from_json != null and from_json.steps.size() == 2 and from_json.steps[0] == Vector2i(30, 45) and from_json.steps[1] == Vector2i(80, 0), "haptic JSON reads back, normalized")


func _pairs(list: Array) -> Array:
	var pairs := []
	for step in list:
		pairs.append([int(step["durationMs"]), int(step["intensity"])])
	return pairs


func _ints(list: Array) -> Array:
	var result := []
	for value in list:
		result.append(int(value))
	return result
