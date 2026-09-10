// Caja de herramientas del aspecto de 2001. Todo lo que estaba copiado en tres
// shaders y todo lo que hace falta para que esto parezca una PlayStation y no
// un render moderno al que le han bajado la resolución.
//
// Vive aparte por una razón concreta: el temblor de vértices y el dither TIENEN
// que ser idénticos en todos los materiales de una misma criatura. Si el hueso
// tiembla con una fórmula y el machete que lleva en la mano con otra, el arma
// se despega de la mano un píxel por fotograma y se ve enseguida.

#define RETRO_INCLUDED

float hash12(vec2 p)
{
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float vnoise(vec2 p)
{
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);

	float a = hash12(i);
	float b = hash12(i + vec2(1.0, 0.0));
	float c = hash12(i + vec2(0.0, 1.0));
	float d = hash12(i + vec2(1.0, 1.0));

	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

float fbm(vec2 p)
{
	float v = 0.0;
	float a = 0.5;

	for (int i = 0; i < 3; i++)
	{
		v += a * vnoise(p);
		p *= 2.11;
		a *= 0.5;
	}

	return v;
}

// Redondea las coordenadas a una rejilla. Es lo que convierte el ruido continuo
// en píxeles cuadrados del tamaño que tendría una textura pintada a mano.
vec2 retro_texel(vec2 p, float texels_per_meter)
{
	float t = max(texels_per_meter, 1.0);
	return (floor(p * t) + 0.5) / t;
}

// Bayer 2x2 en forma cerrada: devuelve {0, 0.5, 0.75, 0.25} recorriendo la
// celda. Sin tablas, que en algunos controladores se compilan a saltos.
float bayer2(vec2 a)
{
	a = floor(a);
	return fract(a.x * 0.5 + a.y * a.y * 0.75);
}

/// Matriz de Bayer 4x4 normalizada a [0, 1). Se construye recursivamente a
/// partir de la de 2x2, que es como se define de verdad.
float bayer4(vec2 a)
{
	return bayer2(a * 0.5) * 0.25 + bayer2(a);
}

// Recorta la gama a un número de escalones.
//
// Se cuantiza el BRILLO y se conserva el tono. Posterizar canal a canal hace
// que cada uno salte en un sitio distinto y aparecen píxeles rojos y verdes
// donde solo debería haber piedra.
//
// El `dither` es lo que separa esto de un filtro de posterizado moderno: en vez
// de que todo un tramo caiga al mismo escalón, el umbral se mueve con la
// posición en pantalla y el borde entre dos escalones se deshace en un tramado
// regular. Es literalmente lo que hacía la consola con sus 15 bits de color, y
// es lo que hay que mirar cuando algo "se ve muy cuadrado": el escalón duro se
// lee como una mancha, el tramado se lee como una textura.
vec3 retro_bands(vec3 albedo, float levels, float dither)
{
	float n = max(levels, 2.0);
	float lum = max(dot(albedo, vec3(0.2126, 0.7152, 0.0722)), 0.0001);
	float graded = floor(lum * n + dither) / n;

	return albedo * (graded / lum);
}

/// Umbral de tramado centrado en cero para un píxel de pantalla.
float retro_dither(vec2 frag_coord, float amount)
{
	return 0.5 + (bayer4(frag_coord) - 0.5) * amount;
}

// El temblor de vértices, que es LA marca de la casa de la época. La consola no
// tenía coma flotante en el transformador de geometría: los vértices caían a
// posiciones enteras de pantalla y por eso los modelos hierven al moverse.
//
// Se hace en espacio de VISTA y no de recorte a propósito. Escribir POSITION
// desactiva el camino de paraboloide dual de Godot y las seis antorchas de la
// sala dejarían de proyectar sombra bien; tocando VERTEX el motor sigue
// haciendo su proyección y solo le llega el vértice ya pegado a la rejilla.
//
// La celda crece con la distancia para que su tamaño en pantalla sea constante,
// que es lo que pasaba de verdad: el temblor no se hace más fino de lejos.
vec3 retro_snap(mat4 modelview, vec3 vertex, float grid)
{
	if (grid <= 0.0)
	{
		return vertex;
	}

	vec4 view_pos = modelview * vec4(vertex, 1.0);
	float cell = grid * max(-view_pos.z, 0.15);
	view_pos.xy = floor(view_pos.xy / cell + 0.5) * cell;

	return (inverse(modelview) * view_pos).xyz;
}

// Sombreado plano: la normal de la CARA en vez de la interpolada. Es lo que
// hace que se cuenten los polígonos de un modelo de pocas caras en vez de que
// parezca una superficie curva mal teselada.
//
// Las derivadas llegan de fuera porque solo existen en el fragmento; y la cara
// se orienta con la normal interpolada porque el signo de dFdy no es el mismo
// en todos los backends y con el producto vectorial a pelo la mitad de las
// caras salen del revés.
vec3 retro_facet(vec3 smooth_normal, vec3 dx, vec3 dy, float amount)
{
	vec3 face = cross(dx, dy);

	if (dot(face, face) < 1e-12)
	{
		return smooth_normal;
	}

	face = normalize(face) * sign(dot(face, smooth_normal));

	return normalize(mix(smooth_normal, face, clamp(amount, 0.0, 1.0)));
}
