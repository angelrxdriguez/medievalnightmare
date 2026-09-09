using Godot;

namespace MedievalNightmare.Player;

/// <summary>
/// Movimiento en tercera persona relativo a la cámara, con gravedad y salto.
/// El CharacterBody3D nunca rota: se rota <c>Visual</c> hacia la dirección de
/// avance y <c>CameraArm</c> con el ratón, para que sean independientes.
/// </summary>
public partial class PlayerController : CharacterBody3D
{
	[ExportGroup("Movimiento")]
	[Export] public float WalkSpeed { get; set; } = 4.0f;
	[Export] public float Acceleration { get; set; } = 30.0f;
	[Export] public float Deceleration { get; set; } = 40.0f;
	[Export] public float TurnSpeed { get; set; } = 14.0f;

	[ExportGroup("Salto")]
	/// <summary>Altura del salto en metros. El impulso se deriva de aquí y de la gravedad.</summary>
	[Export] public float JumpHeight { get; set; } = 1.0f;
	[Export] public float GravityScale { get; set; } = 2.0f;
	/// <summary>Margen para saltar justo después de salirse de un borde.</summary>
	[Export] public float CoyoteTime { get; set; } = 0.12f;
	/// <summary>Margen para pulsar salto justo antes de tocar el suelo.</summary>
	[Export] public float JumpBufferTime { get; set; } = 0.12f;

	[ExportGroup("Cámara")]
	[Export] public float MouseSensitivity { get; set; } = 0.0025f;
	[Export] public float MinPitchDegrees { get; set; } = -60.0f;
	[Export] public float MaxPitchDegrees { get; set; } = 40.0f;

	private Node3D _visual;
	private SpringArm3D _cameraArm;

	private float _gravity;
	private float _jumpVelocity;
	private float _yaw;
	private float _pitch;
	private float _coyoteTimer;
	private float _jumpBufferTimer;

	public override void _Ready()
	{
		_visual = GetNode<Node3D>("Visual");
		_cameraArm = GetNode<SpringArm3D>("CameraArm");

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
		if (Input.IsActionJustPressed("jump"))
		{
			_jumpBufferTimer = JumpBufferTime;
		}

		if (_jumpBufferTimer > 0.0f && _coyoteTimer > 0.0f)
		{
			velocity.Y = _jumpVelocity;
			_jumpBufferTimer = 0.0f;
			_coyoteTimer = 0.0f;
		}

		Vector3 direction = GetMoveDirection();
		Vector3 horizontal = new Vector3(velocity.X, 0.0f, velocity.Z);
		float rate = direction.IsZeroApprox() ? Deceleration : Acceleration;
		horizontal = horizontal.MoveToward(direction * WalkSpeed, rate * dt);

		velocity.X = horizontal.X;
		velocity.Z = horizontal.Z;
		Velocity = velocity;
		MoveAndSlide();

		FaceMoveDirection(direction, dt);
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
}
