extends Node3D

# Herramienta de desarrollo, no forma parte del juego. Pone un esqueleto solo
# para poder mirarlo sin la mazmorra encima.
#   godot --path . tools/Rig.tscn
#
# Hace dos pasadas y las dos hacen falta:
#
# - RETRATO. El bicho quieto, desde cuatro lados y un primer plano de la cabeza.
#   Es donde se ve la silueta y donde se juzgan las proporciones.
# - MAZMORRA. Lo mismo con la luz del juego —una antorcha cálida, ambiente casi
#   nulo y niebla— porque un esqueleto que se lee con luz de estudio puede
#   perderse entero en la sala de verdad, que es donde va a estar.
#
# Y luego el ciclo de marcha, el ataque y el derrumbe, que es para lo que se
# escribió esto.

const SKELETON := preload("res://src/Enemies/Skeleton/Skeleton.tscn")

var _skeleton: CharacterBody3D
var _bait: Node3D
var _camera: Camera3D
var _key: OmniLight3D
var _fill: OmniLight3D
var _environment: Environment
var _follow := true


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

	_fill = OmniLight3D.new()
	_fill.position = Vector3(-3.0, 2.0, 1.0)
	_fill.light_energy = 3.0
	_fill.light_color = Color(0.6, 0.7, 1.0)
	_fill.omni_range = 14.0
	add_child(_fill)

	var env := WorldEnvironment.new()
	_environment = Environment.new()
	_environment.background_mode = Environment.BG_COLOR
	_environment.background_color = Color(0.05, 0.06, 0.09)
	_environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	_environment.ambient_light_color = Color(0.3, 0.34, 0.45)
	_environment.ambient_light_energy = 0.35
	env.environment = _environment
	add_child(env)

	call_deferred("_run")


func _process(_delta: float) -> void:
	if _skeleton == null or not _follow:
		return
	var target := _skeleton.global_position
	_camera.global_position = target + Vector3(3.2, 1.15, 0.9)
	_camera.look_at(target + Vector3(0, 0.85, 0), Vector3.UP)
	_key.global_position = target + Vector3(2.6, 2.4, 2.6)


# Camara quieta alrededor del bicho. El angulo va en grados alrededor del eje Y
# —0 es DE FRENTE, o sea desde -Z, que es a donde mira el bicho: en Godot el
# frente de un Node3D es -Z y poner la camara en +Z retrata la nuca— y la altura
# y la distancia van en metros.
func _aim(degrees: float, distance: float, height: float, look_at_height: float, fov: float) -> void:
	_follow = false
	var pivot := _skeleton.global_position
	var angle := deg_to_rad(degrees)
	_camera.fov = fov
	_camera.global_position = pivot + Vector3(sin(angle) * distance, height, -cos(angle) * distance)
	_camera.look_at(pivot + Vector3(0, look_at_height, 0), Vector3.UP)


# La luz de la mazmorra de verdad: una antorcha calida a la altura de la cintura
# y a un lado, ambiente casi nulo, niebla azulada. Los numeros salen de ARTE.md.
func _dungeon_light() -> void:
	var pivot := _skeleton.global_position
	_key.light_color = Color(1.0, 0.72, 0.40)
	_key.light_energy = 3.2
	_key.omni_range = 8.0
	_key.omni_attenuation = 1.6
	_key.shadow_enabled = true
	_key.global_position = pivot + Vector3(1.5, 1.5, -2.2)

	_fill.light_color = Color(0.56, 0.68, 0.95)
	_fill.light_energy = 0.35
	_fill.global_position = pivot + Vector3(-2.5, 3.0, 1.5)

	_environment.background_color = Color(0.04, 0.045, 0.062)
	_environment.ambient_light_color = Color(0.26, 0.33, 0.50)
	_environment.ambient_light_energy = 0.06
	_environment.fog_enabled = true
	_environment.fog_light_color = Color(0.05, 0.058, 0.082)
	_environment.fog_density = 0.05
	_environment.volumetric_fog_enabled = true
	_environment.volumetric_fog_density = 0.03
	_environment.volumetric_fog_albedo = Color(0.05, 0.058, 0.082)


func _studio_light() -> void:
	var pivot := _skeleton.global_position
	_key.global_position = pivot + Vector3(2.2, 2.6, -2.6)
	_key.light_color = Color(1, 1, 1)
	_key.light_energy = 7.0
	_key.omni_range = 14.0
	_key.shadow_enabled = false
	_fill.light_energy = 3.0
	_environment.ambient_light_energy = 0.35
	_environment.fog_enabled = false
	_environment.volumetric_fog_enabled = false


# Las seis tomas del retrato: cuatro lados, un primer plano de la cabeza y el
# torso. Se usan las mismas con luz de estudio y con luz de mazmorra para poder
# comparar una contra otra.
func _portrait(prefix: String) -> void:
	await _shoot_at(prefix + "_frente", 0.0, 3.4, 1.1, 0.95, 34.0)
	await _shoot_at(prefix + "_tres_cuartos", 38.0, 3.4, 1.2, 0.95, 34.0)
	await _shoot_at(prefix + "_perfil", 90.0, 3.4, 1.1, 0.95, 34.0)
	await _shoot_at(prefix + "_espalda", 180.0, 3.4, 1.1, 0.95, 34.0)
	await _shoot_at(prefix + "_cabeza", 16.0, 1.3, 1.64, 1.6, 32.0)
	await _shoot_at(prefix + "_torso", 28.0, 1.9, 1.35, 1.25, 32.0)
	await _shoot_at(prefix + "_lejos", 25.0, 11.0, 1.7, 1.0, 34.0)


func _shoot_at(name: String, degrees: float, distance: float, height: float, look_at_height: float, fov: float) -> void:
	_aim(degrees, distance, height, look_at_height, fov)
	await _frames(2)
	await _shoot(name)


func _run() -> void:
	await _frames(30)

	# El cebo se va lejos para el retrato: el esqueleto persigue a tres metros por
	# segundo y en los dos fotogramas que van de apuntar la camara a disparar se
	# mueve diez centimetros, que a un metro de distancia saca la cabeza del cuadro.
	_bait.position = Vector3(0, 0, -60)
	await _frames(30)

	_follow = false
	_studio_light()
	await _portrait("retrato")

	_dungeon_light()
	await _frames(4)
	await _portrait("mazmorra")

	_studio_light()
	_follow = true
	_camera.fov = 40.0

	# Y el cebo vuelve a tiro: sin esto el bicho se queda quieto —el cebo esta a
	# sesenta metros y solo detecta a nueve— y las ocho tomas del paso salen todas
	# con las piernas juntas.
	_bait.position = Vector3(0, 0, -6)
	await _frames(10)

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

	# Un golpe encajado: el respingo dura 0,18 s, o sea dos fotogramas de los doce
	# a los que se escribe la pose. Se capturan cinco seguidos para verlo entrar y
	# salir.
	_skeleton.get_node("Health").ApplyDamage(6.0, Vector3.ZERO, false)
	for i in 5:
		await _shoot("golpe_%d" % i)
		await _frames(3)

	# Y se cae a trozos.
	_skeleton.get_node("Health").ApplyDamage(999.0, Vector3.ZERO, false)
	for i in 8:
		await _shoot("muerte_%d" % i)
		await _frames(6)
	await _frames(80)
	await _shoot("muerte_final")

	# Quien se ha quedado de pie. Un monton de huesos no tiene ni una pieza de
	# canto, asi que esto no deberia imprimir nada nunca: si imprime algo, es que
	# esa pieza no llego a posarse y se quedo clavada con la orientacion con la que
	# cayo. Asi se encontro que la condicion de reposo no se cumplia jamas.
	for piece in _skeleton.get_node("Visual/Debris").get_children():
		var axis := absf(piece.global_transform.basis.y.normalized().y)
		if piece.global_position.y > 0.2 and axis > 0.6:
			print("DE PIE: %s y=%.3f eje=%.2f" % [piece.name, piece.global_position.y, axis])

	get_tree().quit()


func _frames(count: int) -> void:
	for i in count:
		await get_tree().process_frame


func _shoot(name: String) -> void:
	await RenderingServer.frame_post_draw
	get_viewport().get_texture().get_image().save_png("user://rig_%s.png" % name)
	print("captura: user://rig_%s.png" % name)
