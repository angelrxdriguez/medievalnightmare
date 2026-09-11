extends Node3D

# Herramienta de desarrollo. Recorre el bucle de M3 entero sin manos:
#   godot --headless --path . tools/Raid.tscn
#
# Comprueba lo que dice el criterio del hito, en el mismo orden: entro con
# espada, cojo una maza, extraigo, y lo guardado dice que vuelvo a entrar con la
# maza. Lo ultimo no se puede mirar jugando sin jugar otra vez, asi que se mira
# el archivo, que es lo que de verdad decide con que bajas.
#
# Con `-- muerte` hace el otro final, que es el que de verdad importa que este
# bien: morir tiene que costar la bolsa Y lo equipado, y dejar el alijo intacto.
# Un fallo aqui no se nota jugando hasta que alguien pierde algo que no debia.
#
# EL ALIJO DE VERDAD NO SE TOCA. La prueba escribe en el mismo archivo que la
# partida, asi que lo aparta al empezar y lo devuelve al terminar. Si esto se
# corta por la mitad, la partida se queda en user://alijo.json.bak.

const LEVEL := "res://src/Levels/TestRoom.tscn"
const SAVE := "user://alijo.json"
const BACKUP := "user://alijo.json.bak"

# La maza esta nada mas entrar en la sala y la salida al fondo del pasillo, que
# es tambien donde se nace. Recogerla no basta para armar la salida —esta a ocho
# metros y hacen falta doce—, asi que hay que meterse en la sala: que es lo que
# se pide, entrar antes de poder salir.
const MACE := Vector3(0.8, 0.05, -11.5)
const ROOM := Vector3(0, 0.05, -2)
const EXIT := Vector3(0, 0.05, -19.5)

var _player: Node3D
var _inventory: Node
var _ok := true
var _mode := "extraccion"


func _ready() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() > 0:
		_mode = args[0]

	# Antes de cargar el nivel: el inventario lee el alijo en cuanto nace.
	_stash_aside()

	add_child(load(LEVEL).instantiate())
	call_deferred("_run")


func _run() -> void:
	await _frames(5)

	_player = get_tree().get_first_node_in_group("player")
	_inventory = _player.get_node("Inventory")

	_check(_name_of(_inventory.Main) == "Espada corta", "se baja con la espada del suelo de seguridad")
	_check(_inventory.Carried == 0, "la bolsa empieza vacia")

	if _mode == "muerte":
		await _die()
		_finish()

		return

	# Se nace DENTRO de la salida, asi que lo primero es comprobar que todavia no
	# funciona. Una salida armada desde el primer fotograma terminaria la
	# incursion antes de jugarla.
	_player.global_position = EXIT
	await _frames(8)
	_check(not _ending(), "la salida no se dispara sin haber entrado")

	_player.global_position = MACE
	await _frames(8)
	_check(_inventory.Carried == 1, "la maza del suelo entra en la bolsa al pisarla")

	_inventory.Equip(0)
	_check(_name_of(_inventory.Main) == "Maza", "la maza pasa a la mano")
	_check(_inventory.Carried == 1, "la espada que llevaba puesta cae en la bolsa")

	_player.global_position = ROOM
	await _frames(8)
	_check(not _ending(), "estar dentro no extrae: la salida esta en el pasillo")

	_player.global_position = EXIT
	await _frames(8)
	_check(_ending(), "volver al pasillo despues de haber entrado extrae")

	var saved := _read_save()
	var loadout: String = saved.get("equipado", {}).get("principal", "")
	var stored: Array = saved.get("alijo", [])

	_check(loadout.ends_with("Maza.tres"), "lo equipado sobrevive: se vuelve a bajar con la maza")
	_check(stored.size() == 1 and String(stored[0]).ends_with("EspadaCorta.tres"), "lo de la bolsa queda en el alijo")

	_finish()


# Morir con algo encima. Lo que se mira no es la pantalla —de eso ya va
# `tools/Ui.tscn`— sino lo que queda escrito: perder la bolsa es media mecanica
# del juego y es lo unico que no se puede deshacer.
func _die() -> void:
	_player.global_position = MACE
	await _frames(8)
	_inventory.Equip(0)

	_check(_name_of(_inventory.Main) == "Maza", "se muere con la maza puesta y la espada en la bolsa")

	_player.get_node("Health").ApplyDamage(999.0, Vector3.ZERO, false)
	await _frames(8)

	_check(_inventory.Carried == 0, "morir vacia la bolsa")
	_check(_name_of(_inventory.Main) == "Espada corta", "vuelve la espada del suelo de seguridad")

	var saved := _read_save()

	_check(saved.get("alijo", []).is_empty(), "el alijo sigue vacio: no se guarda lo que se pierde")
	_check(String(saved.get("equipado", {}).get("principal", "")).ends_with("EspadaCorta.tres"), "lo guardado dice que se vuelve a bajar con la espada")


func _ending() -> bool:
	var screen := get_tree().get_first_node_in_group("raid_end")

	return screen != null and screen.IsEnding


func _read_save() -> Dictionary:
	var file := FileAccess.open(SAVE, FileAccess.READ)
	if file == null:
		_check(false, "el alijo se ha escrito en disco")

		return {}

	var parsed = JSON.parse_string(file.get_as_text())

	return parsed if parsed is Dictionary else {}


func _name_of(item) -> String:
	return item.DisplayName if item != null else "(nada)"


func _check(ok: bool, what: String) -> void:
	print("%s  %s" % ["OK   " if ok else "FALLO", what])

	if not ok:
		_ok = false


func _finish() -> void:
	# La extraccion para el arbol entero. Salir con el juego en pausa deja la
	# ventana congelada un instante antes de cerrarse y parece que ha petado.
	get_tree().paused = false

	_stash_back()

	print("RESULTADO: %s" % ("bien" if _ok else "mal"))
	get_tree().quit(0 if _ok else 1)


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


func _frames(count: int) -> void:
	for i in count:
		await get_tree().physics_frame
