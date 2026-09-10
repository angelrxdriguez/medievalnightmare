extends Node3D

# Herramienta de desarrollo, no forma parte del juego. Pone un esqueleto solo,
# con luz plana y camara de perfil, para poder mirarle el ciclo de marcha sin la
# mazmorra encima.
#   godot --path . tools/Rig.tscn

const SKELETON := preload("res://src/Enemies/Skeleton/Skeleton.tscn")

var _skeleton: CharacterBody3D
var _bait: Node3D
var _camera: Camera3D
var _key: OmniLight3D


func _ready() -> void:
	var floor_body := StaticBody3D.new()
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(40, 1, 40)
	shape.shape = box
	shape.position = Vector3(0, -0.5, 0)
	floor_body.add_child(shape)
	floor_body.collision_layer = 1
	add_child(floor_body)

	var plane := MeshInstance3D.new()
	var plane_mesh := PlaneMesh.new()
	plane_mesh.size = Vector2(40, 40)
	plane.mesh = plane_mesh
	add_child(plane)

	# El cebo va en el grupo del jugador: es lo unico que mira la IA. Se aleja a
	# la misma velocidad a la que anda el esqueleto para que no lo alcance nunca.
	_bait = Node3D.new()
	_bait.add_to_group("player")
	_bait.position = Vector3(0, 0, -6)
	add_child(_bait)

	_skeleton = SKELETON.instantiate()
	_skeleton.position = Vector3(0, 0.2, 2)
	add_child(_skeleton)

	_camera = Camera3D.new()
	_camera.fov = 40.0
	add_child(_camera)

	_key = OmniLight3D.new()
	_key.light_energy = 7.0
	_key.omni_range = 14.0
	add_child(_key)

	var fill := OmniLight3D.new()
	fill.position = Vector3(-3.0, 2.0, 1.0)
	fill.light_energy = 3.0
	fill.light_color = Color(0.6, 0.7, 1.0)
	fill.omni_range = 14.0
	add_child(fill)

	var env := WorldEnvironment.new()
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color(0.05, 0.06, 0.09)
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color(0.3, 0.34, 0.45)
	environment.ambient_light_energy = 0.35
	env.environment = environment
	add_child(env)

	call_deferred("_run")


func _process(_delta: float) -> void:
	if _skeleton == null:
		return
	var target := _skeleton.global_position
	_camera.global_position = target + Vector3(3.2, 1.15, 0.9)
	_camera.look_at(target + Vector3(0, 0.85, 0), Vector3.UP)
	_key.global_position = target + Vector3(2.6, 2.4, 2.6)


func _run() -> void:
	await _frames(30)

	# Ocho instantes repartidos por el ciclo de marcha.
	for i in 8:
		await _shoot("paso_%d" % i)
		await _frames(5)

	# Se para y ataca: el cebo se pone dentro del alcance. Se capturan doce
	# instantes seguidos para ver el gesto entero y no un fotograma suelto.
	_bait.position = _skeleton.global_position + Vector3(0, 0, -1.2)
	await _frames(22)
	for i in 12:
		await _shoot("ataque_%02d" % i)
		await _frames(4)

	# Y se cae a trozos.
	_skeleton.get_node("Health").ApplyDamage(999.0, Vector3.ZERO, false)
	for i in 8:
		await _shoot("muerte_%d" % i)
		await _frames(6)
	await _frames(80)
	await _shoot("muerte_final")

	get_tree().quit()


func _frames(count: int) -> void:
	for i in count:
		await get_tree().process_frame


func _shoot(name: String) -> void:
	await RenderingServer.frame_post_draw
	get_viewport().get_texture().get_image().save_png("user://rig_%s.png" % name)
	print("captura: user://rig_%s.png" % name)
