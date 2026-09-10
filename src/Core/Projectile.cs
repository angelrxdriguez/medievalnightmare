using Godot;

namespace MedievalNightmare.Core;

/// <summary>
/// Virote de ballesta. No es instantáneo: viaja, y por eso a un enemigo que se
/// mueve hay que adelantarle el tiro.
///
/// Avanza a mano y comprueba con un rayo el tramo que acaba de recorrer, en vez
/// de fiarse de las colisiones del motor. Es la misma razón que en
/// <see cref="MeleeHitbox"/>: a esta velocidad, un fotograma es medio metro y
/// un cuerpo delgado se atraviesa sin enterarse.
/// </summary>
public partial class Projectile : Node3D
{
	/// <summary>Mundo y enemigos: se clava en la pared y hace daño a quien alcanza.</summary>
	[Export(PropertyHint.Layers3DPhysics)] public uint HitMask { get; set; } = 5;

	/// <summary>Si no acierta nada, desaparece. Evita dejar virotes vagando por el nivel.</summary>
	[Export] public float Lifetime { get; set; } = 5.0f;

	/// <summary>Lo que se queda clavado a la vista después de acertar.</summary>
	[Export] public float StickSeconds { get; set; } = 1.5f;

	private Vector3 _direction = Vector3.Forward;
	private float _speed = 18.0f;
	private float _damage;
	private float _age;
	private bool _spent;

	public void Launch(Vector3 origin, Vector3 direction, float speed, float damage)
	{
		GlobalPosition = origin;
		_direction = direction.Normalized();
		_speed = speed;
		_damage = damage;

		// Mirar hacia donde vuela. Con el cabeceo topado en ±80° nunca apunta recto
		// arriba, que es lo único que rompería el LookAt.
		if (Mathf.Abs(_direction.Dot(Vector3.Up)) < 0.99f)
		{
			LookAt(origin + _direction, Vector3.Up);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		_age += (float)delta;
		if (_age >= Lifetime)
		{
			QueueFree();
			return;
		}

		if (_spent)
		{
			return;
		}

		Vector3 from = GlobalPosition;
		Vector3 to = from + _direction * _speed * (float)delta;

		PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(from, to, HitMask);
		query.CollideWithAreas = false;
		Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);

		if (hit.Count == 0)
		{
			GlobalPosition = to;
			return;
		}

		GlobalPosition = (Vector3)hit["position"];

		// El origen del daño es de donde viene el virote, no de donde ha salido el
		// disparo: el bloqueo mira el ángulo del impacto.
		hit["collider"].As<Node>()?.GetNodeOrNull<Health>("Health")?.ApplyDamage(_damage, from);

		_spent = true;
		_age = Mathf.Max(_age, Lifetime - StickSeconds);
	}
}
