using Godot;

namespace MedievalNightmare.Player;

/// <summary>
/// Sacudidas y coces de la cámara. Cuelga del propio nodo de cámara, que no
/// rota por su cuenta —el cabeceo y el giro los lleva <c>Head</c>—, así que su
/// rotación local está libre para los efectos y no hay que descontar nada.
///
/// Dos gestos distintos porque cuentan cosas distintas:
///
/// - LA COZ (<see cref="Kick"/>): un empujón direccional que se recoge solo.
///   Es la respuesta de tu cuerpo a algo concreto: tu golpe conectando.
/// - EL TEMBLOR (<see cref="AddTrauma"/>): ruido sin dirección que se apaga.
///   Es encajar: recibir un golpe, que te rompan la guardia.
///
/// El temblor escala con el trauma AL CUADRADO: los golpes pequeños apenas se
/// notan y los grandes se notan mucho, en vez de vivir todos en el mismo
/// mareo intermedio.
/// </summary>
public partial class CameraFeedback : Camera3D
{
	/// <summary>Temblor a trauma 1. Se queda corto a propósito: esto se dispara
	/// decenas de veces por pelea y el mareo acumulado no avisa.</summary>
	[Export] public float MaxShakeDegrees { get; set; } = 2.4f;

	/// <summary>Trauma que se apaga por segundo. El temblor de un golpe dura medio segundo mal contado.</summary>
	[Export] public float TraumaDecay { get; set; } = 2.2f;

	/// <summary>Lo rápido que se recoge la coz. Alto: es un latigazo, no un vaivén.</summary>
	[Export] public float KickRecover { get; set; } = 11.0f;

	/// <summary>Lo rápido que vuelve el campo de visión tras un puñetazo de FOV.</summary>
	[Export] public float FovRecover { get; set; } = 7.0f;

	private float _trauma;
	private Vector3 _kick;
	private float _fovPunch;
	private float _baseFov;
	private float _time;

	public override void _Ready()
	{
		_baseFov = Fov;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_time += dt;

		_trauma = Mathf.Max(0.0f, _trauma - TraumaDecay * dt);
		_kick = _kick.Lerp(Vector3.Zero, 1.0f - Mathf.Exp(-KickRecover * dt));
		_fovPunch = Mathf.Lerp(_fovPunch, 0.0f, 1.0f - Mathf.Exp(-FovRecover * dt));

		// Senos a frecuencias que no se dividen entre sí: en ráfagas de menos de
		// un segundo no da tiempo a leer el patrón y sale más barato que el ruido.
		float amount = _trauma * _trauma * Mathf.DegToRad(MaxShakeDegrees);
		Vector3 shake = new(
			(Mathf.Sin(_time * 127.0f) * 0.6f + Mathf.Sin(_time * 311.0f) * 0.4f) * amount,
			(Mathf.Sin(_time * 89.0f + 1.3f) * 0.6f + Mathf.Sin(_time * 251.0f + 2.1f) * 0.4f) * amount,
			Mathf.Sin(_time * 163.0f + 0.7f) * amount * 0.5f);

		Rotation = shake + new Vector3(
			Mathf.DegToRad(_kick.X),
			Mathf.DegToRad(_kick.Y),
			Mathf.DegToRad(_kick.Z));

		Fov = _baseFov + _fovPunch;
	}

	/// <summary>Suma temblor, de 0 a 1. Se acumula pero con techo: dos golpes seguidos no marean el doble.</summary>
	public void AddTrauma(float amount)
	{
		_trauma = Mathf.Clamp(_trauma + amount, 0.0f, 1.0f);
	}

	/// <summary>Empujón direccional en grados (cabeceo, giro, alabeo).</summary>
	public void Kick(Vector3 degrees)
	{
		_kick += degrees;
	}

	/// <summary>Puñetazo de campo de visión. Positivo abre: es el "uff" del golpe pesado.</summary>
	public void PunchFov(float degrees)
	{
		_fovPunch += degrees;
	}
}
