using Godot;
using MedievalNightmare.Core;
using MedievalNightmare.Enemies;

namespace MedievalNightmare.Levels;

/// <summary>
/// La sala de entrenamiento: grande, llana y con luz. Existe para una sola
/// cosa: probar el combate sin que la mazmorra se meta por medio. Aquí no hay
/// niebla, ni extracción, ni botín: hay un muñeco que canta números y teclas
/// para montar la pelea que quieras en dos segundos.
///
/// Las teclas van por código físico y fuera del mapa de acciones a propósito:
/// son herramienta de desarrollo, no controles del juego, y no deben aparecer
/// en ninguna pantalla de opciones.
///
/// F1-F4  espada corta / maza / mandoble / ballesta
/// K / L  un esqueleto delante / tres alrededor
/// J      despejar enemigos      H  curarse
/// R      reiniciar la sala
/// </summary>
public partial class CombatArena : Node3D
{
	private const string SkeletonScene = "res://src/Enemies/Skeleton/Skeleton.tscn";

	/// <summary>Hasta dónde se puede spawnear. Un pelo por dentro de las paredes.</summary>
	private const float Bounds = 19.0f;

	private static readonly (Key Key, string Path)[] Weapons =
	{
		(Key.F1, "res://src/Player/Weapons/EspadaCorta.tres"),
		(Key.F2, "res://src/Player/Weapons/Maza.tres"),
		(Key.F3, "res://src/Player/Weapons/Mandoble.tres"),
		(Key.F4, "res://src/Player/Weapons/Ballesta.tres"),
	};

	private Node3D _player;

	public override void _Ready()
	{
		_player = GetTree().GetFirstNodeInGroup("player") as Node3D;
		BuildLegend();
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey key || !key.Pressed || key.Echo)
		{
			return;
		}

		switch (key.PhysicalKeycode)
		{
			case Key.K:
				SpawnAhead();
				break;

			case Key.L:
				SpawnRing(3);
				break;

			case Key.J:
				ClearEnemies();
				break;

			case Key.H:
				_player?.GetNodeOrNull<Health>("Health")?.Restore();
				break;

			case Key.R:
				GetTree().ReloadCurrentScene();
				break;

			default:
				foreach ((Key hotkey, string path) in Weapons)
				{
					if (key.PhysicalKeycode == hotkey)
					{
						Equip(path);
						return;
					}
				}

				break;
		}
	}

	/// <summary>
	/// Cambia el arma puesta escribiendo directamente en el alijo, que es donde
	/// vive el equipo. El arma en la mano (<c>WeaponView</c>) se da cuenta sola:
	/// compara lo que enseña con lo que hay puesto en cada fotograma.
	/// </summary>
	private static void Equip(string path)
	{
		if (GD.Load<ItemData>(path) is { } weapon)
		{
			Stash.Loadout[Stash.IndexOf(EquipSlot.Main)] = weapon;
		}
	}

	/// <summary>Uno solo, plantado delante de la vista: el duelo que se repite mil veces.</summary>
	private void SpawnAhead()
	{
		if (_player == null)
		{
			return;
		}

		Vector3 forward = -(_player.GetNodeOrNull<Node3D>("Head")?.GlobalBasis.Z ?? Vector3.Forward);
		forward.Y = 0.0f;
		forward = forward.IsZeroApprox() ? Vector3.Forward : forward.Normalized();

		Spawn(_player.GlobalPosition + forward * 6.0f);
	}

	/// <summary>Tres repartidos alrededor: la pelea de colocación, la del anillo.</summary>
	private void SpawnRing(int count)
	{
		if (_player == null)
		{
			return;
		}

		float offset = GD.Randf() * Mathf.Tau;

		for (int i = 0; i < count; i++)
		{
			float angle = offset + Mathf.Tau * i / count;
			Vector3 direction = new(Mathf.Sin(angle), 0.0f, Mathf.Cos(angle));

			Spawn(_player.GlobalPosition + direction * 6.5f);
		}
	}

	private void Spawn(Vector3 position)
	{
		SkeletonAI skeleton = GD.Load<PackedScene>(SkeletonScene).Instantiate<SkeletonAI>();

		// La posición se fija ANTES de entrar al árbol: el esqueleto apunta su
		// "casa" en _Ready y no debe apuntar el origen de la escena.
		skeleton.Position = new Vector3(
			Mathf.Clamp(position.X, -Bounds, Bounds),
			0.1f,
			Mathf.Clamp(position.Z, -Bounds, Bounds));

		AddChild(skeleton);
	}

	private void ClearEnemies()
	{
		foreach (Node node in GetTree().GetNodesInGroup(SkeletonAI.EnemyGroup))
		{
			(node as Node3D)?.GetNodeOrNull<Health>("Health")?.ApplyDamage(999999.0f);
		}
	}

	/// <summary>La chuleta de teclas, siempre a la vista. Es la única UI de la sala.</summary>
	private void BuildLegend()
	{
		var legend = new Label
		{
			Text = "ARENA   F1 espada · F2 maza · F3 mandoble · F4 ballesta\n" +
				"K esqueleto · L tres · J despejar · H curar · R reiniciar",
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.6f),
		};

		legend.AddThemeFontSizeOverride("font_size", 14);

		// Abajo a la izquierda: la esquina de arriba ya la ocupa el DebugHud.
		legend.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
		legend.Position = new Vector2(16.0f, -56.0f);

		var layer = new CanvasLayer();
		layer.AddChild(legend);
		AddChild(layer);
	}
}
