shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_lambert, specular_disabled;

#include "res://assets/shaders/retro.gdshaderinc"

// El suelo de fuera: hierba muerta, barro y calvas de tierra. Es lo que hay
// DEBAJO de las briznas, y hace más trabajo que ellas.
//
// POR QUÉ NO ES TRIPLANAR. La sillería de la mazmorra lo necesita porque es
// pared, suelo y columna con el mismo material. Esto es siempre horizontal, así
// que se mapea por la planta y punto: un tercio del coste y ni un píxel de
// diferencia. Las caras verticales del cajón del suelo no se ven nunca; están
// fuera del muro y detrás de la niebla.
//
// TRES CAPAS Y NINGUNA ES "HIERBA":
//
// - LA MATA. Ruido grande que decide dónde la hierba está viva y dónde está
//   seca. Manda sobre todo lo demás, porque es lo único que se ve a diez metros.
// - LA CALVA. Donde el ruido cae, asoma la tierra. Sin calvas, un prado es una
//   moqueta verde, y una moqueta verde es lo que hace que un exterior parezca de
//   juguete.
// - EL PEINE. Ruido fino y ESTIRADO en una dirección. No se lee como hierba, se
//   lee como que algo ha pasado por encima, y es lo que da la sensación de que
//   el suelo tiene pelo aunque no haya ni una brizna.
//
// Los caminos no se pintan aquí. Un sendero es un material distinto sobre una
// pieza de CSG un centímetro por encima: así se dibuja andando por el nivel en
// vez de peleándose con una máscara.

group_uniforms Hierba;
uniform vec3 grass_live : source_color = vec3(0.115, 0.145, 0.072);
uniform vec3 grass_dead : source_color = vec3(0.155, 0.140, 0.082);
uniform vec3 grass_deep : source_color = vec3(0.030, 0.040, 0.024);

/// Cuánta hierba está seca. A cero es primavera, a uno es un cementerio.
uniform float dead_amount : hint_range(0.0, 1.0) = 0.6;

/// Tamaño de la mata, en metros.
uniform float clump_size : hint_range(0.2, 8.0) = 2.2;

group_uniforms Tierra;
uniform vec3 soil_color : source_color = vec3(0.085, 0.068, 0.048);
uniform float soil_amount : hint_range(0.0, 1.0) = 0.45;

/// Piedrecillas. Cuatro por metro cuadrado y del tamaño de un texel: lo justo
/// para que el suelo no sea liso al mirarlo de cerca.
uniform float grit_amount : hint_range(0.0, 1.0) = 0.35;

group_uniforms Peine;
uniform vec2 comb_axis = vec2(1.0, 0.35);
uniform float comb_stretch : hint_range(1.0, 40.0) = 14.0;
uniform float comb_amount : hint_range(0.0, 1.0) = 0.4;

group_uniforms Retro;

/// 44 texeles/m es la misma densidad que las losas de la cripta. La hierba y la
/// piedra tienen que estar pintadas a la misma resolución o se nota al pasar de
/// una a otra.
uniform float texels_per_meter : hint_range(4.0, 512.0) = 44.0;

uniform float color_levels : hint_range(2.0, 64.0) = 12.0;
uniform float dither_amount : hint_range(0.0, 2.0) = 1.0;
uniform float roughness_base : hint_range(0.0, 1.0) = 0.97;

varying vec3 world_pos;

void vertex()
{
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment()
{
	vec2 p = retro_texel(world_pos.xz, texels_per_meter);

	// La mata. De ella salen las dos cosas que importan: si hay hierba y si está
	// viva, así que se evalúa una vez y se reaprovecha.
	float clump = fbm(p / max(clump_size, 0.01));

	vec3 blades = mix(grass_live, grass_dead, smoothstep(0.35, 0.75, clump) * dead_amount);
	blades = mix(grass_deep, blades, 0.35 + clump * 0.65);

	// El peine: ruido aplastado en una dirección. Se aplasta la COORDENADA, no el
	// ruido, que es lo que lo deja continuo entre texeles.
	vec2 axis = normalize(comb_axis + vec2(0.0001));
	vec2 combed = p - axis * dot(p, axis) * (1.0 - 1.0 / comb_stretch);
	blades *= mix(1.0, 0.80 + vnoise(combed * 26.0) * 0.42, comb_amount);

	// La calva. Va sobre el mismo ruido de la mata: donde la hierba es más pobre
	// es donde asoma la tierra, que es lo que pasa de verdad.
	float bare = smoothstep(0.46, 0.18, clump) * soil_amount;
	vec3 col = mix(blades, soil_color, bare);

	// Piedrecillas, solo en la calva. Sobre la hierba no se verían.
	float grit = step(0.86, hash12(floor(p * texels_per_meter * 0.5)));
	col = mix(col, col * 1.9, grit * grit_amount * bare);

	ALBEDO = retro_bands(col, color_levels, retro_dither(FRAGCOORD.xy, dither_amount));
	METALLIC = 0.0;
	ROUGHNESS = roughness_base;
	SPECULAR = 0.05;

	// La hierba se come la luz que le entra de lado. Sin esto, un prado a la luz
	// de la luna es una lámina plana y gris.
	AO = mix(0.55, 1.0, clump);
	AO_LIGHT_AFFECT = 0.5;
}
