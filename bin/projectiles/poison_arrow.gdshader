shader_type canvas_item;

// 与毒箭僵尸立绘的红色箭头、紫色箭杆一致；保留箭矢自身的透明边缘与阴影。
uniform vec4 head_color : source_color = vec4(1.0, 0.08, 0.18, 1.0);
uniform vec4 shaft_color : source_color = vec4(0.48, 0.16, 0.95, 1.0);

void fragment() {
    vec4 texel = texture(TEXTURE, UV);
    float brightness = dot(texel.rgb, vec3(0.299, 0.587, 0.114));
    vec3 tint = mix(shaft_color.rgb, head_color.rgb, smoothstep(0.79, 0.85, UV.x));
    COLOR = vec4(tint * (0.35 + 0.9 * brightness), texel.a);
}
