shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, diffuse_lambert, specular_disabled;

#include "res://assets/shaders/retro.gdshaderinc"

// Tela podrida. Lo que le queda puesto al esqueleto de lo que llevaba cuando
// todavía respiraba: un faldón de tiras, no una prenda.
//
// Es el séptimo shader del proyecto y entra por una razón de lectura, no de
// adorno: un esqueleto son dos palos, un cubo y unos aros, y el hueco entre la
// pelvis y las rodillas está VACÍO. Una masa oscura ahí abajo le da al bicho un
// peso que las piernas solas no tienen, y de paso tapa el sitio por donde se le
// ve el truco.
//
// TRES COSAS Y NINGUNA ES LA TEXTURA:
//
// - EL RECORTE DE UN BIT. El bajo del harapo no es una línea recta: se come con
//   ruido, y el ruido se evalúa en una rejilla GRUESA, así que el diente tiene el
//   tamaño de un píxel de la época y el borde sale escalonado. Cada píxel está o
//   no está y no hay mezcla, que es lo único que sabía hacer la consola. Con
//   transparencia de verdad y borde suave esto sería una cortina de un juego de
//   ahora; con el diente cuadrado se reconoce la época antes de mirar nada más.
// - EL VUELO DE VÉRTICES. La tela no se simula: se mueve con un seno en el
//   vértice y la amplitud sube hacia el bajo. Es como se hacía una capa en 1999
//   y cuesta dos instrucciones.
// - EL MISMO TEMBLOR QUE EL HUESO. Sale del include común. Si el harapo no
//   temblara igual que la pelvis de la que cuelga, se le despegaría un píxel por
//   fotograma y se vería enseguida.
//
// Los cuatro paños comparten material, así que sin semilla los cuatro se
// romperían por el mismo sitio y el faldón se leería como cuatro copias. La
// semilla la reparte `SkeletonRig`, igual que a los huesos.
//
// Los harapos NO proyectan sombra, y eso se apaga en la escena y no aquí. Una
// sombra de una malla con recorte de alfa obliga al motor a evaluar el recorte
// otra vez en cada pasada de sombra de cada antorcha, y son seis. Un harapo no
// vale seis pasadas.

group_uniforms Tela;
uniform vec3 cloth_pale : source_color = vec3(0.40, 0.34, 0.24);
uniform vec3 cloth_deep : source_color = vec3(0.15, 0.13, 0.10);
uniform vec3 rot_color : source_color = vec3(0.15, 0.15, 0.10);
uniform float rot_amount : hint_range(0.0, 1.0) = 0.55;

/// Cuánto se oscurece hacia el bajo. La mugre sube desde el suelo, no baja.
uniform float hem_grime : hint_range(0.0, 1.0) = 0.45;

group_uniforms Roto;

/// Dónde queda el bajo más ALTO, en fracción de la altura del paño. Por encima
/// de esta línea la tela está entera pase lo que pase.
uniform float tear_start : hint_range(0.0, 1.0) = 0.42;

/// Cuánto baja el diente más largo. A 1 alguna tira llega hasta abajo del todo.
uniform float tear_bite : hint_range(0.0, 1.0) = 0.85;

/// Cuántas tiras salen a lo ancho del paño. Pocas: cuatro o cinco tiras anchas
/// se leen a diez metros; veinte flecos finos son ruido y desaparecen.
uniform float tear_teeth : hint_range(1.0, 30.0) = 4.5;

/// Escalones del borde. ES el tamaño del píxel del recorte: subirlo afina el
/// diente y con él se va el aspecto de la época.
uniform float tear_steps : hint_range(4.0, 128.0) = 20.0;

/// Agujeros sueltos en medio de la tela. Poco: si se pasa, no queda harapo.
uniform float holes : hint_range(0.0, 1.0) = 0.3;

group_uniforms Vuelo;
uniform float sway_hz : hint_range(0.0, 3.0) = 0.55;
uniform float sway_amount : hint_range(0.0, 0.3) = 0.035;

group_uniforms Retro;
uniform float texels_per_meter : hint_range(8.0, 512.0) = 150.0;
uniform float color_levels : hint_range(2.0, 64.0) = 7.0;
uniform float dither_amount : hint_range(0.0, 2.0) = 1.0;
uniform float vertex_grid : hint_range(0.0, 0.03) = 0.006;

group_uniforms Luz;
uniform float light_levels : hint_range(1.0, 24.0) = 4.0;
uniform float light_dither : hint_range(0.0, 2.0) = 0.9;
uniform float light_wrap : hint_range(0.0, 1.0) = 0.6;
uniform float light_knee : hint_range(0.0, 2.0) = 0.25;

/// La semilla de este paño, de 0 a 1. La reparte `SkeletonRig`.
instance uniform float piece_seed = 0.0;

varying vec2 cloth_uv;

void vertex()
{
	// El bajo vuela y la cintura no. Al cuadrado y no lineal: una tela colgada no
	// se dobla por igual en toda su altura, se abre desde donde deja de tirar el
	// peso, y con la curva lineal el harapo entero se mueve como una tabla.
	float hang = UV.y * UV.y;
	float phase = TIME * sway_hz * TAU + piece_seed * TAU + VERTEX.x * 5.0;

	VERTEX.x += sin(phase) * sway_amount * hang;
	VERTEX.z += cos(phase * 0.7) * sway_amount * 0.6 * hang;
	VERTEX.y -= abs(sin(phase)) * sway_amount * 0.25 * hang;

	VERTEX = retro_snap(MODELVIEW_MATRIX, VERTEX, vertex_grid);

	cloth_uv = UV;
}

void fragment()
{
	// Los texeles se cuadran en el espacio del PAÑO y no del modelo: un harapo es
	// una tira de tela plana y su dibujo va con la trama, no con el mundo.
	vec2 p = retro_texel(cloth_uv * vec2(0.30, 0.42), texels_per_meter)
		+ vec2(piece_seed * 5.1, piece_seed * 3.7);

	// La trama: dos ruidos muy estirados, uno en cada dirección. Es lo que
	// distingue tela de papel.
	float warp = vnoise(p * vec2(150.0, 9.0));
	float weft = vnoise(p * vec2(9.0, 150.0));
	float weave = (warp + weft) * 0.5;

	// Pliegues: bandas verticales anchas. Sin esto el paño es una mancha plana.
	float folds = fbm(vec2(p.x * 24.0, p.y * 3.0));

	vec3 col = mix(cloth_deep, cloth_pale, 0.22 + folds * 0.75 + weave * 0.28);

	// Podredumbre en manchas grandes, y mugre subiendo desde el bajo.
	float rot = smoothstep(0.42, 0.78, fbm(p * 8.0));
	col = mix(col, rot_color, rot * rot_amount);
	col *= mix(1.0, 0.5, cloth_uv.y * hem_grime);

	ALBEDO = retro_bands(col, color_levels, retro_dither(FRAGCOORD.xy, dither_amount));
	ROUGHNESS = 0.95;
	METALLIC = 0.0;
	SPECULAR = 0.0;

	// EL BAJO NO ES UNA LÍNEA. Se decide en una rejilla gruesa —`tear_steps`
	// escalones a lo ancho y a lo alto— y por eso el diente sale de píxeles
	// cuadrados del tamaño que tendría una textura de la época. Evaluar el ruido
	// en coordenadas continuas daría el mismo dibujo con el borde suave, y con el
	// borde suave se va la mitad del efecto.
	vec2 t = (floor(cloth_uv * tear_steps) + 0.5) / tear_steps;

	float bite = vnoise(vec2(t.x * tear_teeth + piece_seed * 37.0, piece_seed * 19.0)) * 0.7
		+ vnoise(vec2(t.x * tear_teeth * 2.6 + piece_seed * 11.0, 4.0)) * 0.3;

	float hem = tear_start + bite * tear_bite * (1.0 - tear_start);

	// Y algún agujero suelto donde la tela ya no aguanta.
	float hole = smoothstep(0.74, 0.84, fbm(vec2(t.x * 9.0 + piece_seed * 23.0, t.y * 9.0)));

	// El alfa sale centrado en el umbral del recorte para que el corte caiga justo
	// en el bajo: por encima entero, por debajo nada.
	ALPHA = 0.5 + (hem - t.y) - hole * holes;
	ALPHA_SCISSOR_THRESHOLD = 0.5;
}

// El mismo sombreado a escalones que el hueso, y por el mismo motivo: si el
// harapo se sombreara con el degradado del motor y la pelvis de la que cuelga a
// cinco niveles, la tela parecería pegada encima en otro programa. La envolvente
// va más alta que en el hueso porque un paño es una cara sola sin volumen: con
// el terminador duro, la mitad de los paños quedaría negra.
void light()
{
	float ndl = dot(normalize(NORMAL), normalize(LIGHT));
	float wrapped = clamp((ndl + light_wrap) / (1.0 + light_wrap), 0.0, 1.0);

	float steps = max(light_levels, 1.0);
	float dither = (bayer4(FRAGCOORD.xy) - 0.5) * light_dither;
	float shade = clamp(floor(wrapped * steps + 0.5 + dither) / steps, 0.0, 1.0);

	// LIGHT_COLOR llega multiplicado por PI —lo mete el motor porque el 1/PI vive
	// dentro de las BRDF físicas— así que lo primero es deshacerlo. Si no, el techo
	// se aplica sobre números tres veces más grandes de lo que parecen y apaga la
	// sala entera en vez de recortar solo el primer plano.
	// Ver `bone.gdshader`: LIGHT_COLOR trae un PI de más y el albedo lo pone el
	// motor al salir de aquí.
	vec3 energy = LIGHT_COLOR * ATTENUATION / PI;
	energy /= 1.0 + energy * light_knee;

	DIFFUSE_LIGHT += energy * shade;
}
