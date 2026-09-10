using Godot;

namespace MedievalNightmare.Props;

/// <summary>
/// Parpadeo de antorcha. Va en el nodo raíz de la antorcha y mueve la luz que
/// tiene colgando; la llama que se ve es cosa del shader.
///
/// Una luz fija delata que es una luz. El fuego no repite: sube, se ahoga, se
/// recupera. Aquí eso son tres capas de ruido, no un seno, porque un seno se
/// oye —el ojo le pilla el compás en cinco segundos y deja de ser fuego para
/// pasar a ser una bombilla con un temporizador.
///
/// 1. TEMBLOR. Ruido rápido de poca amplitud. Es el fuego respirando.
/// 2. AHOGO. Ruido lento y profundo que de vez en cuando casi apaga la
///    antorcha. Es lo que hace que una sala iluminada dé miedo: la luz que
///    tienes no es una garantía, es una que a veces te deja.
/// 3. BAILE. La luz se desplaza unos centímetros. Como las sombras las proyecta
///    ella, mover la fuente mueve todas las sombras de la sala a la vez, que es
///    de donde sale la sensación de que la mazmorra está viva.
///
/// Cuanto más baja está la llama, más roja: un fuego que se apaga pierde la
/// punta blanca antes que el cuerpo naranja.
///
/// Cada antorcha arranca con una fase distinta. Sin eso, ocho antorchas laten
/// al unísono y la sala entera pulsa como un corazón, que es un efecto muy
/// llamativo y exactamente el contrario del que se busca.
/// </summary>
public partial class TorchLight : Node3D
{
	[Export] public NodePath LightPath { get; set; } = "Light";

	/// <summary>Malla de la llama. Se le pasa la fase para que tampoco vaya a compás.</summary>
	[Export] public NodePath FlamePath { get; set; } = "Flame";

	[ExportGroup("Temblor")]

	/// <summary>Fracción de la energía base que se va en el temblor rápido.</summary>
	[Export] public float FlickerAmount { get; set; } = 0.22f;

	[Export] public float FlickerSpeed { get; set; } = 6.5f;

	[ExportGroup("Ahogo")]

	/// <summary>Cuánto llega a bajar la llama en el peor momento. 0,45 es medio apagarse.</summary>
	[Export] public float GutterDepth { get; set; } = 0.42f;

	[Export] public float GutterSpeed { get; set; } = 0.45f;

	[ExportGroup("Baile")]

	/// <summary>Cuánto se mueve la fuente de luz, en metros. Poco: mueve las sombras.</summary>
	[Export] public float SwayAmount { get; set; } = 0.045f;

	[Export] public float SwaySpeed { get; set; } = 2.1f;

	[ExportGroup("Llama")]

	/// <summary>
	/// Brillo del cuadro de la llama. La antorcha de mano va a medio metro del
	/// ojo, así que necesita bastante menos que una de pared a ocho metros.
	/// </summary>
	[Export] public float FlameIntensity { get; set; } = 2.6f;

	[ExportGroup("Color")]

	/// <summary>Hacia dónde va el color cuando la llama baja. Rojo sangre, no naranja.</summary>
	[Export] public Color EmberColor { get; set; } = new(1.0f, 0.38f, 0.13f);

	private OmniLight3D _light;
	private MeshInstance3D _flame;
	private ShaderMaterial _flameMaterial;

	private float _time;
	private float _phase;
	private float _baseEnergy;
	private Color _baseColor;
	private Vector3 _basePosition;
	private Vector3 _flameScale = Vector3.One;

	public override void _Ready()
	{
		_light = GetNodeOrNull<OmniLight3D>(LightPath);
		_flame = GetNodeOrNull<MeshInstance3D>(FlamePath);

		RandomNumberGenerator rng = new();
		rng.Randomize();
		_phase = rng.RandfRange(0.0f, 90.0f);

		if (_light != null)
		{
			_baseEnergy = _light.LightEnergy;
			_baseColor = _light.LightColor;
			_basePosition = _light.Position;
		}

		if (_flame != null)
		{
			_flameScale = _flame.Scale;

			// El material es local a la escena, así que esta copia es solo de esta
			// antorcha y ponerle semilla no afecta a las demás.
			_flameMaterial = _flame.MaterialOverride as ShaderMaterial;
			_flameMaterial?.SetShaderParameter("seed", _phase);
			_flameMaterial?.SetShaderParameter("intensity", FlameIntensity);
		}

		if (_light == null)
		{
			GD.PushWarning($"{Name}: no encuentro la luz en '{LightPath}'.");
			SetProcess(false);
		}
	}

	public override void _Process(double delta)
	{
		_time += (float)delta;

		float t = _time + _phase;

		// Temblor: dos octavas para que no se le vea el periodo a ninguna.
		float shake = Noise(t * FlickerSpeed) * 0.62f
			+ Noise(t * FlickerSpeed * 2.7f + 31.0f) * 0.38f;

		// Ahogo: solo la mitad positiva del ruido lento, para que la antorcha esté
		// bien la mayor parte del tiempo y falle de vez en cuando.
		float gutter = Mathf.Max(0.0f, Noise(t * GutterSpeed + 71.0f));
		gutter *= gutter;

		float level = (1.0f + shake * FlickerAmount) * (1.0f - gutter * GutterDepth);
		level = Mathf.Max(level, 0.05f);

		_light.LightEnergy = _baseEnergy * level;

		// Al bajar la llama se pierde el blanco antes que el naranja.
		_light.LightColor = _baseColor.Lerp(EmberColor, Mathf.Clamp(1.0f - level, 0.0f, 1.0f) * 0.8f);

		_light.Position = _basePosition + new Vector3(
			Noise(t * SwaySpeed + 11.0f),
			Noise(t * SwaySpeed * 1.4f + 47.0f) * 0.6f,
			Noise(t * SwaySpeed + 93.0f)) * SwayAmount;

		if (_flame != null)
		{
			// La llama se encoge con el ahogo. Sin esto la luz baja pero se sigue
			// viendo el mismo fuego, y el truco se cae.
			_flame.Scale = _flameScale * new Vector3(
				0.9f + level * 0.12f,
				0.55f + level * 0.5f,
				1.0f);
		}
	}

	/// <summary>Ruido de valor en 1D, suave y sin periodo audible. Devuelve [-1, 1].</summary>
	private static float Noise(float x)
	{
		int i = Mathf.FloorToInt(x);
		float f = x - i;
		f = f * f * (3.0f - 2.0f * f);

		return Mathf.Lerp(Hash(i), Hash(i + 1), f);
	}

	private static float Hash(int n)
	{
		n = (n << 13) ^ n;
		int m = n * (n * n * 15731 + 789221) + 1376312589;

		return 1.0f - (m & 0x7fffffff) / 1073741824.0f;
	}
}
