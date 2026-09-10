using System.Collections.Generic;
using Godot;

namespace MedievalNightmare.Core;

/// <summary>
/// La telegrafía de un enemigo: qué va a hacer y cuándo. Sin esto no hay
/// combate, solo daño que llega.
///
/// Era un cubo gris flotando sobre la cabeza. Ahora es un <c>Node3D</c> que
/// pinta todas las mallas que cuelguen de él y alimenta una luz opcional, así
/// que se le pueden colgar los dos ojos de un cráneo y encender las cuencas.
/// El cambio no es cosmético: un cubo por encima de la cabeza obliga a mirar
/// arriba, y lo que hay que mirar es el bicho.
///
/// Los colores son los mismos que usa la mira del jugador, de modo que la
/// lectura vale para los dos bandos y no hay que aprenderla dos veces:
///
/// - Ámbar: anticipación. Le da tiempo a apartarse.
/// - Morado: anticipación imparable. La guardia no sirve, hay que esquivar.
/// - Rojo: el filo está fuera. Ya es tarde.
/// - Azul: recuperación. Es tu turno.
///
/// La anticipación late. Es lo que separa "hay algo encendido ahí" de "eso me
/// va a pegar", y en una sala a oscuras el latido se ve mucho antes que el
/// color: primero notas el parpadeo por el rabillo del ojo, luego lo miras y ya
/// distingues si es ámbar o morado. Ese medio segundo es el juego entero.
/// </summary>
public partial class TelegraphMarker : Node3D
{
	[ExportGroup("Colores")]
	[Export] public Color IdleColor { get; set; } = new(0.42f, 0.10f, 0.04f);
	[Export] public Color WindupColor { get; set; } = new(0.98f, 0.66f, 0.10f);
	[Export] public Color ActiveColor { get; set; } = new(1.0f, 0.14f, 0.06f);
	[Export] public Color RecoveryColor { get; set; } = new(0.26f, 0.44f, 0.78f);

	/// <summary>Anticipación de un ataque imparable: no se bloquea, hay que esquivarlo.</summary>
	[Export] public Color UnblockableColor { get; set; } = new(0.80f, 0.20f, 0.96f);

	[ExportGroup("Intensidad")]

	/// <summary>En reposo las cuencas están encendidas, pero apenas. Solo dicen "sigo aquí".</summary>
	[Export] public float IdleEnergy { get; set; } = 0.45f;

	[Export] public float WindupEnergy { get; set; } = 2.4f;
	[Export] public float ActiveEnergy { get; set; } = 3.4f;
	[Export] public float RecoveryEnergy { get; set; } = 0.7f;

	[ExportGroup("Latido")]

	/// <summary>Solo late la anticipación. Lo demás es luz fija: latir todo no dice nada.</summary>
	[Export] public float PulseHz { get; set; } = 5.5f;

	/// <summary>Profundidad del latido. 0,45 llega a bajar a poco menos de la mitad.</summary>
	[Export] public float PulseDepth { get; set; } = 0.45f;

	private readonly List<MeshInstance3D> _meshes = new();

	private StandardMaterial3D _material;
	private OmniLight3D _glow;
	private float _glowRangeScale;
	private float _time;
	private Color _color;
	private float _energy;
	private bool _pulsing;
	private float _fade = 1.0f;
	private float _fadeRate;

	public override void _Ready()
	{
		// Sin luz recibida y sin sombras: una cuenca encendida es una fuente, no
		// una superficie. Si se sombrea, se apaga en cuanto el bicho entra en una
		// zona oscura, que es justo donde hace falta verla.
		_material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			EmissionEnabled = true,
			DisableReceiveShadows = true,
		};

		CollectMeshes(this);

		foreach (MeshInstance3D mesh in _meshes)
		{
			mesh.MaterialOverride = _material;
			mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		}

		_glow = FindGlow(this);
		_glowRangeScale = _glow?.OmniRange ?? 0.0f;

		SetPhase(CombatPhase.Idle);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		if (_fadeRate > 0.0f && _fade > 0.0f)
		{
			_fade = Mathf.Max(0.0f, _fade - _fadeRate * dt);
			Apply(_color, _energy);

			if (_fade <= 0.0f)
			{
				SetProcess(false);

				// Y se esconden. Apagadas siguen siendo dos esferas, y como el cráneo
				// se ha ido al suelo con el resto del montón, esas dos esferas se
				// quedan flotando a la altura de la cabeza que ya no está: dos puntos
				// negros en el aire que se leen como un fallo de dibujado. Esconder el
				// nodo se lleva también la luz, que es hija suya.
				Hide();
			}

			return;
		}

		if (!_pulsing)
		{
			return;
		}

		_time += dt;

		// Onda por encima de cero: la anticipación nunca se apaga del todo, solo
		// baja. Un parpadeo que llega a negro se confundiría con haber terminado.
		float wave = 1.0f - PulseDepth * (0.5f - 0.5f * Mathf.Cos(_time * PulseHz * Mathf.Tau));

		Apply(_color, _energy * wave);
	}

	/// <summary>
	/// Se apagan las cuencas. Es lo único que dice de verdad que ha muerto: en una
	/// sala a oscuras, del bicho solo se ven los ojos, así que apagarlos ES la
	/// muerte y todo lo demás —los huesos cayendo— es la consecuencia.
	///
	/// Se apaga en un cuarto de segundo y no de golpe. De golpe se lee como un
	/// fallo de dibujado; con un cuarto de segundo se lee como que se va.
	/// </summary>
	public void Extinguish(float seconds = 0.28f)
	{
		_pulsing = false;
		_color = ActiveColor;
		_energy = ActiveEnergy;
		_fadeRate = 1.0f / Mathf.Max(seconds, 0.01f);
		SetProcess(true);
	}

	public void SetPhase(CombatPhase phase, bool unblockable = false)
	{
		_color = phase switch
		{
			CombatPhase.Windup => unblockable ? UnblockableColor : WindupColor,
			CombatPhase.Active => ActiveColor,
			CombatPhase.Recovery => RecoveryColor,
			_ => IdleColor,
		};

		_energy = phase switch
		{
			CombatPhase.Windup => WindupEnergy,
			CombatPhase.Active => ActiveEnergy,
			CombatPhase.Recovery => RecoveryEnergy,
			_ => IdleEnergy,
		};

		_pulsing = phase == CombatPhase.Windup;
		_time = 0.0f;

		Apply(_color, _energy);
	}

	private void Apply(Color color, float energy)
	{
		energy *= _fade;

		_material.AlbedoColor = color * 0.35f * _fade;
		_material.Emission = color;
		_material.EmissionEnergyMultiplier = energy;

		if (_glow == null)
		{
			return;
		}

		_glow.LightColor = color;

		// La luz de las cuencas es un detalle, no iluminación: se queda muy por
		// debajo de la emisión para que alumbre el pómulo y nada más.
		_glow.LightEnergy = energy * 0.16f;

		// Al encenderse también llega más lejos. Es lo que hace que la anticipación
		// se note en el suelo que tiene delante antes de que veas los ojos.
		_glow.OmniRange = _glowRangeScale * (0.7f + Mathf.Min(energy, 8.0f) * 0.06f) * _fade;
	}

	private void CollectMeshes(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is MeshInstance3D mesh)
			{
				_meshes.Add(mesh);
			}

			CollectMeshes(child);
		}
	}

	private static OmniLight3D FindGlow(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is OmniLight3D light)
			{
				return light;
			}

			OmniLight3D found = FindGlow(child);
			if (found != null)
			{
				return found;
			}
		}

		return null;
	}
}
