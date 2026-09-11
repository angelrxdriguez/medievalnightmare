using System.Collections.Generic;
using Godot;

namespace MedievalNightmare.Core;

/// <summary>
/// El alijo: lo único que no se pierde. Vive en disco porque es lo que separa
/// una incursión de la siguiente, y sobrevive a morir, a reiniciar y a cerrar el
/// juego.
///
/// Guarda DOS cosas y conviene no confundirlas:
///
/// - EL ALIJO son los objetos guardados. Entran al extraer y solo salen si los
///   sacas del arcón antes de bajar. Ahí no los puede perder nadie.
/// - EL EQUIPO es lo que llevas puesto. Se guarda aquí porque sin campamento no
///   hay ningún otro sitio donde apuntarlo, pero NO está a salvo: es justo lo que
///   se pierde al morir.
///
/// Es estático a propósito. El alijo no pertenece a ningún nodo: la escena se
/// recarga entera al morir y al extraer, y lo que tiene que sobrevivir a eso no
/// puede colgar del árbol.
///
/// Se escribe en disco en CUANTO cambia, no al salir. Sacar la maza del arcón y
/// morir tres segundos después tiene que costarte la maza; si el guardado
/// esperase al final de la partida, morir sería la forma barata de deshacer.
/// </summary>
public static class Stash
{
	private const string SavePath = "user://alijo.json";

	/// <summary>Lo guardado. A salvo de todo.</summary>
	public static readonly List<ItemData> Items = new();

	/// <summary>Lo puesto, por ranura. Se pierde al morir.</summary>
	public static readonly ItemData[] Loadout = new ItemData[3];

	private static bool _loaded;

	/// <summary>Índice de una ranura en <see cref="Loadout"/>. None no tiene sitio.</summary>
	public static int IndexOf(EquipSlot slot)
	{
		return (int)slot - 1;
	}

	/// <summary>
	/// Carga el archivo la primera vez que alguien lo pide. Las siguientes ya no
	/// toca el disco: lo que hay en memoria es lo que se acaba de escribir.
	/// </summary>
	public static void Ensure()
	{
		if (_loaded)
		{
			return;
		}

		_loaded = true;

		using FileAccess file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			// Primera partida. No es un error: el alijo empieza vacío.
			return;
		}

		Variant parsed = Json.ParseString(file.GetAsText());
		if (parsed.VariantType != Variant.Type.Dictionary)
		{
			GD.PushWarning($"El alijo de {SavePath} no se entiende. Se empieza de cero.");
			return;
		}

		Godot.Collections.Dictionary data = parsed.AsGodotDictionary();

		Items.Clear();
		if (data.TryGetValue("alijo", out Variant stored))
		{
			foreach (Variant path in stored.AsGodotArray())
			{
				ItemData item = Read(path.AsString());
				if (item != null)
				{
					Items.Add(item);
				}
			}
		}

		if (data.TryGetValue("equipado", out Variant equipped))
		{
			Godot.Collections.Dictionary slots = equipped.AsGodotDictionary();
			Loadout[IndexOf(EquipSlot.Main)] = Read(Field(slots, "principal"));
			Loadout[IndexOf(EquipSlot.Secondary)] = Read(Field(slots, "secundaria"));
			Loadout[IndexOf(EquipSlot.Amulet)] = Read(Field(slots, "amuleto"));
		}
	}

	public static void Save()
	{
		Godot.Collections.Array stored = new();
		foreach (ItemData item in Items)
		{
			if (item != null)
			{
				stored.Add(item.ResourcePath);
			}
		}

		Godot.Collections.Dictionary data = new()
		{
			["alijo"] = stored,
			["equipado"] = new Godot.Collections.Dictionary
			{
				["principal"] = PathOf(Loadout[IndexOf(EquipSlot.Main)]),
				["secundaria"] = PathOf(Loadout[IndexOf(EquipSlot.Secondary)]),
				["amuleto"] = PathOf(Loadout[IndexOf(EquipSlot.Amulet)]),
			},
		};

		using FileAccess file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
		if (file == null)
		{
			GD.PushWarning($"No se ha podido escribir el alijo en {SavePath}.");
			return;
		}

		// Con sangrado: es un archivo que hay que poder abrir y arreglar a mano
		// mientras no exista el campamento.
		file.StoreString(Json.Stringify(data, "\t"));
	}

	private static string Field(Godot.Collections.Dictionary slots, string key)
	{
		return slots.TryGetValue(key, out Variant value) ? value.AsString() : string.Empty;
	}

	private static string PathOf(ItemData item)
	{
		return item?.ResourcePath ?? string.Empty;
	}

	/// <summary>
	/// Un objeto que ya no existe en disco no se recupera: se pierde y se avisa.
	/// Pasa al renombrar un <c>.tres</c> durante el desarrollo, y colgar la
	/// partida por eso sería peor.
	/// </summary>
	private static ItemData Read(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return null;
		}

		if (!ResourceLoader.Exists(path))
		{
			GD.PushWarning($"El alijo guarda {path}, que ya no existe. Se descarta.");
			return null;
		}

		return ResourceLoader.Load<ItemData>(path);
	}
}
