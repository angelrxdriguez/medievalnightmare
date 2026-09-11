using Godot;

namespace MedievalNightmare.Fx;

/// <summary>
/// El chispazo del impacto: una salpicadura de esquirlas y un destello de luz
/// de un décima de segundo en el punto del golpe. En una mazmorra a oscuras el
/// destello es la mitad del efecto: ilumina al enemigo justo en el fotograma
/// del contacto, que es cuando lo estás mirando.
///
/// Se construye entero por código, sin escena, por la misma razón que la malla
/// de navegación: es una docena de números que se leen mejor juntos que
/// repartidos por un inspector. Los recursos compartidos (malla y materiales)
/// se crean una vez y se reutilizan: el efecto se dispara cientos de veces por
/// incursión.
/// </summary>
public partial class HitSpark : GpuParticles3D
{
	private static Mesh _chipMesh;
	private static Material _chipMaterial;
	private static ParticleProcessMaterial _process;

	private OmniLight3D _flash;
	private float _age;

	public static void Spawn(Node context, Vector3 position)
	{
		if (context?.GetTree()?.CurrentScene is not { } scene)
		{
			return;
		}

		EnsureShared();

		HitSpark spark = new()
		{
			Amount = 14,
			Lifetime = 0.4f,
			OneShot = true,
			Explosiveness = 1.0f,
			ProcessMaterial = _process,
			DrawPass1 = _chipMesh,

			// Sin caja de visibilidad explícita, la que calcula el motor para un
			// one-shot recién nacido es un punto y el frustum lo descarta entero.
			VisibilityAabb = new Aabb(new Vector3(-2.5f, -2.5f, -2.5f), new Vector3(5.0f, 5.0f, 5.0f)),
		};

		spark._flash = new OmniLight3D
		{
			LightColor = new Color(1.0f, 0.9f, 0.7f),
			LightEnergy = 2.2f,
			OmniRange = 2.6f,
			ShadowEnabled = false,
		};

		spark.AddChild(spark._flash);
		scene.AddChild(spark);
		spark.GlobalPosition = position;
		spark.Emitting = true;
	}

	public override void _Process(double delta)
	{
		_age += (float)delta;

		// El destello se apaga en un suspiro; las esquirlas sobreviven al destello.
		_flash.LightEnergy = Mathf.Max(0.0f, 2.2f * (1.0f - _age / 0.12f));

		if (_age > 0.55f)
		{
			QueueFree();
		}
	}

	private static void EnsureShared()
	{
		if (_chipMesh != null)
		{
			return;
		}

		// Sin sombreado y con un punto de emisión: las esquirlas tienen que verse
		// en la oscuridad, que es donde vive todo el juego.
		_chipMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.93f, 0.88f, 0.76f),
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			EmissionEnabled = true,
			Emission = new Color(0.65f, 0.58f, 0.45f),
		};

		_chipMesh = new BoxMesh
		{
			Size = new Vector3(0.035f, 0.035f, 0.035f),
			Material = _chipMaterial,
		};

		_process = new ParticleProcessMaterial
		{
			Direction = new Vector3(0.0f, 1.0f, 0.0f),
			Spread = 180.0f,
			InitialVelocityMin = 1.8f,
			InitialVelocityMax = 4.2f,
			Gravity = new Vector3(0.0f, -11.0f, 0.0f),
			ScaleMin = 0.5f,
			ScaleMax = 1.3f,
			AngularVelocityMin = -360.0f,
			AngularVelocityMax = 360.0f,
			DampingMin = 1.0f,
			DampingMax = 3.0f,
		};
	}
}
