shader_type canvas_item;
render_mode blend_disabled;

#include "res://Shader/sound-envelope-common.gdshaderinc"

uniform vec2 iResolution;
uniform int iFrame;
uniform sampler2D iChannel0; // display texture
uniform sampler2D iChannel1; // audio texture

#define WAVEFORM_SAMPLE_COUNT 512

uniform float uUpdateInterval = 10.0; //interval update frequency
uniform float uEnvelopeSmoothing = 0.4; //0.3-0.5 recommend
uniform float uCompressK = 1.0;

const int samples_per_bin = WAVEFORM_SAMPLE_COUNT / NUMBER_OF_BINS;

float sample_waveform_envelope(int bin_index) {
	float sum_amplitude = 0.0;
	for (int i = 0; i < samples_per_bin; i++) {
		float sample_x = (float(bin_index * samples_per_bin + i) + 0.5) / float(WAVEFORM_SAMPLE_COUNT);
		float sample_value = texture(iChannel1, vec2(sample_x, 1.0)).r;
		sum_amplitude += abs(sample_value);
	}
	return sum_amplitude / float(samples_per_bin);
}

vec4 shift_envelope_history(vec2 uv_coordinates, float row_height) {
	return texture(iChannel0, uv_coordinates + vec2(0.0, row_height));
}

float compress_envelope(float x) {
    float k = max(uCompressK, 0.0);
    return x / (1.0 + k * x);
}
float smoothEnvelope(float old, float current, float smoothing) {
    return (current * smoothing) + (old * (1.0 - smoothing));
}
void fragment() {
	vec2 uv = FRAGCOORD.xy / iResolution.xy;

	float row_height = 1.0 / float(NUMBER_OF_HISTORY_ROWS);

	if (uv.y < 1.0 - row_height) {
		COLOR = shift_envelope_history(uv, row_height);
	} else {
		float update_interval = max(uUpdateInterval, 1.0);
		float blend_factor = mod(float(iFrame), update_interval) / update_interval;

		int bin_index = int(floor(uv.x * float(NUMBER_OF_BINS)));
		float new_envelope = sample_waveform_envelope(bin_index);
		float old_envelope = texture(iChannel0, uv).r;

		float effective_blend = clamp(blend_factor * uEnvelopeSmoothing, 0.0, 1.0);

		// 应用平滑处理
		float smoothed_envelope = smoothEnvelope(old_envelope, new_envelope, uEnvelopeSmoothing);
		smoothed_envelope = compress_envelope(smoothed_envelope);

		// 使用平滑后的值作为最终输出
		float blended_envelope = mix(old_envelope, smoothed_envelope, effective_blend);
		COLOR = vec4(blended_envelope, blended_envelope, blended_envelope, 1.0);
	}
}


