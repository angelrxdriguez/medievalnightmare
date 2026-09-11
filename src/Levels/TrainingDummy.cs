using Godot;
using MedievalNightmare.Core;
using MedievalNightmare.Fx;

namespace MedievalNightmare.Levels;

/// <summary>
/// El muñeco de entrenamiento de la arena. Aguanta lo que le echen —la vida se
/// rellena tras cada golpe—, se estremece al recibir y canta los números: cada
/// impacto suelta una cifra flotante y un contador acumula la racha hasta que
/// dejas de pegar dos segundos. Con él se compara un arma con otra sin tener
/// que perseguir esqueletos por la sala.
///
/// Se construye entero por código: es equipamiento de pruebas, no una pieza del
/// juego, y no merece una escena que mantener.
/// </summary>
public partial class TrainingDummy : StaticBody3D
{
	/// <summary>Sin golpes durante este tiempo, la racha se da por cerrada.</summary>
	[Export] public float ComboResetSeconds { get; set; } = 2.0f;

	private Health _health;
	private Node3D _visual;
	private Label3D _combo;
	private float _comboTotal;
	private float _comboIdle;
	private float _tilt;

	public override void _Ready()
	{
		// La capa de enemigo (3): es la que busca el barrido del jugador. Sin
		// máscara: el muñeco no golpea a nadie.
		CollisionLayer = 4;
		CollisionMask = 0;

		_health = new Health { Name = "Health", Max = 1_000_000.0f };
		AddChild(_health);
		_health.Damaged += OnDamaged;
		_health.Staggered += OnStaggered;

		AddChild(new CollisionShape3D
		{
			Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.8f },
			Position = new Vector3(0.0f, 0.9f, 0.0f),
		});

		BuildBody();
		BuildComboLabel();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		// El respingo: se clava de golpe al recibir y se recoge amortiguado.
		_tilt = Mathf.Lerp(_tilt, 0.0f, 1.0f - Mathf.Exp(-7.0f * dt));
		_visual.Rotation = new Vector3(Mathf.DegToRad(-7.0f) * _tilt, 0.0f, 0.0f);

		if (_comboTotal <= 0.0f)
		{
			return;
		}

		_comboIdle += dt;
		if (_comboIdle >= ComboResetSeconds)
		{
			_comboTotal = 0.0f;
			_combo.Visible = false;
		}
	}

	private void OnDamaged(float amount, float remaining)
	{
		DamageNumber.Spawn(this, GlobalPosition + new Vector3(0.0f, 1.9f, 0.0f), amount);

		_comboTotal += amount;
		_comboIdle = 0.0f;
		_combo.Text = $"{_comboTotal:0}";
		_combo.Visible = true;

		_tilt = Mathf.Min(_tilt + 1.0f, 1.6f);

		// Inmortal por relleno y no por vida infinita a secas: así los números de
		// cada golpe salen del daño real que aplica el arma, no de un resto.
		_health.Restore();
	}

	/// <summary>El pesado también se ve aquí: el muñeco se dobla el doble.</summary>
	private void OnStaggered()
	{
		_tilt = 2.6f;
	}

	/// <summary>Un poste con travesaño, saco y cabeza. Madera del juego, greybox el resto.</summary>
	private void BuildBody()
	{
		_visual = new Node3D { Position = new Vector3(0.0f, 0.0f, 0.0f) };
		AddChild(_visual);

		Material oak = GD.Load<Material>("res://assets/materials/weapons/Oak.tres");

		var sack = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.46f, 0.3f), Roughness = 1.0f };

		Add(new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.09f, Height = 1.7f, Material = oak },
			new Vector3(0.0f, 0.85f, 0.0f));
		Add(new BoxMesh { Size = new Vector3(1.1f, 0.11f, 0.11f), Material = oak },
			new Vector3(0.0f, 1.3f, 0.0f));
		Add(new CapsuleMesh { Radius = 0.26f, Height = 1.0f, Material = sack },
			new Vector3(0.0f, 1.05f, 0.0f));
		Add(new SphereMesh { Radius = 0.16f, Height = 0.32f, Material = sack },
			new Vector3(0.0f, 1.72f, 0.0f));
	}

	private void Add(Mesh mesh, Vector3 position)
	{
		_visual.AddChild(new MeshInstance3D { Mesh = mesh, Position = position });
	}

	private void BuildComboLabel()
	{
		_combo = new Label3D
		{
			FontSize = 96,
			OutlineSize = 22,
			PixelSize = 0.004f,
			Modulate = new Color(1.0f, 0.78f, 0.35f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = true,
			Position = new Vector3(0.0f, 2.35f, 0.0f),
			Visible = false,
		};

		AddChild(_combo);
	}
}
