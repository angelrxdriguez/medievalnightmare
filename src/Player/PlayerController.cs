using Godot;
using MedievalNightmare.Core;
using MedievalNightmare.Fx;

namespace MedievalNightmare.Player;

/// <summary>
/// Movimiento en primera persona, con gravedad, salto y combate.
///
/// El CharacterBody3D nunca rota. El ratón gira <c>Head</c>, que es la vista, y
/// el cuerpo (<c>Visual</c>, que es quien lleva el arma) se pega a ella sin
/// interpolar: en primera persona el golpe tiene que salir exactamente por
/// donde apunta la mira o no se puede apuntar nada. Los dos se separan en el
/// único caso que pide el diseño: durante un golpe pesado ya lanzado la vista
/// sigue libre, pero el arma ya no gira contigo.
///
/// El combate no gasta ningún recurso. El golpe ligero es rápido, encadenable
/// y permite moverte a velocidad reducida. El pesado te clava en el sitio
/// durante casi dos segundos: ahí es donde los enemigos te castigan.
///
/// Los tiempos son los mismos para todas las armas; lo que cambia es el arco
/// que barren, el alcance y lo rápido que salen. La espada barre casi de lado
/// a lado, la maza abre un arco corto y estrecho que obliga a meterse, y el
/// pesado del mandoble gira sobre sí mismo. La ballesta no golpea: suelta un
/// virote que tarda en llegar y luego se recarga.
///
/// Tienes dos respuestas defensivas y ninguna es gratis. El bloqueo solo para
/// lo que te llega de frente y no sirve contra los ataques imparables. La
/// esquiva sí los evita, pero tiene recarga y te compromete mientras dura.
/// </summary>
public partial class PlayerController : CharacterBody3D, IDamageGuard
{
	/// <summary>
	/// La guardia se acaba de romper. Lo escucha quien tenga que contarlo: de
	/// momento el arma, que sale despedida, y en su día el HUD.
	/// </summary>
	[Signal] public delegate void GuardBrokenEventHandler();

	/// <summary>
	/// Un golpe tuyo ha conectado. Lo escuchan la mira (pulso de confirmación) y
	/// el arma (el mordisco): las piezas que cuentan el impacto, cada una lo suyo.
	/// </summary>
	[Signal] public delegate void MeleeHitEventHandler(bool heavy);

	[ExportGroup("Movimiento")]
	[Export] public float WalkSpeed { get; set; } = 4.5f;
	[Export] public float Acceleration { get; set; } = 34.0f;
	[Export] public float Deceleration { get; set; } = 44.0f;

	[ExportGroup("Salto")]
	[Export] public float JumpHeight { get; set; } = 1.0f;
	[Export] public float GravityScale { get; set; } = 2.0f;
	[Export] public float CoyoteTime { get; set; } = 0.12f;
	[Export] public float JumpBufferTime { get; set; } = 0.12f;

	[ExportGroup("Vista")]
	[Export] public float MouseSensitivity { get; set; } = 0.0025f;

	/// <summary>
	/// Tope de cabeceo. No llega a la vertical a propósito: mirando recto arriba
	/// la dirección de avance se queda sin definir y el virote sale sin orientar.
	/// </summary>
	[Export] public float MinPitchDegrees { get; set; } = -80.0f;

	[Export] public float MaxPitchDegrees { get; set; } = 80.0f;

	[ExportGroup("Combate")]

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

	[ExportSubgroup("Disparo")]
	[Export] public float ShotWindup { get; set; } = 0.15f;
	[Export] public float ShotActive { get; set; } = 0.05f;
	[Export] public float ShotRecovery { get; set; } = 0.35f;

	/// <summary>
	/// Lo que se adelanta el virote al nacer. Sale del ojo, así que sin este
	/// margen nacería dentro de la pared que tengas pegada a la cara.
	/// </summary>
	[Export] public float ShotOffset { get; set; } = 0.6f;

	[ExportSubgroup("Bloqueo")]

	/// <summary>Lo que queda del daño al bloquear. 0,2 es la reducción del 80 % del diseño.</summary>
	[Export] public float BlockDamageScale { get; set; } = 0.2f;

	/// <summary>Arco frontal que cubre la guardia. Por la espalda no bloqueas nada.</summary>
	[Export] public float BlockArcDegrees { get; set; } = 120.0f;

	[Export] public float BlockMoveScale { get; set; } = 0.45f;

	/// <summary>
	/// Daño BRUTO que aguanta la guardia dentro de la ventana antes de romperse.
	/// Veinticuatro son dos golpes de esqueleto: aguantar deja de ser respuesta a
	/// partir del segundo.
	///
	/// Se cuenta el daño y no los impactos a propósito. Así un golpe pesado rompe
	/// la guardia él solo sin que haya que marcarlo como tal en ningún sitio: lo
	/// que abre la guardia es lo fuerte que venía el golpe, que es lo que el
	/// jugador ve.
	/// </summary>
	[Export] public float GuardBreakDamage { get; set; } = 24.0f;

	/// <summary>
	/// Ventana en la que se suma el daño bloqueado, contada desde el ÚLTIMO
	/// impacto. Encajar dos golpes sueltos a lo largo de una pelea no rompe nada;
	/// aguantar con la guardia alta mientras te llueven, sí.
	/// </summary>
	[Export] public float GuardLoadWindow { get; set; } = 3.0f;

	/// <summary>
	/// Lo que tardas en recuperar la guardia una vez rota. Diez segundos es una
	/// pelea entera a pecho descubierto: es el precio de haber aguantado en vez de
	/// haberte movido.
	/// </summary>
	[Export] public float GuardBreakRecovery { get; set; } = 10.0f;

	[ExportSubgroup("Esquiva")]
	[Export] public float DashSpeed { get; set; } = 9.5f;
	[Export] public float DashDuration { get; set; } = 0.4f;

	/// <summary>Se cuenta desde que empieza la esquiva: puedes esquivar cada 2 s.</summary>
	[Export] public float DashCooldown { get; set; } = 2.0f;

	private const string WhooshLightSound = "res://assets/audio/whoosh_light.wav";
	private const string WhooshHeavySound = "res://assets/audio/whoosh_heavy.wav";
	private const string HitBoneSound = "res://assets/audio/hit_bone.wav";
	private const string HitBlockSound = "res://assets/audio/hit_block.wav";
	private const string HitTakenSound = "res://assets/audio/hit_taken.wav";
	private const string GuardBreakSound = "res://assets/audio/guard_break.wav";
	private const string BoltShotSound = "res://assets/audio/bolt_shot.wav";
	private const string DashSound = "res://assets/audio/dash.wav";

	private Node3D _visual;
	private Node3D _head;
	private MeleeHitbox _hitbox;
	private CameraFeedback _camera;
	private Health _health;
	private Inventory _inventory;

	private float _gravity;
	private float _jumpVelocity;
	private float _yaw;
	private float _pitch;
	private float _coyoteTimer;
	private float _jumpBufferTimer;

	private CombatPhase _phase = CombatPhase.Idle;
	private float _phaseTimer;
	private float _phaseDuration;
	private bool _heavySwing;
	private WeaponData _swingWeapon;
	private float _attackBuffer;
	private bool _bufferedHeavy;
	private bool _blocking;
	private float _guardLoad;
	private float _guardWindowTimer;
	private float _guardBreakTimer;
	private float _dashTimer;
	private float _dashCooldownTimer;
	private Vector3 _dashDirection;
	private float _reloadTimer;
	private bool _dead;

	/// <summary>Cuándo paró algo la guardia por última vez. Es lo que separa el
	/// clang del bloqueo del golpe sordo de encajar: los dos salen de la misma
	/// señal de daño y solo este instante dice cuál toca.</summary>
	private ulong _lastBlockMsec;

	/// <summary>
	/// El arma de la ranura principal, y nada más. El jugador no lleva una lista
	/// de armas: lleva la que se ha puesto, y cambiarla es abrir el inventario.
	/// Sin arma equipada no hay golpe, que es exactamente lo que pasa si te la
	/// quitas.
	/// </summary>
	public WeaponData CurrentWeapon => _inventory?.Main as WeaponData;

	/// <summary>Guardia alta. Se cae sola al atacar, al esquivar y al morir.</summary>
	public bool IsBlocking => _blocking;

	/// <summary>
	/// La guardia está rota y no se puede levantar. Dura
	/// <see cref="GuardBreakRecovery"/> segundos.
	/// </summary>
	public bool IsGuardBroken => _guardBreakTimer > 0.0f;

	/// <summary>Segundos que faltan para recuperar la guardia. Cero si está entera.</summary>
	public float GuardBreakRemaining => _guardBreakTimer;

	/// <summary>
	/// Lo cargada que está la guardia, de 0 a 1. Uno es romperse. Nadie lo pinta
	/// todavía: es lo que va a leer el HUD cuando exista.
	/// </summary>
	public float GuardLoadRatio =>
		GuardBreakDamage > 0.0f ? Mathf.Clamp(_guardLoad / GuardBreakDamage, 0.0f, 1.0f) : 0.0f;

	public bool IsDashing => _dashTimer > 0.0f;

	/// <summary>Segundos que faltan para poder volver a esquivar. Cero si está lista.</summary>
	public float DashCooldownRemaining => _dashCooldownTimer;

	/// <summary>Segundos que faltan para poder volver a disparar. Cero si está cargada.</summary>
	public float ReloadRemaining => _reloadTimer;

	/// <summary>
	/// Fase del golpe en curso. En primera persona no te ves a ti mismo, así que
	/// esto es lo que lee la mira: es toda la telegrafía que tienes de ti.
	/// </summary>
	public CombatPhase Phase => _phase;

	/// <summary>
	/// El golpe en curso ya no se reorienta por mucho que gires la vista. Solo le
	/// pasa al pesado, y es justo lo que lo convierte en una apuesta.
	/// </summary>
	public bool IsCommitted => _phase != CombatPhase.Idle && !CanTurn();

	/// <summary>
	/// Lo recorrido de la fase actual, de 0 a 1. Es lo que hace que el arma que se
	/// ve (<see cref="WeaponView"/>) no tenga sus propios tiempos: la animación no
	/// dura lo que diga una curva, dura exactamente lo que dura el golpe. Con esto
	/// el mandoble se ve lento porque ES lento, no porque tenga otra animación.
	/// </summary>
	public float PhaseProgress =>
		_phaseDuration > 0.0f ? Mathf.Clamp(1.0f - _phaseTimer / _phaseDuration, 0.0f, 1.0f) : 1.0f;

	/// <summary>El golpe en curso es el pesado. Vale también durante la recuperación.</summary>
	public bool IsHeavySwing => _heavySwing;

	/// <summary>Lo recorrido de la recarga, de 0 a 1. Uno es lista para disparar.</summary>
	public float ReloadProgress
	{
		get
		{
			float total = CurrentWeapon?.ReloadSeconds ?? 0.0f;

			return total > 0.0f ? Mathf.Clamp(1.0f - _reloadTimer / total, 0.0f, 1.0f) : 1.0f;
		}
	}

	/// <summary>Virotes que te quedan. Sin munición la ballesta es peso muerto.</summary>
	public int CurrentAmmo => _inventory?.AmmoOf(CurrentWeapon) ?? 0;

	public int CurrentAmmoCapacity => CurrentWeapon?.AmmoCapacity ?? 0;

	public override void _Ready()
	{
		_visual = GetNode<Node3D>("Visual");
		_head = GetNode<Node3D>("Head");
		_hitbox = GetNode<MeleeHitbox>("Visual/AttackHitbox");
		_hitbox.HitLanded += OnMeleeHitLanded;
		_camera = GetNodeOrNull<CameraFeedback>("Head/Camera");
		_health = GetNode<Health>("Health");
		_health.Died += OnDied;
		_health.Damaged += OnDamaged;
		_inventory = GetNode<Inventory>("Inventory");

		_gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle() * GravityScale;
		_jumpVelocity = Mathf.Sqrt(2.0f * _gravity * JumpHeight);

		_yaw = _head.Rotation.Y;
		_pitch = _head.Rotation.X;
		_visual.Rotation = new Vector3(0.0f, _yaw, 0.0f);

		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Escape no se toca aquí: lo lleva el menú de pausa, que es quien sabe si
		// hay que soltar el ratón o volver a capturarlo.
		if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			_yaw -= motion.Relative.X * MouseSensitivity;
			_pitch = Mathf.Clamp(
				_pitch - motion.Relative.Y * MouseSensitivity,
				Mathf.DegToRad(MinPitchDegrees),
				Mathf.DegToRad(MaxPitchDegrees));
			_head.Rotation = new Vector3(_pitch, _yaw, 0.0f);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		// Lo primero: el cuerpo se pega a la vista. Así lo que salga en este
		// fotograma —un golpe, la guardia, una esquiva— sale ya apuntando a donde
		// miras, sin un fotograma de retraso.
		AimBodyAtView();

		// La esquiva va antes que el golpe: si termina en este fotograma, un ataque
		// que hayas dejado en el buffer sale ya, sin esperar al siguiente.
		UpdateDash(dt);
		UpdateSwing(dt);

		UpdateGuard(dt);

		_blocking = !_dead
			&& !IsDashing
			&& !IsGuardBroken
			&& _phase == CombatPhase.Idle
			&& Input.IsActionPressed("block");

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
		if (!_dead && !IsDashing && Input.IsActionJustPressed("jump"))
		{
			_jumpBufferTimer = JumpBufferTime;
		}

		if (_jumpBufferTimer > 0.0f && _coyoteTimer > 0.0f)
		{
			velocity.Y = _jumpVelocity;
			_jumpBufferTimer = 0.0f;
			_coyoteTimer = 0.0f;
		}

		Vector3 direction = _dead || IsDashing ? Vector3.Zero : GetMoveDirection();

		if (IsDashing)
		{
			// Velocidad fija y sin frenada: la esquiva es un desplazamiento, no una carrera.
			velocity.X = _dashDirection.X * DashSpeed;
			velocity.Z = _dashDirection.Z * DashSpeed;
		}
		else
		{
			Vector3 target = direction * WalkSpeed * MoveScale();
			Vector3 horizontal = new Vector3(velocity.X, 0.0f, velocity.Z);
			float rate = target.IsZeroApprox() ? Deceleration : Acceleration;
			horizontal = horizontal.MoveToward(target, rate * dt);

			velocity.X = horizontal.X;
			velocity.Z = horizontal.Z;
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	/// <summary>
	/// El arma sale por donde apunta la mira, salvo cuando el golpe en curso ya no
	/// se puede reorientar. No hay giro progresivo a propósito: cualquier retraso
	/// entre lo que ves y por dónde sale el filo se siente roto en primera persona.
	/// </summary>
	private void AimBodyAtView()
	{
		if (!CanTurn())
		{
			return;
		}

		Vector3 rotation = _visual.Rotation;
		rotation.Y = _yaw;
		_visual.Rotation = rotation;
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

		if (_phase != CombatPhase.Idle)
		{
			return _heavySwing ? 0.0f : LightMoveScale;
		}

		return _blocking ? BlockMoveScale : 1.0f;
	}

	private bool CanTurn()
	{
		if (_dead)
		{
			return false;
		}

		if (_heavySwing && _phase != CombatPhase.Idle)
		{
			// Solo puedes apuntar el pesado durante la anticipación. Después la vista
			// sigue girando libre, pero el arma se queda donde la dejaste.
			return _phase == CombatPhase.Windup;
		}

		return true;
	}

	/// <summary>Convierte el input en una dirección de mundo relativa a la vista.</summary>
	private Vector3 GetMoveDirection()
	{
		Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
		Basis view = _head.GlobalBasis;

		Vector3 forward = new Vector3(-view.Z.X, 0.0f, -view.Z.Z).Normalized();
		Vector3 right = new Vector3(view.X.X, 0.0f, view.X.Z).Normalized();

		return forward * -input.Y + right * input.X;
	}

	/// <summary>Hacia dónde miras, aplanado al suelo: el cabeceo no mueve ni empuja.</summary>
	private Vector3 GetViewForward()
	{
		Basis view = _head.GlobalBasis;
		return new Vector3(-view.Z.X, 0.0f, -view.Z.Z).Normalized();
	}

	/// <summary>
	/// La esquiva no da invulnerabilidad: lo que te salva es dejar de estar donde
	/// va a caer el golpe. No se puede usar a mitad de un ataque, porque ningún
	/// golpe se cancela.
	/// </summary>
	private void UpdateDash(float dt)
	{
		_dashCooldownTimer = Mathf.Max(0.0f, _dashCooldownTimer - dt);

		if (IsDashing)
		{
			_dashTimer -= dt;
			return;
		}

		if (_dead || _phase != CombatPhase.Idle || _dashCooldownTimer > 0.0f)
		{
			return;
		}

		if (!Input.IsActionJustPressed("dash"))
		{
			return;
		}

		Vector3 direction = GetMoveDirection();
		if (direction.IsZeroApprox())
		{
			direction = GetViewForward();
		}

		if (direction.IsZeroApprox())
		{
			return;
		}

		_dashDirection = direction.Normalized();
		_dashTimer = DashDuration;
		_dashCooldownTimer = DashCooldown;
		Sfx.Play(this, DashSound, -6.0f);
	}

	private void UpdateSwing(float dt)
	{
		if (_dead)
		{
			return;
		}

		_reloadTimer = Mathf.Max(0.0f, _reloadTimer - dt);

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
			// Durante la esquiva se puede encolar el golpe, pero no sacarlo: sale
			// en cuanto termina el desplazamiento.
			if (_attackBuffer > 0.0f && !IsDashing && CanStartSwing())
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
				OpenActiveWindow();
				break;

			case CombatPhase.Active:
				_hitbox.Close();
				EnterPhase(CombatPhase.Recovery, RecoveryTime());
				break;

			case CombatPhase.Recovery:
				EnterPhase(CombatPhase.Idle, 0.0f);
				break;
		}
	}

	/// <summary>
	/// Fin de la anticipación: aquí sale el golpe. El cuerpo a cuerpo abre el arco
	/// durante toda la ventana activa; la ballesta suelta el virote y empieza a
	/// recargar en el mismo instante, así que la cadencia la marca la recarga.
	/// </summary>
	private void OpenActiveWindow()
	{
		if (_swingWeapon.Kind == WeaponKind.Ranged)
		{
			EnterPhase(CombatPhase.Active, ShotActive);
			FireProjectile();
			_reloadTimer = _swingWeapon.ReloadSeconds;
			return;
		}

		EnterPhase(CombatPhase.Active, _heavySwing ? HeavyActive : LightActive);
		Sfx.Play(this, _heavySwing ? WhooshHeavySound : WhooshLightSound, -4.0f);

		// Solo el pesado interrumpe al que lo encaja. El ligero deja flinch y nada
		// más: si también cortara, spamearlo sería la respuesta a todo (M2).
		_hitbox.Open(SwingDamage(), _swingWeapon.Range, SwingArc(), _phaseTimer, unblockable: false, stagger: _heavySwing);
	}

	/// <summary>
	/// El golpe ha conectado: aquí vive todo lo que convierte "le he quitado
	/// vida" en "le he PEGADO". Parón, coz de cámara, chispas y sonido salen del
	/// mismo sitio para que no puedan desincronizarse entre sí.
	///
	/// Si el barrido alcanza a varios, esto entra una vez por cabeza: dos
	/// impactos seguidos SUENAN a dos impactos, y el parón se encadena solo.
	/// </summary>
	private void OnMeleeHitLanded(Node3D body, Vector3 point)
	{
		bool heavy = _heavySwing;

		HitStop.Apply(this, heavy ? 0.09f : 0.05f);
		Sfx.Play(this, HitBoneSound, heavy ? 2.0f : -1.0f, heavy ? 0.85f : 1.0f);
		HitSpark.Spawn(this, point);

		// La coz muerde hacia abajo, siguiendo al filo que se clava. El pesado
		// además abre el campo de visión un instante: es el "uff" del esfuerzo.
		_camera?.Kick(new Vector3(heavy ? -2.2f : -0.9f, 0.0f, heavy ? 0.8f : 0.35f));
		if (heavy)
		{
			_camera?.PunchFov(2.5f);
		}

		EmitSignal(SignalName.MeleeHit, heavy);
	}

	/// <summary>
	/// Te han dado a ti. El tinte rojo lo lleva la viñeta; aquí va lo que se
	/// siente en el cuerpo: el temblor y el golpe sordo. Si la guardia acaba de
	/// parar este mismo golpe, el clang ya ha sonado y el temblor se queda en
	/// nada: bloquear tiene que SENTIRSE mejor que encajar o no vale para nada.
	/// </summary>
	private void OnDamaged(float amount, float remaining)
	{
		bool blocked = Time.GetTicksMsec() - _lastBlockMsec < 60;

		if (!blocked)
		{
			Sfx.Play(this, HitTakenSound, 2.0f);
		}

		_camera?.AddTrauma(blocked ? 0.2f : Mathf.Clamp(0.35f + amount / 45.0f, 0.0f, 0.85f));
	}

	private float RecoveryTime()
	{
		if (_swingWeapon.Kind == WeaponKind.Ranged)
		{
			return ShotRecovery;
		}

		return _heavySwing ? HeavyRecovery : LightRecovery;
	}

	/// <summary>
	/// Sin virotes o con la ballesta a medio recargar no sale nada. El botón se
	/// queda muerto a propósito: es la única forma de que gastar munición pese.
	/// </summary>
	private bool CanStartSwing()
	{
		WeaponData weapon = CurrentWeapon;

		if (weapon == null)
		{
			return false;
		}

		if (weapon.Kind != WeaponKind.Ranged)
		{
			return true;
		}

		return _reloadTimer <= 0.0f && CurrentAmmo > 0;
	}

	private void StartSwing(bool heavy)
	{
		_swingWeapon = CurrentWeapon;

		// La ballesta no tiene golpe pesado: los dos botones sueltan el mismo virote.
		_heavySwing = heavy && _swingWeapon.Kind == WeaponKind.Melee;

		EnterPhase(
			CombatPhase.Windup,
			_swingWeapon.Kind == WeaponKind.Ranged
				? ShotWindup
				: _heavySwing ? HeavyWindup : LightWindup);
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
		_phaseDuration = _phaseTimer;
	}

	private float SwingDamage()
	{
		return _heavySwing
			? _swingWeapon.Damage * _swingWeapon.HeavyDamageMultiplier
			: _swingWeapon.Damage;
	}

	/// <summary>
	/// Lo que separa a un arma de otra. La maza abre un arco estrecho y hay que
	/// apuntarla; la espada barre casi de lado a lado; el pesado del mandoble da
	/// la vuelta entera, y por eso es lo que se saca cuando te rodean.
	/// </summary>
	private float SwingArc()
	{
		return _heavySwing ? _swingWeapon.HeavyArcDegrees : _swingWeapon.LightArcDegrees;
	}

	private void FireProjectile()
	{
		if (_swingWeapon.Projectile == null)
		{
			GD.PushWarning($"{_swingWeapon.DisplayName} no tiene proyectil asignado.");
			return;
		}

		_inventory.SpendAmmo(_swingWeapon);

		// Sale del ojo y en la dirección exacta de la mira, cabeceo incluido: es lo
		// que hace que apuntar con la ballesta consista en poner la cruz encima.
		Vector3 direction = (-_head.GlobalBasis.Z).Normalized();
		Vector3 origin = _head.GlobalPosition + direction * ShotOffset;

		Sfx.Play(this, BoltShotSound, -2.0f);

		Projectile bolt = _swingWeapon.Projectile.Instantiate<Projectile>();
		GetTree().CurrentScene.AddChild(bolt);
		bolt.Launch(origin, direction, _swingWeapon.ProjectileSpeed, _swingWeapon.Damage);
	}

	/// <summary>
	/// Enfría la guardia rota y vacía la carga si ha pasado la ventana sin recibir
	/// nada. Lo segundo es lo que separa "me han pegado dos veces seguidas" de "me
	/// han pegado dos veces en toda la sala".
	/// </summary>
	private void UpdateGuard(float dt)
	{
		if (_guardBreakTimer > 0.0f)
		{
			_guardBreakTimer = Mathf.Max(0.0f, _guardBreakTimer - dt);
		}

		if (_guardWindowTimer > 0.0f)
		{
			_guardWindowTimer -= dt;
			if (_guardWindowTimer <= 0.0f)
			{
				_guardLoad = 0.0f;
			}
		}
	}

	/// <summary>
	/// Suma lo que acaba de parar la guardia y la rompe si se pasa. El golpe que
	/// la rompe SÍ se bloquea: la guardia aguanta ese y cede después. Romperse
	/// antes de parar el golpe convierte el segundo impacto en daño doble y eso no
	/// se lee, se sufre.
	/// </summary>
	private void LoadGuard(float rawDamage)
	{
		_lastBlockMsec = Time.GetTicksMsec();
		Sfx.Play(this, HitBlockSound, -2.0f);

		_guardLoad += rawDamage;
		_guardWindowTimer = GuardLoadWindow;

		if (_guardLoad < GuardBreakDamage)
		{
			return;
		}

		_guardLoad = 0.0f;
		_guardWindowTimer = 0.0f;
		_guardBreakTimer = GuardBreakRecovery;
		_blocking = false;

		// El clang del golpe parado ya ha sonado; esto es la guardia cediendo
		// DESPUÉS, que es exactamente el orden que cuenta la regla.
		Sfx.Play(this, GuardBreakSound, 2.0f);
		_camera?.AddTrauma(0.65f);
		EmitSignal(SignalName.GuardBroken);
	}

	/// <summary>
	/// El bloqueo solo cubre el arco frontal y no existe contra los imparables.
	/// Es lo que impide que la guardia alta sea la respuesta a todo.
	///
	/// Lo que para de verdad carga la guardia: ver <see cref="LoadGuard"/>.
	/// </summary>
	public float FilterDamage(float amount, Vector3 origin, bool unblockable)
	{
		if (unblockable || !_blocking)
		{
			return amount;
		}

		Vector3 toOrigin = origin - GlobalPosition;
		toOrigin.Y = 0.0f;

		if (toOrigin.IsZeroApprox())
		{
			LoadGuard(amount);
			return amount * BlockDamageScale;
		}

		Vector3 facing = -_visual.GlobalBasis.Z;
		facing.Y = 0.0f;

		float angle = Mathf.RadToDeg(facing.Normalized().AngleTo(toOrigin.Normalized()));

		// Por el flanco y por la espalda no hay guardia que romper: el golpe entra
		// entero y no carga nada.
		if (angle > BlockArcDegrees * 0.5f)
		{
			return amount;
		}

		LoadGuard(amount);

		return amount * BlockDamageScale;
	}

	/// <summary>
	/// Se queda quieto y suelta todo lo que tuviera empezado. Contar la muerte y
	/// reiniciar es cosa de <see cref="Ui.RaidEndScreen"/>: quien sabe cuándo se
	/// ha terminado de contar es quien la está contando.
	/// </summary>
	private void OnDied()
	{
		_dead = true;
		_phase = CombatPhase.Idle;
		_blocking = false;
		_guardLoad = 0.0f;
		_guardWindowTimer = 0.0f;
		_guardBreakTimer = 0.0f;
		_dashTimer = 0.0f;
		_hitbox.Close();
	}
}
