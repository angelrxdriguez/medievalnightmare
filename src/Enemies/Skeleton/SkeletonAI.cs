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

	/// <summary>Arco que barre su golpe. Estrecho: apartarse de lado lo esquiva.</summary>
	[Export(PropertyHint.Range, "10,350,5")] public float ArcDegrees { get; set; } = 70.0f;

	[Export] public float Windup { get; set; } = 0.45f;
	[Export] public float Active { get; set; } = 0.15f;
	[Export] public float Recovery { get; set; } = 0.50f;

	/// <summary>Un ataque imparable atraviesa el bloqueo. El esqueleto no lo usa: es
	/// para el ogro y el jefe, que obligan a esquivar en vez de aguantar.</summary>
	[Export] public bool Unblockable { get; set; }

	[ExportGroup("Muerte")]

	/// <summary>
	/// Segundos que dura el montón de huesos antes de desaparecer. A cero se
	/// queda para siempre, que es lo que se quiere: son treinta mallas quietas sin
	/// script ni colisión, y cuentan por dónde has pasado.
	/// </summary>
	[Export] public float CorpseSeconds { get; set; }

	private Node3D _visual;
	private SkeletonRig _rig;
	private MeleeHitbox _hitbox;
	private TelegraphMarker _marker;
	private Health _health;
	private Node3D _target;

	private float _gravity;
	private CombatPhase _phase = CombatPhase.Idle;
	private float _phaseTimer;
	private float _phaseDuration;
	private bool _dead;

	/// <summary>
	/// Lo recorrido de la fase actual, de 0 a 1. Lo lee el esqueleto para saber
	/// cuánto lleva levantado el brazo: así la animación dura lo que dura la
	/// anticipación y no lo que diga una pista grabada.
	/// </summary>
	public float PhaseProgress =>
		_phaseDuration > 0.0f ? Mathf.Clamp(1.0f - _phaseTimer / _phaseDuration, 0.0f, 1.0f) : 1.0f;

	public override void _Ready()
	{
		_visual = GetNode<Node3D>("Visual");
		_rig = _visual as SkeletonRig;
		_hitbox = GetNode<MeleeHitbox>("Visual/AttackHitbox");

		// Por búsqueda y no por ruta: las cuencas viven colgadas del cráneo, y el
		// cráneo se mueve de sitio cada vez que se retoca el esqueleto.
		_marker = _visual.FindChild("Telegraph", true, false) as TelegraphMarker;
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

		_rig?.Drive(_phase, PhaseProgress);
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
				_hitbox.Open(Damage, AttackRange, ArcDegrees, Active, Unblockable);
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
		_phaseDuration = duration;
		_marker?.SetPhase(phase, Unblockable);
		_rig?.Drive(phase, 0.0f);
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

	/// <summary>
	/// Muere en dos tiempos, y el orden importa. Primero se APAGAN las cuencas:
	/// son lo único del bicho que se ve en una sala a oscuras, así que apagarlas
	/// es lo que se lee como "ya está". Solo después se sueltan los huesos.
	///
	/// Al revés —el montón primero y la luz después— parece un fallo.
	/// </summary>
	private void OnDied()
	{
		_dead = true;
		_phase = CombatPhase.Idle;
		_hitbox.Close();
		_marker?.Extinguish();

		// Sale de la capa de enemigos para que el cadáver no estorbe.
		SetCollisionLayerValue(3, false);

		_rig?.Collapse();

		// El montón se queda. Un esqueleto que se desvanece deja la sala igual que
		// estaba y no cuenta nada; los huesos por el suelo dicen dónde has estado y
		// cuánto te ha costado, que es media atmósfera de una mazmorra.
		if (CorpseSeconds > 0.0f)
		{
			SceneTreeTimer timer = GetTree().CreateTimer(CorpseSeconds);
			timer.Timeout += QueueFree;
		}
	}
}
