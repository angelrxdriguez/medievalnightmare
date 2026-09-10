shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

#include "res://assets/shaders/retro.gdshaderinc"

// La mano del jugador. Es el octavo shader y el único que no es materia muerta:
// todo lo demás del juego es piedra, hueso, hierro o trapo.
//
// LA TRAMPA DE LA PIEL. Una piel bien hecha se parece a la de ahora, y eso es
// exactamente lo que no se quiere. Lo que la delata como del 2000 no es que
// esté peor pintada, son tres cosas:
//
// - NO ES ROSA. La carne de la época era ocre y estaba desaturada, porque venía
//   de una paleta indexada de 256 colores en la que el rosa se comía media
//   rampa. Aquí sale de mezclar dos tonos que ya están sucios, y el `color_levels`
//   la deja en nueve escalones. En cuanto se satura, parece un guante de goma.
// - LA MANCHA ES GRANDE. El detalle fino —poro, huella, pelo— desaparece a un
//   palmo de la cámara: lo que se ve es la mancha de dos centímetros, y por eso
//   el ruido grande manda sobre el pequeño y no al revés.
// - NO TIENE SUBSURFACE. La piel de verdad deja pasar la luz por los bordes
//   finos. Poniéndolo, la mano se vuelve de cera moderna. En su lugar hay un
//   reborde caliente y a escalones (`rim_*`) que hace el mismo trabajo de
//   despegarla del fondo negro sin traicionar la época, y que es el mismo truco
//   que usa el hueso.
//
// EL TEMBLOR TIENE QUE SER EL MISMO QUE EL DEL ARMA. `vertex_grid` sale a 0,0035
// como el acero, la madera y el cuero. Si la mano temblara con otra rejilla, se
// despegaría del mango un píxel por fotograma y se vería enseguida: es la razón
// por la que la caja de herramientas retro existe.

group_uniforms Carne;
uniform vec3 skin_pale : source_color = vec3(0.56, 0.41, 0.32);
uniform vec3 skin_deep : source_color = vec3(0.21, 0.14, 0.11);

/// Cuánto manda la mancha grande. Es lo que separa una mano de un tubo de carne.
uniform float mottle_amount : hint_range(0.0, 1.0) = 0.6;

group_uniforms Venas;
uniform vec3 vein_color : source_color = vec3(0.17, 0.15, 0.19);
uniform float vein_amount : hint_range(0.0, 1.0) = 0.3;

/// Eje LARGO de la pieza. Las venas corren hacia la muñeca, no en cualquier
/// dirección, y es lo único que dice hacia dónde va la mano.
uniform vec3 vein_axis = vec3(0.0, 1.0, 0.0);
uniform float vein_stretch : hint_range(1.0, 32.0) = 8.0;

group_uniforms Mugre;

/// Lo que lleva encima de la mazmorra. Sin esto la mano está recién lavada y no
/// pega con un sitio en el que llevas media hora matando cosas.
uniform vec3 grime_color : source_color = vec3(0.10, 0.07, 0.05);
uniform float grime_amount : hint_range(0.0, 1.0) = 0.5;

group_uniforms Reborde;
uniform vec3 rim_color : source_color = vec3(0.55, 0.30, 0.14);
uniform float rim_amount : hint_range(0.0, 2.0) = 0.35;

/// Escalones del reborde. Un degradado continuo se lee como un shader de ahora;
/// a tres escalones se lee como una rampa de paleta.
uniform float rim_levels : hint_range(1.0, 8.0) = 3.0;

group_uniforms Retro;

/// Un dedo mide dos centímetros: a 380 le caben ocho píxeles de ancho, que es
/// justo lo que tenía una mano de 1999 en la textura de 128.
uniform float texels_per_meter : hint_range(8.0, 512.0) = 380.0;

uniform float color_levels : hint_range(2.0, 64.0) = 9.0;
uniform float dither_amount : hint_range(0.0, 2.0) = 1.0;
uniform float facet_amount : hint_range(0.0, 1.0) = 0.5;

/// La MISMA rejilla que el acero y el cuero. No se toca por separado.
uniform float vertex_grid : hint_range(0.0, 0.03) = 0.0035;

uniform float roughness_base : hint_range(0.0, 1.0) = 0.74;

/// Semilla por pieza. Cinco dedos con el mismo material y sin semilla son cinco
/// veces el mismo dedo, y se ve. La reparte la escena, pieza a pieza.
instance uniform float piece_seed = 0.0;

varying vec3 local_pos;
varying vec3 local_nrm;

vec3 skin_at(vec2 p, vec2 stretched)
{
	// La mancha grande manda. La pequeña solo la ensucia.
	float blotch = fbm(p * 9.0);
	float grain = vnoise(p * 34.0);

	vec3 col = mix(skin_deep, skin_pale, 0.30 + blotch * mottle_amount + grain * 0.12);

	// Las venas: ruido aplastado hacia la muñeca y doblado sobre sí mismo, que es
	// lo que convierte una mancha alargada en una línea.
	float vein = abs(fract(fbm(stretched) * 3.2) * 2.0 - 1.0);
	col = mix(col, vein_color, smoothstep(0.82, 0.99, vein) * vein_amount);

	// Mugre: donde el ruido grande hace valle. Se agarra en el mismo sitio en el
	// que se agarraría de verdad, que es donde la piel se pliega.
	col = mix(col, grime_color, smoothstep(0.55, 0.15, blotch) * grime_amount);

	return col;
}

void vertex()
{
	VERTEX = retro_snap(MODELVIEW_MATRIX, VERTEX, vertex_grid);

	local_pos = VERTEX;
	local_nrm = normalize(NORMAL);
}

void fragment()
{
	vec3 blend = pow(abs(local_nrm), vec3(5.0));
	blend /= max(blend.x + blend.y + blend.z, 0.0001);

	// La semilla desplaza las coordenadas, no el color: así el dedo de al lado
	// tiene otras manchas en vez de ser el mismo dedo más oscuro.
	vec3 p = local_pos + vec3(piece_seed * 31.7, piece_seed * 17.3, piece_seed * 43.1);

	vec3 axis = normalize(vein_axis + vec3(0.0001));
	vec3 stretched = p - axis * dot(p, axis) * (1.0 - 1.0 / vein_stretch);

	vec3 albedo = skin_at(retro_texel(p.zy, texels_per_meter), stretched.zy * 7.0) * blend.x
		+ skin_at(retro_texel(p.xz, texels_per_meter), stretched.xz * 7.0) * blend.y
		+ skin_at(retro_texel(p.xy, texels_per_meter), stretched.xy * 7.0) * blend.z;

	vec3 normal = retro_facet(NORMAL, dFdx(VERTEX), dFdy(VERTEX), facet_amount);

	// El reborde va con la normal FACETADA. Con la suave, el hilo de luz recorre
	// una silueta redonda sobre una mano hecha de cajas y se ve el truco.
	float rim = 1.0 - clamp(dot(normal, normalize(VIEW)), 0.0, 1.0);
	rim = floor(pow(rim, 2.5) * rim_levels) / rim_levels;
	albedo += rim_color * rim * rim_amount;

	ALBEDO = retro_bands(albedo, color_levels, retro_dither(FRAGCOORD.xy, dither_amount));
	NORMAL = normal;

	METALLIC = 0.0;

	// La piel no es mate del todo: el poco brillo que tiene es lo que devuelve la
	// antorcha en los nudillos, y es lo que dice que está viva.
	ROUGHNESS = roughness_base;
	SPECULAR = 0.28;
}
