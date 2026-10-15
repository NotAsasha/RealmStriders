Shader "ProjectPortal/CRTPortalScreen"
{
    Properties
    {
        [MainTexture] _MainTex("Portal Texture", 2D) = "black" {}
        _Progress("Transition Progress (0=Open, 1=Closed)", Range(0, 1)) = 0.0
        [HDR] _GlowColor("Glow Color", Color) = (0.3, 1.5, 2.5, 1.0)
        _LineThickness("Line Thickness", Range(0.001, 0.08)) = 0.015
        _GlowIntensity("Glow Intensity", Range(1.0, 30.0)) = 10.0
        _GlowFalloff("Glow Falloff", Range(5.0, 100.0)) = 35.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+450"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "CRTPortalPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _GlowColor;
                float4 _MainTex_ST;
                float _Progress;
                float _LineThickness;
                float _GlowIntensity;
                float _GlowFalloff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Fully open idle state: direct screen-space projection of portal texture without extra overhead
                if (_Progress <= 0.0001)
                {
                    float2 directScreenUV = input.screenPos.xy / max(input.screenPos.w, 0.00001);
                    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, directScreenUV);
                }

                // Fully closed idle state: clean black portal screen
                if (_Progress >= 0.999)
                {
                    return half4(0.0, 0.0, 0.0, 1.0);
                }

                float p = saturate(_Progress);

                // --- Calculate CRT collapse / expand dimensions ---
                float halfThickness = max(0.001, _LineThickness * 0.5);
                float h = 0.5;
                float w = 0.5;
                float fade = 1.0;

                if (p <= 0.6)
                {
                    // Phase 1 (0.0 -> 0.6): Vertical squeeze into thin horizontal line
                    // Opening reverse (0.6 -> 0.0): Horizontal line expands vertically to full height
                    float t1 = p / 0.6;
                    float easeY = t1 * t1; // Snappy CRT cathode compression
                    h = lerp(0.5, halfThickness, easeY);
                    w = 0.5;
                }
                else if (p <= 0.9)
                {
                    // Phase 2 (0.6 -> 0.9): Horizontal collapse of the line into central dot
                    // Opening reverse (0.9 -> 0.6): Central dot shoots out horizontally across portal width
                    float t2 = (p - 0.6) / 0.3;
                    float easeX = t2 * t2;
                    h = lerp(halfThickness, halfThickness * 0.5, t2);
                    w = lerp(0.5, halfThickness, easeX);
                }
                else
                {
                    // Phase 3 (0.9 -> 1.0): Dissipation of the dot glow into black
                    // Opening reverse (1.0 -> 0.9): Dot sparks and reaches full luminescence
                    float t3 = (p - 0.9) / 0.1;
                    fade = 1.0 - smoothstep(0.0, 1.0, t3);
                    h = halfThickness * 0.5 * fade;
                    w = halfThickness * fade;
                }

                // UV coordinates relative to Quad center (range: -0.5 .. 0.5)
                float2 d = abs(input.uv - float2(0.5, 0.5));

                // Screen coordinates and quad center projection
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 0.00001);
                float4 centerCS = TransformObjectToHClip(float3(0.0, 0.0, 0.0));
                float2 centerScreenUV = ComputeScreenPos(centerCS).xy / max(centerCS.w, 0.00001);
                float2 screenOffset = screenUV - centerScreenUV;

                // Squeeze portal screen projection towards quad center
                float scaleX = 0.5 / max(w, 0.0005);
                float scaleY = 0.5 / max(h, 0.0005);
                float2 squeezedUV = centerScreenUV + screenOffset * float2(scaleX, scaleY);
                squeezedUV = saturate(squeezedUV);

                float4 portalColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, squeezedUV);

                // Phosphor beam energy concentration
                float energyConcentration = 1.0 + (0.25 / max(w * h * 4.0, 0.015)) * 0.4;
                energyConcentration = min(energyConcentration, 8.0);

                bool isInside = (d.x <= w) && (d.y <= h);

                float3 finalColor;
                if (isInside)
                {
                    // Slit interior: compressed portal image boosted by cathode luminescence
                    // openTransition smoothly fades extra glow as portal settles to 0
                    float openTransition = smoothstep(0.0, 0.12, p);
                    float boost = (energyConcentration - 1.0) * openTransition;
                    float3 phosphorTint = _GlowColor.rgb * (energyConcentration * 0.15 * fade * openTransition);

                    finalColor = (portalColor.rgb * (1.0 + boost) + phosphorTint) * fade;
                }
                else
                {
                    // Exterior: Solid dark CRT screen + phosphor edge glow
                    float distX = max(0.0, d.x - w);
                    float distY = max(0.0, d.y - h);
                    float distOutside = length(float2(distX, distY));

                    float glow = exp(-distOutside * _GlowFalloff) * _GlowIntensity * fade;
                    float3 edgeGlow = _GlowColor.rgb * glow;

                    finalColor = edgeGlow;
                }

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
