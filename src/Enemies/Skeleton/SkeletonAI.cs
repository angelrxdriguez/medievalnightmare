using Godot;
using MedievalNightmare.Core;

namespace MedievalNightmare.Enemies;

/// <summary>
/// El esqueleto: el enemigo básico. Un único ataque con anticipación larga y
/// clarísima. Su trabajo es enseñar a leer la telegrafía, no ser difícil.
///
/// Se compromete igual que el jugador: una vez empieza la anticipación, el
/// golpe sale aunque te apartes. Apartarse a tiempo es la respuesta correcta.
/// </summary>
public partial class SkeletonAI : CharacterBody3D
{
	[ExportGroup("Movimiento")]
	[Export] public float MoveSpeed { get; set; } = 3.0f;
	[Export] public float TurnSpeed { get; set; } = 8.0f;
	[Export] public float DetectionRange { get; set; } = 9.0f;

	[ExportGroup("Ataque")]
	[Export] public float Damage { get; set; } = 12.0f;
	[Export] public float AttackRange { get; set; } = 1.7f;
	[Export] public float Windup { get; set; } = 0.45f;
	[Export] public float Active { get; set; } = 0.15f;
	[Export] public float Recovery { get; set; } = 0.50f;

	private Node3D _visual;
	private MeleeHitbox _hitbox;
	private TelegraphMarker _marker;
	private Health _health;
	private Node3D _target;

	private float _gravity;
	private CombatPhase _phase = CombatPhase.Idle;
	private float _phaseTimer;
	private bool _dead;

	public override void _Ready()
	{
		_visual = GetNode<Node3D>("Visual");
		_hitbox = GetNode<MeleeHitbox>("Visual/AttackHitbox");
		_marker = GetNodeOrNull<TelegraphMarker>("Visual/Telegraph");
		_health = GetNode<Health>("Health");
		_health.Died += OnDied;

		_gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle() * 2.0f;
		_target = GetTree().GetFirstNodeInGroup("player") as Node3D;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		Vector3 velocity = Velocity;

		if (!IsOnFloor())
		{
			velocity.Y -= _gravity * dt;
		}

		if (_dead || _target == null || !IsInstanceValid(_target))
		{
			velocity.X = 0.0f;
			velocity.Z = 0.0f;
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		Vector3 toTarget = _target.GlobalPosition - GlobalPosition;
		toTarget.Y = 0.0f;
		float distance = toTarget.Length();

		UpdateSwing(dt, distance);

		bool chasing = _phase == CombatPhase.Idle
			&& distance < DetectionRange
			&& distance > AttackRange;

		Vector3 desired = chasing ? toTarget.Normalized() * MoveSpeed : Vector3.Zero;
		velocity.X = desired.X;
		velocity.Z = desired.Z;
		Velocity = velocity;
		MoveAndSlide();

		if (_phase == CombatPhase.Idle || _phase == CombatPhase.Windup)
		{
			FaceTarget(toTarget, dt);
		}
	}

	private void UpdateSwing(float dt, float distance)
	{
		if (_phase == CombatPhase.Idle)
		{
			if (distance <= AttackRange)
			{
				EnterPhase(CombatPhase.Windup, Windup);
			}

			return;
		}

		_phaseTimer -= dt;
		if (_phaseTimer > 0.0f)
		{
			return;
		}

		switch (_phase)
		{
			case CombatPhase.Windup:
				EnterPhase(CombatPhase.Active, Active);
				_hitbox.Open(Damage, AttackRange);
				break;

			case CombatPhase.Active:
				_hitbox.Close();
				EnterPhase(CombatPhase.Recovery, Recovery);
				break;

			case CombatPhase.Recovery:
				EnterPhase(CombatPhase.Idle, 0.0f);
				break;
		}
	}

	private void EnterPhase(CombatPhase phase, float duration)
	{
		_phase = phase;
		_phaseTimer = duration;
		_marker?.SetPhase(phase);
	}

	private void FaceTarget(Vector3 toTarget, float dt)
	{
		if (toTarget.IsZeroApprox())
		{
			return;
		}

		Vector3 direction = toTarget.Normalized();
		float targetYaw = Mathf.Atan2(-direction.X, -direction.Z);
		float weight = 1.0f - Mathf.Exp(-TurnSpeed * dt);

		Vector3 rotation = _visual.Rotation;
		rotation.Y = Mathf.LerpAngle(rotation.Y, targetYaw, weight);
		_visual.Rotation = rotation;
	}

	private void OnDied()
	{
		_dead = true;
		_phase = CombatPhase.Idle;
		_hitbox.Close();
		_marker?.SetPhase(CombatPhase.Idle);

		// Sale de la capa de enemigos para que el cadáver no estorbe.
		SetCollisionLayerValue(3, false);

		Tween tween = CreateTween();
		tween.TweenProperty(_visual, "rotation:x", Mathf.DegToRad(-85.0f), 0.45f);
		tween.TweenInterval(1.5);
		tween.TweenCallback(Callable.From(QueueFree));
	}
}
