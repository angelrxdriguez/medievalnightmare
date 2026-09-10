using Godot;
using MedievalNightmare.Core;

namespace MedievalNightmare.Ui;

/// <summary>
/// El único aviso de que te están dando: el borde de la pantalla se tiñe de rojo.
///
/// Dos capas. Un pico al recibir el golpe, proporcional a lo que te ha quitado,
/// y un latido continuo cuando bajas del umbral que va más rápido cuanto peor
/// estás. No hay números ni barra: la vida se lee mirando el borde.
/// </summary>
public partial class DamageVignette : CanvasLayer
{
	/// <summary>Fracción de vida por debajo de la cual empieza el latido.</summary>
	[Export] public float HeartbeatThreshold { get; set; } = 0.4f;

	[Export] public float HeartbeatSlowHz { get; set; } = 1.0f;
	[Export] public float HeartbeatFastHz { get; set; } = 2.4f;
	[Export] public float HeartbeatMaxIntensity { get; set; } = 0.75f;

	/// <summary>Daño que llena el pico del todo. Por debajo, el pico es proporcional.</summary>
	[Export] public float FullPulseDamage { get; set; } = 30.0f;

	[Export] public float PulseFadeSeconds { get; set; } = 0.55f;

	private ColorRect _fill;
	private ShaderMaterial _material;
	private Health _health;

	private float _pulse;
	private float _beatPhase;

	public override void _Ready()
	{
		_fill = GetNode<ColorRect>("Fill");
		_material = (ShaderMaterial)_fill.Material;
		_health = GetParent().GetNode<Health>("Health");
		_health.Damaged += OnDamaged;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		_pulse = Mathf.Max(0.0f, _pulse - dt / PulseFadeSeconds);

		float fraction = _health.Max > 0.0f ? _health.Current / _health.Max : 0.0f;
		float intensity = _pulse;

		if (fraction < HeartbeatThreshold && !_health.IsDead)
		{
			// Cuanto menos vida, más rápido y más fuerte. A 0 de vida el latido
			// ya no importa: ahí manda la pantalla de muerte.
			float urgency = 1.0f - Mathf.Clamp(fraction / HeartbeatThreshold, 0.0f, 1.0f);
			_beatPhase += dt * Mathf.Lerp(HeartbeatSlowHz, HeartbeatFastHz, urgency);
			_beatPhase %= 1.0f;

			// Elevar el seno a una potencia alta convierte la onda en un golpe seco.
			float beat = Mathf.Pow((Mathf.Sin(_beatPhase * Mathf.Tau) + 1.0f) * 0.5f, 6.0f);
			intensity = Mathf.Max(intensity, beat * urgency * HeartbeatMaxIntensity);
		}
		else
		{
			_beatPhase = 0.0f;
		}

		_material.SetShaderParameter("intensity", intensity);
	}

	private void OnDamaged(float amount, float remaining)
	{
		// Un golpe bloqueado quita poco, así que apenas tiñe: la reducción del
		// daño se lee sola, sin necesidad de un icono de "bloqueado".
		_pulse = Mathf.Max(_pulse, Mathf.Clamp(amount / FullPulseDamage, 0.2f, 1.0f));
	}
}
