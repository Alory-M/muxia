shader_type canvas_item;

// The controller advances this value only while the world is running.
uniform float glow_strength : hint_range(0.0, 1.0) = 0.0;
uniform vec4 glow_color : source_color = vec4(1.0, 0.78, 0.34, 1.0);
varying vec4 item_tint;

void vertex() {
	item_tint = COLOR;
}

void fragment() {
	vec4 original = texture(TEXTURE, UV) * item_tint;
	vec2 radius = TEXTURE_PIXEL_SIZE * 12.0;
	float near_alpha = 0.0;
	near_alpha = max(near_alpha, texture(TEXTURE, UV + vec2(radius.x, 0.0)).a);
	near_alpha = max(near_alpha, texture(TEXTURE, UV - vec2(radius.x, 0.0)).a);
	near_alpha = max(near_alpha, texture(TEXTURE, UV + vec2(0.0, radius.y)).a);
	near_alpha = max(near_alpha, texture(TEXTURE, UV - vec2(0.0, radius.y)).a);
	near_alpha = max(near_alpha, texture(TEXTURE, UV + radius * 0.7).a);
	near_alpha = max(near_alpha, texture(TEXTURE, UV - radius * 0.7).a);
	float edge = max(near_alpha * item_tint.a - original.a, 0.0) * glow_strength;
	vec3 lit = mix(original.rgb, glow_color.rgb, glow_strength * 0.20);
	COLOR = vec4(mix(lit, glow_color.rgb, edge), max(original.a, edge * 0.85));
}
