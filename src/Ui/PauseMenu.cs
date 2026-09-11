using Godot;
using MedievalNightmare.Core;

namespace MedievalNightmare.Ui;

/// <summary>
/// La pausa. Tres opciones y ni una más: continuar, reiniciar y salir.
///
/// Se construye en código y no en la escena por lo mismo que la telegrafía o la
/// mira: son cuatro nodos y quince colores, y en un <c>.tscn</c> eso son
/// doscientas líneas que nadie puede leer y que el editor reordena sola. Aquí
/// el aspecto se lee de arriba abajo y se cambia en un sitio.
///
/// TRES COSAS QUE HAY QUE HACER BIEN O LA PAUSA SE NOTA ROTA:
///
/// - EL RATÓN. Pausar lo suelta y continuar lo vuelve a capturar. Si no, o no
///   puedes pulsar los botones, o al continuar te giras media vuelta con el
///   primer movimiento.
/// - EL PROPIO NODO NO SE PAUSA. Todo el árbol se para menos esto, que es lo
///   único que puede despausarlo.
/// - CON LA INCURSIÓN ACABADA NO SE PAUSA. La pantalla de fin se está contando
///   sola y tiene su propio reinicio: dejar abrir la pausa encima es acabar con
///   dos reinicios pedidos a la vez.
/// </summary>
public partial class PauseMenu : CanvasLayer
{
	[ExportGroup("Aspecto")]

	/// <summary>Lo que se oscurece el juego detrás. No del todo: sigues viendo
	/// dónde te has dejado la partida, que es la mitad de lo que es una pausa.</summary>
	[Export] public Color DimColor { get; set; } = new(0.015f, 0.016f, 0.022f, 0.74f);

	/// <summary>Hueso. El mismo color que el texto del HUD.</summary>
	[Export] public Color TextColor { get; set; } = new(0.88f, 0.85f, 0.78f);

	/// <summary>Ámbar de antorcha, el mismo de la anticipación. Aquí solo marca
	/// dónde está el dedo: en un menú no hay nada que telegrafiar.</summary>
	[Export] public Color HighlightColor { get; set; } = new(0.98f, 0.72f, 0.25f);

	[Export] public int TitleSize { get; set; } = 34;
	[Export] public int OptionSize { get; set; } = 22;

	private Health _health;
	private VBoxContainer _options;
	private Button _first;

	public bool IsOpen => Visible;

	public override void _Ready()
	{
		// Por encima de la mira y de la viñeta, por debajo del negro de la muerte.
		Layer = 8;

		// Lo único que sigue vivo con el árbol parado. Sin esto, abrir la pausa es
		// pausarse a sí misma y no hay forma de salir.
		ProcessMode = ProcessModeEnum.Always;

		Visible = false;

		_health = GetParent().GetNode<Health>("Health");

		Build();
	}

	private void Build()
	{
		ColorRect dim = new()
		{
			Color = DimColor,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};

		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(dim);

		_options = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
		};

		_options.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_options.AddThemeConstantOverride("separation", 14);
		AddChild(_options);

		Label title = new()
		{
			Text = "PAUSA",
			HorizontalAlignment = HorizontalAlignment.Center,
		};

		title.AddThemeFontSizeOverride("font_size", TitleSize);
		title.AddThemeColorOverride("font_color", TextColor);
		title.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f));
		title.AddThemeConstantOverride("outline_size", 8);
		_options.AddChild(title);

		// Un hueco antes de las opciones. Pegado al título, "Continuar" parece
		// parte del título y se lee dos veces antes de entenderlo.
		_options.AddChild(new Control { CustomMinimumSize = new Vector2(0.0f, 26.0f) });

		_first = AddOption("Continuar", Resume);
		AddOption("Reiniciar", Restart);
		AddOption("Salir", Quit);
	}

	private Button AddOption(string text, System.Action action)
	{
		Button button = new()
		{
			Text = text,
			Flat = true,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
		};

		button.AddThemeFontSizeOverride("font_size", OptionSize);
		button.AddThemeColorOverride("font_color", TextColor);
		button.AddThemeColorOverride("font_hover_color", HighlightColor);
		button.AddThemeColorOverride("font_focus_color", HighlightColor);
		button.AddThemeColorOverride("font_pressed_color", HighlightColor);
		button.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f));
		button.AddThemeConstantOverride("outline_size", 6);
		button.Pressed += action;

		_options.AddChild(button);

		return button;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel"))
		{
			return;
		}

		// Con la incursión acabándose no se pausa: la pantalla de fin ya está
		// contando y tiene su propio reinicio. Vale para morir y para extraer, que
		// además para el árbol entero.
		if (_health.IsDead || RaidEndScreen.IsRaidEnding(GetTree()))
		{
			return;
		}

		GetViewport().SetInputAsHandled();

		if (IsOpen)
		{
			Resume();
		}
		else
		{
			Open();
		}
	}

	private void Open()
	{
		Visible = true;
		GetTree().Paused = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;

		// El foco puesto para que también valga el mando y las flechas. Sin esto,
		// con un mando en la mano la pausa es un callejón sin salida.
		_first.GrabFocus();
	}

	private void Resume()
	{
		Visible = false;
		GetTree().Paused = false;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	private void Restart()
	{
		// Despausar ANTES de recargar. Una escena que nace con el árbol parado se
		// queda congelada y parece que el juego se ha colgado.
		GetTree().Paused = false;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		GetTree().ReloadCurrentScene();
	}

	private void Quit()
	{
		GetTree().Paused = false;
		GetTree().Quit();
	}
}
