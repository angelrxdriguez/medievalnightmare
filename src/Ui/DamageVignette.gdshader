shader_type canvas_item;

// Viñeta roja en el borde de la pantalla. El centro siempre queda limpio: es
// aviso, no obstáculo. La intensidad la lleva DamageVignette.cs.

uniform float intensity : hint_range(0.0, 1.0) = 0.0;
uniform vec3 tint : source_color = vec3(0.62, 0.03, 0.03);
uniform float inner_edge : hint_range(0.0, 1.0) = 0.22;
uniform float outer_edge : hint_range(0.0, 1.5) = 0.72;

void fragment() {
	float distance_to_center = length(UV - vec2(0.5));
	float edge = smoothstep(inner_edge, outer_edge, distance_to_center);
	COLOR = vec4(tint, edge * intensity);
}
