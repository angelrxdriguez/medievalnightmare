shader_type sky;

#include "res://assets/shaders/retro.gdshaderinc"

// El cielo de noche. Es lo primero que se ve al salir de la mazmorra y hace
// tres trabajos que no puede hacer una malla:
//
// - PONER LA LUNA DONDE ESTÁ LA LUZ. El disco no se coloca a mano: se saca de
//   `LIGHT0_DIRECTION`, o sea, de la DirectionalLight3D del nivel. Así girar la
//   luz mueve la luna, y las sombras largas del suelo siempre apuntan al revés
//   que ella. Puesta a mano, se descuadra la primera vez que alguien toca la luz
//   y nadie sabe por qué las sombras van en otra dirección.
// - DAR SILUETA. Un cementerio de noche se lee por los NEGROS recortados contra
//   algo: los árboles secos, las cruces y el tejado de la cripta solo existen si
//   hay un cielo más claro detrás. Por eso el horizonte no es negro: es azul
//   sucio, y sube a casi negro en el cenit.
// - CONTAR CUÁNTO SE VE. Las nubes tapan estrellas y bajan el horizonte. Es la
//   forma barata de que dos capturas del mismo sitio no se parezcan.
//
// LA ÉPOCA. Un cielo de 2001 no tenía degradado continuo: tenía BANDAS, porque
// se pintaba en una textura de 256 colores. `retro_bands` con el tramado de
// Bayer las devuelve, y es lo que separa esto de un degradado de ahora. Las
// estrellas van sobre una rejilla cuantizada por lo mismo: son píxeles, no
// puntos, y no se hacen más finas al acercar la vista.

group_uniforms Cielo;
uniform vec3 zenith_color : source_color = vec3(0.006, 0.009, 0.020);
uniform vec3 horizon_color : source_color = vec3(0.055, 0.072, 0.115);

/// Lo que hay por debajo de la línea del horizonte. Casi negro: ahí abajo solo
/// se ve niebla, y si esto aclara se ve el borde del suelo.
uniform vec3 ground_color : source_color = vec3(0.004, 0.005, 0.009);

uniform float horizon_falloff : hint_range(0.05, 2.0) = 0.42;

group_uniforms Luna;
uniform vec3 moon_color : source_color = vec3(0.82, 0.87, 1.0);

/// Radio angular del disco, en coseno. Pequeño: una luna grande es de dibujo
/// animado, y además se lleva toda la atención del encuadre.
uniform float moon_size : hint_range(0.0005, 0.06) = 0.011;

uniform vec3 halo_color : source_color = vec3(0.30, 0.38, 0.62);
uniform float halo_amount : hint_range(0.0, 3.0) = 1.1;
uniform float halo_falloff : hint_range(1.0, 200.0) = 46.0;

/// Manchas de la luna. Sin ellas es un círculo blanco y parece un agujero.
uniform float moon_maria : hint_range(0.0, 1.0) = 0.45;

group_uniforms Estrellas;
uniform vec3 star_color : source_color = vec3(0.66, 0.72, 0.92);

/// Celdas por radián. Sube el número de estrellas y baja su tamaño a la vez.
uniform float star_scale : hint_range(20.0, 600.0) = 210.0;

/// Cuántas celdas tienen estrella. 0,988 deja unas pocas: un cielo lleno de
/// puntos se lee como ruido, no como estrellas.
uniform float star_threshold : hint_range(0.9, 0.9999) = 0.988;

uniform float star_twinkle : hint_range(0.0, 1.0) = 0.35;

group_uniforms Nubes;
uniform vec3 cloud_color : source_color = vec3(0.085, 0.10, 0.135);
uniform float cloud_amount : hint_range(0.0, 1.0) = 0.55;
uniform float cloud_scale : hint_range(0.2, 8.0) = 1.6;
uniform float cloud_speed : hint_range(0.0, 0.2) = 0.014;

group_uniforms Retro;
uniform float color_levels : hint_range(2.0, 64.0) = 16.0;
uniform float dither_amount : hint_range(0.0, 2.0) = 1.0;

void sky()
{
	vec3 dir = EYEDIR;

	// Coordenadas del cielo: rumbo y altura. Es la proyección con la que se
	// pintaba un cielo entonces, y la que hace que las estrellas no se apelotonen
	// en el cenit.
	vec2 sky_uv = vec2(atan(dir.x, dir.z), asin(clamp(dir.y, -1.0, 1.0)));

	float up = smoothstep(0.0, horizon_falloff, dir.y);
	vec3 col = mix(horizon_color, zenith_color, up);

	// Nubes. Dos capas a distinta velocidad: con una sola se ve que es una
	// textura que se desliza.
	float drift = TIME * cloud_speed;
	float clouds = fbm(sky_uv * cloud_scale * vec2(2.2, 3.4) + vec2(drift, 0.0)) * 0.65
		+ fbm(sky_uv * cloud_scale * vec2(4.7, 6.1) + vec2(-drift * 1.7, drift * 0.4)) * 0.35;

	clouds = smoothstep(0.42, 0.78, clouds) * cloud_amount;

	// Las estrellas van DEBAJO de las nubes: lo que tapa una nube no se ve.
	vec2 star_grid = sky_uv * star_scale;
	vec2 cell = floor(star_grid);
	float pick = hash12(cell);

	if (pick > star_threshold)
	{
		// La estrella es un PUNTO dentro de su celda, no la celda entera. Pintando
		// la celda salen cuadrados del tamaño de la rejilla y el cielo se lee como
		// nieve; con el punto, el tamaño lo decide `star_size` y se puede bajar
		// hasta el píxel, que es lo que era una estrella en 2001.
		vec2 spot = fract(star_grid) - vec2(hash12(cell + 11.7), hash12(cell + 23.9));
		float bright = hash12(cell + 37.1);

		// Sin suavizar el borde: un punto con degradado es un shader de ahora.
		float dot_mask = step(length(spot), 0.16 + bright * 0.2);

		float beat = 0.5 + 0.5 * sin(TIME * (0.7 + bright * 1.9) + bright * 40.0);
		float star = dot_mask * (0.35 + bright * 0.65) * mix(1.0, beat, star_twinkle);

		col += star_color * star * (1.0 - clouds) * smoothstep(-0.02, 0.12, dir.y);
	}

	col = mix(col, cloud_color, clouds * smoothstep(-0.05, 0.30, dir.y));

	// La luna, donde apunte la luz direccional del nivel.
	if (LIGHT0_ENABLED)
	{
		vec3 moon_dir = -LIGHT0_DIRECTION;
		float d = dot(dir, moon_dir);

		// Halo primero: es lo que la despega del cielo y lo que hace que la niebla
		// de abajo tenga a dónde apuntar.
		col += halo_color * pow(max(d, 0.0), halo_falloff) * halo_amount * (1.0 - clouds * 0.8);

		float disc = smoothstep(1.0 - moon_size, 1.0 - moon_size * 0.55, d);

		if (disc > 0.0)
		{
			// Los mares. El ruido se evalúa en coordenadas de la propia luna para
			// que no se le deslicen por encima al girar la cabeza.
			vec3 tangent = normalize(cross(moon_dir, vec3(0.0, 1.0, 0.0)) + vec3(0.0001));
			vec3 bitangent = cross(moon_dir, tangent);
			vec2 face = vec2(dot(dir, tangent), dot(dir, bitangent)) / max(moon_size, 0.0001);

			float maria = fbm(face * 0.9 + 5.0);
			vec3 surface = moon_color * mix(1.0, 0.55 + maria * 0.5, moon_maria);

			col = mix(col, surface, disc * (1.0 - clouds * 0.9));
		}
	}

	// Por debajo del horizonte no hay cielo, hay niebla. El corte es blando: con
	// un borde duro se ve la costura entre el cielo y el suelo a diez metros.
	col = mix(ground_color, col, smoothstep(-0.10, 0.015, dir.y));

	COLOR = retro_bands(col, color_levels, retro_dither(FRAGCOORD.xy, dither_amount));
}
