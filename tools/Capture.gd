extends Node3D

# Herramienta de desarrollo, no forma parte del juego. Arranca la sala de
# pruebas, coloca al jugador, pulsa botones y guarda capturas en user://.
#   godot --path . tools/Capture.tscn

const ROOM := preload("res://src/Levels/TestRoom.tscn")

var _shot := 0


func _ready() -> void:
	add_child(ROOM.instantiate())
	call_deferred("_run")


func _run() -> void:
	await _frames(40)
	var player: Node = get_tree().get_first_node_in_group("player")

	# Dentro de la sala y no donde empiece la partida. El jugador arranca en el
	# pasillo, que es un tubo de 2,4 m: con la antorcha en la mano, las paredes
	# salen quemadas y las armas no se ven contra ellas.
	player.global_position = Vector3(0, 0.05, 6)
	player.get_node("Head").rotation = Vector3(-0.04, 0, 0)
	await _frames(10)

	await _swing("espada", "attack_light", 8, 3)
	_press("weapon_2")
	await _frames(30)
	await _swing("maza", "attack_light", 8, 3)
	_press("weapon_3")
	await _frames(30)
	await _swing("mandoble", "attack_heavy", 10, 9)
	_press("weapon_4")
	await _frames(30)
	await _swing("ballesta", "attack_light", 8, 6)

	# Guardia y esquiva, que tambien tienen pose.
	_press("weapon_1")
	await _frames(30)
	Input.action_press("block")
	await _frames(14)
	await _shoot("pose_guardia")
	Input.action_release("block")
	await _frames(14)
	Input.action_press("dash")
	await _frames(2)
	Input.action_release("dash")
	await _frames(10)
	await _shoot("pose_esquiva")
	await _frames(40)

	# Un esqueleto acercandose y muriendo, ya dentro de la mazmorra.
	var skeleton: Node3D = _find_skeleton()
	skeleton.global_position = Vector3(0.6, 0.05, 4.0)
	await _frames(24)
	await _shoot("sala_esqueleto_lejos")
	await _frames(30)
	await _shoot("sala_esqueleto_cerca")
	await _frames(36)
	await _shoot("sala_esqueleto_ataque")
	skeleton.get_node("Health").ApplyDamage(999.0, Vector3.ZERO, false)
	await _frames(8)
	await _shoot("sala_muerte_a")
	await _frames(18)
	await _shoot("sala_muerte_b")
	await _frames(110)
	await _shoot("sala_muerte_monton")

	get_tree().quit()


func _swing(name: String, action: String, count: int, gap: int) -> void:
	# Guardar un PNG cuesta decimas de segundo reales, y el delta del motor es
	# tiempo real: sin frenar el reloj, entre captura y captura se va medio golpe.
	await _shoot("%s_00" % name)
	Engine.time_scale = 0.12
	Input.action_press(action)
	await _frames(2)
	Input.action_release(action)
	for i in count:
		await _shoot("%s_%02d" % [name, i + 1])
		await _frames(gap)
	Engine.time_scale = 1.0


func _find_skeleton() -> Node3D:
	for node in find_children("Skeleton*", "", true, false):
		return node
	return null


func _press(action: String) -> void:
	Input.action_press(action)
	await _frames(2)
	Input.action_release(action)


func _frames(count: int) -> void:
	for i in count:
		await get_tree().process_frame


func _shoot(name: String) -> void:
	await RenderingServer.frame_post_draw
	var image := get_viewport().get_texture().get_image()
	_shot += 1
	image.save_png("user://shot_%s.png" % name)
	print("captura: user://shot_%s.png" % name)
