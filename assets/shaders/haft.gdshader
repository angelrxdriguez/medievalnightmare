shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

#include "res://assets/shaders/retro.gdshaderinc"

// Todo lo que se agarra: astiles de madera, cachas, la caja de la ballesta y el
// cuero enrollado de las empuñaduras.
//
// Es un solo shader y no dos porque madera y cuero se distinguen por UNA cosa:
// hacia dónde va el dibujo. La madera lo lleva a lo LARGO de la pieza —la fibra
// del árbol— y el cuero lo lleva CRUZADO, porque es una tira enrollada. Con
// `wrap_bands` a cero sale un astil; subiéndolo sale la misma pieza forrada.
//
// Los nudos de la madera no son opcionales. Un cilindro con veta y sin nudos se
// lee como plástico marrón: lo que dice "esto salió de un árbol" es que cada
// tanto la fibra se cierra en un remolino.

group_uniforms Madera;
uniform vec3 wood_dark : source_color = vec3(0.085, 0.058, 0.036);
uniform vec3 wood_light : source_color = vec3(0.30, 0.20, 0.115);

/// Eje LARGO de la pieza en espacio de modelo. La fibra corre en esta dirección.
uniform vec3 grain_axis = vec3(0.0, 1.0, 0.0);
uniform float grain_stretch : hint_range(1.0, 48.0) = 16.0;

group_uniforms Nudos;
uniform float knot_density : hint_range(0.0, 8.0) = 2.2;
uniform float knot_amount : hint_range(0.0, 1.0) = 0.5;

group_uniforms Cuero;

/// Vueltas de correa por metro. A cero la pieza es madera desnuda.
uniform float wrap_bands : hint_range(0.0, 120.0) = 0.0;
uniform vec3 wrap_color : source_color = vec3(0.13, 0.085, 0.055);

group_uniforms Retro;
uniform float texels_per_meter : hint_range(8.0, 512.0) = 260.0;
uniform float color_levels : hint_range(2.0, 64.0) = 10.0;
uniform float dither_amount : hint_range(0.0, 2.0) = 1.0;
uniform float facet_amount : hint_range(0.0, 1.0) = 0.55;
uniform float vertex_grid : hint_range(0.0, 0.03) = 0.0035;

uniform float roughness_base : hint_range(0.0, 1.0) = 0.88;

varying vec3 local_pos;
varying vec3 local_nrm;

vec3 wood_at(vec2 p, vec2 fiber)
{
	// La veta: ruido aplastado en la dirección del eje. El aplastamiento va en
	// las coordenadas, no en el ruido, que es lo que lo deja continuo.
	float grain = fbm(fiber);

	// Anillos. Se sacan doblando la veta sobre sí misma: es lo que hace que
	// aparezcan líneas finas y paralelas en vez de manchas alargadas.
	//
	// El contraste va bajo a propósito. Subiéndolo salen bandas duras y una caja
	// de ballesta pasa de ser madera a ser un tigre: la veta se lee por su
	// DIRECCIÓN, no por lo marcada que esté.
	float rings = abs(fract(grain * 5.5) * 2.0 - 1.0);

	vec3 col = mix(wood_dark, wood_light, 0.25 + grain * 0.6);
	col *= 0.88 + rings * 0.2;

	// Nudos: donde el ruido de baja frecuencia hace cresta, la fibra se cierra y
	// se oscurece en un anillo apretado.
	float knot = fbm(p * knot_density);
	float swirl = smoothstep(0.62, 0.86, knot);
	col = mix(col, wood_dark * 0.6, swirl * knot_amount);

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

	vec3 albedo = wood_at(retro_texel(local_pos.zy, texels_per_meter), stretched.zy * 6.0) * blend.x
		+ wood_at(retro_texel(local_pos.xz, texels_per_meter), stretched.xz * 6.0) * blend.y
		+ wood_at(retro_texel(local_pos.xy, texels_per_meter), stretched.xy * 6.0) * blend.z;

	// La correa. `along` es lo que ha avanzado la tira a lo largo de la pieza, así
	// que el surco entre vuelta y vuelta sale solo de su parte fraccionaria.
	float wrapped = step(0.0001, wrap_bands);
	float along = dot(local_pos, axis) * wrap_bands;
	float seam = abs(fract(along) * 2.0 - 1.0);

	// Cada vuelta se abomba en el centro y se hunde en la costura: es lo que hace
	// que un cilindro liso se lea como una tira enrollada y no como un tubo.
	vec3 strap = wrap_color * (0.62 + seam * 0.62);
	strap *= 0.85 + fbm(retro_texel(local_pos.xz, texels_per_meter) * 40.0) * 0.4;

	albedo = mix(albedo, strap, wrapped);

	albedo = retro_bands(albedo, color_levels, retro_dither(FRAGCOORD.xy, dither_amount));

	ALBEDO = albedo;
	METALLIC = 0.0;
	ROUGHNESS = mix(roughness_base, 0.72 - seam * 0.14, wrapped);
	SPECULAR = 0.18;

	// El surco de la correa se come la luz. Sin esto las vueltas se ven pintadas.
	AO = mix(1.0, 0.6 + seam * 0.4, wrapped);
	AO_LIGHT_AFFECT = 0.6;

	NORMAL = retro_facet(NORMAL, dFdx(VERTEX), dFdy(VERTEX), facet_amount);
}
