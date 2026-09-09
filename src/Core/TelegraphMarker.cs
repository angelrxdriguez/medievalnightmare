using Godot;

namespace MedievalNightmare.Core;

/// <summary>
/// Marcador de greybox que colorea la fase de ataque. Sustituye a las animaciones
/// hasta que haya arte: sin él no se puede leer la telegrafía de un enemigo.
/// </summary>
public partial class TelegraphMarker : MeshInstance3D
{
	[Export] public Color IdleColor { get; set; } = new(0.22f, 0.22f, 0.26f);
	[Export] public Color WindupColor { get; set; } = new(0.95f, 0.72f, 0.12f);
	[Export] public Color ActiveColor { get; set; } = new(0.88f, 0.14f, 0.10f);
	[Export] public Color RecoveryColor { get; set; } = new(0.28f, 0.44f, 0.70f);

	private StandardMaterial3D _material;

	public override void _Ready()
	{
		_material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = IdleColor,
		};
		MaterialOverride = _material;
	}

	public void SetPhase(CombatPhase phase)
	{
		_material.AlbedoColor = phase switch
		{
			CombatPhase.Windup => WindupColor,
			CombatPhase.Active => ActiveColor,
			CombatPhase.Recovery => RecoveryColor,
			_ => IdleColor,
		};
	}
}
