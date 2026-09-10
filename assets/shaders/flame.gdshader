shader_type spatial;
render_mode blend_add, unshaded, cull_disabled, depth_draw_never, shadows_disabled;

// La llama. Un cuadrado que siempre te mira, con ruido subiendo por dentro.
//
// El billboard gira SOLO en Y. Un billboard esférico se tumba cuando miras al
// suelo y la llama se convierte en una pegatina; anclando el eje vertical, al
// mirar hacia abajo la llama se escorza como haría una llama de verdad.
//
// Aditivo y sin luz: el fuego no se ilumina, ilumina. Como suma en vez de
// mezclar, el `glow` del entorno lo pilla solo y florece sin tener que tocar
// nada más. Iluminar la sala no es cosa suya: eso es la OmniLight3D que va al
// lado. Aquí solo está el trozo que se ve.
//
// Dos ruidos a velocidades distintas: uno lento que da la forma general y otro
// rápido que la rompe. Con uno solo, el fuego late; con dos, hierve.

uniform vec3 core_color : source_color = vec3(1.0, 0.95, 0.72);
uniform vec3 mid_color : source_color = vec3(1.0, 0.52, 0.13);
uniform vec3 edge_color : source_color = vec3(0.62, 0.11, 0.02);

uniform float rise_speed : hint_range(0.0, 6.0) = 1.9;
uniform float noise_scale : hint_range(0.5, 12.0) = 3.4;

// Cuánto se estrecha la llama hacia arriba. Alto = lengua fina.
uniform float taper : hint_range(0.5, 6.0) = 2.2;

uniform float intensity : hint_range(0.0, 8.0) = 2.6;

// Desfase por antorcha. Sin esto todas las llamas del nivel laten a la vez y
// la sala parece que respira, que es exactamente lo que no se quiere.
uniform float seed : hint_range(0.0, 100.0) = 0.0;

uniform float color_levels : hint_range(2.0, 32.0) = 8.0;

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

	for (int i = 0; i < 4; i++)
	{
		v += a * vnoise(p);
		p *= 2.13;
		a *= 0.5;
	}

	return v;
}

void vertex()
{
	// Billboard de eje Y: se queda de pie y solo gira hacia la cámara.
	vec3 to_cam = INV_VIEW_MATRIX[2].xyz;
	vec3 right = normalize(cross(vec3(0.0, 1.0, 0.0), to_cam));
	vec3 fwd = normalize(cross(right, vec3(0.0, 1.0, 0.0)));

	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
		vec4(right, 0.0),
		vec4(0.0, 1.0, 0.0, 0.0),
		vec4(fwd, 0.0),
		MODEL_MATRIX[3]);
}

void fragment()
{
	vec2 uv = UV;

	// v = 0 en la base, 1 en la punta.
	float v = 1.0 - uv.y;
	float centre = abs(uv.x - 0.5) * 2.0;

	float t = TIME * rise_speed + seed;

	// Lento: la forma. Rápido y más pequeño: el hervor.
	float slow = fbm(vec2(uv.x * noise_scale, uv.y * noise_scale * 1.6 - t));
	float fast = fbm(vec2(uv.x * noise_scale * 2.7 + seed, uv.y * noise_scale * 3.4 - t * 2.1));
	float turbulence = slow * 0.68 + fast * 0.32;

	// La lengua se estrecha con la altura y se deshace por arriba.
	float width = pow(1.0 - v, 1.0 / taper);
	float body = (1.0 - centre / max(width, 0.001)) * (1.0 - v * 0.25);
	float shape = body * (0.55 + turbulence * 0.9) - v * 0.35;

	float mask = smoothstep(0.02, 0.42, shape);

	if (mask <= 0.001)
	{
		discard;
	}

	// El corazón es blanco, el cuerpo naranja y la punta se apaga en rojo.
	float heat = clamp(shape * 1.5 - v * 0.55, 0.0, 1.0);
	vec3 col = mix(edge_color, mid_color, smoothstep(0.05, 0.45, heat));
	col = mix(col, core_color, smoothstep(0.55, 0.95, heat));

	float levels = max(color_levels, 2.0);
	col = floor(col * levels + 0.5) / levels;

	ALBEDO = vec3(0.0);
	EMISSION = col * mask * intensity;
	ALPHA = mask;
}
