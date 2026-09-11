extends Node3D

# Herramienta de usar y tirar: carga la arena de combate, monta una pelea con
# las teclas de test y guarda capturas en user://. No forma parte del juego.
#   godot --path . tools/Arena.tscn


func _ready() -> void:
	add_child(load("res://src/Levels/CombatArena.tscn").instantiate())
	call_deferred("_run")


func _run() -> void:
	await _frames(50)
	await _shoot("arena_vista")

	var player: Node3D = get_tree().get_first_node_in_group("player")

	# Delante del muñeco y con la espada puesta: un ligero (chispas, pulso de
	# mira, numero) y un pesado (respingo grande y numero gordo).
	player.global_position = Vector3(-8, 0.05, 5.5)
	player.get_node("Head").rotation = Vector3(-0.1, 0, 0)
	_key(KEY_F1)
	await _frames(40)

	Engine.time_scale = 0.12
	Input.action_press("attack_light")
	await _frames(2)
	Input.action_release("attack_light")
	for i in 6:
		await _shoot("dummy_ligero_%02d" % i)
		await _frames(3)
	Engine.time_scale = 1.0
	await _frames(30)

	Input.action_press("attack_heavy")
	await _frames(2)
	Input.action_release("attack_heavy")
	await _frames(30)
	Engine.time_scale = 0.12
	for i in 6:
		await _shoot("dummy_pesado_%02d" % i)
		await _frames(4)
	Engine.time_scale = 1.0
	await _frames(40)
	await _shoot("dummy_numeros")

	# Media vuelta hacia campo abierto —el muñeco no puede hacer de pared— y un
	# esqueleto por la tecla de test, acercandose y encajando un pesado.
	player.get_node("Head").rotation = Vector3(-0.05, PI, 0)
	await _frames(5)
	_key(KEY_K)
	await _frames(30)
	await _shoot("arena_esqueleto_lejos")
	await _frames(70)
	await _shoot("arena_esqueleto_cerca")

	Input.action_press("attack_heavy")
	await _frames(2)
	Input.action_release("attack_heavy")
	await _frames(38)
	await _shoot("arena_esqueleto_pesado")
	await _frames(12)
	await _shoot("arena_esqueleto_stagger")
	await _frames(60)
	await _shoot("arena_esqueleto_final")

	get_tree().quit()


func _key(code: int) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = true
	get_viewport().push_input(event)
	await _frames(2)

	event = InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = false
	get_viewport().push_input(event)


func _frames(count: int) -> void:
	for i in count:
		await get_tree().process_frame


func _shoot(name: String) -> void:
	await RenderingServer.frame_post_draw
	var image := get_viewport().get_texture().get_image()
	image.save_png("user://shot_%s.png" % name)
	print("captura: user://shot_%s.png" % name)
