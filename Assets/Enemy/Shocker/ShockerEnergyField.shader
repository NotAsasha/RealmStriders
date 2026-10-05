Shader "Enemy/ShockerEnergyField"
{
    Properties
    {
        _Color          ("Color", Color)                  = (0.2, 0.8, 1.0, 1.0)
        _FresnelPower   ("Fresnel Power", Range(0.5, 8))  = 3.0
        _ScanlineFreq   ("Scanline Frequency", Range(1, 80))  = 30.0
        _ScanlineWidth  ("Scanline Width", Range(0.01, 0.5))  = 0.08
        _NoiseScale     ("Noise Scale", Range(1, 40))     = 10.0
        _Progress       ("Expansion Progress (0-1)", Range(0, 1)) = 0.0
        _Alpha          ("Alpha", Range(0, 1))            = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent+10"
        }

        Pass
        {
            Name "ShockerEnergyField"
            Tags { "LightMode" = "UniversalForward" }

            // Additive blend: glows and accumulates on top of scene
            Blend One One
            ZWrite Off
            // Cull Back (default): render outer surface of the sphere,
            // which is the side visible to the player as the shell expands outward.
            Cull Back
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                half   _FresnelPower;
                half   _ScanlineFreq;
                half   _ScanlineWidth;
                half   _NoiseScale;
                half   _Progress;
                half   _Alpha;
            CBUFFER_END

            // Simple hash for per-scanline noise
            float hash(float n) { return frac(sin(n) * 43758.5453); }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   norInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.normalWS    = norInputs.normalWS;
                OUT.viewDirWS   = GetWorldSpaceViewDir(posInputs.positionWS);
                OUT.positionWS  = posInputs.positionWS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                // --- Fresnel rim ---
                // NdotV = 1 at center (facing camera directly) → fresnel = 0 (dark)
                // NdotV = 0 at edges (perpendicular to camera) → fresnel = 1 (bright rim)
                half NdotV   = saturate(dot(N, V));
                half fresnel = pow(1.0 - NdotV, _FresnelPower);

                // --- Horizontal scanlines ---
                // World Y keeps lines horizontal regardless of sphere rotation
                float scanCoord = IN.positionWS.y * _ScanlineFreq;
                float scanEdge  = abs(frac(scanCoord) - 0.5);
                float scanBand  = 1.0 - smoothstep(0.0, _ScanlineWidth, scanEdge);

                // Per-band flickering noise
                float bandNoise = lerp(0.5, 1.0, hash(floor(scanCoord) + _Progress * 37.3));
                scanBand *= bandNoise;

                // --- Electric crackle along the rim ---
                float crackleA = hash(IN.positionWS.x * _NoiseScale + _Progress * 11.1);
                float crackleB = hash(IN.positionWS.z * _NoiseScale + _Progress *  7.3);
                float crackle  = fresnel * (0.6 + 0.4 * (crackleA + crackleB));

                // --- Combine: rim dominates, scanlines fade as shell expands ---
                half intensity = saturate(crackle + scanBand * (1.0 - _Progress) * 0.5);

                // Fade: fast bright pulse at t=0, non-linear tail
                half fade = (1.0 - _Progress * _Progress) * _Alpha;

                // Multiply by 2 so additive blend achieves a bright glow
                return half4(_Color.rgb * intensity * fade * 2.0, 0.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

