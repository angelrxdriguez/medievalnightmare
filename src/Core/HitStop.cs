using Godot;

namespace MedievalNightmare.Core;

/// <summary>
/// El parón de impacto: congela el juego entero unas centésimas cuando un golpe
/// conecta. Es el truco más viejo del género y el que más peso mete por línea
/// de código: el filo se clava en algo y el mundo entero lo acusa.
///
/// Va sobre <c>Engine.TimeScale</c> y no sobre pausas por nodo porque tiene que
/// parar TODO —el enemigo, las partículas, tu propia arma— o no se lee como
/// impacto, se lee como un tirón de fotogramas.
/// </summary>
public static class HitStop
{
	private static int _version;

	/// <param name="seconds">Duración REAL del parón: el temporizador ignora la escala.</param>
	/// <param name="scale">Lo que queda corriendo mientras tanto. No baja a cero
	/// porque a cero la física puede perderse el cierre de la ventana activa;
	/// a 0,05 se ve igual de parado y nada se salta ningún paso.</param>
	public static void Apply(Node context, float seconds, float scale = 0.05f)
	{
		SceneTree tree = context?.GetTree();
		if (tree == null)
		{
			return;
		}

		// Cada parón pisa al anterior y solo el último restaura. Sin la versión,
		// el primero de dos golpes casi seguidos devolvería el tiempo a la mitad
		// del parón del segundo.
		_version++;
		int version = _version;

		Engine.TimeScale = scale;

		SceneTreeTimer timer = tree.CreateTimer(seconds, processAlways: true, processInPhysics: false, ignoreTimeScale: true);
		timer.Timeout += () =>
		{
			if (_version == version)
			{
				Engine.TimeScale = 1.0;
			}
		};
	}
}
