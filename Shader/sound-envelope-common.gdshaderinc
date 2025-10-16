//采样频率区间数量
#define NUMBER_OF_BINS 64
//包络数量
#define NUMBER_OF_HISTORY_ROWS 5

const float amplitude_scale = 100.0;//幅度缩放
const float isometric_zoom   = 3.0;//等轴缩放(镜头)
const float row_spacing      = 8.0;//间距

//等轴视角投射
vec2 project_envelope(vec3 envelope_coordinate) {
	float bin_index       = envelope_coordinate.x;
	float envelope_value  = envelope_coordinate.y;
	float history_row     = envelope_coordinate.z;
	float effective_row   = history_row * row_spacing;

    float projected_x = bin_index - effective_row;
	float projected_y = (bin_index + effective_row) * 0.5 - envelope_value * amplitude_scale;
	return vec2(projected_x, projected_y) * isometric_zoom;
}
//中心
vec2 compute_envelope_grid_center() {
	vec2 projected_bottom_left  = project_envelope(vec3(0.0, 0.0, 0.0));
	vec2 projected_bottom_right = project_envelope(vec3(float(NUMBER_OF_BINS - 1), 0.0, 0.0));
	vec2 projected_top_left     = project_envelope(vec3(0.0, 1.0, float(NUMBER_OF_HISTORY_ROWS - 1)));
	vec2 projected_top_right    = project_envelope(vec3(float(NUMBER_OF_BINS - 1), 1.0, float(NUMBER_OF_HISTORY_ROWS - 1)));

	vec2 minimum_corner = min(min(projected_bottom_left, projected_bottom_right),
	min(projected_top_left, projected_top_right));
	vec2 maximum_corner = max(max(projected_bottom_left, projected_bottom_right),
	max(projected_top_left, projected_top_right));
	return (minimum_corner + maximum_corner) * 0.5;
}
//计算点到直线距离
float distance_to_line(vec2 pixel_coordinate, vec2 line_start, vec2 line_end) {
	vec2 line_vector = line_end - line_start;
	float line_length_squared = dot(line_vector, line_vector);
	float projection_factor = dot(pixel_coordinate - line_start, line_vector) / line_length_squared;
	float clamped_projection_factor = clamp(projection_factor, 0.0, 1.0);
	vec2 closest_point = line_start + clamped_projection_factor * line_vector;
	return distance(pixel_coordinate, closest_point);
}


