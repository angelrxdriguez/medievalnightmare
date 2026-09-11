using Godot;
using MedievalNightmare.Core;
using MedievalNightmare.Ui;

namespace MedievalNightmare.Levels;

/// <summary>
/// La salida. Pisarla acaba la incursión: lo que llevas en la bolsa pasa al
/// alijo y no te lo puede quitar nadie.
///
/// SE ARMA SOLA, Y HASTA QUE NO SE ARMA NO EXISTE. Se entra al nivel por el
/// mismo sitio por el que se sale, así que una zona que funcionara desde el
/// primer fotograma se dispararía sin haber jugado. Se enciende cuando te has
/// alejado <see cref="ArmDistance"/>, que en la práctica es cuando has entrado
/// en la sala. No hace falta acordarse de nada: para poder salir hay que haber
/// entrado.
///
/// NO ES UN Area3D. Se mide la distancia al jugador y punto: un área pediría
/// capas, máscaras y un cuerpo que la toque, y lo que hay que saber aquí es
/// exactamente esto —cómo de lejos estás— para las dos cosas que hace este nodo.
///
/// No avisa de nada en pantalla. Que la salida sea la salida lo cuenta el nivel
/// —el pasillo por el que entraste— y no una etiqueta (`HUD.md` §7).
/// </summary>
public partial class ExtractionZone : Node3D
{
	/// <summary>
	/// Las salidas de un nivel se marcan con este grupo. Lo usan las herramientas
	/// de desarrollo para apagarlas: recorrer un nivel a teletransportes las
	/// dispara, y una herramienta que MIRA el nivel no puede terminar la partida.
	/// </summary>
	public const string Group = "extraccion";

	/// <summary>A cuántos metros del centro se extrae.</summary>
	[Export] public float Radius { get; set; } = 2.0f;

	/// <summary>
	/// Lo que hay que alejarse para que la salida se encienda. Doce metros es
	/// haber salido del pasillo y estar dentro de la sala.
	/// </summary>
	[Export] public float ArmDistance { get; set; } = 12.0f;

	private Node3D _player;
	private bool _armed;
	private bool _spent;

	public override void _Ready()
	{
		AddToGroup(Group);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_spent)
		{
			return;
		}

		// Se busca aquí y no en _Ready: el jugador puede entrar al árbol después
		// que esto, y un nulo al arrancar dejaría la salida muerta toda la partida.
		_player ??= GetTree().GetFirstNodeInGroup("player") as Node3D;

		if (_player == null || !IsInstanceValid(_player))
		{
			return;
		}

		float distance = GlobalPosition.DistanceTo(_player.GlobalPosition);

		if (!_armed)
		{
			_armed = distance > ArmDistance;
			return;
		}

		if (distance <= Radius)
		{
			Extract();
		}
	}

	private void Extract()
	{
		_spent = true;

		// Guardar ANTES de contar el final: la pantalla para el árbol, y lo que se
		// guarde después de eso se guardaría a medias o no se guardaría.
		_player.GetNodeOrNull<Inventory>("Inventory")?.Extract();

		if (GetTree().GetFirstNodeInGroup(RaidEndScreen.Group) is RaidEndScreen screen)
		{
			screen.Extract();
			return;
		}

		GD.PushWarning("No hay pantalla de fin de incursión: se ha extraído sin contarlo.");
	}
}
