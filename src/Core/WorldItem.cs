using Godot;

namespace MedievalNightmare.Core;

/// <summary>
/// Un objeto tirado en el suelo. Se recoge al pasarle por encima.
///
/// SIN AVISO EN PANTALLA Y SIN TECLA. No hay "pulsa E", y no es por ahorrar
/// trabajo: `HUD.md` §7 deja fuera los tutoriales en pantalla, y una tecla de
/// recoger obliga a poner uno. Pasar por encima se entiende sin que nadie lo
/// explique, y lo que confirma que lo has cogido es que el arma ya no está en el
/// suelo.
///
/// Con la bolsa llena no pasa nada: el objeto se queda donde está y lo vuelves a
/// pisar cuando hagas hueco. Perder botín por caminar sería lo peor que podría
/// hacer este script.
///
/// El modelo se monta al arrancar a partir del <see cref="ItemData"/>, así que
/// una escena de nivel solo tiene que decir QUÉ hay en el suelo, nunca cómo se
/// dibuja.
/// </summary>
public partial class WorldItem : Area3D
{
	private const string ScenePath = "res://src/Core/WorldItem.tscn";

	[Export] public ItemData Item { get; set; }

	/// <summary>
	/// Dónde se cuelga el modelo. Lleva en la escena la rotación que tumba el
	/// arma en el suelo, para que colocar una en un nivel sea mover un nodo y no
	/// pelearse con los grados.
	/// </summary>
	private Node3D _pivot;

	public override void _Ready()
	{
		_pivot = GetNode<Node3D>("Model");

		BuildModel();

		BodyEntered += OnBodyEntered;
	}

	/// <summary>
	/// Suelta un objeto en el mundo. La escena se carga por ruta para que quien
	/// suelta botín —un esqueleto, un cofre reventado— no tenga que llevar la
	/// escena colgada en un export que alguien puede dejar vacío.
	///
	/// El alta se aplaza porque esto se llama desde dentro de un paso de física:
	/// meter un Area3D en el árbol mientras el motor está resolviendo consultas
	/// es justo el error que no da error hasta que un día lo da.
	/// </summary>
	public static void Spawn(Node context, ItemData item, Vector3 position)
	{
		if (item == null)
		{
			return;
		}

		PackedScene scene = ResourceLoader.Load<PackedScene>(ScenePath);
		if (scene == null)
		{
			GD.PushWarning($"No se encuentra {ScenePath}: el botín se pierde.");
			return;
		}

		WorldItem dropped = scene.Instantiate<WorldItem>();
		dropped.Item = item;
		dropped.Position = position;

		Node parent = context.GetTree().CurrentScene;
		parent.CallDeferred(Node.MethodName.AddChild, dropped);
	}

	private void BuildModel()
	{
		if (Item == null)
		{
			GD.PushWarning($"{Name} no tiene objeto: no hay nada que recoger.");
			return;
		}

		PackedScene scene = Item.WorldModel ?? (Item as WeaponData)?.ViewModel;
		if (scene == null)
		{
			return;
		}

		Node3D model = scene.Instantiate<Node3D>();
		_pivot.AddChild(model);

		HideHand(model);
	}

	/// <summary>
	/// Los modelos de arma llevan puesto el puño que los agarra, porque en la mano
	/// no se entienden sin él. En el suelo ese puño es una mano cortada.
	/// </summary>
	private static void HideHand(Node3D model)
	{
		if (model.GetNodeOrNull<Node3D>("Hand") is Node3D hand)
		{
			hand.Visible = false;
		}
	}

	private void OnBodyEntered(Node3D body)
	{
		if (!body.IsInGroup("player"))
		{
			return;
		}

		if (body.GetNodeOrNull<Inventory>("Inventory") is not Inventory inventory)
		{
			return;
		}

		// La bolsa llena no se avisa: el objeto sigue ahí y se ve que sigue ahí.
		if (!inventory.TryAdd(Item))
		{
			return;
		}

		QueueFree();
	}
}
