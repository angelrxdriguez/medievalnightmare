using Godot;
using MedievalNightmare.Core;

namespace MedievalNightmare.Ui;

/// <summary>
/// El inventario. Se abre con I, para el juego y enseña tres listas: lo que
/// llevas puesto, lo que llevas encima y —solo si estás delante del arcón— lo
/// que tienes guardado.
///
/// NO ES HUD, ES UN MENÚ, y por eso puede tener letras. `HUD.md` §6.3 ya eligió
/// esta salida entre las tres posibles: la decisión de extraer se toma con el
/// inventario delante, no con un contador en una esquina. En combate la pantalla
/// sigue vacía.
///
/// SE MARCA Y LUEGO SE ACTÚA. Un clic marca; los botones de abajo son lo único
/// que mueve objetos. Es más lento que "clic para equipar", y es a propósito:
/// delante del arcón el mismo clic tendría que significar dos cosas distintas
/// —equipar o guardar— y la primera vez que el jugador guarde un arma creyendo
/// que se la estaba poniendo, dejará de fiarse de esta pantalla.
///
/// SE CONSTRUYE EN CÓDIGO, como la pausa y la mira: son listas que cambian de
/// tamaño y de color según el estado, y eso en un <c>.tscn</c> es un árbol que
/// hay que mantener a mano cada vez que se añade una ranura.
///
/// Se repinta al abrirse y cuando el inventario avisa de que ha cambiado. Nunca
/// por fotograma: con el árbol parado no hay nada que actualizar.
/// </summary>
public partial class InventoryScreen : CanvasLayer
{
	/// <summary>Los arcones se marcan con este grupo. No necesitan script.</summary>
	public const string StashGroup = "alijo";

	[ExportGroup("Aspecto")]

	/// <summary>Lo que se oscurece el juego detrás. El mismo velo que la pausa.</summary>
	[Export] public Color DimColor { get; set; } = new(0.015f, 0.016f, 0.022f, 0.74f);

	/// <summary>Hueso. El color base de todo lo que se lee en este juego.</summary>
	[Export] public Color TextColor { get; set; } = new(0.88f, 0.85f, 0.78f);

	/// <summary>Ámbar de antorcha. Aquí solo dice dónde tienes el dedo.</summary>
	[Export] public Color HighlightColor { get; set; } = new(0.98f, 0.72f, 0.25f);

	/// <summary>Hueso apagado, para los huecos vacíos y los títulos de columna.</summary>
	[Export] public Color FadedColor { get; set; } = new(0.55f, 0.53f, 0.48f);

	[Export] public int TitleSize { get; set; } = 26;
	[Export] public int RowSize { get; set; } = 17;

	[ExportGroup("Arcón")]

	/// <summary>
	/// A qué distancia del arcón se ve el alijo. Tres metros es tenerlo delante:
	/// desde el otro lado del pasillo no se saca nada.
	/// </summary>
	[Export] public float StashRange { get; set; } = 3.0f;

	private enum Zone
	{
		None,
		Equipment,
		Bag,
		Stash,
	}

	private Inventory _inventory;
	private Node3D _body;
	private VBoxContainer _panel;

	private Zone _zone = Zone.None;
	private int _index = -1;
	private bool _atStash;

	public bool IsOpen => Visible;

	public override void _Ready()
	{
		// Por encima de la mira y de la viñeta, por debajo del negro del final.
		Layer = 7;

		// Lo único que sigue vivo con el árbol parado, igual que la pausa.
		ProcessMode = ProcessModeEnum.Always;

		Visible = false;

		_body = GetParent<Node3D>();
		_inventory = _body.GetNode<Inventory>("Inventory");
		_inventory.Changed += Refresh;

		Build();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (IsOpen && @event.IsActionPressed("ui_cancel"))
		{
			GetViewport().SetInputAsHandled();
			Close();

			return;
		}

		if (!@event.IsActionPressed("inventory"))
		{
			return;
		}

		GetViewport().SetInputAsHandled();

		if (IsOpen)
		{
			Close();

			return;
		}

		// Si el árbol ya está parado, lo ha parado otro —la pausa, o una extracción
		// en curso— y abrirse encima solo sirve para dejar dos menús peleándose por
		// el ratón.
		if (GetTree().Paused || RaidEndScreen.IsRaidEnding(GetTree()))
		{
			return;
		}

		Open();
	}

	private void Build()
	{
		ColorRect dim = new()
		{
			Color = DimColor,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};

		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(dim);

		CenterContainer center = new();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(center);

		_panel = new VBoxContainer();
		_panel.AddThemeConstantOverride("separation", 10);
		center.AddChild(_panel);
	}

	private void Open()
	{
		// El arcón se mira al abrir y no cada fotograma: con el juego parado no te
		// vas a acercar a él sin cerrar esto antes.
		_atStash = IsAtStash();
		_zone = Zone.None;
		_index = -1;

		Visible = true;
		GetTree().Paused = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;

		Refresh();
	}

	private void Close()
	{
		Visible = false;
		GetTree().Paused = false;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	/// <summary>
	/// Se tira todo y se vuelve a montar. Son treinta nodos con el juego parado:
	/// cuesta menos que llevar la cuenta de qué fila hay que repintar, y así no
	/// puede quedarse una fila vieja enseñando un arma que ya no está ahí.
	/// </summary>
	private void Refresh()
	{
		if (!IsOpen)
		{
			return;
		}

		foreach (Node child in _panel.GetChildren())
		{
			_panel.RemoveChild(child);
			child.QueueFree();
		}

		_panel.AddChild(Text("INVENTARIO", TitleSize, TextColor));

		HBoxContainer columns = new();
		columns.AddThemeConstantOverride("separation", 46);
		_panel.AddChild(columns);

		columns.AddChild(CarriedColumn());

		if (_atStash)
		{
			columns.AddChild(StashColumn());
		}

		_panel.AddChild(Actions());
		_panel.AddChild(Text(Hint(), RowSize, FadedColor));
	}

	/// <summary>Lo puesto y lo que llevas encima: las dos cosas que bajan contigo.</summary>
	private Control CarriedColumn()
	{
		VBoxContainer column = new()
		{
			CustomMinimumSize = new Vector2(330.0f, 0.0f),
		};

		column.AddThemeConstantOverride("separation", 4);

		column.AddChild(Header("EQUIPO"));
		column.AddChild(EquipRow("Principal", EquipSlot.Main));
		column.AddChild(EquipRow("Secundaria", EquipSlot.Secondary));
		column.AddChild(EquipRow("Amuleto", EquipSlot.Amulet));

		column.AddChild(new Control { CustomMinimumSize = new Vector2(0.0f, 12.0f) });

		column.AddChild(Header($"ENCIMA   {_inventory.Carried} / {_inventory.Capacity}"));

		// Los doce huecos en dos columnas y no en una lista. En una sola fila por
		// hueco la pantalla no cabe en 720 de alto —se come el pie— y además una
		// lista de doce se lee de arriba abajo, que es más lento que verlos todos
		// de un vistazo.
		GridContainer bag = new()
		{
			Columns = 2,
		};

		bag.AddThemeConstantOverride("h_separation", 10);
		bag.AddThemeConstantOverride("v_separation", 4);
		column.AddChild(bag);

		for (int i = 0; i < _inventory.Capacity; i++)
		{
			ItemData item = i < _inventory.Bag.Count ? _inventory.Bag[i] : null;
			bag.AddChild(Row(item?.DisplayName ?? "—", Zone.Bag, i, item != null));
		}

		return column;
	}

	private Control StashColumn()
	{
		VBoxContainer column = new()
		{
			CustomMinimumSize = new Vector2(330.0f, 0.0f),
		};

		column.AddThemeConstantOverride("separation", 4);
		column.AddChild(Header($"ALIJO   {Stash.Items.Count}"));

		if (Stash.Items.Count == 0)
		{
			column.AddChild(Row("—", Zone.Stash, -1, enabled: false));

			return column;
		}

		for (int i = 0; i < Stash.Items.Count; i++)
		{
			column.AddChild(Row(Stash.Items[i].DisplayName, Zone.Stash, i, enabled: true));
		}

		return column;
	}

	private Control EquipRow(string slotName, EquipSlot slot)
	{
		ItemData item = _inventory.Equipped(slot);

		return Row($"{slotName}   {item?.DisplayName ?? "—"}", Zone.Equipment, (int)slot, item != null);
	}

	/// <summary>
	/// Una fila. Las vacías se dibujan igual pero no se pueden marcar: doce huecos
	/// tienen que verse como doce huecos, y un hueco no es un objeto.
	/// </summary>
	private Button Row(string text, Zone zone, int index, bool enabled)
	{
		bool selected = _zone == zone && _index == index;
		Color color = selected ? HighlightColor : enabled ? TextColor : FadedColor;

		Button button = new()
		{
			Text = text,
			Flat = true,
			Alignment = HorizontalAlignment.Left,
			Disabled = !enabled,
			FocusMode = enabled ? Control.FocusModeEnum.All : Control.FocusModeEnum.None,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};

		button.AddThemeFontSizeOverride("font_size", RowSize);
		button.AddThemeColorOverride("font_color", color);
		button.AddThemeColorOverride("font_hover_color", HighlightColor);
		button.AddThemeColorOverride("font_focus_color", color);
		button.AddThemeColorOverride("font_pressed_color", HighlightColor);
		button.AddThemeColorOverride("font_disabled_color", FadedColor);
		button.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f));
		button.AddThemeConstantOverride("outline_size", 5);

		if (enabled)
		{
			button.Pressed += () => Select(zone, index);
		}

		return button;
	}

	private void Select(Zone zone, int index)
	{
		_zone = zone;
		_index = index;

		Refresh();
	}

	/// <summary>
	/// Los botones que mueven cosas. Solo aparece el que se puede pulsar: un botón
	/// gris que no hace nada obliga a averiguar por qué, y aquí no hay nada que
	/// averiguar.
	/// </summary>
	private Control Actions()
	{
		HBoxContainer actions = new()
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			CustomMinimumSize = new Vector2(0.0f, 34.0f),
		};

		actions.AddThemeConstantOverride("separation", 26);

		switch (_zone)
		{
			case Zone.Bag when CanEquipSelection():
				actions.AddChild(Action("Equipar", EquipSelection));
				break;

			case Zone.Equipment:
				actions.AddChild(Action("Quitar", UnequipSelection));
				break;

			case Zone.Stash:
				actions.AddChild(Action("Sacar", TakeSelection));
				break;
		}

		// Guardar solo existe delante del arcón. Es la única forma de sacar algo de
		// la bolsa sin extraer, y por eso no puede estar en mitad de la mazmorra.
		if (_atStash && _zone == Zone.Bag)
		{
			actions.AddChild(Action("Guardar", StoreSelection));
		}

		return actions;
	}

	private Button Action(string text, System.Action pressed)
	{
		Button button = new()
		{
			Text = text,
			Flat = true,
		};

		button.AddThemeFontSizeOverride("font_size", RowSize + 3);
		button.AddThemeColorOverride("font_color", TextColor);
		button.AddThemeColorOverride("font_hover_color", HighlightColor);
		button.AddThemeColorOverride("font_focus_color", HighlightColor);
		button.AddThemeColorOverride("font_pressed_color", HighlightColor);
		button.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f));
		button.AddThemeConstantOverride("outline_size", 5);
		button.Pressed += pressed;

		return button;
	}

	private bool CanEquipSelection()
	{
		ItemData item = SelectedBagItem();

		return item != null && item.Slot != EquipSlot.None;
	}

	private ItemData SelectedBagItem()
	{
		return _index >= 0 && _index < _inventory.Bag.Count ? _inventory.Bag[_index] : null;
	}

	private void EquipSelection()
	{
		_inventory.Equip(_index);
		ClearSelection();
	}

	private void UnequipSelection()
	{
		_inventory.Unequip((EquipSlot)_index);
		ClearSelection();
	}

	private void TakeSelection()
	{
		_inventory.TakeFromStash(_index);
		ClearSelection();
	}

	private void StoreSelection()
	{
		_inventory.StoreInStash(_index);
		ClearSelection();
	}

	/// <summary>
	/// Después de mover algo, lo marcado ya no está donde estaba. Dejar la marca
	/// puesta señalaría a otro objeto distinto, que es la forma más rápida de
	/// guardar lo que no querías.
	/// </summary>
	private void ClearSelection()
	{
		_zone = Zone.None;
		_index = -1;

		// El inventario avisa del cambio y eso ya repinta; esto es para cuando la
		// acción no ha podido hacer nada, como quitarse algo con la bolsa llena.
		Refresh();
	}

	private string Hint()
	{
		return _atStash
			? "En el arcón: lo que dejes aquí no se pierde.      I o Esc para cerrar"
			: "I o Esc para cerrar";
	}

	private Label Header(string text)
	{
		return Text(text, RowSize, FadedColor);
	}

	private Label Text(string text, int size, Color color)
	{
		Label label = new()
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
		};

		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f));
		label.AddThemeConstantOverride("outline_size", 6);

		return label;
	}

	/// <summary>
	/// El arcón no tiene script: es un grupo y una distancia. Lo único que hay que
	/// saber de él es si lo tienes delante, y eso se mide desde aquí.
	/// </summary>
	private bool IsAtStash()
	{
		foreach (Node node in GetTree().GetNodesInGroup(StashGroup))
		{
			if (node is Node3D chest && chest.GlobalPosition.DistanceTo(_body.GlobalPosition) <= StashRange)
			{
				return true;
			}
		}

		return false;
	}
}
