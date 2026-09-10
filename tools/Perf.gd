extends Node3D

# Herramienta de desarrollo. Mide fps en siete encuadres de la sala de pruebas,
# los mismos que cita ARTE.md, para poder comparar antes y despues.
#   godot --path . tools/Perf.tscn

const ROOM := preload("res://src/Levels/TestRoom.tscn")

const SPOTS := [
	[Vector3(0, 0.05, 11), 0.0],
	[Vector3(0, 0.05, 4), 0.0],
	[Vector3(-8, 0.05, 0), -1.2],
	[Vector3(8, 0.05, -6), 2.4],
	[Vector3(0, 0.05, -10), 3.14],
	[Vector3(-11, 0.05, -11), 0.8],
	[Vector3(11, 0.05, 10), 3.9],
]


func _ready() -> void:
	# Sin vsync. Con el sincronismo puesto, cualquier maquina que llegue holgada
	# marca 60 clavados en los siete encuadres y la medida no dice nada: no se ve
	# lo que cuesta un cambio hasta que ya es tarde y baja de 60.
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	Engine.max_fps = 0

	add_child(ROOM.instantiate())
	call_deferred("_run")


func _run() -> void:
	await _frames(90)
	var player: Node = get_tree().get_first_node_in_group("player")
	var head: Node3D = player.get_node("Head")
	var low := 999.0
	var high := 0.0

	for spot in SPOTS:
		player.global_position = spot[0]
		head.rotation = Vector3(-0.05, spot[1], 0)
		await _frames(30)

		# Se cronometran 120 fotogramas y se divide, en vez de promediar
		# `get_frames_per_second()`: ese contador se actualiza una vez por segundo, asi
		# que muestreandolo 40 veces seguidas se lee 40 veces el mismo numero y los
		# siete encuadres salen identicos.
		var start := Time.get_ticks_usec()
		await _frames(120)
		var fps := 120.0 * 1000000.0 / float(Time.get_ticks_usec() - start)
		low = min(low, fps)
		high = max(high, fps)
		print("encuadre %s: %.1f fps" % [spot[0], fps])

	print("RANGO: %.0f - %.0f fps" % [low, high])
	get_tree().quit()


func _frames(count: int) -> void:
	for i in count:
		await get_tree().process_frame
