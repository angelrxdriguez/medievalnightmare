shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

#include "res://assets/shaders/retro.gdshaderinc"

// Hierro picado. Va en los soportes de las antorchas, en los pebeteros y en el
// filo de los esqueletos.
//
// El hierro no es un color: es un contraste. La chapa limpia es metálica,
// oscura y devuelve la antorcha en un destello duro; el óxido es dieléctrico,
// mate y naranja apagado. Lo que vende el material es que METALLIC y ROUGHNESS
// cambien de sitio a sitio siguiendo la misma máscara que el color, no que el
// albedo tenga manchitas. Por eso aquí la máscara de óxido gobierna las tres.
//
// Coordenadas en espacio de modelo, como el hueso: el arma se mueve con su
// dueño y su corrosión tiene que ir pegada a ella.
//
// El temblor de vértices no es decoración aquí: es obligatorio. Este material
// va en el machete que lleva el esqueleto en la mano, y si los huesos tiemblan
// con la rejilla puesta y el machete no, la mano se separa del mango un píxel
// por fotograma. Los dos comparten `retro_snap` y por eso caen a la misma
// rejilla. La reja del techo y los aros de las antorchas lo llevan a cero: son
// piezas grandes y quietas, y el temblor solo se lee bien en lo que se mueve.

group_uniforms Metal;
uniform vec3 iron_color : source_color = vec3(0.085, 0.085, 0.095);
uniform vec3 iron_sheen : source_color = vec3(0.26, 0.27, 0.30);

group_uniforms Oxido;
uniform vec3 rust_dark : source_color = vec3(0.19, 0.085, 0.035);
uniform vec3 rust_light : source_color = vec3(0.42, 0.20, 0.075);

// Cuánta superficie se ha comido el óxido. 0 = recién forjado, 1 = chatarra.
uniform float rust_amount : hint_range(0.0, 1.0) = 0.55;

// Lo brusco que es el borde entre chapa y óxido. Duro se lee mejor de lejos.
uniform float rust_edge : hint_range(0.01, 0.5) = 0.13;

group_uniforms Retro;
uniform float texels_per_meter : hint_range(8.0, 512.0) = 180.0;
uniform float color_levels : hint_range(2.0, 64.0) = 10.0;
uniform float dither_amount : hint_range(0.0, 2.0) = 1.0;
uniform float facet_amount : hint_range(0.0, 1.0) = 0.0;

/// Rejilla del temblor de vértices, en radianes de ángulo visual. A 0 se apaga,
/// que es lo que quiere la reja del techo y lo que NO quiere un arma en la mano.
uniform float vertex_grid : hint_range(0.0, 0.03) = 0.0;

/// La semilla de esta pieza, de 0 a 1. La reparte quien la lleve puesta: en el
/// esqueleto, `SkeletonRig`. Sin ella, el yelmo y la hombrera del mismo bicho
/// —dos piezas del mismo material y parecido tamaño— se oxidan calcadas.
instance uniform float piece_seed = 0.0;

varying vec3 local_pos;
varying vec3 local_nrm;

// x = máscara de óxido (0 chapa, 1 óxido), y = picado fino.
vec2 corrosion(vec2 p)
{
	p += vec2(piece_seed * 8.3, piece_seed * 19.7);

	float patches = fbm(p * 5.5);
	float mask = smoothstep(1.0 - rust_amount - rust_edge, 1.0 - rust_amount + rust_edge, patches);
	float pits = fbm(p * 34.0);

	return vec2(mask, pits);
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

	vec2 cx = corrosion(retro_texel(local_pos.zy, texels_per_meter));
	vec2 cy = corrosion(retro_texel(local_pos.xz, texels_per_meter));
	vec2 cz = corrosion(retro_texel(local_pos.xy, texels_per_meter));

	vec2 c = cx * blend.x + cy * blend.y + cz * blend.z;
	float rust = c.x;
	float pits = c.y;

	vec3 metal = mix(iron_color, iron_sheen, smoothstep(0.45, 0.85, pits));
	vec3 oxide = mix(rust_dark, rust_light, pits);

	vec3 albedo = mix(metal, oxide, rust);

	ALBEDO = retro_bands(albedo, color_levels, retro_dither(FRAGCOORD.xy, dither_amount));

	// Aquí está el material: el óxido deja de ser metal y deja de brillar.
	METALLIC = (1.0 - rust) * 0.85;
	ROUGHNESS = mix(0.34 + pits * 0.22, 0.94, rust);
	SPECULAR = mix(0.5, 0.15, rust);
	AO = mix(1.0, 0.75, rust * (1.0 - pits));
	AO_LIGHT_AFFECT = 0.5;

	NORMAL = retro_facet(NORMAL, dFdx(VERTEX), dFdy(VERTEX), facet_amount);
}
