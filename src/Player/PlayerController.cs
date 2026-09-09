using Godot;
using MedievalNightmare.Core;

namespace MedievalNightmare.Player;

/// <summary>
/// Movimiento en tercera persona relativo a la cámara, con gravedad, salto y
/// ataque cuerpo a cuerpo.
///
/// El CharacterBody3D nunca rota: se rota <c>Visual</c> hacia la dirección de
/// avance y <c>CameraArm</c> con el ratón, para que sean independientes.
///
/// El combate no gasta ningún recurso. El golpe ligero es rápido, encadenable
/// y permite moverte a velocidad reducida. El pesado te clava en el sitio
/// durante casi dos segundos: ahí es donde los enemigos te castigan.
/// </summary>
public partial class PlayerController : CharacterBody3D
{
	[ExportGroup("Movimiento")]
	[Export] public float WalkSpeed { get; set; } = 4.5f;
	[Export] public float Acceleration { get; set; } = 34.0f;
	[Export] public float Deceleration { get; set; } = 44.0f;
	[Export] public float TurnSpeed { get; set; } = 16.0f;

	[ExportGroup("Salto")]
	[Export] public float JumpHeight { get; set; } = 1.0f;
	[Export] public float GravityScale { get; set; } = 2.0f;
	[Export] public float CoyoteTime { get; set; } = 0.12f;
	[Export] public float JumpBufferTime { get; set; } = 0.12f;

	[ExportGroup("Cámara")]
	[Export] public float MouseSensitivity { get; set; } = 0.0025f;
	[Export] public float MinPitchDegrees { get; set; } = -60.0f;
	[Export] public float MaxPitchDegrees { get; set; } = 40.0f;

	[ExportGroup("Combate")]
	[Export] public Godot.Collections.Array<WeaponData> Weapons { get; set; } = new();

	/// <summary>Margen para encadenar: pulsar durante la recuperación no se pierde.</summary>
	[Export] public float AttackBufferTime { get; set; } = 0.25f;

	[ExportSubgroup("Golpe ligero")]
	[Export] public float LightWindup { get; set; } = 0.12f;
	[Export] public float LightActive { get; set; } = 0.10f;
	[Export] public float LightRecovery { get; set; } = 0.22f;

	/// <summary>Fracción de la velocidad de marcha que conservas golpeando ligero.</summary>
	[Export] public float LightMoveScale { get; set; } = 0.55f;

	[ExportSubgroup("Golpe pesado")]
	[Export] public float HeavyWindup { get; set; } = 0.75f;
	[Export] public float HeavyActive { get; set; } = 0.20f;
	[Export] public float HeavyRecovery { get; set; } = 0.70f;

	[ExportSubgroup("Muerte")]
	[Export] public float RestartDelay { get; set; } = 2.0f;

	private Node3D _visual;
	private SpringArm3D _cameraArm;
	private MeleeHitbox _hitbox;
	private TelegraphMarker _marker;
	private Health _health;

	private float _gravity;
	private float _jumpVelocity;
	private float _yaw;
	private float _pitch;
	private float _coyoteTimer;
	private float _jumpBufferTimer;

	private CombatPhase _phase = CombatPhase.Idle;
	private float _phaseTimer;
	private bool _heavySwing;
	private WeaponData _swingWeapon;
	private int _weaponIndex;
	private float _attackBuffer;
	private bool _bufferedHeavy;
	private bool _dead;

	public WeaponData CurrentWeapon =>
		Weapons.Count > 0 ? Weapons[Mathf.Clamp(_weaponIndex, 0, Weapons.Count - 1)] : null;

	public override void _Ready()
	{
		_visual = GetNode<Node3D>("Visual");
		_cameraArm = GetNode<SpringArm3D>("CameraArm");
		_hitbox = GetNode<MeleeHitbox>("Visual/AttackHitbox");
		_marker = GetNodeOrNull<TelegraphMarker>("Visual/Telegraph");
		_health = GetNode<Health>("Health");
		_health.Died += OnDied;

		_gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle() * GravityScale;
		_jumpVelocity = Mathf.Sqrt(2.0f * _gravity * JumpHeight);

		_yaw = _cameraArm.Rotation.Y;
		_pitch = _cameraArm.Rotation.X;

		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
				? Input.MouseModeEnum.Visible
				: Input.MouseModeEnum.Captured;
			return;
		}

		if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			_yaw -= motion.Relative.X * MouseSensitivity;
			_pitch = Mathf.Clamp(
				_pitch - motion.Relative.Y * MouseSensitivity,
				Mathf.DegToRad(MinPitchDegrees),
				Mathf.DegToRad(MaxPitchDegrees));
			_cameraArm.Rotation = new Vector3(_pitch, _yaw, 0.0f);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		UpdateSwing(dt);

		Vector3 velocity = Velocity;

		if (IsOnFloor())
		{
			_coyoteTimer = CoyoteTime;
		}
		else
		{
			velocity.Y -= _gravity * dt;
			_coyoteTimer -= dt;
		}

		_jumpBufferTimer -= dt;
		if (!_dead && Input.IsActionJustPressed("jump"))
		{
			_jumpBufferTimer = JumpBufferTime;
		}

		if (_jumpBufferTimer > 0.0f && _coyoteTimer > 0.0f)
		{
			velocity.Y = _jumpVelocity;
			_jumpBufferTimer = 0.0f;
			_coyoteTimer = 0.0f;
		}

		Vector3 direction = _dead ? Vector3.Zero : GetMoveDirection();
		Vector3 target = direction * WalkSpeed * MoveScale();
		Vector3 horizontal = new Vector3(velocity.X, 0.0f, velocity.Z);
		float rate = target.IsZeroApprox() ? Deceleration : Acceleration;
		horizontal = horizontal.MoveToward(target, rate * dt);

		velocity.X = horizontal.X;
		velocity.Z = horizontal.Z;
		Velocity = velocity;
		MoveAndSlide();

		if (CanTurn())
		{
			FaceMoveDirection(direction, dt);
		}
	}

	/// <summary>
	/// El ligero apenas te frena; el pesado te clava en el sitio. Es la única
	/// diferencia mecánica que hace que elegir el pesado sea una apuesta.
	/// </summary>
	private float MoveScale()
	{
		if (_dead)
		{
			return 0.0f;
		}

		if (_phase == CombatPhase.Idle)
		{
			return 1.0f;
		}

		return _heavySwing ? 0.0f : LightMoveScale;
	}

	private bool CanTurn()
	{
		if (_dead)
		{
			return false;
		}

		if (_heavySwing && _phase != CombatPhase.Idle)
		{
			// Solo puedes apuntar el pesado durante la anticipación.
			return _phase == CombatPhase.Windup;
		}

		return true;
	}

	/// <summary>Convierte el input en una dirección de mundo relativa a la cámara.</summary>
	private Vector3 GetMoveDirection()
	{
		Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
		Basis camera = _cameraArm.GlobalBasis;

		Vector3 forward = new Vector3(-camera.Z.X, 0.0f, -camera.Z.Z).Normalized();
		Vector3 right = new Vector3(camera.X.X, 0.0f, camera.X.Z).Normalized();

		return forward * -input.Y + right * input.X;
	}

	private void FaceMoveDirection(Vector3 direction, float dt)
	{
		if (direction.IsZeroApprox())
		{
			return;
		}

		float targetYaw = Mathf.Atan2(-direction.X, -direction.Z);
		float weight = 1.0f - Mathf.Exp(-TurnSpeed * dt);

		Vector3 rotation = _visual.Rotation;
		rotation.Y = Mathf.LerpAngle(rotation.Y, targetYaw, weight);
		_visual.Rotation = rotation;
	}

	private void UpdateSwing(float dt)
	{
		if (_dead)
		{
			return;
		}

		// Fuera del if: cambiar de arma a mitad de golpe no debe perderse. Surte
		// efecto en el golpe siguiente, nunca en el que ya está en curso.
		SelectWeaponFromInput();

		_attackBuffer -= dt;
		if (Input.IsActionJustPressed("attack_light"))
		{
			_attackBuffer = AttackBufferTime;
			_bufferedHeavy = false;
		}
		else if (Input.IsActionJustPressed("attack_heavy"))
		{
			_attackBuffer = AttackBufferTime;
			_bufferedHeavy = true;
		}

		if (_phase == CombatPhase.Idle)
		{
			if (_attackBuffer > 0.0f)
			{
				_attackBuffer = 0.0f;
				StartSwing(_bufferedHeavy);
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
				EnterPhase(CombatPhase.Active, _heavySwing ? HeavyActive : LightActive);
				_hitbox.Open(SwingDamage(), _swingWeapon.Range);
				break;

			case CombatPhase.Active:
				_hitbox.Close();
				EnterPhase(CombatPhase.Recovery, _heavySwing ? HeavyRecovery : LightRecovery);
				break;

			case CombatPhase.Recovery:
				EnterPhase(CombatPhase.Idle, 0.0f);
				break;
		}
	}

	private void SelectWeaponFromInput()
	{
		if (Input.IsActionJustPressed("weapon_1"))
		{
			_weaponIndex = 0;
		}
		else if (Input.IsActionJustPressed("weapon_2"))
		{
			_weaponIndex = 1;
		}
		else if (Input.IsActionJustPressed("weapon_3"))
		{
			_weaponIndex = 2;
		}
	}

	private void StartSwing(bool heavy)
	{
		if (CurrentWeapon == null)
		{
			return;
		}

		_swingWeapon = CurrentWeapon;
		_heavySwing = heavy;
		EnterPhase(CombatPhase.Windup, heavy ? HeavyWindup : LightWindup);
	}

	/// <summary>
	/// Los tiempos base se escalan con el arma: el mandoble es literalmente más
	/// lento. Se usa el arma capturada al empezar el golpe, no la actual, para
	/// que cambiar de arma a media animación no altere el golpe en curso.
	/// </summary>
	private void EnterPhase(CombatPhase phase, float duration)
	{
		_phase = phase;
		_phaseTimer = duration * (_swingWeapon?.SpeedScale ?? 1.0f);
		_marker?.SetPhase(phase);
	}

	private float SwingDamage()
	{
		return _heavySwing
			? _swingWeapon.Damage * _swingWeapon.HeavyDamageMultiplier
			: _swingWeapon.Damage;
	}

	private void OnDied()
	{
		_dead = true;
		_phase = CombatPhase.Idle;
		_hitbox.Close();
		_marker?.SetPhase(CombatPhase.Idle);
		Input.MouseMode = Input.MouseModeEnum.Visible;
		GD.Print("El jugador ha muerto. Reiniciando la sala.");

		SceneTreeTimer timer = GetTree().CreateTimer(RestartDelay);
		timer.Timeout += () => GetTree().ReloadCurrentScene();
	}
}
