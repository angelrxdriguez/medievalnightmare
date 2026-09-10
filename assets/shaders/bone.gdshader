shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

#include "res://assets/shaders/retro.gdshaderinc"

// Hueso viejo. Mismo truco que la sillería —triplanar procedural, texeles
// cuadrados, paleta a escalones— pero en espacio de MODELO y no de mundo: si
// las coordenadas fueran de mundo, las manchas se quedarían quietas y el
// esqueleto andaría por debajo de su propia textura.
//
// Lo que hace que un hueso parezca hueso y no plástico beige son dos cosas y
// ninguna es el color base:
//
// - LAS VETAS. Ruido estirado a lo largo del eje del hueso, no isótropo. Los
//   huesos largos tienen fibra y se nota aunque no sepas por qué.
// - LA MUGRE EN LOS HUECOS. Tierra oscura acumulada donde la forma se estrecha.
//   Se aproxima con ruido de baja frecuencia y basta.
//
// El `rim` es lo que hace que un esqueleto se lea en una sala a oscuras. Con la
// niebla volumétrica detrás, el borde del cráneo recoge un hilo de luz y ya
// sabes que hay algo ahí sin verlo del todo. Es la lectura que se quiere: te
// enteras de que no estás solo antes de poder contar cuántos son.
//
// TRES COSAS LO MUDARON DE "PROCEDURAL POSTERIZADO" A PLAYSTATION, y ninguna
// tiene que ver con el ruido, que ya estaba bien:
//
// - EL TRAMADO. El escalón duro entre dos niveles de la paleta se lee como una
//   mancha y hace que el bicho parezca hecho de cubos pintados. Deshecho en un
//   Bayer de 4x4 se lee como superficie. Es exactamente lo que hacía la consola
//   con sus 15 bits de color, y es lo que se ve al mirar una captura de la época.
// - EL FACETADO. La normal de la cara en vez de la interpolada. Un modelo de
//   pocos polígonos con normales suaves parece una esfera mal teselada; con la
//   normal de la cara se le CUENTAN los triángulos, que es de lo que presumía
//   la época.
// - EL TEMBLOR. Los vértices caen a una rejilla de pantalla y el modelo hierve
//   al moverse. Ver `retro_snap` en el include para por qué se hace ahí y no
//   escribiendo POSITION.

group_uniforms Hueso;
uniform vec3 bone_pale : source_color = vec3(0.62, 0.60, 0.52);
uniform vec3 bone_deep : source_color = vec3(0.30, 0.27, 0.21);
uniform vec3 stain_color : source_color = vec3(0.16, 0.12, 0.07);
uniform float stain_amount : hint_range(0.0, 1.0) = 0.55;

group_uniforms Vetas;

// Eje largo del hueso en espacio de modelo. Las vetas se estiran en esta
// dirección: en un fémur va a lo largo, en el cráneo da igual.
uniform vec3 grain_axis = vec3(0.0, 1.0, 0.0);
uniform float grain_stretch : hint_range(1.0, 24.0) = 7.0;
uniform float grain_amount : hint_range(0.0, 1.0) = 0.4;

group_uniforms Retro;

// Más denso de lo que estaba. Un hueso tiene seis centímetros de ancho: a 200
// texeles por metro le caben doce píxeles y cada uno se ve como un ladrillo.
// La textura de la época era de baja resolución en la PARED, no en un objeto de
// seis centímetros; lo que la delataba de cerca era el tramado, no el tamaño de
// los texeles.
uniform float texels_per_meter : hint_range(8.0, 512.0) = 320.0;
uniform float color_levels : hint_range(2.0, 64.0) = 11.0;
uniform float dither_amount : hint_range(0.0, 2.0) = 1.0;

/// Cuánto manda la normal de la cara sobre la interpolada. A 1 el bicho es un
/// poliedro y se le cuentan las caras.
uniform float facet_amount : hint_range(0.0, 1.0) = 0.7;

/// Rejilla del temblor de vértices, en radianes de ángulo visual. A 0 se apaga.
uniform float vertex_grid : hint_range(0.0, 0.03) = 0.006;

group_uniforms Lectura;

// Luz de borde. Es lo que dibuja la silueta contra la niebla.
uniform float rim_power : hint_range(0.5, 8.0) = 2.6;
uniform float rim_strength : hint_range(0.0, 2.0) = 0.8;

// Emisión mínima en el borde para que el bicho exista en la oscuridad total.
// Muy baja a propósito: la idea es intuirlo, no verlo.
uniform float ghost_rim : hint_range(0.0, 0.3) = 0.025;
uniform vec3 ghost_tint : source_color = vec3(0.36, 0.40, 0.44);

uniform float roughness_base : hint_range(0.0, 1.0) = 0.78;

varying vec3 local_pos;
varying vec3 local_nrm;

// Un hueso: base pálida, veta, mugre y grietas finas.
vec3 bone_at(vec2 p, vec2 grain)
{
	float base = fbm(p * 8.0) * 0.6 + fbm(p * 30.0) * 0.4;
	vec3 col = mix(bone_deep, bone_pale, 0.35 + base * 0.75);

	// Veta estirada. El estiramiento va en las coordenadas, no en el ruido.
	float fiber = fbm(grain);
	col = mix(col, col * (0.72 + fiber * 0.5), grain_amount);

	// Mugre acumulada, en manchas grandes.
	float dirt = smoothstep(0.40, 0.80, fbm(p * 3.3));
	col = mix(col, stain_color, dirt * stain_amount);

	// Grietas: cresta de ruido invertida. Finas y oscuras.
	float crack = 1.0 - abs(fbm(p * 14.0) * 2.0 - 1.0);
	col *= mix(1.0, 0.35, smoothstep(0.86, 1.0, crack));

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

	vec3 axis = normalize(grain_axis + vec3(0.0001));
	vec3 stretched = local_pos - axis * dot(local_pos, axis) * (1.0 - 1.0 / grain_stretch);

	vec3 albedo = bone_at(retro_texel(local_pos.zy, texels_per_meter), stretched.zy * 6.0) * blend.x
		+ bone_at(retro_texel(local_pos.xz, texels_per_meter), stretched.xz * 6.0) * blend.y
		+ bone_at(retro_texel(local_pos.xy, texels_per_meter), stretched.xy * 6.0) * blend.z;

	ALBEDO = retro_bands(albedo, color_levels, retro_dither(FRAGCOORD.xy, dither_amount));
	ROUGHNESS = roughness_base;
	SPECULAR = 0.25;
	METALLIC = 0.0;

	RIM = rim_strength;
	RIM_TINT = 0.35;

	vec3 faceted = retro_facet(NORMAL, dFdx(VERTEX), dFdy(VERTEX), facet_amount);

	// El fresnel se calcula con la normal FACETADA: si se usara la suave, el hilo
	// de luz del borde recorrería una silueta redonda sobre un modelo anguloso y
	// el truco se vería. Con la de la cara, cada faceta se enciende entera y es
	// justo lo que hace de la silueta una lectura de pocos polígonos.
	float fresnel = pow(1.0 - clamp(dot(faceted, normalize(VIEW)), 0.0, 1.0), rim_power);
	EMISSION = ghost_tint * fresnel * ghost_rim;

	NORMAL = faceted;
}
