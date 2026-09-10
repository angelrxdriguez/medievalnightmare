using Godot;

namespace MedievalNightmare.Props;

/// <summary>
/// Un árbol muerto, construido al arrancar a partir de una semilla.
///
/// SE GENERA Y NO SE MODELA, y no es por ahorrarse el trabajo: un cementerio
/// necesita cuatro o cinco árboles y cuatro copias del mismo árbol se reconocen
/// al instante, sobre todo de noche, que es cuando lo único que se ve de ellos
/// es la SILUETA. Cambiando un número salen cuatro árboles distintos; modelando,
/// serían cuatro escenas que mantener.
///
/// Y la silueta es todo lo que hay. Sin hojas, un árbol es un dibujo de líneas
/// negras contra el cielo, así que lo único que importa es cómo se reparten esas
/// líneas. De ahí las tres reglas de abajo:
///
/// - EL TRONCO SE DOBLA. Va en tres tramos, cada uno un poco más torcido que el
///   anterior. Un tronco recto se lee como un poste de teléfono, y ningún árbol
///   de un cementerio es un poste de teléfono.
/// - LAS RAMAS SUBEN AL SALIR. Nacen abiertas y se enderezan: es lo que hace la
///   madera de verdad buscando la luz, y es lo que separa un árbol de un
///   plumero. Ramas rectas en abanico se leen como un tenedor.
/// - LOS RAMILLOS SON MUCHOS Y CORTOS. Los que dan la silueta erizada no son las
///   ramas gruesas, son los de segundo orden. Sin ellos el árbol se ve pelado, y
///   pelado no da miedo: da pena.
///
/// Cada rama lleva su propia malla en vez de escalar una compartida. Escalar
/// sería más barato, pero el shader de madera mide sus texeles en el espacio del
/// MODELO: un ramillo escalado a la vigésima parte tendría la veta veinte veces
/// más fina y herviría de aliasing al moverse.
/// </summary>
public partial class DeadTree : Node3D
{
	[ExportGroup("Semilla")]

	/// <summary>Cambia el árbol entero. Dos instancias con la misma semilla son
	/// el mismo árbol, así que en un nivel se pone una distinta a cada una.</summary>
	[Export] public int Seed { get; set; } = 1;

	[ExportGroup("Tronco")]
	[Export] public float MinHeight { get; set; } = 4.2f;
	[Export] public float MaxHeight { get; set; } = 6.4f;
	[Export] public float BaseRadius { get; set; } = 0.26f;

	/// <summary>Lo que queda del grosor al llegar arriba.</summary>
	[Export(PropertyHint.Range, "0.05,0.9,0.05")] public float TopRatio { get; set; } = 0.34f;

	/// <summary>Grados que se tuerce cada tramo respecto al anterior.</summary>
	[Export] public float LeanDegrees { get; set; } = 9.0f;

	[ExportGroup("Ramas")]
	[Export] public int MinBranches { get; set; } = 5;
	[Export] public int MaxBranches { get; set; } = 8;

	/// <summary>Desde qué altura del tronco salen. Por debajo no hay ninguna.</summary>
	[Export(PropertyHint.Range, "0,1,0.05")] public float BranchStart { get; set; } = 0.45f;

	[Export] public float MinBranchAngle { get; set; } = 32.0f;
	[Export] public float MaxBranchAngle { get; set; } = 68.0f;

	/// <summary>Cuánto se endereza la rama de la mitad al final.</summary>
	[Export] public float BranchRise { get; set; } = 26.0f;

	[Export] public int MinTwigs { get; set; } = 1;
	[Export] public int MaxTwigs { get; set; } = 3;

	[ExportGroup("Material")]
	[Export] public Material Bark { get; set; }

	/// <summary>Caras del cilindro. Cinco: se cuentan, que es de lo que se trata.</summary>
	[Export] public int Sides { get; set; } = 5;

	private RandomNumberGenerator _rng;

	public override void _Ready()
	{
		_rng = new RandomNumberGenerator { Seed = (ulong)Seed };

		float height = _rng.RandfRange(MinHeight, MaxHeight);
		float lean = Mathf.DegToRad(LeanDegrees);

		// El tronco, en tres tramos. Cada uno arranca donde acabó el anterior y
		// con un poco más de inclinación, así que la torcedura se acumula hacia
		// arriba igual que en un árbol de verdad.
		Vector3 tip = Vector3.Zero;
		Vector3 direction = Vector3.Up;
		float azimuth = _rng.RandfRange(0.0f, Mathf.Tau);
		float radius = BaseRadius;

		Vector3[] joints = new Vector3[4];
		joints[0] = tip;

		for (int i = 0; i < 3; i++)
		{
			float segment = height / 3.0f;
			float top = BaseRadius * Mathf.Lerp(1.0f, TopRatio, (i + 1) / 3.0f);

			direction = Tilt(direction, azimuth + _rng.RandfRange(-0.8f, 0.8f), lean).Normalized();

			Vector3 next = tip + direction * segment;
			Limb(tip, next, radius, top);

			tip = next;
			radius = top;
			joints[i + 1] = tip;
		}

		int branches = _rng.RandiRange(MinBranches, MaxBranches);

		for (int i = 0; i < branches; i++)
		{
			// Repartidas en altura y en rumbo, pero con ruido: a intervalos exactos
			// se ve la espiral y el árbol se lee como una escalera de caracol.
			float along = Mathf.Lerp(BranchStart, 0.97f, (i + _rng.RandfRange(0.0f, 0.7f)) / branches);
			float turn = i * Mathf.Tau / branches + _rng.RandfRange(-0.5f, 0.5f);

			Vector3 from = TrunkPoint(joints, along);
			float thickness = BaseRadius * Mathf.Lerp(0.55f, 0.2f, along);

			Branch(from, turn, thickness, height * _rng.RandfRange(0.22f, 0.42f), 1);
		}
	}

	/// <summary>
	/// Una rama y sus ramillos. Se dibuja en dos tramos —el segundo más
	/// enderezado— porque una recta no se lee como madera; lo que dice "esto
	/// creció" es que la dirección cambie a mitad.
	/// </summary>
	private void Branch(Vector3 from, float turn, float thickness, float length, int depth)
	{
		float angle = Mathf.DegToRad(_rng.RandfRange(MinBranchAngle, MaxBranchAngle));
		Vector3 direction = Tilt(Vector3.Up, turn, angle).Normalized();

		Vector3 middle = from + direction * length * 0.55f;
		Limb(from, middle, thickness, thickness * 0.72f);

		// El segundo tramo sube: la rama se endereza buscando la luz.
		Vector3 risen = Tilt(direction, turn, -Mathf.DegToRad(BranchRise)).Normalized();
		Vector3 end = middle + risen * length * 0.45f;
		Limb(middle, end, thickness * 0.72f, thickness * 0.35f);

		if (depth >= 2)
		{
			return;
		}

		int twigs = _rng.RandiRange(MinTwigs, MaxTwigs);

		for (int i = 0; i < twigs; i++)
		{
			Vector3 root = middle.Lerp(end, _rng.RandfRange(0.1f, 0.9f));

			Branch(
				root,
				turn + _rng.RandfRange(-1.5f, 1.5f),
				thickness * 0.45f,
				length * _rng.RandfRange(0.35f, 0.6f),
				depth + 1);
		}
	}

	/// <summary>
	/// Un tramo de madera entre dos puntos. La malla es propia y no una
	/// compartida escalada: ver la nota de la clase sobre los texeles.
	/// </summary>
	private void Limb(Vector3 from, Vector3 to, float bottom, float top)
	{
		Vector3 axis = to - from;
		float length = axis.Length();

		if (length < 0.01f)
		{
			return;
		}

		CylinderMesh mesh = new()
		{
			TopRadius = top,
			BottomRadius = bottom,
			Height = length,
			RadialSegments = Sides,
			Rings = 1,
			Material = Bark,
		};

		MeshInstance3D limb = new()
		{
			Mesh = mesh,
			Transform = new Transform3D(Aim(axis / length), (from + to) * 0.5f),

			// Las ramas SÍ dan sombra: son la silueta del árbol tirada sobre la
			// hierba, y es media atmósfera de un cementerio a la luz de la luna.
			CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
		};

		AddChild(limb);
	}

	/// <summary>Base cuyo eje Y es la dirección dada: el cilindro crece a lo largo de Y.</summary>
	private static Basis Aim(Vector3 direction)
	{
		Vector3 side = direction.Cross(Vector3.Forward);

		// Mirando recto arriba el producto vectorial se anula y la base sale
		// degenerada, que en pantalla es una rama que desaparece.
		if (side.LengthSquared() < 0.0001f)
		{
			side = direction.Cross(Vector3.Right);
		}

		side = side.Normalized();

		return new Basis(side, direction, side.Cross(direction));
	}

	/// <summary>Inclina una dirección <paramref name="angle"/> radianes hacia el rumbo dado.</summary>
	private static Vector3 Tilt(Vector3 direction, float azimuth, float angle)
	{
		Vector3 axis = new Vector3(Mathf.Cos(azimuth), 0.0f, Mathf.Sin(azimuth));

		return direction.Rotated(axis.Normalized(), angle);
	}

	/// <summary>Punto del tronco a una fracción de su altura, siguiendo sus tramos.</summary>
	private static Vector3 TrunkPoint(Vector3[] joints, float along)
	{
		float scaled = Mathf.Clamp(along, 0.0f, 1.0f) * (joints.Length - 1);
		int index = Mathf.Min((int)scaled, joints.Length - 2);

		return joints[index].Lerp(joints[index + 1], scaled - index);
	}
}
