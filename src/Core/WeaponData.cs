using Godot;

namespace MedievalNightmare.Core;

/// <summary>
/// Datos de un arma. Las tres armas comparten los mismos tiempos base y se
/// diferencian por daño, alcance y <see cref="SpeedScale"/>.
/// </summary>
[GlobalClass]
public partial class WeaponData : Resource
{
	[Export] public string DisplayName { get; set; } = "Arma";
	[Export] public float Damage { get; set; } = 18.0f;
	[Export] public float Range { get; set; } = 1.8f;

	/// <summary>Multiplica los tiempos base. Menor que 1 es más rápida, mayor es más lenta.</summary>
	[Export] public float SpeedScale { get; set; } = 1.0f;

	[Export] public float HeavyDamageMultiplier { get; set; } = 1.8f;
}
