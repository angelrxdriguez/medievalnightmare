using Godot;
using MedievalNightmare.Core;

namespace MedievalNightmare.Player;

/// <summary>
/// El arma en la mano: la carga, la cambia y la mueve. Cuelga de <c>Head</c>,
/// así que hereda la vista gratis y solo tiene que ocuparse del brazo.
///
/// No hay <c>AnimationPlayer</c> y no es por vagancia. Los tiempos del combate
/// no son constantes: cada arma escala los suyos con <c>SpeedScale</c>, y el
/// jugador puede encadenar, bloquear o esquivar a mitad de cualquier fase. Una
/// pista grabada a 0,12 s se descuadra en cuanto el mandoble multiplica por
/// 1,35, y a partir de ahí el filo se ve salir cuando el golpe ya ha pasado.
/// Aquí la pose se calcula a partir de
/// <see cref="PlayerController.PhaseProgress"/>, así que la animación no puede
/// desincronizarse del golpe: ES el golpe.
///
/// El movimiento se compone de cinco cosas que se suman, y ninguna sabe de las
/// otras:
///
/// - EL GOLPE. Un arco alrededor del HOMBRO, no del ojo. Es la diferencia entre
///   un arma que barre y una que gira sobre su propio mango como una peonza.
/// - EL PASO. Un ocho tumbado: la mano baja dos veces por zancada y se escora
///   una. Es lo único que dice que estás andando cuando no ves los pies.
/// - EL RETRASO DE LA VISTA. El arma llega un pelo tarde a donde miras. Es lo
///   que hace que el arma pese; sin esto va clavada a la cámara y parece pintada
///   sobre el cristal.
/// - LA GUARDIA. Se cruza delante y tapa parte de la vista, que es su precio.
/// - LA RECARGA. Se sale de la mira y vuelve. Mientras, el virote no está.
///
/// La única regla al tocar esto: la fase manda sobre todo lo demás. Si el
/// balanceo del paso pudiera mover el arma durante un golpe, el filo dejaría de
/// salir por donde apunta la mira.
/// </summary>
public partial class WeaponView : Node3D
{
	[ExportGroup("Hombro")]

	/// <summary>
	/// El punto alrededor del que gira el golpe, respecto al ojo. Está atrás y
	/// abajo porque ahí está el hombro: girar alrededor del ojo convierte
	/// cualquier tajo en un molinillo delante de la cara.
	/// </summary>
	[Export] public Vector3 ShoulderPivot { get; set; } = new(0.16f, -0.36f, -0.04f);

	/// <summary>
	/// Qué parte del arco RECORRE el arma, frente a lo que GIRA sobre sí misma.
	///
	/// A uno, el arma orbita el hombro los mismos grados que rota, que es lo que
	/// haría un brazo de verdad... y lo que la saca de la pantalla: un tajo de
	/// ciento cuarenta grados deja el arma detrás de tu propia oreja durante media
	/// animación, y lo que ve el jugador es que el arma desaparece justo en el
	/// fotograma en el que abre la caja de golpe.
	///
	/// Bajándolo, el filo sigue barriendo el arco entero —la rotación no se toca—
	/// pero el arma cruza la pantalla en vez de irse de ella. Es mentira, y es la
	/// misma mentira que lleva contando el género desde 1993.
	/// </summary>
	[Export(PropertyHint.Range, "0,1,0.05")] public float SwingOrbit { get; set; } = 0.25f;

	[ExportGroup("Paso")]
	[Export] public float BobHz { get; set; } = 1.05f;
	[Export] public float BobAmount { get; set; } = 0.022f;
	[Export] public float BobRollDegrees { get; set; } = 2.4f;

	/// <summary>Respiración en parado. Muy lenta y muy corta: solo dice que sigues vivo.</summary>
	[Export] public float BreathHz { get; set; } = 0.28f;

	[Export] public float BreathAmount { get; set; } = 0.006f;

	[ExportGroup("Retraso de la vista")]

	/// <summary>Metros de desvío por radián de giro de cabeza.</summary>
	[Export] public float SwayAmount { get; set; } = 0.09f;

	/// <summary>Tope del desvío. Sin él, un giro brusco saca el arma de la pantalla.</summary>
	[Export] public float SwayLimit { get; set; } = 0.055f;

	/// <summary>Lo rápido que vuelve el arma al centro. Alto es un arma ligera.</summary>
	[Export] public float SwayRecover { get; set; } = 9.0f;

	[ExportGroup("Salto")]

	/// <summary>Lo que se hunde el arma al aterrizar. Es el peso del jugador, no el del arma.</summary>
	[Export] public float LandDip { get; set; } = 0.07f;

	[Export] public float LandRecover { get; set; } = 6.5f;

	[ExportGroup("Guardia rota")]

	/// <summary>
	/// El empujón del momento en que se rompe la guardia: el arma sale despedida
	/// hacia fuera y hacia atrás. Es corto a propósito. Lo que tiene que contar no
	/// es "te han hecho daño" —de eso ya va el borde rojo— sino "esto que estabas
	/// haciendo se ha acabado".
	/// </summary>
	[Export] public Vector3 GuardBreakKick { get; set; } = new(0.13f, -0.06f, 0.15f);

	[Export] public Vector3 GuardBreakKickDegrees { get; set; } = new(-16.0f, 28.0f, 36.0f);

	/// <summary>Lo rápido que se recoge el empujón. Alto es un golpe seco.</summary>
	[Export] public float GuardBreakRecover { get; set; } = 4.5f;

	/// <summary>
	/// Dónde se queda el arma los diez segundos que dura la guardia rota: caída y
	/// abierta, porque no hay guardia que poner. Sin esto el jugador pulsa bloquear,
	/// no pasa nada y lo lee como un fallo del juego en vez de como un estado.
	/// Hasta que haya HUD, esta pose ES el indicador.
	/// </summary>
	[Export] public Vector3 BrokenGuardOffset { get; set; } = new(0.03f, -0.10f, 0.0f);

	[Export] public Vector3 BrokenGuardDegrees { get; set; } = new(10.0f, 0.0f, -8.0f);

	[ExportGroup("Cambio de arma")]

	/// <summary>Lo que tarda en bajar la vieja y subir la nueva. El cambio no es gratis.</summary>
	[Export] public float SwapSeconds { get; set; } = 0.34f;

	/// <summary>Lo que baja el arma para salir de cuadro durante el cambio.</summary>
	[Export] public float SwapDrop { get; set; } = 0.45f;

	[ExportGroup("Retro")]

	/// <summary>
	/// Fotogramas por segundo de la pose. La consola movía a doce o quince y se
	/// nota, pero aquí va a cero por defecto y no es una rendición: la ventana
	/// activa del golpe ligero dura 0,10 s, así que a doce pasos por segundo el
	/// arma se vería salir hasta 80 ms tarde y el jugador estaría leyendo un golpe
	/// que ya ha pasado. El escalonado se lo queda el esqueleto
	/// (<c>SkeletonRig</c>), donde es adorno; aquí sería mentir sobre los tiempos.
	/// Se deja expuesto para poder verlo puesto.
	/// </summary>
	[Export] public float PoseHz { get; set; }

	private PlayerController _player;
	private Node3D _head;
	private WeaponModel _model;
	private int _shownIndex = -1;

	private float _bobPhase;
	private float _breathPhase;
	private Vector2 _sway;
	private float _lastYaw;
	private float _lastPitch;
	private float _land;
	private bool _wasOnFloor = true;
	private float _blockWeight;
	private float _breakPunch;
	private float _brokenWeight;
	private float _dashWeight;
	private float _reloadWeight;
	private float _swapTimer;
	private float _stepTimer;

	public override void _Ready()
	{
		// Por grupo y no por ruta hacia arriba: así el arma se puede colgar de
		// cualquier sitio de la cabeza sin que este script sepa dónde está.
		_player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
		_head = GetParent<Node3D>();

		if (_player == null)
		{
			GD.PushWarning("WeaponView no encuentra al jugador: el arma no se va a mover.");
			return;
		}

		_lastYaw = _head.Rotation.Y;
		_lastPitch = _head.Rotation.X;
		_player.GuardBroken += OnGuardBroken;

		SwapTo(_player.WeaponIndex, instant: true);
	}

	public override void _Process(double delta)
	{
		if (_player == null)
		{
			return;
		}

		float dt = (float)delta;

		UpdateSway(dt);
		UpdateSwap(dt);
		UpdateWeights(dt);
		UpdateBob(dt);

		if (_model == null)
		{
			return;
		}

		// El escalonado se aplica AQUÍ y no en cada término: si cada pieza del
		// movimiento se cuantizara por su cuenta, unas saltarían en un fotograma y
		// otras en el siguiente, y saldría temblor en vez de fotogramas.
		if (PoseHz > 0.0f)
		{
			_stepTimer += dt;
			if (_stepTimer < 1.0f / PoseHz)
			{
				return;
			}

			_stepTimer = 0.0f;
		}

		ApplyPose();
	}

	/// <summary>
	/// El desvío por girar la cabeza. Se alimenta de cuánto ha girado la vista en
	/// este fotograma, no de hacia dónde mira: lo que se busca es que el arma
	/// reaccione al MOVIMIENTO y se olvide, no que quede torcida mirando al norte.
	/// </summary>
	private void UpdateSway(float dt)
	{
		float yaw = _head.Rotation.Y;
		float pitch = _head.Rotation.X;

		_sway.X += Mathf.Wrap(yaw - _lastYaw, -Mathf.Pi, Mathf.Pi) * SwayAmount;
		_sway.Y -= (pitch - _lastPitch) * SwayAmount;

		_lastYaw = yaw;
		_lastPitch = pitch;

		_sway = _sway.LimitLength(SwayLimit);
		_sway = _sway.Lerp(Vector2.Zero, 1.0f - Mathf.Exp(-SwayRecover * dt));
	}

	private void UpdateSwap(float dt)
	{
		if (_swapTimer > 0.0f)
		{
			float previous = _swapTimer;
			_swapTimer = Mathf.Max(0.0f, _swapTimer - dt);

			// El arma se sustituye en el punto más bajo del gesto, fuera de cuadro.
			// Es lo único que evita ver una espada volverse maza a media pantalla.
			if (previous > SwapSeconds * 0.5f && _swapTimer <= SwapSeconds * 0.5f)
			{
				SwapTo(_player.WeaponIndex, instant: false);
			}

			return;
		}

		if (_player.WeaponIndex != _shownIndex)
		{
			_swapTimer = SwapSeconds;
		}
	}

	private void UpdateWeights(float dt)
	{
		// Todo lo que no es el golpe entra y sale con amortiguación. Poner y quitar
		// una pose de golpe se ve como un salto de fotograma, y de los feos.
		_blockWeight = Damp(_blockWeight, _player.IsBlocking ? 1.0f : 0.0f, 16.0f, dt);
		_dashWeight = Damp(_dashWeight, _player.IsDashing ? 1.0f : 0.0f, 12.0f, dt);

		// El empujón se recoge solo; el brazo caído se queda mientras dure la rotura.
		_breakPunch = Damp(_breakPunch, 0.0f, GuardBreakRecover, dt);
		_brokenWeight = Damp(_brokenWeight, _player.IsGuardBroken ? 1.0f : 0.0f, 7.0f, dt);

		bool reloading = _player.ReloadRemaining > 0.0f;
		_reloadWeight = Damp(_reloadWeight, reloading ? 1.0f : 0.0f, 9.0f, dt);

		_model?.SetLoaded(!reloading && _player.CurrentAmmo > 0);
	}

	private void UpdateBob(float dt)
	{
		bool onFloor = _player.IsOnFloor();

		// La zancada se acelera con la velocidad, no solo se agranda. Un ciclo de
		// paso a frecuencia fija con más amplitud parece un barco, no una carrera.
		float pace = Pace();
		_bobPhase += dt * BobHz * Mathf.Tau * (0.7f + pace * 0.9f) * (onFloor ? pace : 0.0f);
		_breathPhase += dt * BreathHz * Mathf.Tau;

		if (onFloor && !_wasOnFloor)
		{
			_land = 1.0f;
		}

		_wasOnFloor = onFloor;
		_land = Damp(_land, 0.0f, LandRecover, dt);
	}

	private void ApplyPose()
	{
		float pace = Pace();

		Vector3 position = _model.RestPosition;
		Basis basis = Basis.FromEuler(DegToRad(_model.RestRotationDegrees));

		// 1. El golpe. Gira el arma sobre sí misma el arco entero, y la pasea
		// alrededor del hombro solo una fracción de ese arco (ver SwingOrbit).
		(Basis swing, Basis orbit, Vector3 reach) = SwingPose();
		position = ShoulderPivot + orbit * (position - ShoulderPivot) + reach;
		basis = swing * basis;

		// 2. Poses que se mezclan por peso, de menos a más mandona.
		position += _model.ReloadOffset * _reloadWeight;
		basis = Basis.FromEuler(DegToRad(_model.ReloadRotationDegrees) * _reloadWeight) * basis;

		position += _model.BlockOffset * _blockWeight;
		basis = Basis.FromEuler(DegToRad(_model.BlockRotationDegrees) * _blockWeight) * basis;

		// La guardia rota va DESPUÉS del bloqueo porque lo contradice: el peso del
		// bloqueo ya está cayendo a cero cuando esto entra, y lo que se ve es que el
		// arma se escapa de la pose de guardia, no que nunca estuvo ahí.
		position += BrokenGuardOffset * _brokenWeight + GuardBreakKick * _breakPunch;
		basis = Basis.FromEuler(
			DegToRad(BrokenGuardDegrees) * _brokenWeight
			+ DegToRad(GuardBreakKickDegrees) * _breakPunch) * basis;

		// La esquiva mete el arma contra el pecho: vas de lado, no atacando.
		position += new Vector3(-0.06f, -0.1f, 0.12f) * _dashWeight;

		// 3. El paso, la respiración y el aterrizaje. Van al final porque son ruido
		// encima de la pose, no pose.
		position += new Vector3(
			Mathf.Sin(_bobPhase) * BobAmount * pace,
			-Mathf.Abs(Mathf.Cos(_bobPhase)) * BobAmount * 0.85f * pace,
			0.0f);

		position += new Vector3(0.0f, Mathf.Sin(_breathPhase) * BreathAmount * (1.0f - pace), 0.0f);
		position += new Vector3(_sway.X, _sway.Y - _land * LandDip, 0.0f);

		basis = Basis.FromEuler(new Vector3(
			_land * 0.35f,
			_sway.X * 1.6f,
			Mathf.Sin(_bobPhase) * Mathf.DegToRad(BobRollDegrees) * pace - _sway.X * 2.2f)) * basis;

		// 4. El cambio de arma manda sobre todo: mientras baja, baja de verdad.
		if (_swapTimer > 0.0f)
		{
			// Triángulo: 0 arriba, 1 justo cuando se sustituye el arma, 0 otra vez.
			float t = 1.0f - Mathf.Abs(_swapTimer / SwapSeconds * 2.0f - 1.0f);
			float drop = Mathf.Sin(t * Mathf.Pi * 0.5f);

			position += new Vector3(0.0f, -SwapDrop * drop, 0.06f * drop);
			basis = Basis.FromEuler(new Vector3(drop * 1.1f, 0.0f, drop * 0.5f)) * basis;
		}

		_model.Transform = new Transform3D(basis, position);
	}

	/// <summary>
	/// El golpe entero: un giro alrededor del hombro y un empujón hacia delante.
	///
	/// El recorrido es una sola recta de grados repartida entre las tres fases —la
	/// anticipación la recorre hacia atrás, el golpe la recorre entera hacia
	/// delante y la recuperación vuelve a cero—, así que el filo pasa por delante
	/// de la mira exactamente cuando la caja de golpe está abierta.
	/// </summary>
	private (Basis, Basis, Vector3) SwingPose()
	{
		if (_player.Phase == CombatPhase.Idle)
		{
			return (Basis.Identity, Basis.Identity, Vector3.Zero);
		}

		bool heavy = _player.IsHeavySwing;
		float t = _player.PhaseProgress;
		float sweep = Mathf.DegToRad(_model.SwingSweepDegrees) * (heavy ? _model.HeavySweepScale : 1.0f);
		float back = sweep * _model.WindupFraction;
		float forward = sweep - back;

		float angle;
		Vector3 offset;

		switch (_player.Phase)
		{
			case CombatPhase.Windup:
				// Se carga rápido y espera. Esa espera ES la telegrafía del pesado: con
				// una anticipación lineal no habría nada que leer.
				angle = -back * EaseOut(t);
				offset = _model.WindupOffset * EaseOut(t);
				break;

			case CombatPhase.Active:
				angle = Mathf.Lerp(-back, forward, EaseIn(t));
				offset = _model.WindupOffset.Lerp(new Vector3(0.0f, 0.0f, -_model.SwingReach), EaseIn(t));
				break;

			default:
				angle = forward * (1.0f - EaseOut(t));
				offset = new Vector3(0.0f, 0.0f, -_model.SwingReach * (1.0f - EaseOut(t)));
				break;
		}

		// El plano del arco. A 0 grados el eje es -X y el arma cae de arriba abajo;
		// a 90 es +Y y barre de derecha a izquierda. La diagonal es lo de en medio,
		// y de ahí sale que la espada y la maza no se parezcan en la mano.
		float plane = Mathf.DegToRad(_model.SwingPlaneDegrees);
		Vector3 axis = new Vector3(-Mathf.Cos(plane), Mathf.Sin(plane), 0.0f).Normalized();
		Basis basis = new Basis(axis, angle);
		Basis orbit = new Basis(axis, angle * SwingOrbit);

		// El pesado del mandoble da la vuelta entera sobre el eje del jugador. Es la
		// única animación que no es un arco, y por eso es la que se reconoce.
		if (heavy && _model.HeavySpins && _player.Phase == CombatPhase.Active)
		{
			// La vuelta entera gira el arma pero NO la pasea: el mandoble da la
			// vuelta delante de ti, no alrededor de ti. Si orbitara, la mitad del
			// giro pasaría por detrás de la cámara y solo verías el final.
			basis = new Basis(Vector3.Up, Mathf.Tau * EaseIn(t)) * basis;
		}

		// La coz del disparo. Sale de golpe y se apaga con la recuperación, justo al
		// revés que un golpe: aquí lo que se ve es la consecuencia, no el gesto.
		if (_model.RecoilKick > 0.0f)
		{
			float kick = _player.Phase switch
			{
				CombatPhase.Active => 1.0f,
				CombatPhase.Recovery => 1.0f - EaseOut(t),
				_ => 0.0f,
			};

			offset += new Vector3(0.0f, 0.045f, 0.09f) * _model.RecoilKick * kick;

			Basis recoil = new Basis(Vector3.Right, Mathf.DegToRad(14.0f) * _model.RecoilKick * kick);
			basis = recoil * basis;
			orbit = recoil * orbit;
		}

		return (basis, orbit, offset);
	}

	/// <summary>Fracción de la velocidad de marcha que llevas, de 0 a 1.</summary>
	private float Pace()
	{
		Vector3 velocity = _player.Velocity;
		float speed = new Vector2(velocity.X, velocity.Z).Length();

		return Mathf.Clamp(speed / Mathf.Max(_player.WalkSpeed, 0.01f), 0.0f, 1.0f);
	}

	/// <summary>
	/// Deja en la mano el arma del hueco pedido. Instantáneo al empezar la partida
	/// y en el punto bajo del cambio; nunca a media pantalla.
	/// </summary>
	private void SwapTo(int index, bool instant)
	{
		_shownIndex = index;

		if (_model != null)
		{
			_model.QueueFree();
			_model = null;
		}

		PackedScene scene = _player.CurrentWeapon?.ViewModel;
		if (scene == null)
		{
			return;
		}

		_model = scene.Instantiate<WeaponModel>();
		AddChild(_model);

		if (instant)
		{
			_swapTimer = 0.0f;
			ApplyPose();
		}
	}

	private static Vector3 DegToRad(Vector3 degrees)
	{
		return new Vector3(
			Mathf.DegToRad(degrees.X),
			Mathf.DegToRad(degrees.Y),
			Mathf.DegToRad(degrees.Z));
	}

	/// <summary>
	/// Se ha roto la guardia. Se arma el empujón de golpe y no con amortiguación:
	/// que te abran la guardia es un instante, no una transición.
	/// </summary>
	private void OnGuardBroken()
	{
		_breakPunch = 1.0f;
	}

	/// <summary>Amortiguación independiente del fotograma. Con lerp puro, a 30 fps va al doble.</summary>
	private static float Damp(float from, float to, float rate, float dt)
	{
		return Mathf.Lerp(from, to, 1.0f - Mathf.Exp(-rate * dt));
	}

	/// <summary>Arranca rápido y frena. Es el gesto de cargar un golpe.</summary>
	private static float EaseOut(float t)
	{
		float u = 1.0f - Mathf.Clamp(t, 0.0f, 1.0f);

		return 1.0f - u * u * u;
	}

	/// <summary>Arranca lento y acelera. Es el gesto de soltarlo.</summary>
	private static float EaseIn(float t)
	{
		float u = Mathf.Clamp(t, 0.0f, 1.0f);

		return u * u * (3.0f - 2.0f * u) * 0.35f + u * u * u * 0.65f;
	}
}
