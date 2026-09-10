using System.Collections.Generic;
using Godot;

namespace MedievalNightmare.Levels;

/// <summary>
/// El prado. Un par de miles de briznas en un solo <c>MultiMesh</c>, sembradas
/// al arrancar el nivel.
///
/// NO SE GUARDA EN LA ESCENA, y es la misma decisión que la malla de navegación:
/// dos mil transformaciones en un <c>.tscn</c> son cien kilobytes de números que
/// nadie puede leer, que ensucian cada diff y que se quedan viejos en cuanto se
/// mueve una lápida. Sembrar cuesta unas décimas al cargar y no puede
/// desfasarse.
///
/// CADA BRIZNA SE APOYA EN EL SUELO DE VERDAD. La altura sale de un rayo hacia
/// abajo contra la geometría, no de una constante: así los montículos de las
/// tumbas y el escalón de la cripta salen con hierba encima en vez de con
/// briznas flotando o enterradas. Es también lo que hace que se pueda mover el
/// terreno sin volver a tocar esto.
///
/// LO QUE NO LLEVA HIERBA se decide aquí y no con una máscara: la senda, que es
/// un pasillo de barro por el centro, y lo que tenga piedra debajo. La senda no
/// es recta —se le suma un ruido— porque un borde recto en un prado se lee como
/// una alfombra recortada.
///
/// La variación viaja en dos sitios y ninguno es el material: la altura y el
/// giro van en la transformación, y el compás del viento y el tono en
/// <c>INSTANCE_CUSTOM</c>. El compás sale de DÓNDE ESTÁ la brizna, así que las
/// vecinas se mueven casi igual y lo que se ve cruzando el prado es una ola, no
/// dos mil briznas temblando cada una por su cuenta.
/// </summary>
public partial class GrassField : MultiMeshInstance3D
{
	[ExportGroup("Siembra")]

	/// <summary>La cuña de una brizna. Su alto tiene que coincidir con
	/// <c>blade_height</c> del material o el viento dobla por donde no es.</summary>
	[Export] public Mesh Blade { get; set; }

	[Export] public int Count { get; set; } = 2600;

	/// <summary>Tamaño del sembrado en metros, centrado en este nodo.</summary>
	[Export] public Vector2 Area { get; set; } = new(40.0f, 48.0f);

	/// <summary>Desde qué altura se tira el rayo que busca el suelo.</summary>
	[Export] public float RayHeight { get; set; } = 6.0f;

	/// <summary>Hasta dónde busca. Lo que caiga más hondo se descarta: es un
	/// agujero, y una brizna dentro de un agujero es una brizna flotando.</summary>
	[Export] public float RayDepth { get; set; } = 8.0f;

	/// <summary>Semilla del sembrado. Cambiarla replanta el prado entero.</summary>
	[Export] public int Seed { get; set; } = 1917;

	[ExportGroup("Brizna")]
	[Export] public float MinScale { get; set; } = 0.7f;
	[Export] public float MaxScale { get; set; } = 1.45f;

	/// <summary>
	/// Dirección hacia la que miran las briznas, en grados. Todas se plantan cerca
	/// de ella: el viento del material dobla en la X del modelo, así que si cada
	/// brizna mirase a un lado, doblarían unas contra otras y el prado tiritaría
	/// en vez de ondear.
	/// </summary>
	[Export] public float FacingDegrees { get; set; } = 20.0f;

	/// <summary>Lo que se le permite desviarse a cada una de esa dirección.</summary>
	[Export] public float FacingSpreadDegrees { get; set; } = 38.0f;

	[ExportGroup("Senda")]

	/// <summary>Medio ancho del camino de barro que cruza el prado, en X.</summary>
	[Export] public float PathHalfWidth { get; set; } = 1.7f;

	/// <summary>Cuánto serpentea la senda. A cero es una alfombra recortada.</summary>
	[Export] public float PathWander { get; set; } = 2.6f;

	[ExportGroup("Ola")]

	/// <summary>Metros que mide una ola de viento. Corta, el prado tirita; larga,
	/// se mueve como una sábana.</summary>
	[Export] public float WaveLength { get; set; } = 7.0f;

	private int _wait = 2;

	/// <summary>
	/// Se siembra dos pasos de física DESPUÉS de arrancar, y esos dos pasos son la
	/// única parte de todo esto que costó encontrar.
	///
	/// El CSG del nivel construye su colisión en una llamada diferida al entrar en
	/// el árbol, y encima las órdenes al servidor de física no surten efecto hasta
	/// que se procesa el siguiente paso. Sembrando antes, los dos mil rayos salen
	/// contra un mundo vacío, no tocan nada y el prado sale sin una sola brizna:
	/// sin error, sin aviso y sin nada en pantalla.
	/// </summary>
	public override void _PhysicsProcess(double delta)
	{
		if (--_wait > 0)
		{
			return;
		}

		SetPhysicsProcess(false);
		Sow();
	}

	private void Sow()
	{
		if (Blade == null)
		{
			GD.PushWarning($"{Name} no tiene malla de brizna: el prado se queda vacío.");
			return;
		}

		RandomNumberGenerator rng = new() { Seed = (ulong)Seed };
		PhysicsDirectSpaceState3D space = GetWorld3D().DirectSpaceState;

		float facing = Mathf.DegToRad(FacingDegrees);
		float spread = Mathf.DegToRad(FacingSpreadDegrees);
		float half = Blade.GetAabb().Size.Y * 0.5f;

		// Se siembra a una lista y solo al final se reserva el MultiMesh. Fijar
		// `InstanceCount` reserva el buffer DE CERO: bajarlo después de escribir
		// las briznas no recorta la lista, la borra entera.
		List<Transform3D> placed = new(Count);
		List<Color> data = new(Count);

		for (int i = 0; i < Count; i++)
		{
			Vector3 origin = GlobalPosition + new Vector3(
				rng.RandfRange(-Area.X, Area.X) * 0.5f,
				0.0f,
				rng.RandfRange(-Area.Y, Area.Y) * 0.5f);

			if (OnPath(origin))
			{
				continue;
			}

			if (!Ground(space, origin, out Vector3 ground))
			{
				continue;
			}

			float scale = rng.RandfRange(MinScale, MaxScale);

			Basis basis = new Basis(Vector3.Up, facing + rng.RandfRange(-spread, spread))
				.Scaled(new Vector3(1.0f, scale, 1.0f));

			// El origen sube medio alto ESCALADO: la cuña está centrada en su
			// origen, así que puesta a ras de suelo se entierra media brizna.
			ground.Y += half * scale;

			placed.Add(new Transform3D(basis, ground));

			// El compás sale de la posición, no del azar: es lo que convierte dos
			// mil senos sueltos en una ola que cruza el prado.
			float wave = (ground.X + ground.Z * 0.6f) / Mathf.Max(WaveLength, 0.01f);

			data.Add(new Color(wave - Mathf.Floor(wave), rng.Randf(), 0.0f, 0.0f));
		}

		MultiMesh field = new()
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			UseCustomData = true,
			Mesh = Blade,
			InstanceCount = placed.Count,
		};

		for (int i = 0; i < placed.Count; i++)
		{
			field.SetInstanceTransform(i, placed[i]);
			field.SetInstanceCustomData(i, data[i]);
		}

		Multimesh = field;

		// La caja de visibilidad no se puede deducir de un MultiMesh generado, y
		// sin ella el prado desaparece en cuanto la cámara mira a otro lado.
		CustomAabb = new Aabb(
			new Vector3(-Area.X * 0.5f, -RayDepth, -Area.Y * 0.5f),
			new Vector3(Area.X, RayDepth + RayHeight, Area.Y));

		// Se dice en voz alta. Un prado vacío no da error de ninguna clase: se ve
		// un descampado y hay que ir a buscar por qué.
		if (placed.Count == 0)
		{
			GD.PushWarning($"{Name} no plantó ni una brizna: ningún rayo encontró suelo.");
		}
		else
		{
			GD.Print($"{Name}: {placed.Count} briznas de {Count} intentos.");
		}
	}

	/// <summary>
	/// El camino de barro. Serpentea con un seno de dos frecuencias en vez de con
	/// ruido: hace falta que la senda de la hierba coincida con la pieza de barro
	/// del nivel, y un seno se puede repetir a mano en el editor.
	/// </summary>
	private bool OnPath(Vector3 point)
	{
		float local = point.Z - GlobalPosition.Z;
		float wander = Mathf.Sin(local * 0.11f) * PathWander + Mathf.Sin(local * 0.31f) * PathWander * 0.35f;

		return Mathf.Abs(point.X - GlobalPosition.X - wander) < PathHalfWidth;
	}

	private bool Ground(PhysicsDirectSpaceState3D space, Vector3 at, out Vector3 hit)
	{
		PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
			at + Vector3.Up * RayHeight,
			at + Vector3.Down * RayDepth,
			1);

		Godot.Collections.Dictionary result = space.IntersectRay(query);

		if (result.Count == 0)
		{
			hit = at;
			return false;
		}

		hit = (Vector3)result["position"];

		// Solo se planta en lo llano. En la pared de un panteón la brizna sale
		// clavada de lado y se ve a diez metros.
		Vector3 normal = (Vector3)result["normal"];

		return normal.Y > 0.75f;
	}
}
