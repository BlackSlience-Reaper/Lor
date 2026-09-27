shader_type canvas_item;

// 使用原图 alpha 和亮色线条去除光晕；已处理的楼层线稿直接使用 alpha。
uniform bool draw_outline = false;
uniform bool extract_lines = true;
// 仅九个楼层接待节点使用实心填充；苍蓝与外描边沿用原有路径。
uniform bool fill_enabled = false;
uniform sampler2D fill_mask : filter_linear, repeat_disable;
uniform vec4 detail_color : source_color = vec4(1.0);
// 普通地图节点原生初始化会写入此参数；线条颜色由节点状态提供。
uniform vec3 map_color : source_color;
varying vec4 icon_tint;

void vertex() {
    icon_tint = COLOR;
}

float coverage(sampler2D icon_texture, vec2 uv) {
    if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0)))) {
        return 0.0;
    }
    vec4 pixel = texture(icon_texture, uv);
    float line = extract_lines ? smoothstep(0.45, 0.75, pixel.b) : 1.0;
    return pixel.a * line;
}

void fragment() {
    float alpha = coverage(TEXTURE, UV);
    vec3 tint = icon_tint.rgb;
    if (fill_enabled) {
        // 主体使用楼层色，原有线条使用对应楼层的对比色保留内部细节。
        float line = smoothstep(0.05, 0.5, alpha);
        tint = mix(tint, detail_color.rgb, line);
        alpha = max(texture(fill_mask, UV).a, line);
    }
    if (draw_outline) {
        // 按原始图片像素计算半径 4 像素的圆形膨胀描边。
        for (float y = -4.0; y <= 4.0; y += 1.0) {
            for (float x = -4.0; x <= 4.0; x += 1.0) {
                vec2 offset = vec2(x, y);
                if (length(offset) <= 4.0) {
                    alpha = max(alpha, coverage(TEXTURE, UV + offset * TEXTURE_PIXEL_SIZE));
                }
            }
        }
    }
    COLOR = vec4(tint, icon_tint.a * alpha);
}
