// Rejilla procedural en espacio de mundo para la fase de greybox.
// Triplanar: se proyecta sobre los tres ejes, así que la cuadrícula mide
// exactamente 1 m en cualquier CSG sin tocar UVs ni escalas.
shader_type spatial;
render_mode cull_back, diffuse_burley, specular_schlick_ggx;

uniform vec3 base_color : source_color = vec3(0.55, 0.55, 0.55);
uniform vec3 line_color : source_color = vec3(0.28, 0.28, 0.28);
uniform float grid_size = 1.0;
uniform float line_width = 0.02;
uniform float roughness_value : hint_range(0.0, 1.0) = 0.9;

varying vec3 world_pos;
varying vec3 world_normal;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

float grid_axis(vec2 p) {
	vec2 dist = abs(fract(p / grid_size - 0.5) - 0.5) * grid_size;
	vec2 fw = fwidth(p) * 0.5 + 0.00001;
	vec2 line = smoothstep(vec2(0.0), max(vec2(line_width), fw), dist);
	return 1.0 - min(line.x, line.y);
}

void fragment() {
	vec3 blend = abs(normalize(world_normal));
	blend /= (blend.x + blend.y + blend.z);
	float g = grid_axis(world_pos.zy) * blend.x
			+ grid_axis(world_pos.xz) * blend.y
			+ grid_axis(world_pos.xy) * blend.z;
	ALBEDO = mix(base_color, line_color, clamp(g, 0.0, 1.0));
	ROUGHNESS = roughness_value;
	SPECULAR = 0.15;
}
