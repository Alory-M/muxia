shader_type canvas_item;
render_mode unshaded;

uniform vec2 viewport_size = vec2(1152.0, 648.0);
uniform vec2 player_screen = vec2(576.0, 324.0);
uniform float pixels_per_world = 2.4;
uniform float world_radius = 70.0;

void fragment() {
    float distance_world = length(SCREEN_UV * viewport_size - player_screen) / pixels_per_world;
    // 羽化只发生在可见半径以内，外圈完全遮蔽；HUD位于更高的独立画布。
    float darkness = smoothstep(world_radius - 14.0, world_radius, distance_world);
    COLOR = vec4(0.005, 0.008, 0.012, darkness);
}
