using Godot;
using MedievalNightmare.Core;

namespace MedievalNightmare.Ui;

/// <summary>
/// El final de una incursión, que solo tiene dos formas: morir o salir. Las dos
/// terminan igual —negro, una línea y volver a empezar— y por eso las cuenta la
/// misma pantalla; lo único que cambia es el color y lo que dice.
///
/// Morir era una línea por consola y la escena recargándose sola: mecánicamente
/// correcto y, jugando, indistinguible de un fallo del juego. Lo que hace falta
/// no es una pantalla bonita, son tres cosas en este orden:
///
/// 1. QUE SE ACABE. El negro entra despacio y desde el borde, que es hacia
///    donde ya venía tirando la viñeta de daño: la pantalla lleva un rato
///    cerrándose y esto es el final de ese gesto, no un corte.
/// 2. QUE SE LEA. El texto entra cuando el negro ya casi ha cerrado. Si entra
///    antes, compite con lo último que estabas mirando —el esqueleto que te ha
///    matado— y no se ve ninguna de las dos cosas.
/// 3. QUE DÉ TIEMPO A ENTENDERLO. Un silencio corto en negro antes de reiniciar.
///    Sin él, la sala vuelve a aparecer antes de que sueltes el ratón y se lee
///    como un tirón, no como una consecuencia.
///
/// EXTRAER SÍ PARA EL MUNDO Y MORIR NO. No es una asimetría gratuita: al morir,
/// que la sala siga su curso mientras se va la luz es medio la escena. Al
/// extraer ya has ganado, y dejar tres esqueletos persiguiéndote durante el
/// fundido significa que te pueden matar DESPUÉS de haber salido, que es la peor
/// forma posible de perder el equipo.
///
/// El reinicio vive aquí y no en el jugador a propósito: quien sabe cuándo se
/// ha terminado de contar el final es quien lo está contando.
/// </summary>
public partial class RaidEndScreen : CanvasLayer
{
	/// <summary>Quien tenga que preguntar si la incursión se está acabando, mira aquí.</summary>
	public const string Group = "raid_end";

	[Export] public float FadeSeconds { get; set; } = 1.5f;

	/// <summary>Lo que se queda el negro quieto antes de reiniciar.</summary>
	[Export] public float HoldSeconds { get; set; } = 1.1f;

	/// <summary>Parte del fundido que pasa antes de que aparezca el texto.</summary>
	[Export(PropertyHint.Range, "0,1,0.05")] public float TitleDelay { get; set; } = 0.55f;

	[ExportGroup("Morir")]
	[Export] public string DeathTitle { get; set; } = "HAS MUERTO";

	/// <summary>Sangre. El mismo rojo al que ya venía tirando el borde de la pantalla.</summary>
	[Export] public Color DeathColor { get; set; } = new(0.72f, 0.15f, 0.09f);

	[ExportGroup("Extraer")]
	[Export] public string ExtractTitle { get; set; } = "HAS EXTRAÍDO";

	/// <summary>
	/// Ámbar de antorcha. Nada de blanco ni de verde de victoria: extraer no es
	/// ganar, es volver con lo puesto (`ARTE.md` §3).
	/// </summary>
	[Export] public Color ExtractColor { get; set; } = new(0.98f, 0.72f, 0.25f);

	private ColorRect _fade;
	private Label _title;
	private float _time;
	private bool _ending;
	private bool _freeze;

	/// <summary>La incursión ya se está acabando. La pausa lo consulta para no estorbar.</summary>
	public bool IsEnding => _ending;

	/// <summary>
	/// Lo pregunta cualquiera que no deba abrirse encima de un final: la pausa y
	/// el inventario. Por grupo, para que ninguno de los dos tenga que saber de
	/// qué nodo cuelga esta pantalla.
	/// </summary>
	public static bool IsRaidEnding(SceneTree tree)
	{
		return tree.GetFirstNodeInGroup(Group) is RaidEndScreen screen && screen.IsEnding;
	}

	public override void _Ready()
	{
		AddToGroup(Group);

		// Extraer para el árbol entero, así que esto tiene que seguir vivo con el
		// juego parado: es lo único que puede volver a arrancarlo.
		ProcessMode = ProcessModeEnum.Always;

		_fade = GetNode<ColorRect>("Fade");
		_title = GetNode<Label>("Title");

		Visible = false;
		SetProcess(false);

		GetParent().GetNode<Health>("Health").Died += OnDied;
	}

	/// <summary>Has salido vivo. Lo que llevabas ya está guardado antes de llegar aquí.</summary>
	public void Extract()
	{
		Begin(ExtractTitle, ExtractColor, freeze: true);
	}

	public override void _Process(double delta)
	{
		_time += (float)delta;

		float fade = Mathf.Clamp(_time / Mathf.Max(FadeSeconds, 0.01f), 0.0f, 1.0f);

		// Al cuadrado: el negro tarda en arrancar y luego cae de golpe. Lineal se
		// lee como una transición de menú; así se lee como perder el sentido.
		_fade.Color = new Color(0.0f, 0.0f, 0.0f, fade * fade);

		float title = Mathf.Clamp((fade - TitleDelay) / Mathf.Max(1.0f - TitleDelay, 0.01f), 0.0f, 1.0f);
		_title.Modulate = new Color(1.0f, 1.0f, 1.0f, title);

		if (_time < FadeSeconds + HoldSeconds)
		{
			return;
		}

		SetProcess(false);

		// Despausar ANTES de recargar: una escena que nace con el árbol parado se
		// queda congelada y parece que el juego se ha colgado.
		if (_freeze)
		{
			GetTree().Paused = false;
		}

		// El ratón vuelve a estar suelto antes de recargar: la escena nueva lo
		// vuelve a capturar ella sola, y si se recarga con él capturado hay un
		// fotograma en el que el juego ya se ha reiniciado y tú sigues sin poder
		// salir de la ventana.
		Input.MouseMode = Input.MouseModeEnum.Visible;
		GetTree().ReloadCurrentScene();
	}

	private void OnDied()
	{
		Begin(DeathTitle, DeathColor, freeze: false);
	}

	private void Begin(string title, Color color, bool freeze)
	{
		if (_ending)
		{
			return;
		}

		_ending = true;
		_freeze = freeze;
		_time = 0.0f;

		_title.Text = title;
		_title.AddThemeColorOverride("font_color", color);

		Visible = true;
		SetProcess(true);

		if (freeze)
		{
			GetTree().Paused = true;
		}
	}
}
