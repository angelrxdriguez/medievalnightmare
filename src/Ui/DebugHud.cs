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
		string guard = GuardText();
		float cooldown = _player.DashCooldownRemaining;
		string dash = cooldown > 0.0f ? $"{cooldown:0.0} s" : "lista";

		_label.Text = $"Vida   {_health.Current:0} / {_health.Max:0}\n"
			+ $"Arma   {name}   (1 / 2 / 3 / 4 para cambiar)\n"
			+ $"Guardia   {guard}      Esquiva   {dash}{CommitLine()}\n"
			+ AmmoLine(weapon)
			+ "Clic izq: ligero   Clic der: pesado   Shift: bloquear   Q: esquivar";
	}

	/// <summary>
	/// La guardia, con su carga. La carga se enseña en crudo porque es justo lo
	/// que hay que mirar mientras se ajusta cuántos golpes aguanta: la pose del
	/// arma dice que está rota, pero no dice lo cerca que estabas de que lo
	/// estuviera.
	/// </summary>
	private string GuardText()
	{
		if (_player.IsGuardBroken)
		{
			return $"ROTA {_player.GuardBreakRemaining:0.0} s";
		}

		string load = _player.GuardLoadRatio > 0.0f ? $" [{_player.GuardLoadRatio * 100.0f:0} %]" : string.Empty;

		return (_player.IsBlocking ? "ALTA" : "baja") + load;
	}

	/// <summary>
	/// Solo mientras el golpe ya no se puede reorientar. En primera persona la
	/// vista sigue girando aunque el arma no, y sin este aviso parece un fallo.
	/// </summary>
	private string CommitLine()
	{
		return _player.IsCommitted ? "      GOLPE COMPROMETIDO" : string.Empty;
	}

	/// <summary>Solo con la ballesta en la mano. Con las demás no hay nada que contar.</summary>
	private string AmmoLine(WeaponData weapon)
	{
		if (weapon == null || weapon.Kind != WeaponKind.Ranged)
		{
			return string.Empty;
		}

		string state = _player.CurrentAmmo <= 0
			? "sin munición"
			: _player.ReloadRemaining > 0.0f
				? $"recargando {_player.ReloadRemaining:0.0} s"
				: "cargada";

		return $"Virotes   {_player.CurrentAmmo} / {_player.CurrentAmmoCapacity}      {state}\n";
	}
}
