using Godot;

namespace MedievalNightmare.Core;

/// <summary>
/// Vida de una entidad. Se cuelga como hijo llamado "Health" de un cuerpo físico;
/// <see cref="MeleeHitbox"/> lo busca por ese nombre para aplicar daño.
///
/// Antes de descontar, ofrece el golpe al padre si este implementa
/// <see cref="IDamageGuard"/>. Así el bloqueo vive en quien se defiende y no
/// aquí, que solo lleva la cuenta.
/// </summary>
public partial class Health : Node
{
	[Signal] public delegate void DamagedEventHandler(float amount, float remaining);
	[Signal] public delegate void DiedEventHandler();

	[Export] public float Max { get; set; } = 100.0f;

	public float Current { get; private set; }
	public bool IsDead => Current <= 0.0f;

	public override void _Ready()
	{
		Current = Max;
	}

	public void ApplyDamage(float amount, Vector3 origin = default, bool unblockable = false)
	{
		if (IsDead || amount <= 0.0f)
		{
			return;
		}

		if (GetParent() is IDamageGuard guard)
		{
			amount = guard.FilterDamage(amount, origin, unblockable);
		}

		if (amount <= 0.0f)
		{
			return;
		}

		Current = Mathf.Max(0.0f, Current - amount);
		EmitSignal(SignalName.Damaged, amount, Current);

		if (IsDead)
		{
			EmitSignal(SignalName.Died);
		}
	}
}
