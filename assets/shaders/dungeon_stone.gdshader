shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

// Sillería de mazmorra generada por completo en el shader. No hay ni un PNG en
// el proyecto y no lo va a haber: una textura de piedra es ruido con juntas, y
// eso el shader lo hace mejor que yo pintando, sin tiling visible y sin pesar.
//
// Tres decisiones que son las que dan el aspecto de 2001 y no de ahora:
//
// 1. TRIPLANAR EN METROS. Las coordenadas son la posición de mundo en metros,
//    así que el ladrillo mide lo que dice `brick_size` y mide lo mismo en una
//    pared, en el suelo y en una columna. La geometría es CSG sin UV: cualquier
//    otra cosa se estiraría.
// 2. TEXELES CUANTIZADOS. Antes de evaluar nada, las coordenadas se redondean a
//    una rejilla de `texels_per_meter`. Eso convierte el ruido continuo en
//    píxeles cuadrados del tamaño exacto que tendría una textura de 128 px, con
//    sus escalones y su aliasing. Es la diferencia entre "procedural moderno" y
//    "esto lo pintó alguien en 2001".
// 3. COLOR POSTERIZADO. `color_levels` recorta la paleta. Las degradaciones a
//    banda son la firma de la época, no un defecto.
//
// La suciedad no es decorativa: la humedad sube desde el suelo y baja la
// rugosidad, así que los charcos de las juntas devuelven la luz de las
// antorchas. El hollín cae desde el techo. Las dos cosas dicen dónde estás.

group_uniforms Piedra;
uniform vec3 stone_light : source_color = vec3(0.40, 0.38, 0.34);
uniform vec3 stone_dark : source_color = vec3(0.17, 0.16, 0.15);
uniform vec3 mortar_color : source_color = vec3(0.10, 0.095, 0.09);

group_uniforms Aparejo;

// Tamaño del sillar en metros. Cuadrado y sin traba = losa de suelo.
uniform vec2 brick_size = vec2(0.90, 0.45);

// Ancho de la junta, también en metros.
uniform float mortar_width : hint_range(0.0, 0.15) = 0.035;

// Traba: cuánto se desplaza cada hilada respecto a la anterior. 0 = a hueso.
uniform float row_offset : hint_range(0.0, 1.0) = 0.5;

// Cuánto se hunde la junta. Es un bump, no desplaza geometría.
uniform float mortar_depth : hint_range(0.0, 3.0) = 1.0;

group_uniforms Desgaste;

// Humedad que sube desde el suelo. Oscurece y pule: eso es lo que brilla.
uniform vec3 damp_color : source_color = vec3(0.05, 0.055, 0.05);
uniform float damp_height : hint_range(0.0, 4.0) = 0.9;
uniform float damp_amount : hint_range(0.0, 1.0) = 0.75;

// Verdín. Vive en las juntas y en la parte baja, que es donde hay agua.
uniform vec3 moss_color : source_color = vec3(0.13, 0.17, 0.09);
uniform float moss_amount : hint_range(0.0, 1.0) = 0.35;

// Hollín. Se sube en techos y encima de las antorchas.
uniform vec3 soot_color : source_color = vec3(0.035, 0.032, 0.030);
uniform float soot_amount : hint_range(0.0, 1.0) = 0.0;

group_uniforms Retro;

// El "tamaño de textura" aparente. 24 texeles/m son píxeles de 4 cm.
uniform float texels_per_meter : hint_range(4.0, 512.0) = 56.0;

// Escalones de color. Cuanto menos, más 2001.
uniform float color_levels : hint_range(2.0, 64.0) = 14.0;

group_uniforms Superficie;
uniform float roughness_base : hint_range(0.0, 1.0) = 0.95;
uniform float cavity_ao : hint_range(0.0, 1.0) = 0.85;
uniform float triplanar_sharpness : hint_range(1.0, 16.0) = 6.0;

varying vec3 world_pos;
varying vec3 world_nrm;

float hash12(vec2 p)
{
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float vnoise(vec2 p)
{
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);

	float a = hash12(i);
	float b = hash12(i + vec2(1.0, 0.0));
	float c = hash12(i + vec2(0.0, 1.0));
	float d = hash12(i + vec2(1.0, 1.0));

	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

float fbm(vec2 p)
{
	float v = 0.0;
	float a = 0.5;

	// Tres octavas. La cuarta cae por debajo de un texel y solo cuesta.
	for (int i = 0; i < 3; i++)
	{
		v += a * vnoise(p);
		p *= 2.07;
		a *= 0.5;
	}

	return v;
}

// Redondea a la rejilla de texeles. Todo lo que se evalúe después sale en
// bloques cuadrados, que es justo lo que se quiere.
vec2 quantize(vec2 p)
{
	float t = max(texels_per_meter, 1.0);
	return (floor(p * t) + 0.5) / t;
}

// x,y = posición dentro del sillar (0..1); z,w = identificador del sillar.
vec4 brick_cell(vec2 p)
{
	float row = floor(p.y / brick_size.y);
	float shift = mod(row * row_offset, 1.0);
	float u = p.x / brick_size.x + shift;

	return vec4(fract(u), fract(p.y / brick_size.y), floor(u), row);
}

// Relieve. La junta se hunde y el sillar tiene su propio picado.
float height_at(vec2 p)
{
	vec4 cell = brick_cell(p);

	vec2 edge = min(cell.xy, 1.0 - cell.xy) * brick_size;
	float joint = smoothstep(0.0, mortar_width + 0.015, min(edge.x, edge.y));

	float pit = vnoise(p * 13.0) * 0.6 + vnoise(p * 43.0) * 0.4;

	// El sillar sobresale; dentro de la junta el relieve es mucho más suave.
	return mix(pit * 0.25, 0.75 + pit * 0.25, joint);
}

vec3 albedo_at(vec2 p, float detail, out float cavity)
{
	vec4 cell = brick_cell(p);

	vec2 edge = min(cell.xy, 1.0 - cell.xy) * brick_size;
	float joint = smoothstep(0.0, mortar_width + 0.015, min(edge.x, edge.y));

	// Cada sillar tiene su tono. Sin esto la pared es un patrón, no una obra.
	float tone = hash12(cell.zw * 3.7 + 11.3);
	vec3 stone = mix(stone_dark, stone_light, 0.25 + tone * 0.75);

	// Manchas grandes de humedad y hollín viejo.
	float stain = fbm(p * 2.3);
	stone = mix(stone, stone * 0.58, smoothstep(0.42, 0.88, stain));

	// Picado fino. Es lo que sobrevive a la cuantización y da el grano.
	float grit = vnoise(p * 27.0) * 0.65 + vnoise(p * 61.0) * 0.35;
	stone *= mix(1.0, 0.88 + grit * 0.24, detail);

	cavity = joint;

	// La junta reaprovecha el ruido de las manchas en vez de pedir otro: dentro
	// de una junta de tres centímetros nadie va a distinguir cuál era cuál.
	return mix(mortar_color * mix(1.0, 0.55 + stain * 0.9, detail), stone, joint);
}

void vertex()
{
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_nrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment()
{
	vec3 blend = pow(abs(world_nrm), vec3(triplanar_sharpness));
	blend /= max(blend.x + blend.y + blend.z, 0.0001);

	// Cuánto mide un texel en píxeles ahora mismo. Por debajo de uno, el detalle
	// fino y el relieve se apagan: no se pierde nada que se pudiera ver y se
	// quita el hormigueo de las superficies lejanas y a contraluz.
	float texel_px = fwidth(world_pos.x + world_pos.y + world_pos.z) * texels_per_meter;
	float detail = mix(0.3, 1.0, 1.0 - smoothstep(0.7, 2.6, texel_px));
	float texel = 1.0 / max(texels_per_meter, 1.0);

	vec3 albedo = vec3(0.0);
	float cavity = 0.0;

	// Gradiente de superficie: la pendiente del relieve en coordenadas de mundo.
	// Restándole después su componente normal queda un vector tangente, que es lo
	// que dobla la normal sin depender de la geometría ni de las UV.
	vec3 grad = vec3(0.0);

	// Un plano con peso despreciable cuesta exactamente lo mismo que uno que se
	// ve. En geometría alineada a los ejes casi siempre manda uno solo, así que
	// saltarse los otros dos divide el coste del shader casi por tres. La rama es
	// coherente dentro de cada cara, que es cuando a una GPU no le duele.
	if (blend.x > 0.01)
	{
		vec2 p = quantize(world_pos.zy);
		float c;
		albedo += albedo_at(p, detail, c) * blend.x;
		cavity += c * blend.x;

		float h = height_at(p);
		grad += vec3(
			0.0,
			height_at(p + vec2(0.0, texel)) - h,
			height_at(p + vec2(texel, 0.0)) - h) * blend.x;
	}

	if (blend.y > 0.01)
	{
		vec2 p = quantize(world_pos.xz);
		float c;
		albedo += albedo_at(p, detail, c) * blend.y;
		cavity += c * blend.y;

		float h = height_at(p);
		grad += vec3(
			height_at(p + vec2(texel, 0.0)) - h,
			0.0,
			height_at(p + vec2(0.0, texel)) - h) * blend.y;
	}

	if (blend.z > 0.01)
	{
		vec2 p = quantize(world_pos.xy);
		float c;
		albedo += albedo_at(p, detail, c) * blend.z;
		cavity += c * blend.z;

		float h = height_at(p);
		grad += vec3(
			height_at(p + vec2(texel, 0.0)) - h,
			height_at(p + vec2(0.0, texel)) - h,
			0.0) * blend.z;
	}

	grad *= mortar_depth * 14.0 * detail;
	grad -= world_nrm * dot(grad, world_nrm);

	vec3 bumped = normalize(world_nrm - grad);

	// Humedad: sube desde el suelo con un borde irregular, no con una línea.
	float damp_edge = damp_height * (0.6 + fbm(world_pos.xz * 0.7) * 0.8);
	float damp = damp_amount * (1.0 - smoothstep(0.0, max(damp_edge, 0.001), world_pos.y));

	// Verdín: solo donde hay humedad y solo en la junta, que es donde agarra.
	float moss = moss_amount * damp * (1.0 - cavity)
		* smoothstep(0.35, 0.7, vnoise(world_pos.xz * 1.9 + world_pos.y));

	albedo = mix(albedo, damp_color, damp * 0.75);
	albedo = mix(albedo, moss_color, moss);
	albedo = mix(albedo, soot_color, soot_amount * smoothstep(0.3, 0.9, vnoise(world_pos.xz * 1.3 + 5.0)));

	// Posterizar canal a canal produce ruido de color en las zonas oscuras: cada
	// canal da su salto en un sitio distinto y salen píxeles rojos y verdes donde
	// solo debería haber piedra. Se cuantiza el BRILLO y se conserva el tono, que
	// es lo que hacía una paleta indexada de la época: banda sin ensuciar.
	float levels = max(color_levels, 2.0);
	float lum = max(dot(albedo, vec3(0.2126, 0.7152, 0.0722)), 0.0001);
	albedo *= (floor(lum * levels + 0.5) / levels) / lum;

	ALBEDO = albedo;
	NORMAL = normalize((VIEW_MATRIX * vec4(bumped, 0.0)).xyz);

	// Mojado = liso. Es lo único que devuelve un reflejo de la antorcha en toda
	// la sala, y por eso el suelo bajo la luz se lee como piedra húmeda.
	ROUGHNESS = clamp(roughness_base - damp * 0.45, 0.08, 1.0);
	SPECULAR = mix(0.18, 0.45, damp);
	METALLIC = 0.0;

	// Oclusión de la junta. Sin esto la sillería es un dibujo plano.
	AO = mix(1.0 - cavity_ao, 1.0, cavity);
	AO_LIGHT_AFFECT = 0.7;
}
