using Godot;

namespace MedievalNightmare.Player;

/// <summary>
/// El arma que se ve. Va en la raíz de cada escena de <c>Models/</c> y no
/// contiene ni una línea de lógica: son los números con los que
/// <see cref="WeaponView"/> construye la animación.
///
/// No hay poses guardadas —ni de anticipación, ni de golpe, ni de guardia— a
/// propósito. Una pose es una docena de números que hay que afinar a ciegas y
/// que además se desincronizan de los tiempos en cuanto se toca
/// <c>SpeedScale</c>. Lo que hay aquí es la DESCRIPCIÓN del golpe: en qué plano
/// va, cuánto barre y cuánto se echa atrás antes. La pose sale de ahí, y por eso
/// cambiar un arma de tajo vertical a barrido horizontal es mover un número.
///
/// Es la misma idea que en <see cref="Core.WeaponData"/>: lo que separa a un
/// arma de otra son tres cifras, no una tabla.
/// </summary>
public partial class WeaponModel : Node3D
{
	[ExportGroup("Reposo")]

	/// <summary>Dónde descansa el arma respecto al ojo. La Z negativa es hacia delante.</summary>
	[Export] public Vector3 RestPosition { get; set; } = new(0.26f, -0.30f, -0.42f);

	[Export] public Vector3 RestRotationDegrees { get; set; } = new(-6.0f, 8.0f, 10.0f);

	[ExportGroup("Golpe")]

	/// <summary>
	/// Plano del arco. 0 es un tajo vertical de arriba abajo, 90 un barrido
	/// horizontal de derecha a izquierda, y lo de en medio es la diagonal.
	/// </summary>
	[Export(PropertyHint.Range, "0,90,1")] public float SwingPlaneDegrees { get; set; } = 55.0f;

	/// <summary>Grados que recorre el arma entre el final de la anticipación y el del golpe.</summary>
	[Export(PropertyHint.Range, "0,360,5")] public float SwingSweepDegrees { get; set; } = 150.0f;

	/// <summary>
	/// Qué parte del arco se gasta echándose atrás. Alto es un arma que se carga
	/// mucho y avisa; bajo es un arma que sale casi desde donde está.
	/// </summary>
	[Export(PropertyHint.Range, "0,0.8,0.01")] public float WindupFraction { get; set; } = 0.4f;

	/// <summary>Lo que se adelanta el arma al golpear. La maza casi no barre: empuja.</summary>
	[Export] public float SwingReach { get; set; } = 0.16f;

	/// <summary>Cuánto se retira el arma hacia el hombro durante la anticipación.</summary>
	[Export] public Vector3 WindupOffset { get; set; } = new(0.05f, 0.05f, 0.14f);

	[ExportGroup("Golpe pesado")]

	/// <summary>Multiplica el arco del ligero. El pesado es el mismo golpe, más largo.</summary>
	[Export] public float HeavySweepScale { get; set; } = 1.5f;

	/// <summary>
	/// El pesado da la vuelta entera sobre el eje del jugador en vez de barrer un
	/// arco. Es lo del mandoble, y es lo que se saca cuando te rodean.
	/// </summary>
	[Export] public bool HeavySpins { get; set; }

	[ExportGroup("Disparo")]

	/// <summary>Coz del disparo: lo que salta el arma hacia atrás y hacia arriba.</summary>
	[Export] public float RecoilKick { get; set; }

	/// <summary>Dónde se va el arma mientras se recarga. Fuera de la mira, que es lo suyo.</summary>
	[Export] public Vector3 ReloadOffset { get; set; } = new(0.06f, -0.16f, 0.06f);

	[Export] public Vector3 ReloadRotationDegrees { get; set; } = new(28.0f, -14.0f, -18.0f);

	[ExportGroup("Guardia")]
	[Export] public Vector3 BlockOffset { get; set; } = new(-0.14f, 0.13f, 0.1f);
	[Export] public Vector3 BlockRotationDegrees { get; set; } = new(6.0f, 62.0f, -74.0f);

	[ExportGroup("Piezas opcionales")]

	/// <summary>
	/// Lo que desaparece mientras el arma está descargada: el virote en el canal.
	/// Sin esto la ballesta se recarga con el virote ya puesto y la recarga no
	/// significa nada.
	/// </summary>
	[Export] public NodePath Ammunition { get; set; } = new();

	private Node3D _ammunition;

	public override void _Ready()
	{
		// El NodePath de una propiedad exportada sin valor llega nulo, no vacío.
		_ammunition = Ammunition is { IsEmpty: false } ? GetNodeOrNull<Node3D>(Ammunition) : null;
	}

	/// <summary>Muestra u oculta la munición cargada. Sin pieza asignada no hace nada.</summary>
	public void SetLoaded(bool loaded)
	{
		if (_ammunition != null)
		{
			_ammunition.Visible = loaded;
		}
	}
}
