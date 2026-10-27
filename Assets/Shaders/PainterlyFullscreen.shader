Shader "Game/Post Processing/Painterly Fullscreen"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 1)) = 0.85
        _StrokeRadius ("Stroke Radius (pixels at 1080p)", Range(1, 12)) = 4.5
        _StrokeStretch ("Stroke Stretch", Range(1, 3)) = 1.5
        _ToneSteps ("Tone Steps", Range(4, 32)) = 12
        _ToneStrength ("Tone Simplification", Range(0, 1)) = 0.25
        _ColorVariation ("Brush Color Variation", Range(0, 0.15)) = 0.035
        _CanvasStrength ("Canvas Texture", Range(0, 0.1)) = 0.018
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "Painterly Fullscreen"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _StrokeRadius;
                float _StrokeStretch;
                float _ToneSteps;
                float _ToneStrength;
                float _ColorVariation;
                float _CanvasStrength;
            CBUFFER_END

            float Hash(float2 p)
            {
                float3 h = frac(float3(p.xyx) * 0.1031);
                h += dot(h, h.yzx + 33.33);
                return frac((h.x + h.y) * h.z);
            }

            float BrushNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                            lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), f.x), f.y);
            }

            float Luma(float3 c)
            {
                return dot(c, float3(0.2126, 0.7152, 0.0722));
            }

            float4 ReadColor(float2 uv)
            {
                // Clamp within the active viewport, including RTHandle scaling.
                float2 halfTexel = _BlitTexture_TexelSize.xy * 0.5;
                float2 limit = _BlitScaleBias.xy + _BlitScaleBias.zw;
                uv = clamp(uv, _BlitScaleBias.zw + halfTexel, limit - halfTexel);
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 original = ReadColor(uv);
                UNITY_BRANCH
                if (_Intensity <= 0.0)
                    return original;

                float2 pixel = (uv - _BlitScaleBias.zw) / _BlitScaleBias.xy;
                pixel *= float2(_ScreenParams.x / _ScreenParams.y, 1.0) * 1080.0;
                float brush = BrushNoise(pixel / float2(23.0, 9.0));
                float angle = (brush - 0.5) * 1.2 + 0.45;
                float sn, cs;
                sincos(angle, sn, cs);
                float2 axisX = float2(cs, sn) * _StrokeStretch;
                float2 axisY = float2(-sn, cs);
                float2 stepUV = _BlitScaleBias.xy / _ScreenParams.xy;
                stepUV *= (_ScreenParams.y / 1080.0) * _StrokeRadius * 0.5;

                float3 sums[4];
                float2 moments[4];
                [unroll]
                for (int index = 0; index < 4; index++)
                {
                    sums[index] = float3(0, 0, 0);
                    moments[index] = float2(0, 0);
                }
                // Four overlapping 3x3 regions share a fixed 5x5 footprint.
                // Low-variance regions flatten texture without averaging across silhouettes.
                [unroll]
                for (int y = -2; y <= 2; y++)
                {
                    [unroll]
                    for (int x = -2; x <= 2; x++)
                    {
                        float3 color = ReadColor(uv + (axisX * x + axisY * y) * stepUV).rgb;
                        float lum = Luma(color);
                        float2 moment = float2(lum, lum * lum);
                        if (x <= 0 && y <= 0) { sums[0] += color; moments[0] += moment; }
                        if (x >= 0 && y <= 0) { sums[1] += color; moments[1] += moment; }
                        if (x <= 0 && y >= 0) { sums[2] += color; moments[2] += moment; }
                        if (x >= 0 && y >= 0) { sums[3] += color; moments[3] += moment; }
                    }
                }

                float3 painted = 0;
                float totalWeight = 0;
                [unroll]
                for (int region = 0; region < 4; region++)
                {
                    float2 m = moments[region] / 9.0;
                    float variance = max(0.0, m.y - m.x * m.x);
                    // Continuous weights avoid abrupt quadrant switches as the camera moves.
                    float weight = rcp(1.0 + variance * 8000.0);
                    weight *= weight;
                    painted += sums[region] / 9.0 * weight;
                    totalWeight += weight;
                }
                painted /= max(totalWeight, 1e-8);

                float luminance = max(Luma(painted), 1e-5);
                float perceptual = sqrt(luminance);
                float quantized = floor(perceptual * _ToneSteps + 0.5) / _ToneSteps;
                painted *= lerp(1.0, quantized * quantized / luminance, _ToneStrength);
                painted *= 1.0 + (brush - 0.5) * _ColorVariation;
                // Static screen-space weave: no animated noise or per-frame flicker.
                float canvas = sin(pixel.x * 2.4) * sin(pixel.y * 2.4);
                painted *= 1.0 + canvas * _CanvasStrength;
                return float4(lerp(original.rgb, max(painted, 0.0), _Intensity), original.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
