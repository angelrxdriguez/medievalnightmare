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
//
// Y DOS MÁS QUE VINIERON DESPUÉS, cuando el bicho ya pasaba de cuarenta piezas y se
// vio que le pasaban dos cosas que no arregla más textura:
//
// - LA SEMILLA POR PIEZA. Cuarenta y siete huesos con el mismo material y la misma
//   fórmula son cuarenta y siete veces el mismo hueso, y el ojo lo pilla enseguida: se
//   lee como una textura repetida y no como un montón de huesos. Con una semilla
//   por pieza —la reparte `SkeletonRig` al arrancar— cada uno mueve sus
//   coordenadas de ruido y su tono, y de paso ningún esqueleto de la sala es
//   idéntico a otro. Cuesta un `instance uniform` y un par de sumas.
// - LA LUZ A ESCALONES. Ver `light()` al final del archivo. Es lo que arregla el
//   hueso quemado al lado de una antorcha, y de paso lo que le da al bicho el
//   sombreado plano de la época en vez del degradado suave de ahora.

group_uniforms Hueso;
uniform vec3 bone_pale : source_color = vec3(0.52, 0.50, 0.42);
uniform vec3 bone_deep : source_color = vec3(0.22, 0.20, 0.15);
uniform vec3 stain_color : source_color = vec3(0.14, 0.10, 0.06);
uniform float stain_amount : hint_range(0.0, 1.0) = 0.62;

group_uniforms Vetas;

// Eje largo del hueso en espacio de modelo. Las vetas se estiran en esta
// dirección: en un fémur va a lo largo, en el cráneo da igual.
uniform vec3 grain_axis = vec3(0.0, 1.0, 0.0);
uniform float grain_stretch : hint_range(1.0, 24.0) = 7.0;
uniform float grain_amount : hint_range(0.0, 1.0) = 0.4;

group_uniforms Pieza;

/// Cuánto cambia el tono de una pieza a otra con la semilla. A 0, todas iguales.
uniform float age_spread : hint_range(0.0, 1.0) = 0.35;

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

group_uniforms Luz;

/// Escalones del sombreado. Cinco se lee como pintado a mano; por encima de doce
/// vuelve a ser un degradado y con él se va la época.
uniform float light_levels : hint_range(1.0, 24.0) = 5.0;

/// Tramado del escalón de luz, en fracción de escalón.
uniform float light_dither : hint_range(0.0, 2.0) = 0.9;

/// Cuánta luz sigue habiendo pasado el terminador.
uniform float light_wrap : hint_range(0.0, 1.0) = 0.35;

/// El techo de energía. Ver `light()`.
uniform float light_knee : hint_range(0.0, 2.0) = 0.25;

group_uniforms Lectura;

// Luz de borde. Es lo que dibuja la silueta contra la niebla.
uniform float rim_power : hint_range(0.5, 8.0) = 2.6;
uniform float rim_strength : hint_range(0.0, 2.0) = 0.8;

// Emisión mínima en el borde para que el bicho exista en la oscuridad total.
// Muy baja a propósito: la idea es intuirlo, no verlo.
uniform float ghost_rim : hint_range(0.0, 0.3) = 0.025;
uniform vec3 ghost_tint : source_color = vec3(0.36, 0.40, 0.44);

uniform float roughness_base : hint_range(0.0, 1.0) = 0.78;

/// La semilla de esta pieza, de 0 a 1. La reparte `SkeletonRig` hueso a hueso.
instance uniform float piece_seed = 0.0;

varying vec3 local_pos;
varying vec3 local_nrm;

// Un hueso: base pálida, veta, mugre y grietas finas.
vec3 bone_at(vec2 p, vec2 grain, float age)
{
	// La semilla mueve el ruido de sitio. Sin esto, dos piezas del mismo tamaño
	// —las dos tibias, los dos antebrazos— salen calcadas.
	p += vec2(piece_seed * 13.7, piece_seed * 29.3);

	float base = fbm(p * 8.0) * 0.6 + fbm(p * 30.0) * 0.4;
	vec3 col = mix(bone_deep, bone_pale, 0.35 + base * 0.75);

	// Y además cada pieza tiene su edad: unas están más comidas que otras, que es
	// lo que le pasa a un esqueleto que lleva doscientos años en un sótano.
	col = mix(col, bone_deep, age * age_spread);

	// Veta estirada. El estiramiento va en las coordenadas, no en el ruido.
	float fiber = fbm(grain);
	col = mix(col, col * (0.72 + fiber * 0.5), grain_amount);

	// Mugre acumulada, en manchas grandes.
	float dirt = smoothstep(0.40, 0.80, fbm(p * 3.3));
	col = mix(col, stain_color, dirt * stain_amount * (0.7 + age * 0.6));

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

	float age = fract(piece_seed * 7.13);

	vec3 albedo = bone_at(retro_texel(local_pos.zy, texels_per_meter), stretched.zy * 6.0, age) * blend.x
		+ bone_at(retro_texel(local_pos.xz, texels_per_meter), stretched.xz * 6.0, age) * blend.y
		+ bone_at(retro_texel(local_pos.xy, texels_per_meter), stretched.xy * 6.0, age) * blend.z;

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

// EL SOMBREADO DE LA ÉPOCA, y de paso el arreglo del hueso quemado.
//
// Quedarse con la luz —que es lo que significa escribir `light()`— sale a cuenta
// por dos cosas que no se pueden hacer desde `fragment()`:
//
// - EL TECHO. Una antorcha a metro y medio le mete al hueso cinco o seis veces
//   la energía que necesita, y como el hueso es claro se va a blanco puro: el
//   bicho se convierte en una silueta recortada justo cuando lo tienes encima.
//   Estaba anotado como fallo pendiente y la salida fácil era bajar `bone_pale`,
//   que arregla el primer plano y deja apagado el resto de la sala. Lo que hay
//   que comprimir es la ENERGÍA, no el color: `e / (1 + e·k)` deja la luz baja
//   como estaba y aplasta la alta contra un tope. Son dos operaciones y es, en
//   el fondo, lo que hacía una tabla de luz de ocho bits.
// - LOS ESCALONES. Un degradado suave sobre un modelo de cuarenta caras es lo
//   que más delata que esto es de ahora. Cuantizando la luz a cinco niveles —y
//   deshaciendo el escalón con el MISMO Bayer que la paleta, que si no vuelven
//   las manchas— el hueso se sombrea como se pintaba entonces.
//
// La envolvente (`light_wrap`) no es adorno: con el terminador duro, un
// esqueleto iluminado de lado se parte en dos mitades y la oscura desaparece
// contra el fondo, que es justo lo que no se quiere de un bicho cuya lectura es
// la silueta.
void light()
{
	float ndl = dot(normalize(NORMAL), normalize(LIGHT));
	float wrapped = clamp((ndl + light_wrap) / (1.0 + light_wrap), 0.0, 1.0);

	float steps = max(light_levels, 1.0);
	float dither = (bayer4(FRAGCOORD.xy) - 0.5) * light_dither;
	float shade = clamp(floor(wrapped * steps + 0.5 + dither) / steps, 0.0, 1.0);

	// DOS TRAMPAS EN DOS LÍNEAS, y las dos se pagan en oscuridad:
	//
	// - LIGHT_COLOR llega multiplicado por PI. Lo mete el motor porque el 1/PI vive
	//   dentro de las BRDF físicas, así que aquí lo primero es deshacerlo: sin eso,
	//   el techo se aplica sobre números tres veces más grandes de lo que parecen y
	//   apaga la sala entera en vez de recortar solo el primer plano.
	// - AQUÍ NO SE MULTIPLICA POR ALBEDO. El motor multiplica DIFFUSE_LIGHT por el
	//   albedo DESPUÉS de llamar a esta función, así que hacerlo también aquí lo
	//   eleva al cuadrado y el bicho sale a un tercio de la luz que le toca. Se
	//   descubrió porque el esqueleto se fue a negro justo al estrenar el escalonado
	//   y parecía culpa del techo.
	vec3 energy = LIGHT_COLOR * ATTENUATION / PI;
	energy /= 1.0 + energy * light_knee;

	DIFFUSE_LIGHT += energy * shade;
}
