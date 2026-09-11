extends Node3D

# Herramienta de desarrollo, no forma parte del juego. Arranca la sala de
# pruebas, coloca al jugador, pulsa botones y guarda capturas en user://.
#   godot --path . tools/Capture.tscn

const ROOM := preload("res://src/Levels/TestRoom.tscn")

# Las armas ya no se cambian con las teclas 1-4: se equipan desde el inventario,
# que es lo unico que las lleva desde M3.
const SWORD := preload("res://src/Player/Weapons/EspadaCorta.tres")
const MACE := preload("res://src/Player/Weapons/Maza.tres")
const GREATSWORD := preload("res://src/Player/Weapons/Mandoble.tres")
const CROSSBOW := preload("res://src/Player/Weapons/Ballesta.tres")

const SAVE := "user://alijo.json"
const BACKUP := "user://alijo.json.bak"

var _shot := 0
var _inventory: Node


func _ready() -> void:
	# Equiparse escribe en la partida guardada, y esta herramienta se pone las
	# cuatro armas: sin apartar el alijo, hacer capturas te cambia lo que llevabas
	# puesto. Se devuelve al terminar.
	_stash_aside()

	add_child(ROOM.instantiate())
	call_deferred("_run")


func _run() -> void:
	await _frames(40)
	var player: Node = get_tree().get_first_node_in_group("player")
	_inventory = player.get_node("Inventory")

	# Fuera las salidas. Esta herramienta se planta en la sala y luego vuelve al
	# pasillo a fotografiar el arcon, que es justo la maniobra que extrae: sin
	# esto, la incursion termina a mitad de sesion de fotos.
	for zone in get_tree().get_nodes_in_group("extraccion"):
		zone.set_physics_process(false)

	# Dentro de la sala y no donde empiece la partida. El jugador arranca en el
	# pasillo, que es un tubo de 2,4 m: con la antorcha en la mano, las paredes
	# salen quemadas y las armas no se ven contra ellas.
	player.global_position = Vector3(0, 0.05, 6)
	player.get_node("Head").rotation = Vector3(-0.04, 0, 0)
	await _frames(10)

	await _swing("espada", "attack_light", 8, 3)
	_equip(MACE)
	await _frames(30)
	await _swing("maza", "attack_light", 8, 3)
	_equip(GREATSWORD)
	await _frames(30)
	await _swing("mandoble", "attack_heavy", 10, 9)
	_equip(CROSSBOW)
	await _frames(30)
	await _swing("ballesta", "attack_light", 8, 6)

	# Guardia y esquiva, que tambien tienen pose.
	_equip(SWORD)
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

	await _inventory_shots(player)

	_stash_back()
	get_tree().quit()


# El inventario es lo unico de M3 que hay que MIRAR, y no se ve jugando: para el
# juego, asi que una captura de la sala no lo enseña nunca. Dos tomas, porque son
# dos pantallas distintas: la de la mazmorra y la de delante del arcon, que es la
# unica que enseña el alijo.
func _inventory_shots(player: Node3D) -> void:
	_key(KEY_I)
	await _frames(8)
	await _shoot("inventario")
	_key(KEY_I)
	await _frames(8)

	player.global_position = Vector3(0, 0.05, -19)
	_inventory.StoreInStash(0)
	await _frames(8)

	_key(KEY_I)
	await _frames(8)
	await _shoot("inventario_arcon")
	_key(KEY_I)
	await _frames(8)


# Una tecla de verdad y no `Input.action_press`: lo segundo solo cambia el estado
# del boton y no llega a `_unhandled_input`, que es quien abre el inventario.
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


func _stash_aside() -> void:
	if not FileAccess.file_exists(SAVE):
		return

	var dir := DirAccess.open("user://")
	if dir != null:
		dir.rename(SAVE, BACKUP)


func _stash_back() -> void:
	var dir := DirAccess.open("user://")
	if dir == null:
		return

	if FileAccess.file_exists(SAVE):
		dir.remove(SAVE)

	if FileAccess.file_exists(BACKUP):
		dir.rename(BACKUP, SAVE)


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


# Meter en la bolsa y ponerse lo ultimo que ha entrado, que es lo que hace el
# jugador con la pantalla de inventario abierta. Acaba dejando un arma repetida
# en la bolsa y da igual: aqui no se mira la bolsa, se miran las poses.
func _equip(item: Resource) -> void:
	_inventory.TryAdd(item)
	_inventory.Equip(_inventory.Carried - 1)


func _frames(count: int) -> void:
	for i in count:
		await get_tree().process_frame


func _shoot(name: String) -> void:
	await RenderingServer.frame_post_draw
	var image := get_viewport().get_texture().get_image()
	_shot += 1
	image.save_png("user://shot_%s.png" % name)
	print("captura: user://shot_%s.png" % name)
