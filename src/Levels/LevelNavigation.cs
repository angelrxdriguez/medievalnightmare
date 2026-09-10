using Godot;

namespace MedievalNightmare.Levels;

/// <summary>
/// La malla por la que andan los enemigos. Se hornea al arrancar el nivel, no
/// se guarda en el <c>.tscn</c>.
///
/// Es una decisión, no una comodidad: el nivel es CSG y se toca a diario —una
/// pared que se mueve un metro, un pilar más—, y una malla guardada se queda
/// vieja en silencio. Lo que se ve entonces es a un esqueleto atravesando una
/// pared que ya no está donde dice su ruta, y eso se tarda media hora en
/// atribuir a la navegación. Horneando al arrancar, la malla no puede estar
/// desfasada nunca. Una sala de 29 × 29 m se hornea en unas décimas y solo se
/// paga al cargar.
///
/// La geometría de origen se pide POR GRUPO y no por hijos: así el CSG del
/// nivel se queda donde está, en su sitio del árbol, y esto es un nodo suelto
/// que no reordena nada. Al grupo entra solo lo que es suelo y pared; el techo
/// se queda fuera a propósito, porque su cara de arriba es tan andable como el
/// suelo para el que hornea y saldría una segunda planta fantasma.
///
/// AVISO CONOCIDO Y ACEPTADO. Godot avisa por consola de que ha tenido que leer
/// las mallas de vuelta desde la GPU para hornear, y recomienda parsear
/// colisionadores. Aquí no se puede: el CSG no cuelga ningún <c>StaticBody3D</c>
/// del árbol —se registra él solo en el servidor de física—, así que por
/// colisionadores no se parsea nada. La lectura se paga UNA vez al cargar el
/// nivel y no vuelve a ocurrir. No hay nada que arreglar.
/// </summary>
public partial class LevelNavigation : NavigationRegion3D
{
	/// <summary>
	/// Grupo del que sale la geometría. Lo lleva el <c>CSGCombiner3D</c> del
	/// nivel, y solo él.
	/// </summary>
	public const string SourceGroup = "navigation_source";

	[ExportGroup("Agente")]

	/// <summary>
	/// Lo gordo que se supone al que anda: es lo que se separa la malla de cada
	/// pared. Un pelo más que el radio del esqueleto (0,35 m) y ni uno más, porque
	/// lo paga la puerta: cada centímetro de aquí se come dos de hueco, y el vano
	/// de 1,8 m es el sitio más estrecho por el que tienen que pasar tres.
	///
	/// Va en múltiplos de <see cref="CellSize"/> a propósito. El horneado lo
	/// redondea hacia arriba de todas formas y avisa por consola de que ha perdido
	/// precisión; dándoselo ya redondeado, lo que se pide es lo que sale.
	/// </summary>
	[Export] public float AgentRadius { get; set; } = 0.4f;

	[Export] public float AgentHeight { get; set; } = 1.8f;

	/// <summary>Lo que sube de un paso. Los escalones de la sala son de 0,25 m.
	/// Múltiplo de <see cref="CellHeight"/>, por lo mismo que el radio.</summary>
	[Export] public float AgentMaxClimb { get; set; } = 0.3f;

	[Export] public float AgentMaxSlopeDegrees { get; set; } = 45.0f;

	[ExportGroup("Horneado")]

	/// <summary>
	/// Resolución del horneado. Bajarlo afina las esquinas y multiplica el coste
	/// por cuatro; 0,2 m deja pasar bien entre un pilar y una pared.
	/// </summary>
	[Export] public float CellSize { get; set; } = 0.2f;

	[Export] public float CellHeight { get; set; } = 0.1f;

	/// <summary>
	/// Volumen que se hornea, en coordenadas del nivel. Recorta por arriba a
	/// propósito: sin esto, la cara superior de un muro de 3,7 m sale como isla
	/// andable, y el horneado tarda el doble en producir suelo al que no se puede
	/// llegar.
	/// </summary>
	[Export] public Aabb BakeBounds { get; set; } = new(new Vector3(-40.0f, -2.0f, -40.0f), new Vector3(80.0f, 5.0f, 80.0f));

	public override void _Ready()
	{
		// El mapa del mundo trae su propia rejilla (0,25 m) y no tiene por qué
		// coincidir con la del horneado. Cuando no coinciden, los bordes de la
		// malla se rasterizan en una rejilla distinta de la que los generó y
		// aparecen costuras por las que el agente se cae de la malla. Manda la
		// del nivel: es la que se ha elegido mirando el ancho de la puerta.
		Rid map = GetWorld3D().NavigationMap;
		NavigationServer3D.MapSetCellSize(map, CellSize);
		NavigationServer3D.MapSetCellHeight(map, CellHeight);

		NavigationMesh = BuildMesh();

		// Un fotograma de margen. El CSG genera su malla en una llamada diferida
		// al entrar en el árbol, y hornear antes que ella devuelve una región
		// vacía y ningún error.
		CallDeferred(MethodName.BakeNow);
	}

	private NavigationMesh BuildMesh()
	{
		return new NavigationMesh
		{
			CellSize = CellSize,
			CellHeight = CellHeight,
			AgentRadius = AgentRadius,
			AgentHeight = AgentHeight,
			AgentMaxClimb = AgentMaxClimb,
			AgentMaxSlope = AgentMaxSlopeDegrees,

			// De las mallas y no de los colisionadores: el CSG no cuelga un
			// StaticBody3D del árbol —se registra él mismo en el servidor de
			// física—, así que por colisionadores no se parsea nada y la región
			// sale vacía sin decir por qué.
			GeometryParsedGeometryType = NavigationMesh.ParsedGeometryType.MeshInstances,
			GeometrySourceGeometryMode = NavigationMesh.SourceGeometryMode.GroupsExplicit,
			GeometrySourceGroupName = SourceGroup,
			FilterBakingAabb = BakeBounds,
		};
	}

	/// <summary>
	/// Hornea y comprueba. El aviso importa más de lo que parece: una región
	/// vacía no da error, simplemente deja a todos los enemigos andando en línea
	/// recta, que es exactamente el fallo que esto viene a arreglar.
	/// </summary>
	private void BakeNow()
	{
		BakeNavigationMesh(onThread: false);

		if (NavigationMesh.GetPolygonCount() == 0)
		{
			GD.PushWarning(
				$"La malla de navegación de {Name} salió vacía. "
				+ $"¿Está el CSG del nivel en el grupo \"{SourceGroup}\"?");
		}
	}
}
