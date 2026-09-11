extends Node3D

# Herramienta de desarrollo. Comprueba que la pausa y el final de la incursion
# hacen lo que dicen, que es lo unico de la interfaz que no se puede mirar en
# una captura.
#   godot --headless --path . tools/Ui.tscn
#
# Se pulsa Escape de verdad —con `parse_input_event`, no con `action_press`, que
# solo cambia el estado del boton y no llega a `_unhandled_input`— y se mira el
# arbol: si esta pausado, si el menu esta visible y si al morir entra el negro.

const ROOM := preload("res://src/Levels/TestRoom.tscn")

var _ok := true


func _ready() -> void:
	add_child(ROOM.instantiate())
	call_deferred("_run")


func _run() -> void:
	await _frames(6)

	var player: Node = get_tree().get_first_node_in_group("player")
	var menu: CanvasLayer = player.get_node("PauseMenu")
	var death: CanvasLayer = player.get_node("RaidEnd")

	_check(not menu.visible, "el menu arranca escondido")
	_check(not death.visible, "el final arranca escondido")

	_escape()
	_check(get_tree().paused, "Escape pausa el arbol")
	_check(menu.visible, "Escape ensena el menu")

	_escape()
	_check(not get_tree().paused, "Escape otra vez despausa")
	_check(not menu.visible, "Escape otra vez esconde el menu")

	# El inventario es lo otro que para el juego, y tiene que llevarse bien con la
	# pausa: si los dos se abren a la vez, se pelean por el raton y por quien
	# despausa.
	var bag: CanvasLayer = player.get_node("InventoryScreen")
	_check(not bag.visible, "el inventario arranca escondido")

	_key(KEY_I)
	_check(bag.visible, "I abre el inventario")
	_check(get_tree().paused, "el inventario para el arbol")

	_key(KEY_I)
	_check(not bag.visible, "I otra vez lo cierra")
	_check(not get_tree().paused, "cerrarlo despausa")

	_key(KEY_I)
	_escape()
	_check(not bag.visible, "Escape tambien cierra el inventario")
	_check(not menu.visible, "y no deja la pausa abierta detras")

	# Muerto no se pausa: la pantalla de fin ya esta contando y trae su propio
	# reinicio, asi que dejar abrir la pausa encima son dos reinicios pedidos.
	player.get_node("Health").ApplyDamage(999.0, Vector3.ZERO, false)
	await _frames(4)

	_check(death.visible, "morir ensena el negro")

	_escape()
	_check(not get_tree().paused, "muerto, Escape no pausa")

	# El negro tiene que ENTRAR, no ponerse de golpe. Sin ventana los fotogramas
	# vuelan, asi que se espera por reloj y no por fotogramas.
	var fade: ColorRect = death.get_node("Fade")
	_check(fade.color.a < 0.2, "el negro empieza claro (%.2f)" % fade.color.a)

	await get_tree().create_timer(0.8).timeout
	var middle := fade.color.a
	_check(middle > 0.05 and middle < 0.99, "a media cuenta esta a medio entrar (%.2f)" % middle)

	# Se sale antes de que la pantalla de fin recargue la escena: aqui la
	# escena actual es esta herramienta, y recargarla seria empezar de cero.
	print("RESULTADO: %s" % ("bien" if _ok else "mal"))
	get_tree().quit(0 if _ok else 1)


func _escape() -> void:
	_key(KEY_ESCAPE)


func _key(code: int) -> void:
	# Se empuja al viewport a mano. Sin ventana, el servidor de pantalla no reparte
	# eventos por el arbol —solo quedan como estado del boton—, asi que
	# `Input.parse_input_event` no llega a `_unhandled_input` y la prueba diria que
	# la pausa no funciona cuando si funciona. Y una TECLA, no un
	# `InputEventAction`: asi se comprueba tambien que la accion esta bien atada.
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = true
	get_viewport().push_input(event)
	await _frames(3)

	event = InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = false
	get_viewport().push_input(event)
	await _frames(3)


func _check(condition: bool, what: String) -> void:
	if condition:
		print("bien: %s" % what)
	else:
		print("FALLO: %s" % what)
		_ok = false


func _frames(count: int) -> void:
	for i in count:
		await get_tree().process_frame
