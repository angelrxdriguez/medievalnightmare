using System.Collections.Generic;
using Godot;

namespace MedievalNightmare.Core;

/// <summary>
/// Lo que llevas encima y lo que llevas puesto. Doce huecos y tres ranuras, y
/// ni un número más: no hay peso, no hay tamaños y no hay gestión de espacio.
///
/// LA BOLSA ES LO QUE ARRIESGAS. Empieza vacía en cada incursión y se pierde
/// entera al morir. Todo lo que recojas está ahí abajo contigo hasta que
/// extraigas, y esa es la apuesta del pilar 2: lo que llevas encima solo vale
/// algo si sales vivo.
///
/// EL EQUIPO SOBREVIVE A LA INCURSIÓN, PERO NO A MORIR. Sin campamento no hay
/// pantalla donde elegir con qué bajas, así que bajas con lo que tuvieras puesto
/// al extraer. Es el atajo consciente de M3 y está anotado en <c>DISENO.md</c>
/// §9.
///
/// EL ALIJO NO ESTÁ AQUÍ. Vive en <see cref="Stash"/>, que es estático porque
/// tiene que sobrevivir a que la escena se recargue. Este nodo es solo la parte
/// que baja a la mazmorra.
///
/// La munición también vive aquí y no en el arma: un <c>.tres</c> es el mismo
/// objeto para todo el juego, así que apuntar los virotes en él sería apuntarlos
/// para siempre. Se cuenta por objeto y por incursión —el diccionario nace vacío
/// y eso ya significa "cargada"—, de forma que cambiar de arma y volver no
/// recarga nada.
/// </summary>
public partial class Inventory : Node
{
	/// <summary>Ha cambiado algo. Lo escucha la pantalla de inventario para repintarse.</summary>
	[Signal] public delegate void ChangedEventHandler();

	[Export] public int Capacity { get; set; } = 12;

	/// <summary>
	/// El suelo de seguridad del diseño: si te quedas sin arma principal —porque
	/// acabas de morir o porque es tu primera partida—, aparece esta. Nunca puedes
	/// quedarte sin poder entrar.
	/// </summary>
	[Export] public ItemData SafetyFloor { get; set; }

	private readonly List<ItemData> _bag = new();
	private readonly Dictionary<ItemData, int> _ammo = new();

	/// <summary>Lo que llevas encima, en orden. Sin huecos: el índice es la fila.</summary>
	public IReadOnlyList<ItemData> Bag => _bag;

	/// <summary>Huecos ocupados. Es lo que arriesgas, contado.</summary>
	public int Carried => _bag.Count;

	public bool IsFull => _bag.Count >= Capacity;

	public ItemData Main => Equipped(EquipSlot.Main);

	public ItemData Secondary => Equipped(EquipSlot.Secondary);

	public ItemData Amulet => Equipped(EquipSlot.Amulet);

	public override void _Ready()
	{
		Stash.Ensure();

		// La bolsa NO se carga del archivo. Lo que había en ella o se extrajo —y
		// entonces está en el alijo— o se perdió al morir.
		_bag.Clear();

		RestoreSafetyFloor();

		GetParent().GetNode<Health>("Health").Died += OnDied;
	}

	public ItemData Equipped(EquipSlot slot)
	{
		return slot == EquipSlot.None ? null : Stash.Loadout[Stash.IndexOf(slot)];
	}

	/// <summary>Recoger. Con la bolsa llena no cabe nada y el objeto se queda donde está.</summary>
	public bool TryAdd(ItemData item)
	{
		if (item == null || IsFull)
		{
			return false;
		}

		_bag.Add(item);
		Touch();

		return true;
	}

	/// <summary>
	/// Ponerse lo que hay en un hueco. Lo que llevabas puesto cae en ese mismo
	/// hueco, no al final: cambiar de arma no debe reordenarte la bolsa.
	/// </summary>
	public void Equip(int index)
	{
		if (index < 0 || index >= _bag.Count)
		{
			return;
		}

		ItemData item = _bag[index];
		if (item == null || item.Slot == EquipSlot.None)
		{
			return;
		}

		int slot = Stash.IndexOf(item.Slot);
		ItemData previous = Stash.Loadout[slot];

		Stash.Loadout[slot] = item;
		_bag.RemoveAt(index);

		if (previous != null)
		{
			_bag.Insert(index, previous);
		}

		Touch();
	}

	/// <summary>Quitarse algo. Si no hay hueco donde meterlo, no se quita.</summary>
	public void Unequip(EquipSlot slot)
	{
		if (slot == EquipSlot.None || IsFull)
		{
			return;
		}

		int index = Stash.IndexOf(slot);
		ItemData item = Stash.Loadout[index];

		if (item == null)
		{
			return;
		}

		Stash.Loadout[index] = null;
		_bag.Add(item);
		Touch();
	}

	/// <summary>Sacar del arcón. Lo que sacas ya está en riesgo: baja contigo.</summary>
	public void TakeFromStash(int index)
	{
		if (IsFull || index < 0 || index >= Stash.Items.Count)
		{
			return;
		}

		_bag.Add(Stash.Items[index]);
		Stash.Items.RemoveAt(index);
		Touch();
	}

	/// <summary>Dejar en el arcón antes de bajar. Lo único que se puede deshacer.</summary>
	public void StoreInStash(int index)
	{
		if (index < 0 || index >= _bag.Count)
		{
			return;
		}

		Stash.Items.Add(_bag[index]);
		_bag.RemoveAt(index);
		Touch();
	}

	/// <summary>
	/// Extraer: la bolsa entera al alijo. El equipo no se toca, que es lo que te
	/// permite volver a bajar sin pasar por un campamento que todavía no existe.
	/// </summary>
	public void Extract()
	{
		foreach (ItemData item in _bag)
		{
			if (item != null)
			{
				Stash.Items.Add(item);
			}
		}

		_bag.Clear();
		Touch();
	}

	/// <summary>Virotes que le quedan a un arma. Sin contar, está cargada.</summary>
	public int AmmoOf(ItemData item)
	{
		if (item is not WeaponData weapon || weapon.Kind != WeaponKind.Ranged)
		{
			return 0;
		}

		return _ammo.TryGetValue(item, out int left) ? left : weapon.AmmoCapacity;
	}

	public void SpendAmmo(ItemData item)
	{
		if (item == null)
		{
			return;
		}

		_ammo[item] = Mathf.Max(0, AmmoOf(item) - 1);
	}

	/// <summary>
	/// Morir. Se pierde la bolsa Y lo equipado; el alijo no se toca. Acto seguido
	/// vuelve la espada del suelo de seguridad, porque entrar sin nada no es una
	/// dificultad, es un callejón.
	/// </summary>
	private void OnDied()
	{
		_bag.Clear();

		for (int i = 0; i < Stash.Loadout.Length; i++)
		{
			Stash.Loadout[i] = null;
		}

		RestoreSafetyFloor();
		Touch();
	}

	private void RestoreSafetyFloor()
	{
		int slot = Stash.IndexOf(EquipSlot.Main);

		if (Stash.Loadout[slot] == null && SafetyFloor != null)
		{
			Stash.Loadout[slot] = SafetyFloor;
		}

		Stash.Save();
	}

	/// <summary>
	/// Cualquier cambio se escribe en disco en el acto. Ver <see cref="Stash"/>:
	/// guardar al salir convertiría morir en una forma de deshacer.
	/// </summary>
	private void Touch()
	{
		Stash.Save();
		EmitSignal(SignalName.Changed);
	}
}
