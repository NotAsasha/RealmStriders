Shader "ProjectPortal/Capture Implosion"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Energy Color", Color) = (0.1, 0.8, 1, 1)
        _CaptureProgress("Capture Progress", Range(0, 1)) = 0
        _CaptureCenterWS("Capture Center", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "CaptureImplosion"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                float _CaptureProgress;
                float3 _CaptureCenterWS;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 originalPositionWS : TEXCOORD2;
            };

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float collapse = smoothstep(0.02, 0.92, _CaptureProgress);
                float3 centerOS = TransformWorldToObject(_CaptureCenterWS);
                float3 positionOS = lerp(input.positionOS.xyz, centerOS, collapse);
                output.positionWS = TransformObjectToWorld(positionOS);
                output.originalPositionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float noise = Hash31(floor(input.originalPositionWS * 8.0));
                clip(noise - _CaptureProgress);

                float energy = saturate(_CaptureProgress * 2.2) * (1.0 - _CaptureProgress * 0.45);
                half3 color = baseSample.rgb + _EmissionColor.rgb * energy;
                half alpha = baseSample.a * (1.0 - _CaptureProgress * 0.55);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
