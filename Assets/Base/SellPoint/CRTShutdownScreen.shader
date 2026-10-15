Shader "ProjectPortal/CRTShutdownScreen"
{
    Properties
    {
        _Progress("Shutdown Progress", Range(0, 1)) = 0.0
        [HDR] _GlowColor("Glow Color", Color) = (0.3, 1.5, 2.5, 1.0)
        _LineThickness("Line Thickness", Range(0.001, 0.08)) = 0.015
        _GlowIntensity("Glow Intensity", Range(1.0, 30.0)) = 10.0
        _GlowFalloff("Glow Falloff", Range(5.0, 100.0)) = 35.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "CRTShutdownPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GlowColor;
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
                // Idle state: absolutely zero rendering impact
                if (_Progress <= 0.001)
                {
                    discard;
                }

                float p = saturate(_Progress);

                // --- Calculate CRT collapse dimensions ---
                float halfThickness = max(0.001, _LineThickness * 0.5);
                float h = 0.5;
                float w = 0.5;
                float fade = 1.0;

                if (p <= 0.6)
                {
                    // Phase 1 (0.0 -> 0.6): Vertical squeeze into thin horizontal line
                    float t1 = p / 0.6;
                    float easeY = t1 * t1; // Snappy CRT cathode compression
                    h = lerp(0.5, halfThickness, easeY);
                    w = 0.5;
                }
                else if (p <= 0.9)
                {
                    // Phase 2 (0.6 -> 0.9): Horizontal collapse of the line into central dot
                    float t2 = (p - 0.6) / 0.3;
                    float easeX = t2 * t2;
                    h = lerp(halfThickness, halfThickness * 0.5, t2);
                    w = lerp(0.5, halfThickness, easeX);
                }
                else
                {
                    // Phase 3 (0.9 -> 1.0): Dissipation of the dot glow and opening transparency
                    float t3 = (p - 0.9) / 0.1;
                    fade = 1.0 - smoothstep(0.0, 1.0, t3);
                    h = halfThickness * 0.5 * fade;
                    w = halfThickness * fade;
                }

                // UV coordinates relative to Quad center (range: -0.5 .. 0.5)
                float2 d = abs(input.uv - float2(0.5, 0.5));

                // Screen coordinates and quad center projection
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 0.00001);
                float4 centerCS = TransformObjectToHClip(float3(0, 0, 0));
                float2 centerScreenUV = ComputeScreenPos(centerCS).xy / max(centerCS.w, 0.00001);
                float2 screenOffset = screenUV - centerScreenUV;

                // Squeeze scene color towards quad center
                float scaleX = 0.5 / max(w, 0.0005);
                float scaleY = 0.5 / max(h, 0.0005);
                float2 squeezedUV = centerScreenUV + screenOffset * float2(scaleX, scaleY);
                squeezedUV = saturate(squeezedUV);

                float3 sceneColor = SampleSceneColor(squeezedUV);

                // Phosphor beam energy concentration
                float energyConcentration = 1.0 + (0.25 / max(w * h * 4.0, 0.015)) * 0.4;
                energyConcentration = min(energyConcentration, 8.0);

                bool isInside = (d.x <= w) && (d.y <= h);

                float3 finalColor;
                if (isInside)
                {
                    // Slit interior: compressed background boosted by cathode luminescence
                    float3 phosphorTint = _GlowColor.rgb * (energyConcentration * 0.15 * fade);
                    finalColor = (sceneColor * energyConcentration + phosphorTint) * fade;
                }
                else
                {
                    // Exterior: Solid black CRT screen + phosphor edge glow
                    float distX = max(0.0, d.x - w);
                    float distY = max(0.0, d.y - h);
                    float distOutside = length(float2(distX, distY));

                    float glow = exp(-distOutside * _GlowFalloff) * _GlowIntensity * fade;
                    float3 edgeGlow = _GlowColor.rgb * glow;

                    finalColor = edgeGlow;
                }

                // Alpha is 1.0 during active shutdown phases, dissolving back to transparent in Phase 3
                float alpha = fade;

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

