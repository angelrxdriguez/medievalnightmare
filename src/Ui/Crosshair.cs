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

	/// <summary>Lo que se abre la cruz al conectar un golpe. Es la confirmación
	/// de impacto: sin ella, con niebla y a contraluz no sabes si has dado.</summary>
	[Export] public float PulsePixels { get; set; } = 5.0f;

	[Export] public float PulseFadeSeconds { get; set; } = 0.16f;

	private PlayerController _player;
	private CombatPhase _phase = CombatPhase.Idle;
	private float _pulse;

	public override void _Ready()
	{
		// Por grupo y no por ruta: la mira no tiene por qué colgar del jugador, y
		// así puede mudarse al HUD del nivel sin tocar nada.
		_player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
		Resized += QueueRedraw;

		if (_player != null)
		{
			_player.MeleeHit += OnMeleeHit;
		}
	}

	private void OnMeleeHit(bool heavy)
	{
		_pulse = 1.0f;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (_pulse > 0.0f)
		{
			_pulse = Mathf.Max(0.0f, _pulse - (float)delta / PulseFadeSeconds);
			QueueRedraw();
		}

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
		float gap = Gap + _pulse * PulsePixels;

		DrawLine(center + direction * gap, center + direction * (gap + ArmLength), color, Thickness);
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
