extends Node3D

# Herramienta de desarrollo. Encuadres fijos de un nivel, para poder MIRARLO sin
# jugarlo: se coloca al jugador en cada sitio, se apunta la vista y se guarda un
# PNG en user://.
#   godot --path . tools/Look.tscn -- cripta
#   godot --path . tools/Look.tscn -- cementerio
#
# Es la unica forma de comparar dos versiones de la iluminacion de una sala: a
# ojo y jugando, la memoria de como se veia hace diez minutos no vale nada.

const LEVELS := {
	"cripta": "res://src/Levels/TestRoom.tscn",
	"cementerio": "res://src/Levels/Graveyard.tscn",
}

# [nombre, posicion, giro, cabeceo, fov]. El giro es en radianes de mundo: 0 mira
# al norte (-Z) y PI al sur (+Z). El fov a 0 deja el del juego; bajarlo es un
# teleobjetivo y sirve para mirar de cerca lo que se lleva en la mano, que en el
# encuadre normal ocupa una esquina.
const SHOTS := {
	"cripta": [
		["pasillo", Vector3(0, 0.05, -19.5), PI, -0.02],
		["puerta", Vector3(0, 0.05, -15.5), PI, -0.02],
		["entrada", Vector3(0, 0.05, -12), PI, -0.03],
		["columna", Vector3(4, 0.05, -3), 2.4, -0.05],
		["esqueleto", Vector3(0.5, 0.05, -10), PI, -0.02],
		["rincon", Vector3(-10, 0.05, 9), 2.0, -0.04],
		["reja", Vector3(-6, 0.05, -1), 0.0, 0.55],
		["mano", Vector3(0, 0.05, -11), PI, -0.1],
	],
	"cementerio": [
		["portal", Vector3(0, 0.05, 25.2), 0.0, 0.0],
		["senda", Vector3(0, 0.05, 14), 0.0, -0.02],
		["lapidas", Vector3(-7, 0.05, 2), 2.3, -0.06],
		["cripta", Vector3(6, 0.05, -2), -0.5, 0.05],
		["arbol", Vector3(-11, 0.05, -8), 1.2, 0.22],
		["luna", Vector3(0, 0.05, -2), -0.4, 0.3],
		["hierba", Vector3(3, 0.05, 6), 2.9, -0.35],
	],
}

var _level := "cripta"


func _ready() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() > 0 and LEVELS.has(args[0]):
		_level = args[0]

	add_child(load(LEVELS[_level]).instantiate())
	call_deferred("_run")


func _run() -> void:
	# Margen para que hornee la navegacion, arranquen las particulas y la niebla
	# volumetrica converja: es temporal y en el primer fotograma no existe.
	await _frames(70)

	var player: Node3D = get_tree().get_first_node_in_group("player")

	# El nodo del jugador puede venir girado del nivel —es su orientacion de
	# entrada—, y aqui estorba: se pone a cero para que el giro de cada encuadre
	# sea en coordenadas de mundo y no relativo a por donde entrase.
	player.global_rotation = Vector3.ZERO

	# Fuera la interfaz. Lo que se mira aqui es la sala, y el HUD de depuracion
	# ocupa un cuarto de la pantalla con numeros que no dicen nada de como se ve.
	player.get_node("DebugHud").visible = false
	player.get_node("Hud").visible = false

	var head: Node3D = player.get_node("Head")
	var camera: Camera3D = head.get_node("Camera")
	var fov := camera.fov

	for shot in SHOTS[_level]:
		player.global_position = shot[1]
		head.rotation = Vector3(shot[3], shot[2], 0)
		camera.fov = shot[4] if shot.size() > 4 and shot[4] > 0.0 else fov

		# El encuadre "mano" es el unico que no mira la sala: trae el arma delante
		# de la camara. Acercarse con el fov no sirve —el arma cuelga de la camara
		# a una distancia fija, asi que estrechar el angulo la saca de cuadro en vez
		# de agrandarla— y en el encuadre de juego el puno ocupa una esquina.
		if shot[0] == "mano":
			_pose_weapon(player)

		await _frames(24)
		await _shoot("%s_%s" % [_level, shot[0]])

	get_tree().quit()


func _pose_weapon(player: Node3D) -> void:
	var hand: Node3D = player.get_node("Head/WeaponHand")
	if hand.get_child_count() == 0:
		return

	# La pose de reposo del modelo, que es de donde parte WeaponView cada
	# fotograma. Tocandola aqui el arma se queda donde se le diga sin desactivar
	# nada de la animacion.
	var model: Node3D = hand.get_child(0)
	model.RestPosition = Vector3(0.07, -0.16, -0.4)
	model.RestRotationDegrees = Vector3(-38, -34, 8)


func _frames(count: int) -> void:
	for i in count:
		await get_tree().process_frame


func _shoot(name: String) -> void:
	await RenderingServer.frame_post_draw
	var image := get_viewport().get_texture().get_image()
	image.save_png("user://look_%s.png" % name)
	print("captura: user://look_%s.png" % name)
