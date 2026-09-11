using System.Collections.Generic;
using Godot;
using MedievalNightmare.Core;

namespace MedievalNightmare.Enemies;

/// <summary>
/// El esqueleto por dentro: quien mueve los huesos. Va en <c>Visual</c> y lo
/// conduce <see cref="SkeletonAI"/>, que le pasa la fase de combate y le avisa
/// cuando muere. La marcha no se la pasa nadie: la saca de la velocidad del
/// cuerpo, y por eso no hay forma de que las piernas y el suelo se
/// desincronicen.
///
/// No hay <c>Skeleton3D</c> ni pesos de vértice, y no hace falta: el bicho ya
/// era treinta piezas sueltas. Lo único que le faltaba era colgarlas de PIVOTES
/// en las articulaciones —cadera, rodilla, tobillo, hombro, codo— en vez de
/// tenerlas todas planas bajo <c>Visual</c>. Un fémur girado sobre su propio
/// centro se hunde en la pelvis; girado sobre la cadera, anda.
///
/// EL PASO. Un ciclo de marcha son cuatro cosas y siempre las mismas:
///
/// - Las piernas se abren y se cierran en oposición de fase.
/// - La rodilla SOLO dobla en la vuelta, nunca en el apoyo. Es lo único que
///   separa andar de patinar con las piernas rígidas.
/// - Los brazos van al revés que las piernas. Sin eso el bicho anda como un
///   juguete de cuerda.
/// - La cadera sube y baja DOS veces por zancada. Es lo que hace que el paso
///   tenga peso.
///
/// La fase del ciclo avanza con la DISTANCIA recorrida, no con el tiempo. Es la
/// única forma de que un esqueleto que se frena contra una pared deje de mover
/// las piernas en vez de correr en el sitio.
///
/// LA MUERTE. Los huesos se sueltan del esqueleto —se reparentan conservando su
/// sitio en el mundo— y a partir de ahí cada uno es un objeto suyo que cae,
/// bota y se acuesta. No es física del motor: son treinta integraciones de
/// Euler a doce pasos por segundo. Cuesta lo que cuesta un bucle y, sobre todo,
/// se ve como se veía entonces.
/// </summary>
public partial class SkeletonRig : Node3D
{
	[ExportGroup("Paso")]

	/// <summary>Metros por zancada completa. Sube esto y el bicho da menos pasos, más largos.</summary>
	[Export] public float StrideMeters { get; set; } = 1.15f;

	/// <summary>Velocidad a la que el paso está a plena amplitud. Por debajo, se encoge.</summary>
	[Export] public float FullStrideSpeed { get; set; } = 3.0f;

	[Export] public float HipSwingDegrees { get; set; } = 26.0f;
	[Export] public float KneeBendDegrees { get; set; } = 46.0f;
	[Export] public float ArmSwingDegrees { get; set; } = 16.0f;

	/// <summary>Lo que sube y baja la cadera en cada apoyo.</summary>
	[Export] public float BobAmount { get; set; } = 0.045f;

	[Export] public float HipRollDegrees { get; set; } = 5.0f;
	[Export] public float TorsoTwistDegrees { get; set; } = 7.0f;

	[ExportGroup("Ralenti")]

	/// <summary>
	/// Quieto, un esqueleto no respira. Lo que hace es BALANCEARSE: es un montón
	/// de huesos que se sostiene por algo que no es músculo, y el balanceo lento es
	/// lo que dice que sigue encendido sin necesidad de que se mueva de sitio.
	/// </summary>
	[Export] public float IdleHz { get; set; } = 0.32f;

	[Export] public float IdleSwayDegrees { get; set; } = 3.2f;

	[ExportGroup("Ataque")]

	/// <summary>Lo que levanta el brazo del machete durante la anticipación.</summary>
	[Export] public float WindupArmDegrees { get; set; } = 125.0f;

	/// <summary>Lo que baja el brazo al soltar el golpe. Va por delante del reposo.</summary>
	[Export] public float StrikeArmDegrees { get; set; } = 58.0f;

	/// <summary>Lo que se gira el torso para acompañar el golpe.</summary>
	[Export] public float StrikeTwistDegrees { get; set; } = 34.0f;

	[ExportGroup("Mandibula")]

	/// <summary>Lo que cuelga la mandíbula en reposo. Un muerto no aprieta los dientes.</summary>
	[Export] public float IdleGapeDegrees { get; set; } = 5.0f;

	/// <summary>Lo que se abre durante la anticipación. Se cierra de golpe al soltar el tajo.</summary>
	[Export] public float GapeDegrees { get; set; } = 26.0f;

	[ExportGroup("Impacto")]

	/// <summary>
	/// Lo que dura el respingo al encajar un golpe. Dos fotogramas de los doce: lo
	/// justo para que se vea que le has dado y no tanto como para interrumpirle el
	/// ataque, que sigue saliendo. El bicho no se detiene, se ESTREMECE.
	/// </summary>
	[Export] public float FlinchSeconds { get; set; } = 0.18f;

	[Export] public float FlinchDegrees { get; set; } = 12.0f;

	[ExportGroup("Muerte")]

	/// <summary>Lo que se abren los huesos al soltarse, en metros por segundo.</summary>
	[Export] public float CollapseSpread { get; set; } = 0.55f;

	/// <summary>Lo que saltan hacia arriba. Poco: es un montón que se derrumba, no una explosión.</summary>
	[Export] public float CollapseLift { get; set; } = 1.15f;

	[Export] public float CollapseSpin { get; set; } = 7.0f;
	[Export] public float CollapseGravity { get; set; } = 13.0f;

	/// <summary>Lo que devuelve el suelo. Un hueso no es una pelota: bota poco y una vez.</summary>
	[Export(PropertyHint.Range, "0,0.9,0.05")] public float CollapseBounce { get; set; } = 0.25f;

	/// <summary>
	/// Por debajo de este rebote, en metros por segundo, la pieza deja de botar y se
	/// acuesta. Es lo que decide cuántos botes da un hueso: a 0,6 y con un rebote
	/// del 25 %, uno o dos.
	/// </summary>
	[Export] public float CollapseSettle { get; set; } = 0.6f;

	[ExportGroup("Retro")]

	/// <summary>
	/// Fotogramas por segundo de la pose. Doce es lo que movía la consola y es la
	/// mitad del efecto: a sesenta esto es un esqueleto procedural moderno, a doce
	/// es un enemigo de 1999. Aquí sí se puede escalonar —al revés que en el arma
	/// del jugador— porque la anticipación del esqueleto dura 0,45 s y a doce pasos
	/// le caben cinco fotogramas, de sobra para leerla.
	/// </summary>
	[Export] public float PoseHz { get; set; } = 12.0f;

	private CharacterBody3D _body;
	private Node3D _hips;
	private Node3D _torso;
	private Node3D _head;
	private Node3D _jaw;
	private Node3D _shoulderL;
	private Node3D _shoulderR;
	private Node3D _elbowL;
	private Node3D _elbowR;
	private Node3D _hipL;
	private Node3D _hipR;
	private Node3D _kneeL;
	private Node3D _kneeR;
	private Node3D _ankleL;
	private Node3D _ankleR;

	/// <summary>El nombre del `instance uniform` que lleva la semilla en los tres shaders de criatura.</summary>
	private static readonly StringName PieceSeed = "piece_seed";

	private readonly Dictionary<Node3D, Transform3D> _rest = new();
	private readonly List<Bone> _debris = new();

	private Node3D _debrisRoot;
	private float _stride;
	private float _idleTime;
	private float _amplitude;
	private float _stepTimer;
	private float _collapseTimer;
	private float _flinch;
	private float _flinchDuration;
	private float _flinchStrength = 1.0f;
	private CombatPhase _phase = CombatPhase.Idle;
	private float _progress;
	private bool _collapsed;

	/// <summary>
	/// Un hueso suelto: lo que queda de una articulación cuando se acaba la magia.
	///
	/// El giro se guarda APARTE de la escala, y no es un detalle. Media docena de
	/// piezas del esqueleto están escaladas —la pelvis se ensancha, las costillas
	/// se aplastan, el cráneo se estira—, y una base con escala no se puede
	/// interpolar: al convertirla a cuaternión sale sin normalizar y el motor lo
	/// rechaza. Se gira lo ortonormal y se le vuelve a poner la escala al escribir.
	/// </summary>
	private sealed class Bone
	{
		public Node3D Node;
		public Vector3 Velocity;
		public Vector3 Spin;
		public Basis Rotation;
		public Basis Lying;
		public Vector3 Scale;
		public float Rest;
		public bool Grounded;
	}

	public override void _Ready()
	{
		_body = GetParent<CharacterBody3D>();

		_hips = GetNode<Node3D>("Hips");
		_torso = GetNode<Node3D>("Hips/Torso");
		_head = GetNode<Node3D>("Hips/Torso/Head");
		_jaw = GetNode<Node3D>("Hips/Torso/Head/JawPivot");
		_shoulderL = GetNode<Node3D>("Hips/Torso/ShoulderL");
		_shoulderR = GetNode<Node3D>("Hips/Torso/ShoulderR");
		_elbowL = GetNode<Node3D>("Hips/Torso/ShoulderL/ElbowL");
		_elbowR = GetNode<Node3D>("Hips/Torso/ShoulderR/ElbowR");
		_hipL = GetNode<Node3D>("Hips/HipL");
		_hipR = GetNode<Node3D>("Hips/HipR");
		_kneeL = GetNode<Node3D>("Hips/HipL/KneeL");
		_kneeR = GetNode<Node3D>("Hips/HipR/KneeR");
		_ankleL = GetNode<Node3D>("Hips/HipL/KneeL/AnkleL");
		_ankleR = GetNode<Node3D>("Hips/HipR/KneeR/AnkleR");

		foreach (Node3D pivot in Pivots())
		{
			_rest[pivot] = pivot.Transform;
		}

		// Cada esqueleto arranca en un punto distinto del ciclo. Sin esto, tres
		// esqueletos de la misma sala andan al paso como un pelotón.
		_stride = GD.Randf() * Mathf.Tau;
		_idleTime = GD.Randf() * 10.0f;

		SeedPieces();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		if (_collapsed)
		{
			UpdateCollapse(dt);
			return;
		}

		// La fase del ciclo avanza con lo ANDADO, no con el reloj: un esqueleto
		// parado contra una pared deja de mover las piernas en vez de correr en el
		// sitio, y uno al que empujan mueve las piernas lo que le empujen.
		Vector3 velocity = _body.Velocity;
		float speed = new Vector2(velocity.X, velocity.Z).Length();

		_stride += speed * dt / Mathf.Max(StrideMeters, 0.05f) * Mathf.Tau;
		_idleTime += dt;
		_flinch = Mathf.Max(0.0f, _flinch - dt);

		// La amplitud entra y sale amortiguada. Sin esto, arrancar a andar levanta
		// las dos piernas de golpe en un fotograma.
		float target = Mathf.Clamp(speed / Mathf.Max(FullStrideSpeed, 0.01f), 0.0f, 1.0f);
		_amplitude = Mathf.Lerp(_amplitude, target, 1.0f - Mathf.Exp(-9.0f * dt));

		if (!Step(dt))
		{
			return;
		}

		ApplyPose();
	}

	/// <summary>
	/// Lo que le cuenta la IA: en qué fase del golpe está y cuánto lleva de ella.
	/// El esqueleto no decide nada aquí; solo lo enseña.
	/// </summary>
	public void Drive(CombatPhase phase, float progress)
	{
		_phase = phase;
		_progress = progress;
	}

	/// <summary>
	/// Le han dado. Un respingo de dos fotogramas: el torso se va hacia atrás, la
	/// cabeza se descuelga y la cadera cede un dedo.
	///
	/// Es lo único que separa un enemigo de un saco. Sin esto le pegas cuatro veces
	/// seguidas y el bicho sigue andando hacia ti sin enterarse, y lo que lee el
	/// jugador no es "es duro" sino "no le he dado", que es lo peor que puede pasar
	/// en un combate cuyo golpe ligero dura 0,10 s.
	///
	/// Aquí no se interrumpe nada: el rig solo enseña. Si el golpe corta el ataque
	/// —el pesado del jugador lo hace— eso lo decide la IA, y entonces pide el
	/// respingo grande subiendo <paramref name="strength"/>.
	/// </summary>
	/// <param name="strength">Tamaño y duración del respingo. Uno es el golpe
	/// normal; el aturdimiento del pesado entra con bastante más.</param>
	public void Flinch(float strength = 1.0f)
	{
		if (_collapsed)
		{
			return;
		}

		_flinch = FlinchSeconds * strength;
		_flinchDuration = _flinch;
		_flinchStrength = strength;
	}

	/// <summary>
	/// Se acabó. Los huesos se sueltan del esqueleto y cada uno pasa a ser un
	/// objeto por su cuenta, con su velocidad y su giro.
	///
	/// Se REPARENTAN conservando su transformación de mundo, y eso es lo único
	/// importante de todo el método: mientras cuelguen de la cadera, mover un hueso
	/// mueve a sus hijos, y un montón de huesos no tiene hijos.
	/// </summary>
	public void Collapse()
	{
		if (_collapsed)
		{
			return;
		}

		_collapsed = true;

		_debrisRoot = new Node3D { Name = "Debris" };
		AddChild(_debrisRoot);

		List<MeshInstance3D> pieces = new();
		CollectBones(_hips, pieces);

		Vector3 center = _hips.Position;

		foreach (MeshInstance3D piece in pieces)
		{
			piece.Reparent(_debrisRoot);

			// Hacia fuera desde el eje del cuerpo, y hacia arriba lo justo. Un
			// esqueleto que muere se DERRUMBA; si sale despedido parece una mina.
			Vector3 outward = piece.Position - center;
			outward.Y = 0.0f;
			outward = outward.IsZeroApprox()
				? new Vector3(GD.Randf() - 0.5f, 0.0f, GD.Randf() - 0.5f)
				: outward.Normalized();

			float height = Mathf.Clamp(piece.Position.Y / 1.8f, 0.0f, 1.0f);

			_debris.Add(new Bone
			{
				Node = piece,
				Rotation = piece.Basis.Orthonormalized(),
				Scale = piece.Basis.Scale,

				// Lo que estaba arriba cae desde más alto y por eso se abre más. Es
				// lo que hace que el cráneo ruede y los pies se queden donde estaban.
				Velocity = outward * CollapseSpread * (0.4f + height)
					+ Vector3.Up * CollapseLift * height
					+ new Vector3(GD.Randf() - 0.5f, 0.0f, GD.Randf() - 0.5f) * 0.22f,

				Spin = new Vector3(
					(GD.Randf() - 0.5f) * CollapseSpin,
					(GD.Randf() - 0.5f) * CollapseSpin,
					(GD.Randf() - 0.5f) * CollapseSpin),

				// A dónde va a parar: tumbado, con el eje largo horizontal y girado
				// al azar. Un montón de huesos no tiene ni una pieza de canto.
				Lying = new Basis(Vector3.Up, GD.Randf() * Mathf.Tau)
					* new Basis(Vector3.Right, Mathf.Pi * 0.5f),

				// A qué altura se queda: la mitad de su lado más fino, que es a lo que
				// se apoya un hueso tumbado. Sin esto el cráneo se entierra hasta los
				// ojos y las costillas flotan.
				Rest = 0.012f + Thickness(piece) + GD.Randf() * 0.02f,
			});
		}
	}

	/// <summary>
	/// El paso de la consola. Los tiempos siguen corriendo a la velocidad del
	/// monitor —si no, el ciclo iría a saltos según los fotogramas por segundo—;
	/// lo que se escalona es solo el momento de ESCRIBIR la pose.
	/// </summary>
	private bool Step(float dt)
	{
		if (PoseHz <= 0.0f)
		{
			return true;
		}

		_stepTimer += dt;
		if (_stepTimer < 1.0f / PoseHz)
		{
			return false;
		}

		_stepTimer = 0.0f;

		return true;
	}

	private void ApplyPose()
	{
		// El respingo baja recto de golpe a cero y no se suaviza con nada. Como la
		// pose se escribe a doce por segundo, esos 0,18 s son dos fotogramas y pico:
		// se ve el tirón y se ve la vuelta, que es justo lo que hacía la época.
		float jolt = _flinchDuration > 0.0f
			? Mathf.DegToRad(FlinchDegrees) * _flinchStrength * (_flinch / _flinchDuration)
			: 0.0f;

		float swing = Mathf.Sin(_stride) * _amplitude;
		float opposite = Mathf.Sin(_stride + Mathf.Pi) * _amplitude;

		// La cadera baja en cada apoyo, o sea DOS veces por zancada. Con una sola
		// el bicho parece que cojea.
		float bob = -Mathf.Abs(Mathf.Cos(_stride)) * BobAmount * _amplitude;

		Pose(_hips, new Vector3(0.0f, bob - jolt * 0.06f, jolt * 0.10f), new Vector3(
			0.0f,
			0.0f,
			Mathf.DegToRad(HipRollDegrees) * swing));

		// El ralentí no compite con el paso: se apaga a medida que el bicho anda.
		float idle = Mathf.Sin(_idleTime * IdleHz * Mathf.Tau) * Mathf.DegToRad(IdleSwayDegrees)
			* (1.0f - _amplitude);

		float twist = Mathf.DegToRad(TorsoTwistDegrees) * opposite;
		// NEGATIVO: el bicho anda ECHADO HACIA DELANTE. Un esqueleto que camina
		// erguido parece que pasea; inclinado parece que va a por ti, y no cuesta
		// nada porque la cabeza lo deshace justo debajo y sigue mirando al frente.
		float lean = Mathf.DegToRad(-6.0f) * _amplitude;

		(float armL, float armR, float strikeElbow, float strikeTwist, float strikeLean) = StrikePose();

		Pose(_torso, Vector3.Zero, new Vector3(
			lean + strikeLean + jolt,
			twist + Mathf.DegToRad(StrikeTwistDegrees) * strikeTwist,
			idle));

		// La cabeza va al revés que el torso: mira al frente mientras el cuerpo
		// gira debajo. Es lo que hace que el bicho parezca que te está mirando.
		Pose(_head, Vector3.Zero, new Vector3(
			-lean * 0.5f + jolt,
			-twist * 0.6f,
			idle * 0.5f));

		// La mandíbula. Cuelga floja, se abre durante la anticipación y se cierra de
		// golpe con el tajo. Es la telegrafía de las cuencas contada otra vez con la
		// forma, y sirve para lo mismo: por el rabillo del ojo se ve que se ABRE algo
		// antes de distinguir de qué color es. Y al golpear, el chasquido.
		Pose(_jaw, Vector3.Zero, new Vector3(-(Mathf.DegToRad(Gape()) + jolt * 0.8f), 0.0f, 0.0f));

		// Piernas. La rodilla solo dobla en la vuelta: doblarla en el apoyo es lo
		// que hace que un ciclo de marcha parezca que el bicho se hunde en el suelo.
		Pose(_hipL, Vector3.Zero, new Vector3(Mathf.DegToRad(HipSwingDegrees) * swing, 0.0f, 0.0f));
		Pose(_hipR, Vector3.Zero, new Vector3(Mathf.DegToRad(HipSwingDegrees) * opposite, 0.0f, 0.0f));

		float kneeL = -Mathf.DegToRad(KneeBendDegrees) * Bend(_stride) * _amplitude;
		float kneeR = -Mathf.DegToRad(KneeBendDegrees) * Bend(_stride + Mathf.Pi) * _amplitude;

		Pose(_kneeL, Vector3.Zero, new Vector3(kneeL, 0.0f, 0.0f));
		Pose(_kneeR, Vector3.Zero, new Vector3(kneeR, 0.0f, 0.0f));

		// El tobillo deshace lo que hacen cadera y rodilla para que la planta llegue
		// al suelo plana. Sin esto el esqueleto anda de puntillas.
		Pose(_ankleL, Vector3.Zero, new Vector3(
			-(Mathf.DegToRad(HipSwingDegrees) * swing + kneeL) * 0.65f, 0.0f, 0.0f));
		Pose(_ankleR, Vector3.Zero, new Vector3(
			-(Mathf.DegToRad(HipSwingDegrees) * opposite + kneeR) * 0.65f, 0.0f, 0.0f));

		// Brazos en oposición a las piernas del mismo lado.
		Pose(_shoulderL, Vector3.Zero, new Vector3(
			Mathf.DegToRad(ArmSwingDegrees) * opposite + armL, 0.0f, 0.0f));

		Pose(_shoulderR, Vector3.Zero, new Vector3(
			Mathf.DegToRad(ArmSwingDegrees) * swing + armR, 0.0f, 0.0f));

		// El codo sigue al hombro con retraso: es lo que da el latigazo del golpe.
		Pose(_elbowL, Vector3.Zero, new Vector3(-Mathf.Abs(opposite) * 0.22f, 0.0f, 0.0f));
		Pose(_elbowR, Vector3.Zero, new Vector3(
			-Mathf.Abs(swing) * 0.22f + strikeElbow, 0.0f, 0.0f));
	}

	/// <summary>
	/// El golpe, en cuatro números: lo que sube cada brazo, lo que gira el torso y
	/// lo que se echa atrás. Sale de la fase y de lo recorrido de ella, así que un
	/// esqueleto con la anticipación más larga levanta el brazo más despacio sin
	/// tener que tocar nada aquí.
	/// </summary>
	private (float ArmL, float ArmR, float ElbowR, float Twist, float Lean) StrikePose()
	{
		// LEVANTAR EL MACHETE ES GIRAR EL HOMBRO HACIA ATRÁS, o sea con la X
		// negativa: el brazo pasa de colgar a apuntar arriba y atrás. Girarlo hacia
		// delante estira el brazo al frente, que es un empujón y no un tajo, y es
		// exactamente el fallo que hacía que la anticipación no se leyera.
		float windup = -Mathf.DegToRad(WindupArmDegrees);
		float strike = Mathf.DegToRad(StrikeArmDegrees);

		switch (_phase)
		{
			case CombatPhase.Windup:
				// Sube rápido y se queda arriba esperando. Esa espera ES la
				// telegrafía: si el brazo subiera lineal no habría nada que leer.
				float w = EaseOut(_progress);

				return (
					-windup * 0.18f * w,
					windup * w,
					1.0f * w,
					-w,
					Mathf.DegToRad(9.0f) * w);

			case CombatPhase.Active:
				// El codo se estira de golpe al final del arco. Es el latigazo: sin
				// él el brazo baja entero y rígido, como una barrera.
				float t = EaseIn(_progress);

				return (
					Mathf.Lerp(-windup * 0.18f, strike * 0.3f, t),
					Mathf.Lerp(windup, strike, t),
					Mathf.Lerp(1.0f, -0.1f, t),
					Mathf.Lerp(-1.0f, 1.0f, t),
					Mathf.Lerp(Mathf.DegToRad(9.0f), Mathf.DegToRad(-13.0f), t));

			case CombatPhase.Recovery:
				float r = 1.0f - EaseOut(_progress);

				return (strike * 0.3f * r, strike * r, -0.1f * r, r, Mathf.DegToRad(-13.0f) * r);

			default:
				return (0.0f, 0.0f, 0.0f, 0.0f, 0.0f);
		}
	}

	/// <summary>
	/// Lo que se abre la mandíbula, en grados. Cuelga floja en reposo —un muerto no
	/// aprieta los dientes—, se abre del todo durante la anticipación y se CIERRA DE
	/// GOLPE en el fotograma en que sale el filo. Ese cierre es el que se lee como
	/// mordisco aunque el bicho ataque con un machete.
	/// </summary>
	private float Gape()
	{
		return _phase switch
		{
			CombatPhase.Windup => Mathf.Lerp(IdleGapeDegrees, GapeDegrees, EaseOut(_progress)),
			CombatPhase.Active => IdleGapeDegrees * 0.2f,
			CombatPhase.Recovery => Mathf.Lerp(IdleGapeDegrees * 0.2f, IdleGapeDegrees, _progress),
			_ => IdleGapeDegrees,
		};
	}

	/// <summary>
	/// Le da a cada pieza su semilla, que es lo que hace que cuarenta huesos con el
	/// mismo material no sean cuarenta veces el mismo hueso. La lee el shader
	/// (`piece_seed`) para mover el ruido y la edad de cada pieza.
	///
	/// Sale del NOMBRE del nodo y no de un contador: así el fémur izquierdo tiene
	/// siempre la misma mancha entre dos ejecuciones, y si mañana se le añade una
	/// costilla no se recolocan las manchas de todo el bicho.
	///
	/// Encima va un desplazamiento por esqueleto: tres esqueletos en la misma sala
	/// tienen los mismos huesos y no pueden tener las mismas manchas en los mismos
	/// sitios, que es lo que los delata como copias.
	/// </summary>
	private void SeedPieces()
	{
		float offset = GD.Randf();

		List<MeshInstance3D> pieces = new();
		CollectBones(_hips, pieces);

		foreach (MeshInstance3D piece in pieces)
		{
			float seed = Hash(piece.Name.ToString()) + offset;
			piece.SetInstanceShaderParameter(PieceSeed, seed - Mathf.Floor(seed));
		}
	}

	/// <summary>Un número estable entre 0 y 1 sacado del nombre del nodo (FNV-1a).</summary>
	private static float Hash(string name)
	{
		uint h = 2166136261u;

		foreach (char c in name)
		{
			h = (h ^ c) * 16777619u;
		}

		return (h & 0xFFFFFFu) / 16777215.0f;
	}

	/// <summary>
	/// La curva de la rodilla: cero durante el apoyo, una campana durante la
	/// vuelta. Es media chuleta de animación en una línea, pero es LA línea: sin
	/// ella el bicho anda con las piernas rectas y se ve a treinta metros.
	/// </summary>
	private static float Bend(float phase)
	{
		return Mathf.Max(0.0f, Mathf.Sin(phase - 0.6f));
	}

	private void Pose(Node3D pivot, Vector3 offset, Vector3 rotation)
	{
		Transform3D rest = _rest[pivot];

		pivot.Transform = new Transform3D(
			rest.Basis * Basis.FromEuler(rotation),
			rest.Origin + offset);
	}

	private void UpdateCollapse(float dt)
	{
		// El derrumbe se integra a pasos fijos y no con el delta del fotograma. Es
		// la misma decisión que el escalonado de la pose y por el mismo motivo: a
		// sesenta pasos por segundo esto es un ragdoll moderno, a doce se ve el
		// hueso saltar de una posición a la siguiente, que es lo que se busca.
		float step = 1.0f / Mathf.Max(PoseHz, 1.0f);

		_collapseTimer += dt;

		while (_collapseTimer >= step)
		{
			_collapseTimer -= step;
			Integrate(step);
		}
	}

	private void Integrate(float step)
	{
		foreach (Bone bone in _debris)
		{
			if (bone.Grounded)
			{
				// Ya en el suelo: se acuesta. Se interpola en vez de encajarse de
				// golpe porque a doce pasos por segundo se ven los tres o cuatro
				// fotogramas de asentarse, y ese asentarse es lo que dice "esto ya
				// no se va a levantar".
				bone.Rotation = bone.Rotation.Slerp(bone.Lying, 0.35f);
				bone.Node.Basis = bone.Rotation.Scaled(bone.Scale);
				continue;
			}

			bone.Velocity += Vector3.Down * CollapseGravity * step;

			Vector3 position = bone.Node.Position + bone.Velocity * step;

			bone.Rotation = (new Basis(Vector3.Right, bone.Spin.X * step)
				* new Basis(Vector3.Up, bone.Spin.Y * step)
				* new Basis(Vector3.Forward, bone.Spin.Z * step)
				* bone.Rotation).Orthonormalized();

			bone.Node.Basis = bone.Rotation.Scaled(bone.Scale);

			if (position.Y <= bone.Rest)
			{
				position.Y = bone.Rest;

				// Un hueso no es una pelota: bota una o dos veces, poco, y se acuesta.
				//
				// SE MIRA LO QUE DEVUELVE EL SUELO, NO LO QUE TRAÍA LA PIEZA. Antes se
				// comparaba la velocidad de llegada contra medio metro por segundo, y esa
				// velocidad ya lleva sumada la gravedad de ESTE paso: a doce pasos por
				// segundo son 1,08 m/s de caída por paso, o sea que la condición no se
				// cumplía NUNCA. El resultado no se veía —la pieza quedaba clavada a su
				// altura de reposo dando botes de un milímetro— pero como no llegaba a
				// posarse tampoco llegaba a ACOSTARSE, y media docena de huesos se
				// quedaban de pie en el montón con la orientación con la que cayeron.
				float bounce = -bone.Velocity.Y * CollapseBounce;

				if (bounce < CollapseSettle)
				{
					bone.Grounded = true;
					bone.Velocity = Vector3.Zero;
					bone.Spin = Vector3.Zero;
				}
				else
				{
					bone.Velocity = new Vector3(
						bone.Velocity.X * 0.45f,
						bounce,
						bone.Velocity.Z * 0.45f);

					bone.Spin *= 0.5f;
				}
			}

			bone.Node.Position = position;
		}
	}

	/// <summary>Medio grosor de la pieza: a lo que se apoya cuando cae de lado.</summary>
	private static float Thickness(MeshInstance3D piece)
	{
		Vector3 size = piece.GetAabb().Size * piece.Scale.Abs();

		return Mathf.Min(size.X, Mathf.Min(size.Y, size.Z)) * 0.5f;
	}

	/// <summary>
	/// Recoge los huesos y deja fuera las cuencas: son una fuente de luz, no una
	/// pieza del bicho, y lo que tienen que hacer al morir es apagarse en el sitio.
	/// </summary>
	private static void CollectBones(Node node, List<MeshInstance3D> into)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is TelegraphMarker)
			{
				continue;
			}

			if (child is MeshInstance3D mesh)
			{
				into.Add(mesh);
			}

			CollectBones(child, into);
		}
	}

	private IEnumerable<Node3D> Pivots()
	{
		yield return _hips;
		yield return _torso;
		yield return _head;
		yield return _jaw;
		yield return _shoulderL;
		yield return _shoulderR;
		yield return _elbowL;
		yield return _elbowR;
		yield return _hipL;
		yield return _hipR;
		yield return _kneeL;
		yield return _kneeR;
		yield return _ankleL;
		yield return _ankleR;
	}

	private static float EaseOut(float t)
	{
		float u = 1.0f - Mathf.Clamp(t, 0.0f, 1.0f);

		return 1.0f - u * u * u;
	}

	private static float EaseIn(float t)
	{
		float u = Mathf.Clamp(t, 0.0f, 1.0f);

		return u * u * u;
	}
}
