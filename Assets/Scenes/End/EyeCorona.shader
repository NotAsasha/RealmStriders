Shader "End/Eye Corona"
{
    Properties
    {
        [HDR] _Color ("Ember", Color) = (2.6, 0.8, 0.14, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 disc : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.disc = input.uv * 2.0 - 1.0;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float radius = length(input.disc);
                float angle = atan2(input.disc.y, input.disc.x);
                float time = _Time.y;
                float center = 0.77 + 0.009 * sin(5.0 * angle + time * 0.29)
                                    + 0.004 * sin(11.0 * angle - time * 0.47);
                float distanceToRing = abs(radius - center);
                float lineWidth = max(fwidth(radius), 0.002);
                float core = 1.0 - smoothstep(0.002, 0.002 + lineWidth, distanceToRing);
                float gas = exp2(-55.0 * distanceToRing);
                float filaments = 0.55 + 0.45 * sin(17.0 * angle - time * 0.7 + sin(5.0 * angle + time * 0.2));
                float arcs = exp2(-160.0 * abs(radius - center - 0.067))
                           * pow(saturate(0.5 + 0.5 * cos(3.0 * angle + time * 0.19)), 4.0);
                return half4(_Color.rgb * (core + gas * (0.2 + 0.6 * filaments) + arcs * 0.55), 0);
            }
            ENDHLSL
        }
    }
}
