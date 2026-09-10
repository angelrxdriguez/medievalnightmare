using Godot;
using MedievalNightmare.Core;

namespace MedievalNightmare.Ui;

/// <summary>
/// Morir. Hasta ahora era una línea por consola y la escena que se recargaba
/// sola dos segundos después: mecánicamente correcto y, jugando, indistinguible
/// de un fallo del juego.
///
/// Lo que hace falta no es una pantalla bonita, son tres cosas en este orden:
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
/// El reinicio vive aquí y no en el jugador a propósito: quien sabe cuándo se
/// ha terminado de contar la muerte es quien la está contando.
/// </summary>
public partial class DeathScreen : CanvasLayer
{
	[Export] public float FadeSeconds { get; set; } = 1.5f;

	/// <summary>Lo que se queda el negro quieto antes de reiniciar.</summary>
	[Export] public float HoldSeconds { get; set; } = 1.1f;

	/// <summary>Parte del fundido que pasa antes de que aparezca el texto.</summary>
	[Export(PropertyHint.Range, "0,1,0.05")] public float TitleDelay { get; set; } = 0.55f;

	[Export] public string Title { get; set; } = "HAS MUERTO";

	private ColorRect _fade;
	private Label _title;
	private float _time;
	private bool _dying;

	public override void _Ready()
	{
		_fade = GetNode<ColorRect>("Fade");
		_title = GetNode<Label>("Title");
		_title.Text = Title;

		Visible = false;
		SetProcess(false);

		GetParent().GetNode<Health>("Health").Died += OnDied;
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

		if (_time >= FadeSeconds + HoldSeconds)
		{
			SetProcess(false);

			// El ratón vuelve a estar suelto antes de recargar: la escena nueva lo
			// vuelve a capturar ella sola, y si se recarga con él capturado hay un
			// fotograma en el que el juego ya se ha reiniciado y tú sigues sin poder
			// salir de la ventana.
			Input.MouseMode = Input.MouseModeEnum.Visible;
			GetTree().ReloadCurrentScene();
		}
	}

	private void OnDied()
	{
		if (_dying)
		{
			return;
		}

		_dying = true;
		_time = 0.0f;
		Visible = true;
		SetProcess(true);
	}
}
