class_name GILLandscapeLayout
extends RefCounted
## Control positions, button sizes and stick parameters exported by one of the app's landscape labs.
## Mirrors src/landscape/layout.ts and the export in src/export/landscapeExport.ts. GILSafeAreaLayout holds
## the placement rules.

const JSON_FORMAT := "game-input-lab/landscape-layout"
const SafeArea := preload("gil_safe_area_layout.gd")
const Aim := preload("gil_aim_behavior.gd")

## "gamepad" (stick and buttons) or "dualStick".
var lab := ""
## Each: { "id": String, "anchor": Vector2 (fraction of the safe area, y up from the bottom), "visual": float, "hit": float }.
## Visual and hit are button diameters in points; a stick is as wide as its ring.
var controls: Array = []
## Stick id ("left", "right") to GILJoystickConfig.
var sticks := {}
## Only the "dualStick" lab exports it; other layouts get the defaults.
var aim := Aim.Config.new()


## Read a JSON export from the app. Stick parameters and aim behavior are clamped to the app's limits.
## Returns null (and prints an error) when the text is not valid or has another format.
static func from_json(text: String) -> GILLandscapeLayout:
	var parsed: Variant = JSON.parse_string(text)
	if not (parsed is Dictionary):
		push_error("Game Input Lab: the JSON is empty or not an object.")
		return null
	var data: Dictionary = parsed
	if str(data.get("format", "")) != JSON_FORMAT:
		push_error("Game Input Lab: unsupported format \"%s\". Expected \"%s\"." % [data.get("format", ""), JSON_FORMAT])
		return null
	var layout := GILLandscapeLayout.new()
	layout.lab = str(data.get("lab", ""))
	for item in data.get("controls", []):
		layout.controls.append({
			"id": str(item["id"]),
			"anchor": Vector2(clampf(float(item.get("anchorX", 0.5)), 0.0, 1.0), clampf(float(item.get("anchorY", 0.5)), 0.0, 1.0)),
			"visual": float(item.get("visual", 0.0)),
			"hit": float(item.get("hit", 0.0)),
		})
	for item in data.get("sticks", []):
		layout.sticks[str(item["id"])] = GILJoystickConfig.from_dict(item).normalized()
	var aim_data: Variant = data.get("aim", {})
	if aim_data is Dictionary:
		layout.aim = Aim.Config.from_dict(aim_data).normalized()
	return layout


## The control with this id, or an empty Dictionary.
func control(id: String) -> Dictionary:
	for item in controls:
		if item["id"] == id:
			return item
	return {}


## The stick's parameters, or null if the layout has no such stick.
func stick(id: String) -> GILJoystickConfig:
	return sticks.get(id, null)


## Centre of a control in a safe area given in points (y up, origin bottom left). Sticks use their fitted radius.
func center_of(id: String, safe: Rect2) -> Vector2:
	var item := control(id)
	assert(not item.is_empty(), "The layout has no control \"%s\"." % id)
	var extent: float = float(item["hit"]) / 2.0
	if sticks.has(id):
		extent = SafeArea.stick_radius(stick(id).radius, safe)
	return SafeArea.center(safe, item["anchor"], extent)


## Place and size `target` inside `safe_root` (a Control that covers the area the layout was made for, with its
## top left as origin). `units_per_point` is how many of the Control's units make one point, for example
## GILSafeAreaLayout.pixels_per_point(). A button's optional `hit_area` takes the touch diameter; it may be a
## child of `target` or a sibling. Controls use y down, so the layout's y up is flipped here.
func apply(safe_root: Control, id: String, target: Control, units_per_point := 1.0, hit_area: Control = null) -> void:
	assert(units_per_point > 0.0, "units_per_point must be above 0.")
	var item := control(id)
	assert(not item.is_empty(), "The layout has no control \"%s\"." % id)
	var safe_points := Rect2(Vector2.ZERO, safe_root.size / units_per_point)
	var visual: float = item["visual"]
	var hit: float = item["hit"]
	if sticks.has(id):
		visual = SafeArea.stick_radius(stick(id).radius, safe_points) * 2.0
		hit = visual
	var centre_up := center_of_in(id, safe_points, hit / 2.0) * units_per_point
	var centre := Vector2(centre_up.x, safe_root.size.y - centre_up.y)
	_place(target, centre, visual * units_per_point)
	if hit_area == null:
		return
	if hit_area.get_parent() == target:
		_place(hit_area, target.size / 2.0, hit * units_per_point)
	else:
		_place(hit_area, centre, hit * units_per_point)


func center_of_in(id: String, safe: Rect2, extent: float) -> Vector2:
	return SafeArea.center(safe, control(id)["anchor"], extent)


# The control's top left is its centre minus half its size.
static func _place(rect: Control, centre: Vector2, size: float) -> void:
	rect.size = Vector2(size, size)
	rect.position = centre - rect.size / 2.0
