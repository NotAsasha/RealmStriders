Shader "ProjectPortal/Capture Particles"
{
    Properties
    {
        [HDR] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [PerRendererData] _CaptureRing("Impact Ring", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "CaptureParticles"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _CaptureRing;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _BaseColor;
                output.uv = input.uv * 2.0 - 1.0;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half falloff = saturate(1.0h - dot(input.uv, input.uv));
                half core = falloff * falloff;
                if (_CaptureRing > 0.5h)
                {
                    float edge = length(input.uv) - 0.83;
                    float width = max(fwidth(edge), 0.025);
                    half rim = 1.0h - smoothstep(0.0, width + 0.035, abs(edge));
                    return half4(input.color.rgb, input.color.a * rim);
                }
                return half4(input.color.rgb * (1.0h + core), input.color.a * core);
            }
            ENDHLSL
        }
    }
}
