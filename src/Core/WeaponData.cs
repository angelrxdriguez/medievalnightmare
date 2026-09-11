using Godot;

namespace MedievalNightmare.Core;

/// <summary>Qué hace el botón de ataque con esta arma en la mano.</summary>
public enum WeaponKind
{
	/// <summary>Barre un arco alrededor del portador.</summary>
	Melee,

	/// <summary>Dispara un proyectil y se recarga.</summary>
	Ranged,
}

/// <summary>
/// Datos de un arma. Los tiempos base y el compromiso son del jugador; aquí
/// vive lo que distingue a un arma de otra.
///
/// La personalidad la dan tres cosas y no los números de daño: el arco que
/// barre el golpe, el alcance y lo rápido que sale. La espada barre casi de
/// lado a lado y perdona fallar; la maza abre un arco estrecho y corto, así que
/// hay que meterse dentro del alcance del enemigo; el mandoble gira sobre sí
/// mismo con el pesado y es la respuesta a estar rodeado.
/// </summary>
[GlobalClass]
public partial class WeaponData : ItemData
{
	/// <summary>
	/// Un arma va siempre en la ranura principal. Se fija aquí y no en cada
	/// <c>.tres</c> porque no es una propiedad del arma: es lo que ES un arma.
	/// </summary>
	public WeaponData()
	{
		DisplayName = "Arma";
		Slot = EquipSlot.Main;
	}

	[Export] public WeaponKind Kind { get; set; } = WeaponKind.Melee;
	[Export] public float Damage { get; set; } = 18.0f;

	/// <summary>Radio del golpe. Sin uso en las armas a distancia.</summary>
	[Export] public float Range { get; set; } = 1.8f;

	/// <summary>Multiplica los tiempos base. Menor que 1 es más rápida, mayor es más lenta.</summary>
	[Export] public float SpeedScale { get; set; } = 1.0f;

	[Export] public float HeavyDamageMultiplier { get; set; } = 2.5f;

	/// <summary>
	/// El arma que se ve en la mano. Va aquí y no en el jugador porque es dato del
	/// arma, igual que su alcance: añadir una quinta arma tiene que ser soltar un
	/// <c>.tres</c> en la lista, no tocar el controlador.
	///
	/// La escena lleva un <c>WeaponModel</c> en la raíz con los números de su
	/// animación; quien la mueve es <c>WeaponView</c>.
	/// </summary>
	[Export] public PackedScene ViewModel { get; set; }

	[ExportGroup("Cuerpo a cuerpo")]

	/// <summary>Arco que barre el golpe ligero. Ancho alcanza a varios; estrecho exige apuntar.</summary>
	[Export(PropertyHint.Range, "10,350,5")]
	public float LightArcDegrees { get; set; } = 60.0f;

	[Export(PropertyHint.Range, "10,350,5")]
	public float HeavyArcDegrees { get; set; } = 60.0f;

	[ExportGroup("A distancia")]

	/// <summary>
	/// Munición con la que entras a la incursión. No se repone: cuando se acaba,
	/// el arma es peso muerto. Es lo que impide que disparar salga gratis.
	/// </summary>
	[Export] public int AmmoCapacity { get; set; } = 6;

	/// <summary>Desde que sale el disparo hasta que puedes volver a disparar.</summary>
	[Export] public float ReloadSeconds { get; set; } = 1.0f;

	/// <summary>El proyectil no es instantáneo: a distancia hay que adelantar el tiro.</summary>
	[Export] public float ProjectileSpeed { get; set; } = 18.0f;

	[Export] public PackedScene Projectile { get; set; }
}
