using Godot;
using MedievalNightmare.Core;

namespace MedievalNightmare.Enemies;

/// <summary>
/// El esqueleto: el enemigo básico. Un único ataque con anticipación larga y
/// clarísima. Su trabajo es enseñar a leer la telegrafía, no ser difícil.
///
/// Se compromete igual que el jugador: una vez empieza la anticipación, el
/// golpe sale aunque te apartes. Apartarse a tiempo es la respuesta correcta.
///
/// TRES COSAS QUE NO SON EL ATAQUE Y SIN LAS CUALES PELEAR CONTRA TRES NO ES
/// UNA PELEA:
///
/// - ANDA POR LA SALA, no hacia el jugador. Con una recta a la posición del
///   jugador, cuatro pilares y una columna bastan para dejar al bicho empujando
///   una esquina. La ruta la pone <c>NavigationAgent3D</c> y la evitación
///   impide que tres convergiendo al mismo punto se conviertan en un tapón.
/// - MIRA. La detección no es un radio: es un radio Y una línea de visión. Sin
///   ella los tres se despiertan a la vez en cuanto cruzas el centro de la sala,
///   y la regla de densidad del diseño —encontrártelos de uno en uno— no se
///   sostiene. De cerca no hace falta ver: a menos de <see cref="HearingRange"/>
///   te oye igual, que para eso arrastra los pies.
/// - ESPERA TURNO. Solo <see cref="MaxAttackers"/> entran a pegar; el resto
///   mantiene un anillo y lo rodea. Es lo que convierte tres esqueletos en un
///   problema de colocación en vez de en una carrera de daño, que es el riesgo
///   que apunta DISENO.md §12. También es lo que hace que el arco de guardia de
///   120° signifique algo: si los tres pegasen a la vez, daría igual a dónde
///   mires.
/// </summary>
public partial class SkeletonAI : CharacterBody3D
{
	/// <summary>
	/// Grupo por el que los enemigos se cuentan entre ellos para repartirse el
	/// turno de ataque. No hay coordinador ni nodo director: el turno se pide
	/// mirando a los vecinos, que para tres bichos sale más barato que mantener
	/// una lista viva.
	/// </summary>
	public const string EnemyGroup = "enemies";

	[ExportGroup("Movimiento")]
	[Export] public float MoveSpeed { get; set; } = 3.0f;
	[Export] public float TurnSpeed { get; set; } = 8.0f;

	[ExportGroup("Percepción")]

	/// <summary>Hasta dónde te ve, si es que te ve. Es también la separación mínima
	/// entre enemigos que pide la regla de densidad del diseño.</summary>
	[Export] public float DetectionRange { get; set; } = 9.0f;

	/// <summary>Hasta dónde te oye aunque no te vea. Sin esto, pegarle por la
	/// espalda desde un metro no lo despierta y parece roto.</summary>
	[Export] public float HearingRange { get; set; } = 3.5f;

	/// <summary>Lo lejos que tienes que irte para que deje de buscarte.</summary>
	[Export] public float ForgetRange { get; set; } = 16.0f;

	/// <summary>Lo que aguanta lejos y sin verte antes de volverse a su sitio.</summary>
	[Export] public float ForgetSeconds { get; set; } = 4.0f;

	/// <summary>Desde dónde mira. A la altura del cráneo, no de los pies: desde el
	/// suelo cualquier escalón le tapa la vista.</summary>
	[Export] public float EyeHeight { get; set; } = 1.45f;

	/// <summary>Cada cuánto se tira el rayo de visión. Cinco veces por segundo
	/// sobra para una anticipación de 0,45 s y no cuesta nada.</summary>
	[Export] public float SightInterval { get; set; } = 0.2f;

	[ExportGroup("Colocación")]

	/// <summary>Radio al que espera el que no tiene turno. Fuera del alcance del
	/// jugador con cualquier arma menos el mandoble, que es de lo que va.</summary>
	[Export] public float RingRadius { get; set; } = 3.4f;

	/// <summary>Lo que rodea por segundo mientras espera. Rodear es lo que te
	/// obliga a girarte, y girarte es lo que abre tu espalda al que sí tiene turno.</summary>
	[Export] public float RingDriftDegrees { get; set; } = 30.0f;

	/// <summary>Cuántos entran a pegar a la vez, contando a todo el grupo. Es EL
	/// número de la pelea contra varios: a tres, aguantar la guardia gana siempre.</summary>
	[Export] public int MaxAttackers { get; set; } = 1;

	/// <summary>Lo que puede tener el turno sin llegar a usarlo. Sin este tope, el
	/// que persigue a un jugador que huye se queda el turno para siempre y los
	/// otros dos lo escoltan sin hacer nada.</summary>
	[Export] public float PressSeconds { get; set; } = 6.0f;

	/// <summary>Lo que espera fuera después de pegar. Es el hueco por el que
	/// contraatacas: sin él, el mismo bicho encadena golpes a bocajarro.</summary>
	[Export] public float RegroupSeconds { get; set; } = 1.2f;

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

	/// <summary>
	/// Lo que suelta al morir, si suelta algo. Va vacío por defecto: un enemigo
	/// que siempre deja algo convierte matar en la forma segura de ganar, y el
	/// diseño quiere lo contrario —que lo que ganas venga de bajar más, no de
	/// limpiar salas.
	///
	/// Lo suelta EN EL SITIO donde ha caído, sin lanzarlo ni buscarle un hueco
	/// libre. Si queda medio enterrado en el montón de huesos, se ve igual: la
	/// antorcha llega antes que tú.
	/// </summary>
	[Export] public ItemData Drop { get; set; }

	private Node3D _visual;
	private SkeletonRig _rig;
	private MeleeHitbox _hitbox;
	private TelegraphMarker _marker;
	private Health _health;
	private NavigationAgent3D _agent;
	private Node3D _target;

	private float _gravity;
	private CombatPhase _phase = CombatPhase.Idle;
	private float _phaseTimer;
	private float _phaseDuration;
	private bool _dead;

	private Vector3 _home;
	private bool _aware;
	private float _sightTimer;
	private float _forgetTimer;
	private bool _pressing;
	private float _pressTimer;
	private float _regroupTimer;
	private float _ringAngle;
	private float _ringSign = 1.0f;

	/// <summary>
	/// Lo recorrido de la fase actual, de 0 a 1. Lo lee el esqueleto para saber
	/// cuánto lleva levantado el brazo: así la animación dura lo que dura la
	/// anticipación y no lo que diga una pista grabada.
	/// </summary>
	public float PhaseProgress =>
		_phaseDuration > 0.0f ? Mathf.Clamp(1.0f - _phaseTimer / _phaseDuration, 0.0f, 1.0f) : 1.0f;

	/// <summary>
	/// Tiene el turno: es de los que están entrando a pegar. Lo consultan los
	/// demás para saber si les toca esperar en el anillo.
	/// </summary>
	public bool IsPressing => _pressing && !_dead;

	public override void _Ready()
	{
		_visual = GetNode<Node3D>("Visual");
		_rig = _visual as SkeletonRig;
		_hitbox = GetNode<MeleeHitbox>("Visual/AttackHitbox");
		_agent = GetNode<NavigationAgent3D>("NavAgent");

		// Por búsqueda y no por ruta: las cuencas viven colgadas del cráneo, y el
		// cráneo se mueve de sitio cada vez que se retoca el esqueleto.
		_marker = _visual.FindChild("Telegraph", true, false) as TelegraphMarker;
		_health = GetNode<Health>("Health");
		_health.Damaged += OnDamaged;
		_health.Died += OnDied;

		_gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle() * 2.0f;
		_target = GetTree().GetFirstNodeInGroup("player") as Node3D;

		AddToGroup(EnemyGroup);
		ConfigureAgent();

		// Su sitio. Es a donde vuelve si le pierdes la pista, y lo que evita que
		// una sala que ya has cruzado se quede con los bichos amontonados al fondo.
		_home = GlobalPosition;

		// El sentido en el que rodea sale del NOMBRE del nodo, igual que la semilla
		// de las piezas (ver CONVENCIONES.md): así dos esqueletos de la misma sala
		// no rodean los dos hacia el mismo lado, y añadir un tercero no le cambia
		// el sentido a los otros dos.
		_ringSign = (GetName().ToString().Hash() & 1) == 0 ? 1.0f : -1.0f;

		// Repartidos para que los rayos de visión de tres esqueletos no caigan
		// todos en el mismo fotograma.
		_sightTimer = (float)GD.RandRange(0.0, SightInterval);
	}

	/// <summary>
	/// El agente se configura aquí y no en la escena a propósito: son seis
	/// números que solo tienen sentido leídos junto al alcance del ataque y al
	/// radio del anillo, y en el inspector quedarían a dos pantallas de ellos.
	/// </summary>
	private void ConfigureAgent()
	{
		_agent.Radius = 0.45f;
		_agent.Height = 1.8f;

		// Se da por llegado antes que el radio del cuerpo: si no, el destino queda
		// dentro de él y el agente pide corregir eternamente.
		_agent.PathDesiredDistance = 0.5f;
		_agent.TargetDesiredDistance = 0.5f;

		// Sin esto, un destino que cae fuera de la malla —el jugador subido a la
		// plataforma, por ejemplo— deja al agente recalculando cada fotograma.
		_agent.PathMaxDistance = 3.0f;

		// La evitación es lo que impide que tres convergiendo se fundan en uno.
		// Va por señal: el motor devuelve la velocidad ya corregida y es ESA la
		// que se mueve.
		_agent.AvoidanceEnabled = true;
		_agent.NeighborDistance = 4.0f;
		_agent.MaxNeighbors = 6;
		_agent.TimeHorizonAgents = 1.2f;
		_agent.MaxSpeed = MoveSpeed;
		_agent.VelocityComputed += OnVelocityComputed;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		if (!IsOnFloor())
		{
			Velocity += Vector3.Down * _gravity * dt;
		}

		if (_dead || _target == null || !IsInstanceValid(_target))
		{
			Advance(Vector3.Zero);
			return;
		}

		Vector3 toTarget = _target.GlobalPosition - GlobalPosition;
		toTarget.Y = 0.0f;
		float distance = toTarget.Length();

		UpdateAwareness(dt, distance);
		UpdateTurn(dt, distance);
		UpdateSwing(dt, distance);

		// Quieto mientras dura el golpe. Ningún ataque se cancela, tampoco el suyo:
		// es lo que hace que apartarse a tiempo sea la respuesta.
		Advance(_phase == CombatPhase.Idle ? Steer(dt) : Vector3.Zero);

		if (_phase == CombatPhase.Idle || _phase == CombatPhase.Windup)
		{
			FaceTarget(_aware ? toTarget : Velocity, dt);
		}

		_rig?.Drive(_phase, PhaseProgress);
	}

	/// <summary>
	/// Te ve o no te ve. El radio solo dice hasta dónde puede llegar la vista; lo
	/// que decide es si hay pared por medio, y por eso una sala con pilares se
	/// cruza despertando a uno y no a los tres.
	/// </summary>
	private void UpdateAwareness(float dt, float distance)
	{
		_sightTimer -= dt;

		if (!_aware)
		{
			if (_sightTimer > 0.0f)
			{
				return;
			}

			_sightTimer = SightInterval;

			if (distance <= HearingRange || (distance <= DetectionRange && CanSeeTarget()))
			{
				_aware = true;
				_forgetTimer = ForgetSeconds;
				AimRingAt();
			}

			return;
		}

		// Perder la pista es cuestión de distancia Y de vista: mientras te tenga
		// delante te sigue por media mazmorra, pero en cuanto lo pierdes de vista y
		// te vas lejos, vuelve a su sitio en vez de vagar detrás de ti.
		if (distance <= ForgetRange)
		{
			_forgetTimer = ForgetSeconds;
			return;
		}

		if (_sightTimer <= 0.0f)
		{
			_sightTimer = SightInterval;

			if (CanSeeTarget())
			{
				_forgetTimer = ForgetSeconds;
				return;
			}
		}

		_forgetTimer -= dt;
		if (_forgetTimer <= 0.0f)
		{
			_aware = false;
			ReleaseTurn();
		}
	}

	/// <summary>
	/// Rayo a la altura de los ojos y solo contra el mundo: los demás esqueletos
	/// no tapan, porque un bicho que no te ve porque tiene a otro delante se lee
	/// como que no funciona.
	/// </summary>
	private bool CanSeeTarget()
	{
		PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
			GlobalPosition + Vector3.Up * EyeHeight,
			_target.GlobalPosition + Vector3.Up * 1.2f,
			1);

		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		return GetWorld3D().DirectSpaceState.IntersectRay(query).Count == 0;
	}

	/// <summary>
	/// El reparto del turno. Pedirlo es contar cuántos lo tienen ya; soltarlo
	/// pasa siempre por <see cref="ReleaseTurn"/>, que es lo que abre el hueco
	/// para contraatacar.
	/// </summary>
	private void UpdateTurn(float dt, float distance)
	{
		_regroupTimer = Mathf.Max(0.0f, _regroupTimer - dt);

		if (_pressing)
		{
			_pressTimer -= dt;

			// Se cansa de perseguir. El turno vuelve al montón y lo coge otro, que
			// a lo mejor lo tiene más a mano.
			if (_pressTimer <= 0.0f && _phase == CombatPhase.Idle)
			{
				ReleaseTurn();
			}

			return;
		}

		if (!_aware || _phase != CombatPhase.Idle || _regroupTimer > 0.0f)
		{
			return;
		}

		if (distance > DetectionRange || Pressing() >= MaxAttackers)
		{
			return;
		}

		_pressing = true;
		_pressTimer = PressSeconds;
	}

	/// <summary>
	/// Cuántos del grupo están entrando a pegar ahora mismo. Se cuenta en vez de
	/// mantenerse una lista porque los bichos de una sala son tres, y una lista
	/// viva habría que limpiarla cada vez que uno muere.
	/// </summary>
	private int Pressing()
	{
		int count = 0;

		foreach (Node node in GetTree().GetNodesInGroup(EnemyGroup))
		{
			if (node != this && node is SkeletonAI other && other.IsPressing)
			{
				count++;
			}
		}

		return count;
	}

	private void ReleaseTurn()
	{
		if (_pressing)
		{
			_pressing = false;
			_regroupTimer = RegroupSeconds;
		}
	}

	/// <summary>
	/// A dónde va: encima del jugador si tiene turno, a su sitio en el anillo si
	/// no, y de vuelta a casa si te ha perdido.
	/// </summary>
	private Vector3 Steer(float dt)
	{
		Vector3 destination;

		if (!_aware)
		{
			destination = _home;
		}
		else if (_pressing)
		{
			destination = _target.GlobalPosition;
		}
		else
		{
			// El ángulo es estado, no se saca de la posición actual: sacándolo de la
			// posición, el punto al que ir queda siempre a tres centímetros y el
			// bicho no llega a arrancar nunca.
			_ringAngle += Mathf.DegToRad(RingDriftDegrees) * _ringSign * dt;
			destination = _target.GlobalPosition
				+ new Vector3(Mathf.Sin(_ringAngle), 0.0f, Mathf.Cos(_ringAngle)) * RingRadius;
		}

		_agent.TargetPosition = destination;

		Vector3 step = _agent.GetNextPathPosition() - GlobalPosition;
		step.Y = 0.0f;

		// Sin malla horneada todavía —el primer fotograma de la partida— el agente
		// devuelve su propia posición. Ahí se tira en recta, que es exactamente lo
		// que hacía antes: peor que la ruta, pero nunca quieto.
		if (step.Length() < 0.05f)
		{
			step = destination - GlobalPosition;
			step.Y = 0.0f;
		}

		return step.Length() < 0.2f ? Vector3.Zero : step.Normalized() * MoveSpeed;
	}

	/// <summary>
	/// Deja el anillo empezado donde ya está el bicho. Sin esto, el que despierta
	/// a nueve metros por detrás cruza media sala hasta su hueco del anillo en vez
	/// de acercarse por donde estaba.
	/// </summary>
	private void AimRingAt()
	{
		Vector3 offset = GlobalPosition - _target.GlobalPosition;
		offset.Y = 0.0f;

		_ringAngle = offset.IsZeroApprox() ? 0.0f : Mathf.Atan2(offset.X, offset.Z);
	}

	/// <summary>
	/// Pide la velocidad al motor de evitación y espera su respuesta. Mover el
	/// cuerpo directamente aquí se saltaría la evitación y tres esqueletos
	/// volverían a apilarse en el mismo metro cuadrado. Muerto ya no evita a
	/// nadie, y entonces se mueve derecho: si no, el montón se queda flotando en
	/// el aire porque nadie le llama al desplazamiento.
	/// </summary>
	private void Advance(Vector3 horizontal)
	{
		if (_agent.AvoidanceEnabled)
		{
			_agent.Velocity = horizontal;
			return;
		}

		Move(horizontal);
	}

	private void OnVelocityComputed(Vector3 safeVelocity)
	{
		Move(safeVelocity);
	}

	private void Move(Vector3 horizontal)
	{
		Velocity = new Vector3(horizontal.X, Velocity.Y, horizontal.Z);
		MoveAndSlide();
	}

	private void UpdateSwing(float dt, float distance)
	{
		if (_phase == CombatPhase.Idle)
		{
			// Solo pega el que tiene turno. Los demás pasan de largo aunque los
			// tengas encima: son los que te están rodeando.
			if (_pressing && _aware && distance <= AttackRange)
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

				// Ha pegado: suelta el turno y se retira al anillo. Ese hueco es el
				// contraataque del jugador, y es lo único que separa pelear contra
				// tres de recibir de tres.
				ReleaseTurn();
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
		toTarget.Y = 0.0f;

		if (toTarget.LengthSquared() < 0.01f)
		{
			return;
		}

		Vector3 direction = toTarget.Normalized();

		// El ángulo sale en coordenadas de mundo, pero el nodo que gira es hijo del
		// cuerpo: hay que descontarle lo que ya gire el cuerpo. Sin esto, un
		// esqueleto colocado girado en el nivel apunta todos sus golpes con ese
		// giro de más, y como el arco de golpe cuelga del mismo nodo, falla por lo
		// mismo y en la misma dirección: se ve al bicho pegando al aire.
		float targetYaw = Mathf.Atan2(-direction.X, -direction.Z) - GlobalBasis.GetEuler().Y;
		float weight = 1.0f - Mathf.Exp(-TurnSpeed * dt);

		Vector3 rotation = _visual.Rotation;
		rotation.Y = Mathf.LerpAngle(rotation.Y, targetYaw, weight);
		_visual.Rotation = rotation;
	}

	/// <summary>
	/// Le han dado. Hace dos cosas: el respingo, y despertarlo. El ataque que
	/// tuviera empezado sigue saliendo, porque el bicho se compromete igual que el
	/// jugador y un golpe a tiempo no cancela el suyo.
	///
	/// Un enemigo que encaja los golpes sin inmutarse no se lee como duro, se lee
	/// como que no le has dado.
	/// </summary>
	private void OnDamaged(float amount, float remaining)
	{
		_rig?.Flinch();

		if (!_aware && _target != null && IsInstanceValid(_target))
		{
			_aware = true;
			_forgetTimer = ForgetSeconds;
			AimRingAt();
		}
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

		// Suelta el turno antes de irse: si se muere teniéndolo, los otros dos se
		// quedan rodeando a un jugador al que ya no ataca nadie.
		_pressing = false;

		// Fuera del grupo y de la capa de enemigos, para que el cadáver no cuente
		// para el turno ni estorbe a los golpes.
		RemoveFromGroup(EnemyGroup);
		SetCollisionLayerValue(3, false);

		// Y fuera de la evitación: un montón de huesos no es un obstáculo que
		// esquivar, es decorado.
		_agent.AvoidanceEnabled = false;

		_rig?.Collapse();

		WorldItem.Spawn(this, Drop, GlobalPosition);

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
