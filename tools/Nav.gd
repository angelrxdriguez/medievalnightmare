extends Node3D

# Herramienta de desarrollo. Comprueba que los esqueletos ANDAN por la sala en
# vez de empujar un pilar, y que se reparten el turno de ataque.
#   godot --headless --path . tools/Nav.tscn
#
# Se mira lo mismo que se miraria jugando, pero sin ojos: cuanto se acercan,
# cuantos entran a pegar a la vez y si alguno se queda clavado. Es la unica
# forma de saber si la navegacion funciona sin abrir el juego.

const LEVELS := {
	"cripta": "res://src/Levels/TestRoom.tscn",
	"cementerio": "res://src/Levels/Graveyard.tscn",
}

# Dos sitios y no uno. Con la regla de densidad del diseno —nueve metros entre
# enemigos— no hay ningun punto de la sala desde el que se despierten los tres,
# que es justamente de lo que va la regla. Asi que el cebo se mueve: primero al
# lado norte, que llama a dos, y luego al oeste, que llama al tercero.
const BAITS := {
	"cripta": [
		[Vector3(2, 0.05, 1), 7.0],
		[Vector3(-6, 0.05, 4), 7.0],
	],
	"cementerio": [
		[Vector3(-5, 0.05, 4), 6.0],
		[Vector3(5, 0.05, -4), 6.0],
		[Vector3(-4, 0.05, -13), 6.0],
	],
}

# Hasta donde tiene que llegar andando de frente desde donde nace. Es la
# comprobacion de que se puede ENTRAR: un vano estrecho o un tapon de CSG
# dejan al jugador encerrado en un tubo oscuro sin que nada de errores.
const WALK_TO := {
	"cripta": -14.0,
	"cementerio": 16.0,
}

var _player: Node3D
var _skeletons: Array[Node3D] = []
var _moved := {}
var _closest := {}
var _last := {}
var _most_pressing := 0


var _level := "cripta"


func _ready() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() > 0 and LEVELS.has(args[0]):
		_level = args[0]

	add_child(load(LEVELS[_level]).instantiate())
	call_deferred("_run")


func _run() -> void:
	await _frames(5)

	var region: NavigationRegion3D = find_child("Navigation", true, false)
	var polygons := region.navigation_mesh.get_polygon_count()
	print("malla de navegacion: %d poligonos" % polygons)

	if polygons == 0:
		print("FALLO: sin malla no hay nada que comprobar")
		get_tree().quit(1)
		return

	_player = get_tree().get_first_node_in_group("player")

	# Lo primero: que se pueda ENTRAR. El jugador nace en el pasillo, y un vano
	# demasiado estrecho o un escalon en el umbral lo dejan encerrado en un tubo
	# oscuro sin que nada de errores. Se anda hacia delante y se mira si cruza.
	var start := _player.global_position
	Input.action_press("move_forward")
	await _watch(4.0)
	Input.action_release("move_forward")

	print("el jugador salio de %.1f a %.1f en Z" % [start.z, _player.global_position.z])

	# Cada nivel se entra en un sentido —a la cripta se entra hacia +Z y al
	# cementerio hacia -Z—, asi que no vale comparar contra un umbral a secas: se
	# mira si ha REBASADO la referencia yendo hacia ella.
	var target: float = WALK_TO[_level]
	if (_player.global_position.z - target) * (target - start.z) <= 0.0:
		print("FALLO: el jugador no llega a entrar en el nivel")
		get_tree().quit(1)
		return

	# El jugador sale de la capa de fisica. No es por hacer trampa: sin esto, en
	# los catorce segundos de la prueba se come siete golpes, se muere y recarga
	# la escena a mitad de medida. Lo que se comprueba aqui es que llegan, no
	# cuanto pegan.
	_player.collision_layer = 0

	for node in get_tree().get_nodes_in_group("enemies"):
		_skeletons.append(node)
		_moved[node] = 0.0
		_closest[node] = 9999.0
		_last[node] = node.global_position

	for bait in BAITS[_level]:
		_player.global_position = bait[0]
		await _watch(bait[1])

	print("---")
	var ok := true

	for s in _skeletons:
		print("%s: recorrio %.1f m, se puso a %.2f m" % [s.name, _moved[s], _closest[s]])

		if _closest[s] > 2.0:
			print("FALLO: %s no llego a pegar (se quedo a %.2f m)" % [s.name, _closest[s]])
			ok = false

	print("maximo de esqueletos entrando a pegar a la vez: %d" % _most_pressing)

	if _most_pressing > 1:
		print("FALLO: entraron %d a la vez y el turno es de uno" % _most_pressing)
		ok = false

	print("RESULTADO: %s" % ("bien" if ok else "mal"))
	get_tree().quit(0 if ok else 1)


func _watch(seconds: float) -> void:
	for i in int(seconds * Engine.physics_ticks_per_second):
		await get_tree().physics_frame

		var pressing := 0
		for s in _skeletons:
			if not is_instance_valid(s):
				continue

			pressing += 1 if s.IsPressing else 0
			_moved[s] += s.global_position.distance_to(_last[s])
			_last[s] = s.global_position
			_closest[s] = min(_closest[s], _flat(s.global_position, _player.global_position))

		_most_pressing = max(_most_pressing, pressing)


func _flat(a: Vector3, b: Vector3) -> float:
	return Vector2(a.x - b.x, a.z - b.z).length()


func _frames(count: int) -> void:
	for i in count:
		await get_tree().physics_frame
