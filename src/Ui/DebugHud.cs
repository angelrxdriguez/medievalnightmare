using Godot;
using MedievalNightmare.Core;
using MedievalNightmare.Player;

namespace MedievalNightmare.Ui;

/// <summary>
/// Lectura mínima para poder evaluar el combate en greybox. Es una herramienta,
/// no interfaz de juego: desaparece cuando haya UI de verdad.
/// </summary>
public partial class DebugHud : CanvasLayer
{
	private Label _label;
	private PlayerController _player;
	private Health _health;

	public override void _Ready()
	{
		_label = GetNode<Label>("Info");
		_player = GetParent<PlayerController>();
		_health = _player.GetNode<Health>("Health");
	}

	public override void _Process(double delta)
	{
		WeaponData weapon = _player.CurrentWeapon;
		string name = weapon?.DisplayName ?? "ninguna";

		_label.Text = $"Vida   {_health.Current:0} / {_health.Max:0}\n"
			+ $"Arma   {name}   (1 / 2 / 3 para cambiar)\n"
			+ "Clic izquierdo: ligero    Clic derecho: pesado";
	}
}
