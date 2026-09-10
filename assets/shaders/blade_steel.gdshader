shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

#include "res://assets/shaders/retro.gdshaderinc"

// Acero del jugador. Es el hermano limpio de `rusted_iron`: lo que llevan los
// esqueletos lleva cincuenta años en el suelo de una mazmorra y lo que llevas tú
// lo afilaste esta mañana.
//
// Un arma en primera persona ocupa un cuarto de la pantalla y está a medio metro
// de la cámara. Eso cambia lo que hay que pintar: de lejos manda la silueta, de
// cerca manda que la superficie tenga DIRECCIÓN. Tres cosas la dan, y ninguna es
// el color base:
//
// - EL FILO. Una franja clara y muy pulida en el canto. Es lo que separa un
//   trozo de metal de un arma: sin ella la espada parece una regla.
// - LA CANAL. El vaciado del centro de la hoja, más oscuro y algo más rugoso.
//   Con el filo, da los tres tramos que tiene una hoja de verdad.
// - LAS MARCAS DE PULIDO. Ruido estiradísimo a lo largo de la hoja. Es lo que
//   hace que el reflejo de la antorcha se arrastre en vez de posarse.
//
// Coordenadas en espacio de MODELO. El arma se mueve con la mano y su desgaste
// tiene que ir pegado a ella; en espacio de mundo la hoja pasaría por debajo de
// su propia suciedad en cada golpe.

group_uniforms Acero;
uniform vec3 steel_dark : source_color = vec3(0.10, 0.105, 0.125);
uniform vec3 steel_light : source_color = vec3(0.46, 0.48, 0.54);
uniform float polish : hint_range(0.0, 1.0) = 0.7;

group_uniforms Desgaste;
uniform vec3 tarnish_color : source_color = vec3(0.14, 0.10, 0.06);

/// Pátina y mordeduras. Cero es recién forjada, uno es un arma con historia.
uniform float tarnish_amount : hint_range(0.0, 1.0) = 0.3;

group_uniforms Filo;

/// Eje ANCHO de la hoja en espacio de modelo: hacia dónde queda el corte.
uniform vec3 edge_axis = vec3(1.0, 0.0, 0.0);

/// Distancia al plano central donde empieza el bisel, y donde ya es filo puro.
uniform float edge_inner : hint_range(0.0, 0.5) = 0.018;
uniform float edge_outer : hint_range(0.0, 0.5) = 0.03;

/// Ancho del vaciado central. A cero la hoja es plana y no hay canal.
uniform float fuller_width : hint_range(0.0, 0.5) = 0.0;

group_uniforms Pulido;

/// Eje LARGO de la hoja: la dirección en la que se arrastran las marcas.
uniform vec3 grain_axis = vec3(0.0, 1.0, 0.0);
uniform float grain_stretch : hint_range(1.0, 48.0) = 22.0;

group_uniforms Retro;
uniform float texels_per_meter : hint_range(8.0, 512.0) = 300.0;
uniform float color_levels : hint_range(2.0, 64.0) = 12.0;
uniform float dither_amount : hint_range(0.0, 2.0) = 1.0;
uniform float facet_amount : hint_range(0.0, 1.0) = 0.55;

/// Rejilla del temblor de vértices, en radianes de ángulo visual. Más baja que
/// la de las criaturas: a medio metro de la cámara el mismo temblor que a cinco
/// metros se convierte en un arma que vibra.
uniform float vertex_grid : hint_range(0.0, 0.03) = 0.0035;

varying vec3 local_pos;
varying vec3 local_nrm;

// Chapa: base, marcas de pulido y mordeduras.
vec3 steel_at(vec2 p, vec2 polish_uv, out float wear)
{
	float base = fbm(p * 7.0) * 0.55 + fbm(p * 26.0) * 0.45;

	// El pulido no cambia el color, cambia CUÁNTO se aclara: una marca de piedra
	// de afilar es una franja que devuelve más luz, no una franja de otro tono.
	float streak = fbm(polish_uv);
	float sheen = mix(base, base * 0.55 + streak * 0.75, polish);

	vec3 col = mix(steel_dark, steel_light, clamp(sheen, 0.0, 1.0));

	// Mordeduras y pátina, en manchas grandes y con borde duro para que se lean
	// como marcas de uso y no como suciedad general.
	wear = smoothstep(0.52, 0.74, fbm(p * 4.2)) * tarnish_amount;

	return mix(col, tarnish_color, wear);
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

	vec3 across = normalize(edge_axis + vec3(0.0001));
	vec3 along = normalize(grain_axis + vec3(0.0001));
	vec3 stretched = local_pos - along * dot(local_pos, along) * (1.0 - 1.0 / grain_stretch);

	float wx;
	float wy;
	float wz;
	vec3 albedo = steel_at(retro_texel(local_pos.zy, texels_per_meter), stretched.zy * 5.0, wx) * blend.x
		+ steel_at(retro_texel(local_pos.xz, texels_per_meter), stretched.xz * 5.0, wy) * blend.y
		+ steel_at(retro_texel(local_pos.xy, texels_per_meter), stretched.xy * 5.0, wz) * blend.z;

	float wear = wx * blend.x + wy * blend.y + wz * blend.z;

	// Los tres tramos de la hoja: canal, plano y filo.
	float across_d = abs(dot(local_pos, across));
	float edge = smoothstep(edge_inner, edge_outer, across_d);
	float fuller = 1.0 - smoothstep(0.0, max(fuller_width, 0.0001), across_d);
	fuller *= step(0.0001, fuller_width);

	albedo = mix(albedo, steel_light, edge * 0.55 * (1.0 - wear));
	albedo = mix(albedo, steel_dark * 0.8, fuller * 0.6);

	albedo = retro_bands(albedo, color_levels, retro_dither(FRAGCOORD.xy, dither_amount));

	ALBEDO = albedo;

	// Aquí está el material. El filo es el sitio más pulido del arma y por eso es
	// donde se ve la antorcha; la pátina deja de ser metal y deja de brillar.
	//
	// METALLIC va deliberadamente bajo para lo que es. Un metal de verdad no tiene
	// componente difusa: solo devuelve lo que le llega, y en una mazmorra con la
	// ambiental a 0,06 y sin sondas de reflejo eso significa que las caras que no
	// miran a la antorcha salen NEGRAS. Con una hoja de cuatro caras, dos están
	// siempre negras y el arma se ve como una silueta recortada. Bajarlo devuelve
	// la difusa y con ella el material.
	//
	// Además es lo que hacía la época: el metal era una textura pintada con el
	// brillo ya dentro, no un cálculo. Aquí el destello lo pone `metallic_specular`
	// alto sobre el filo, que es donde de verdad se ve.
	METALLIC = (1.0 - wear) * 0.35;
	ROUGHNESS = mix(mix(0.46, 0.16, edge) + fuller * 0.12, 0.88, wear);
	SPECULAR = mix(0.75, 0.22, wear);

	// El canto recoge un hilo de luz contra la niebla, igual que el hueso. Sin
	// esto el arma desaparece en cuanto te alejas de una antorcha.
	RIM = 0.5;
	RIM_TINT = 0.3;

	NORMAL = retro_facet(NORMAL, dFdx(VERTEX), dFdy(VERTEX), facet_amount);
}
