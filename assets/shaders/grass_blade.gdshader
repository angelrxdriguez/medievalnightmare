shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, diffuse_lambert, specular_disabled;

#include "res://assets/shaders/retro.gdshaderinc"

// Las briznas. Son un MultiMesh de un par de miles de cuñas: una sola llamada
// de dibujado, y la variación entera —altura, giro, tono y compás del viento—
// viaja en la transformación y en `INSTANCE_CUSTOM`.
//
// POR QUÉ SON GEOMETRÍA Y NO UN RECORTE. Lo estándar es un cuadrado con una
// textura de briznas recortada por alfa. Aquí no hay texturas, y además el
// recorte obliga a evaluar el alfa otra vez en cada pasada de sombra. Una cuña
// de ocho triángulos cuesta menos, no desaparece de canto —que es lo que le pasa
// a un cuadrado plano cuando lo miras desde el lado— y se dobla de verdad.
//
// EL VIENTO ES UN SENO, Y ESTÁ BIEN QUE LO SEA. En las antorchas un seno se
// oye: el ojo le pilla el compás y el fuego pasa a ser una bombilla. Aquí no,
// porque cada brizna lleva su propio desfase sacado de dónde está, así que lo
// que se ve no es un seno sino una OLA cruzando el prado. El compás solo se
// notaría si todas se movieran a la vez.
//
// La curva es cuadrática en la altura: la base no se mueve —está enraizada— y la
// punta se va entera. Lineal, la brizna se inclina como un poste; cuadrática, se
// dobla como una brizna.

group_uniforms Brizna;
uniform vec3 blade_base : source_color = vec3(0.045, 0.058, 0.030);
uniform vec3 blade_edge : source_color = vec3(0.20, 0.19, 0.105);

/// Alto de la malla SIN escalar, en metros. Tiene que coincidir con el de la
/// cuña de la escena: es de donde sale a qué altura está cada vértice y, por
/// tanto, cuánto se dobla. Escalar la instancia no lo estropea, porque el
/// vértice se mide antes de escalar.
uniform float blade_height : hint_range(0.05, 2.0) = 0.34;

/// Lo que se estrecha la punta. Las cuñas ya se estrechan por geometría; esto
/// afina el remate para que no acabe en un chaflán ancho.
uniform float taper : hint_range(0.0, 1.0) = 0.55;

group_uniforms Viento;

/// En espacio de MODELO de la brizna. Las briznas se plantan con un giro
/// pequeño alrededor de una dirección común, así que el modelo de todas apunta
/// casi al mismo sitio y doblarlas en su propia X sale coherente sin invertir
/// una matriz por vértice.
uniform float wind_amount : hint_range(0.0, 0.6) = 0.11;

uniform float wind_speed : hint_range(0.0, 8.0) = 1.35;

/// Racha: un segundo ruido lento que de vez en cuando aprieta. Es lo que hace
/// que el prado no lata siempre igual.
uniform float gust_amount : hint_range(0.0, 1.0) = 0.55;

uniform float gust_speed : hint_range(0.0, 2.0) = 0.22;

group_uniforms Retro;
uniform float color_levels : hint_range(2.0, 64.0) = 8.0;
uniform float dither_amount : hint_range(0.0, 2.0) = 1.0;

/// Sin temblor de vértices. La rejilla es angular y constante, así que a dos
/// metros una brizna de tres centímetros SALTARÍA de celda en celda y el prado
/// entero herviría. El temblor es para lo que mide más que la celda.
uniform float vertex_grid : hint_range(0.0, 0.03) = 0.0;

varying float height_t;
varying float tint;

void vertex()
{
	// Altura normalizada, de 0 en la raíz a 1 en la punta. La malla está centrada
	// en su origen, de ahí el medio.
	height_t = clamp(VERTEX.y / max(blade_height, 0.001) + 0.5, 0.0, 1.0);

	// El desfase y el tono vienen de la instancia: los reparte GrassField a
	// partir de dónde cae cada brizna, y por eso el viento cruza el prado en vez
	// de sacudirlo entero.
	float phase = INSTANCE_CUSTOM.x * TAU;
	tint = INSTANCE_CUSTOM.y;

	float gust = 0.6 + gust_amount * fbm(vec2(TIME * gust_speed, phase * 0.15));
	float sway = sin(TIME * wind_speed + phase) * wind_amount * gust;

	// Cuadrática: la raíz no se mueve y la punta se va entera.
	VERTEX.x += sway * height_t * height_t;

	// El remate. Sin esto la cuña acaba en un chaflán tan ancho como un texel y
	// se le ve el corte.
	VERTEX.x *= mix(1.0, 1.0 - taper, height_t);
	VERTEX.z *= mix(1.0, 1.0 - taper, height_t);

	VERTEX = retro_snap(MODELVIEW_MATRIX, VERTEX, vertex_grid);
}

void fragment()
{
	// De la raíz a la punta: oscura abajo, donde no llega la luz entre matas, y
	// seca arriba. Es el degradado que hace que un prado tenga fondo.
	vec3 col = mix(blade_base, blade_edge, height_t * height_t);

	// El tono de la instancia. Sin él, dos mil briznas del mismo verde se leen
	// como una alfombra de plástico.
	col *= 0.72 + tint * 0.56;

	ALBEDO = retro_bands(col, color_levels, retro_dither(FRAGCOORD.xy, dither_amount));
	METALLIC = 0.0;
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;

	// La base está en sombra por definición: tiene dos mil briznas alrededor.
	AO = mix(0.35, 1.0, height_t);
	AO_LIGHT_AFFECT = 0.8;
}
