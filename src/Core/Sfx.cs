using Godot;

namespace MedievalNightmare.Core;

/// <summary>
/// Dispara sonidos de una vez sin que cada escena tenga que llevar sus
/// reproductores. Crea un reproductor transitorio, lo suelta en la escena y el
/// propio reproductor se destruye al terminar.
///
/// Todo sale con un poco de tono aleatorio. No es adorno: el mismo WAV dos
/// veces seguidas al mismo tono se lee como una metralleta de sampler, y el
/// combate repite los mismos cuatro sonidos cientos de veces por incursión.
///
/// Los streams se cargan una vez por ruta y se quedan cacheados en
/// <see cref="GD.Load{T}(string)"/>, así que llamar con la ruta cada vez no
/// toca disco más que la primera.
/// </summary>
public static class Sfx
{
	/// <summary>
	/// Sonido plano, sin posición: lo que te pasa A TI (tus golpes, tu guardia,
	/// tu esquiva). Lo tuyo no viene de ningún sitio, te suena en la mano.
	/// </summary>
	public static void Play(Node context, string path, float volumeDb = 0.0f, float pitchScale = 1.0f)
	{
		AudioStream stream = GD.Load<AudioStream>(path);
		if (stream == null || context?.GetTree()?.CurrentScene is not { } scene)
		{
			return;
		}

		AudioStreamPlayer player = new()
		{
			Stream = stream,
			VolumeDb = volumeDb,
			PitchScale = pitchScale * Jitter(),
		};

		player.Finished += player.QueueFree;
		scene.AddChild(player);
		player.Play();
	}

	/// <summary>
	/// Sonido con posición: lo que hacen LOS DEMÁS. Que el windup del esqueleto
	/// suene por detrás es media telegrafía en un juego donde no ves tu espalda.
	/// </summary>
	public static void PlayAt(Node context, string path, Vector3 position, float volumeDb = 0.0f, float pitchScale = 1.0f)
	{
		AudioStream stream = GD.Load<AudioStream>(path);
		if (stream == null || context?.GetTree()?.CurrentScene is not { } scene)
		{
			return;
		}

		AudioStreamPlayer3D player = new()
		{
			Stream = stream,
			VolumeDb = volumeDb,
			PitchScale = pitchScale * Jitter(),
			UnitSize = 6.0f,
			MaxDistance = 30.0f,
		};

		player.Finished += player.QueueFree;
		scene.AddChild(player);
		player.GlobalPosition = position;
		player.Play();
	}

	private static float Jitter()
	{
		return (float)GD.RandRange(0.94, 1.06);
	}
}
