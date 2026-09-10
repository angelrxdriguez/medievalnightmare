using System.Collections.Generic;
using Godot;

namespace MedievalNightmare.Core;

/// <summary>
/// Volumen de golpe. Solo hace daño mientras está abierto y como mucho una vez
/// por objetivo y por golpe.
///
/// No es una caja recta: es un arco. La forma física es un cilindro de radio
/// igual al alcance y el filtro de verdad es el ángulo. Cada fotograma se
/// comprueba qué trozo de arco ha barrido el filo desde el fotograma anterior,
/// de izquierda a derecha, y se golpea a quien caiga dentro de ese trozo. Como
/// los trozos son contiguos por construcción, un barrido rápido no se salta a
/// nadie por muchos fotogramas que dure, y un arco ancho alcanza a varios
/// enemigos en el mismo golpe sin que deje de importar dónde están.
/// </summary>
public partial class MeleeHitbox : Area3D
{
	/// <summary>Alto del cilindro. Cubre a un humanoide de 1,8 m de pies a cabeza.</summary>
	[Export] public float Height { get; set; } = 2.0f;

	private readonly HashSet<ulong> _alreadyHit = new();

	private CollisionShape3D _shape;
	private MeshInstance3D _marker;
	private float _damage;
	private float _range;
	private float _halfArc;
	private float _duration;
	private float _elapsed;
	private bool _unblockable;
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
	/// <param name="arcDegrees">Anchura del barrido. Estrecho exige apuntar; ancho barre a varios.</param>
	/// <param name="duration">Lo que dura la ventana activa: el filo la recorre entera.</param>
	/// <param name="unblockable">Un golpe imparable atraviesa el bloqueo: hay que esquivarlo.</param>
	public void Open(float damage, float range, float arcDegrees, float duration, bool unblockable = false)
	{
		_damage = damage;
		_range = range;
		_halfArc = Mathf.DegToRad(Mathf.Clamp(arcDegrees, 1.0f, 350.0f)) * 0.5f;
		_duration = Mathf.Max(duration, 0.001f);
		_elapsed = 0.0f;
		_unblockable = unblockable;
		_alreadyHit.Clear();
		Resize();
		_open = true;
		Monitoring = true;
		if (_marker != null)
		{
			_marker.Visible = true;
			PlaceMarker(_halfArc);
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

		// El filo va de izquierda (ángulo positivo) a derecha (negativo). Se golpea
		// a todo lo que el filo ya ha rebasado, no solo al trozo de este fotograma:
		// el Area3D no tiene solapes el primer fotograma después de abrirse, y así
		// nadie se salva por haber caído justo en ese hueco. Repetir no puede,
		// porque cada objetivo entra una sola vez en la lista de alcanzados.
		_elapsed += (float)delta;
		float edge = SweepAngle(_elapsed);

		PlaceMarker(edge);
		HitSweptArc(edge);
	}

	/// <summary>Dónde está el filo en el instante <paramref name="time"/> de la ventana activa.</summary>
	private float SweepAngle(float time)
	{
		float t = Mathf.Clamp(time / _duration, 0.0f, 1.0f);
		return Mathf.Lerp(_halfArc, -_halfArc, t);
	}

	/// <param name="edge">Dónde está el filo ahora. Lo que queda entre él y el
	/// principio del arco es lo que ya ha barrido.</param>
	private void HitSweptArc(float edge)
	{
		Vector3 facing = -GlobalBasis.Z;
		facing.Y = 0.0f;
		if (facing.IsZeroApprox())
		{
			return;
		}

		facing = facing.Normalized();

		foreach (Node3D body in GetOverlappingBodies())
		{
			if (_alreadyHit.Contains(body.GetInstanceId()))
			{
				continue;
			}

			Vector3 toBody = body.GlobalPosition - GlobalPosition;
			toBody.Y = 0.0f;

			// Encima de ti no tiene ángulo: cuenta como alcanzado igual.
			if (!toBody.IsZeroApprox())
			{
				float angle = facing.SignedAngleTo(toBody.Normalized(), Vector3.Up);
				if (angle < edge || angle > _halfArc)
				{
					continue;
				}
			}

			_alreadyHit.Add(body.GetInstanceId());

			// El Area3D no se desplaza al redimensionar (solo su forma), así que su
			// posición global es la del atacante: justo lo que necesita el bloqueo.
			body.GetNodeOrNull<Health>("Health")?.ApplyDamage(_damage, GlobalPosition, _unblockable);
		}
	}

	private void Resize()
	{
		if (_shape.Shape is CylinderShape3D cylinder)
		{
			cylinder.Radius = _range;
			cylinder.Height = Height;
			_shape.Position = new Vector3(0.0f, Height * 0.5f, 0.0f);
		}

		if (_marker is { Mesh: BoxMesh mesh })
		{
			mesh.Size = new Vector3(0.16f, 0.16f, _range);
		}
	}

	/// <summary>
	/// El filo de greybox. Es lo único que enseña por dónde va el barrido hasta
	/// que haya animaciones, y gira alrededor del atacante, no sobre sí mismo.
	/// </summary>
	private void PlaceMarker(float angle)
	{
		if (_marker == null)
		{
			return;
		}

		Basis basis = new(Vector3.Up, angle);
		_marker.Transform = new Transform3D(basis, basis * new Vector3(0.0f, 1.0f, -_range * 0.5f));
	}
}
