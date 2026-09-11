using Godot;

namespace MedievalNightmare.Fx;

/// <summary>
/// Un número que salta del golpe, sube y se apaga. Solo lo usa el muñeco de la
/// arena de pruebas: el juego no enseña números (DISENO.md §5) y esto es un
/// aparato de medir, no HUD. Por eso también ignora la profundidad: un
/// instrumento que se tapa con el propio muñeco no mide nada.
/// </summary>
public partial class DamageNumber : Label3D
{
	private float _age;
	private Vector3 _drift;

	public static void Spawn(Node context, Vector3 position, float amount)
	{
		if (context?.GetTree()?.CurrentScene is not { } scene)
		{
			return;
		}

		DamageNumber number = new()
		{
			Text = Mathf.RoundToInt(amount).ToString(),
			FontSize = 72,
			OutlineSize = 18,
			PixelSize = 0.004f,
			Modulate = new Color(0.97f, 0.93f, 0.8f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = true,
		};

		// La deriva lateral es aleatoria para que dos golpes seguidos no escriban
		// una cifra encima de la otra.
		number._drift = new Vector3((GD.Randf() - 0.5f) * 0.9f, 1.5f, (GD.Randf() - 0.5f) * 0.4f);

		scene.AddChild(number);
		number.GlobalPosition = position;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_age += dt;

		GlobalPosition += _drift * dt;
		_drift.Y = Mathf.Max(0.35f, _drift.Y - 2.4f * dt);

		Color color = Modulate;
		color.A = Mathf.Clamp(1.6f - _age * 2.0f, 0.0f, 1.0f);
		Modulate = color;

		if (_age > 0.8f)
		{
			QueueFree();
		}
	}
}
