using Godot;

namespace MedievalNightmare.Core;

/// <summary>
/// Dónde se lleva puesto un objeto. Son las tres ranuras del diseño y no hay
/// más: un arma, escudo o antorcha, y un amuleto. Lo que vale para todas es que
/// solo cabe UNA cosa, y de ahí sale que llevar antorcha sea renunciar al
/// escudo.
/// </summary>
public enum EquipSlot
{
	/// <summary>Se lleva en la bolsa y no se pone: pociones, botín y poco más.</summary>
	None,

	/// <summary>Un arma.</summary>
	Main,

	/// <summary>Escudo o antorcha.</summary>
	Secondary,

	/// <summary>Una habilidad.</summary>
	Amulet,
}

/// <summary>
/// Cualquier cosa que ocupe un hueco. Es deliberadamente pobre: un nombre y
/// dónde se pone.
///
/// No lleva peso, ni tamaño, ni rareza, ni nivel, ni precio. El diseño no tiene
/// economía ni mejora de equipo (§10), así que un objeto no necesita ningún
/// número propio: lo que hace lo dice su clase derivada —un arma tiene alcance y
/// arco— y lo que vale lo decide el jugador al elegir si extrae con ello o sigue.
///
/// El identificador de un objeto es la ruta de su <c>.tres</c>: es lo que se
/// escribe en el alijo y lo que se vuelve a cargar al entrar. Por eso todo
/// objeto tiene que existir como archivo en disco, no valen los creados a mano.
/// </summary>
[GlobalClass]
public partial class ItemData : Resource
{
	[Export] public string DisplayName { get; set; } = "Objeto";

	[Export] public EquipSlot Slot { get; set; } = EquipSlot.None;

	/// <summary>
	/// Lo que se ve tirado por el suelo. Vacío, un arma se dibuja con el modelo
	/// que ya usa en la mano y se le esconde el puño: son las mismas piezas, y
	/// tenerlas apuntadas en dos sitios es garantizar que un día dejen de
	/// parecerse.
	/// </summary>
	[Export] public PackedScene WorldModel { get; set; }
}
