using Godot;

namespace MedievalNightmare.Core;

/// <summary>
/// Lo implementa el cuerpo del que cuelga un <see cref="Health"/> cuando puede
/// reducir el daño que recibe. Si no lo implementa, el daño entra entero: los
/// enemigos no se defienden, solo el jugador.
/// </summary>
public interface IDamageGuard
{
	/// <param name="amount">Daño original del golpe.</param>
	/// <param name="origin">De dónde viene el golpe. Sirve para exigir que llegue de frente.</param>
	/// <param name="unblockable">Los ataques imparables ignoran cualquier defensa.</param>
	/// <returns>El daño que se aplica de verdad.</returns>
	float FilterDamage(float amount, Vector3 origin, bool unblockable);
}
