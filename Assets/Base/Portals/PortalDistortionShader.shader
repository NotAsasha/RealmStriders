Shader "Custom/PortalDistortion"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _PortalDistortionIntensity ("Intensity", Range(0, 1)) = 0
        _PortalDistortionTime ("Time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "PortalDistortion"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _PortalDistortionIntensity; // 0..1
                float _PortalDistortionTime;
            CBUFFER_END

            // Fast pseudo-random hash
            float2 Hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float ValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash22(i).x;
                float b = Hash22(i + float2(1, 0)).x;
                float c = Hash22(i + float2(0, 1)).x;
                float d = Hash22(i + float2(1, 1)).x;

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float power = saturate(_PortalDistortionIntensity);
                if (power <= 0.0001)
                {
                    return SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);
                }

                float t = _PortalDistortionTime;
                float2 uv = input.texcoord;
                float2 center = float2(0.5, 0.5);
                float2 delta = uv - center;
                float dist = length(delta);
                float2 dir = dist > 0.0001 ? (delta / dist) : float2(0, 0);

                // 1. Organic noise turbulence
                float2 noiseUV = uv * 3.5 + float2(t * 0.4, t * 0.25);
                float n1 = (ValueNoise(noiseUV) - 0.5) * 2.0;
                float n2 = (ValueNoise(noiseUV * 1.5 + float2(3.1, 7.4)) - 0.5) * 2.0;
                float2 noiseOffset = float2(n1, n2) * (power * 0.025);

                // 2. Radial shockwave / lens warp (pinch towards center + expanding wave)
                float wave = sin(dist * 18.0 - t * 12.0) * 0.5 + 0.5;
                float pinch = (1.0 - smoothstep(0.0, 0.8, dist)) * 0.08 * power;
                float waveDisplacement = wave * 0.035 * power * smoothstep(0.0, 0.5, dist);

                float2 warpedUV = uv + dir * (pinch - waveDisplacement) + noiseOffset;

                // 3. Radial chromatic aberration (stronger towards edges & at peak power)
                float chromaStrength = power * (0.025 + dist * 0.035);
                float2 chromaDelta = dir * chromaStrength;

                half r = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, warpedUV + chromaDelta).r;
                half g = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, warpedUV).g;
                half b = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, warpedUV - chromaDelta).b;
                half a = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, warpedUV).a;

                half3 col = half3(r, g, b);

                // 4. Subtle radial blur / streak at high intensity (blend samples along direction)
                float2 blurDelta = dir * (power * 0.015);
                half3 blurSample1 = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, warpedUV + blurDelta).rgb;
                half3 blurSample2 = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, warpedUV - blurDelta).rgb;
                col = lerp(col, (col + blurSample1 + blurSample2) * 0.3333, power * 0.6);

                // 5. Subtle vignette at edges during transition
                float vignette = smoothstep(0.8, 0.2, dist);
                col *= lerp(1.0, vignette, power * 0.35);

                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
