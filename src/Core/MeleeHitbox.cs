using System.Collections.Generic;
using Godot;

namespace MedievalNightmare.Core;

/// <summary>
/// Volumen de golpe. Solo hace daño mientras está abierto y como mucho una vez
/// por objetivo y por golpe. Se comprueba por sondeo en vez de por señal para
/// que la ventana activa sea exacta en fotogramas.
/// </summary>
public partial class MeleeHitbox : Area3D
{
	private readonly HashSet<ulong> _alreadyHit = new();

	private CollisionShape3D _shape;
	private MeshInstance3D _marker;
	private float _damage;
	private bool _open;

	public override void _Ready()
	{
		_shape = GetNode<CollisionShape3D>("Shape");
		_marker = GetNodeOrNull<MeshInstance3D>("Marker");
		Monitoring = false;
		if (_marker != null)
		{
			_marker.Visible = false;
		}
	}

	/// <summary>Abre la ventana activa. Un objetivo solo puede recibir un impacto por llamada.</summary>
	public void Open(float damage, float range)
	{
		_damage = damage;
		_alreadyHit.Clear();
		Resize(range);
		_open = true;
		Monitoring = true;
		if (_marker != null)
		{
			_marker.Visible = true;
		}
	}

	public void Close()
	{
		_open = false;
		Monitoring = false;
		if (_marker != null)
		{
			_marker.Visible = false;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_open)
		{
			return;
		}

		foreach (Node3D body in GetOverlappingBodies())
		{
			if (!_alreadyHit.Add(body.GetInstanceId()))
			{
				continue;
			}

			body.GetNodeOrNull<Health>("Health")?.ApplyDamage(_damage);
		}
	}

	private void Resize(float range)
	{
		if (_shape.Shape is not BoxShape3D box)
		{
			return;
		}

		box.Size = new Vector3(1.1f, 1.2f, range);
		_shape.Position = new Vector3(0.0f, 1.0f, -range * 0.5f);

		if (_marker is { Mesh: BoxMesh mesh })
		{
			mesh.Size = box.Size;
			_marker.Position = _shape.Position;
		}
	}
}
