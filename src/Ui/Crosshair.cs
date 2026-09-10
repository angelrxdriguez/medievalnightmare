using Godot;
using MedievalNightmare.Core;
using MedievalNightmare.Player;

namespace MedievalNightmare.Ui;

/// <summary>
/// La mira. En primera persona hace dos trabajos que antes hacían dos cosas
/// distintas del muñeco.
///
/// El primero es apuntar: la ballesta suelta el virote exactamente por donde
/// pasa la cruz, así que sin ella no hay forma de adelantarle el tiro a nada.
///
/// El segundo es la telegrafía de tus propios golpes, que antes llevaba el cubo
/// flotante sobre tu cabeza y que en primera persona no se ve. Los colores son
/// los mismos que usan los enemigos (<see cref="TelegraphMarker"/>), así que la
/// lectura no cambia de bando a bando: ámbar es anticipación, rojo es que el
/// filo ya está fuera, azul es recuperación.
///
/// El hueco central no es decorativo: sin él la mira tapa justo al enemigo que
/// estás intentando medir.
/// </summary>
public partial class Crosshair : Control
{
	[Export] public Color IdleColor { get; set; } = new(0.92f, 0.90f, 0.84f, 0.55f);
	[Export] public Color WindupColor { get; set; } = new(0.95f, 0.72f, 0.12f);
	[Export] public Color ActiveColor { get; set; } = new(0.88f, 0.14f, 0.10f);
	[Export] public Color RecoveryColor { get; set; } = new(0.28f, 0.44f, 0.70f);

	[Export] public float ArmLength { get; set; } = 7.0f;

	/// <summary>Distancia del centro a la que empieza cada brazo.</summary>
	[Export] public float Gap { get; set; } = 4.0f;

	[Export] public float Thickness { get; set; } = 2.0f;

	private PlayerController _player;
	private CombatPhase _phase = CombatPhase.Idle;

	public override void _Ready()
	{
		// Por grupo y no por ruta: la mira no tiene por qué colgar del jugador, y
		// así puede mudarse al HUD del nivel sin tocar nada.
		_player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
		Resized += QueueRedraw;
	}

	public override void _Process(double delta)
	{
		CombatPhase phase = _player?.Phase ?? CombatPhase.Idle;

		// Redibujar solo al cambiar de fase. Son cuatro líneas, pero repintarlas
		// cada fotograma para nada es exactamente el tipo de gasto que no se ve.
		if (phase == _phase)
		{
			return;
		}

		_phase = phase;
		QueueRedraw();
	}

	public override void _Draw()
	{
		Vector2 center = Size * 0.5f;
		Color color = PhaseColor();

		DrawArm(center, Vector2.Up, color);
		DrawArm(center, Vector2.Down, color);
		DrawArm(center, Vector2.Left, color);
		DrawArm(center, Vector2.Right, color);
	}

	private void DrawArm(Vector2 center, Vector2 direction, Color color)
	{
		DrawLine(center + direction * Gap, center + direction * (Gap + ArmLength), color, Thickness);
	}

	private Color PhaseColor()
	{
		return _phase switch
		{
			CombatPhase.Windup => WindupColor,
			CombatPhase.Active => ActiveColor,
			CombatPhase.Recovery => RecoveryColor,
			_ => IdleColor,
		};
	}
}
