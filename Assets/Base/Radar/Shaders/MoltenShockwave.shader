Shader "ProjectPortal/VFX/MoltenShockwave"
{
    Properties
    {
        [Header(HDR Thermal Palette)]
        [HDR] _CoreColor ("Leading Edge Color (White-Hot Plasma)", Color) = (8.0, 6.0, 2.0, 1.0)
        [HDR] _MainColor ("Middle Ring Color (Molten Orange)", Color) = (5.0, 1.6, 0.1, 1.0)
        [HDR] _RimColor ("Trailing Tail Color (Smoky Crimson)", Color) = (1.5, 0.25, 0.03, 1.0)
        _Intensity ("Global Emission Intensity", Range(0.1, 10.0)) = 1.5

        [Header(Shockwave Expansion and Timing)]
        _Progress ("Shockwave Expansion Progress (0..1)", Range(0.0, 1.0)) = 0.35
        [Toggle] _UseAutoTime ("Auto-Animate Progress with Time", Float) = 0.0
        _AutoSpeed ("Auto-Animation Frequency", Range(0.05, 5.0)) = 0.75
        _MaxRadius ("Max UV Radius", Range(0.2, 1.0)) = 0.92

        [Header(Concentric Ring Profile)]
        _RingWidth ("Shockwave Ring Thickness", Range(0.02, 0.6)) = 0.24
        _LeadingEdgeSharpness ("White-Hot Front Edge Sharpness", Range(2.0, 32.0)) = 14.0
        _TrailingFalloff ("Smoky Crimson Tail Falloff Power", Range(0.5, 6.0)) = 2.2
        _SecondaryWaveIntensity ("Inner Aftershock Ripples", Range(0.0, 1.0)) = 0.35

        [Header(Polar Turbulence and Distortion)]
        [Toggle] _UseNoiseTex ("Use Custom Noise Texture", Float) = 0.0
        _NoiseTex ("Optional Polar Noise Texture", 2D) = "white" {}
        _AngularTiling ("Angular Spoke Frequency (Integer)", Range(1.0, 32.0)) = 10.0
        _RadialTiling ("Radial Wave Frequency", Range(1.0, 24.0)) = 6.0
        _RadialSpeed ("Outward Plasma Ejection Speed", Range(0.0, 15.0)) = 4.5
        _DistortionStrength ("Ring Front Turbulence Warp", Range(0.0, 0.35)) = 0.085

        [Header(Soft Depth Intersection)]
        _DepthFade ("Soft Depth Intersection Distance", Range(0.01, 5.0)) = 0.65
        _OuterQuadFade ("Quad Boundary Safety Fade", Range(0.8, 1.0)) = 0.96
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+55"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "MoltenShockwaveForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _MainColor;
                float4 _RimColor;
                float4 _NoiseTex_ST;
                float _Intensity;
                float _Progress;
                float _UseAutoTime;
                float _AutoSpeed;
                float _MaxRadius;
                float _RingWidth;
                float _LeadingEdgeSharpness;
                float _TrailingFalloff;
                float _SecondaryWaveIntensity;
                float _UseNoiseTex;
                float _AngularTiling;
                float _RadialTiling;
                float _RadialSpeed;
                float _DistortionStrength;
                float _DepthFade;
                float _OuterQuadFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                half4  color      : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21PeriodicX(float2 p, float periodX)
            {
                p.x = fmod(p.x, periodX);
                if (p.x < 0.0) p.x += periodX;
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 34.19);
                return frac(p.x * p.y);
            }

            // Seamless polar noise that wraps cleanly across angle (-PI to PI)
            float PolarValueNoise(float angle01, float radius, float angularFreq, float radialFreq, float2 scroll)
            {
                float periodX = max(1.0, round(angularFreq));
                float2 p = float2(angle01 * periodX, radius * radialFreq) + scroll;

                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash21PeriodicX(i + float2(0.0, 0.0), periodX);
                float b = Hash21PeriodicX(i + float2(1.0, 0.0), periodX);
                float c = Hash21PeriodicX(i + float2(0.0, 1.0), periodX);
                float d = Hash21PeriodicX(i + float2(1.0, 1.0), periodX);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = posInputs.positionCS;
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(posInputs.positionCS);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Convert quad UV (0..1) to centered polar coordinates (radius 0..1 at edges)
                float2 centeredUV = (input.uv - 0.5) * 2.0;
                float radius = length(centeredUV);
                float angle01 = (atan2(centeredUV.y, centeredUV.x) + PI) * INV_TWO_PI;

                // Lifecycle progress (0..1), either driven by script/ParticleSystem or auto-looping time
                float time = _Time.y;
                float rawProgress = (_UseAutoTime > 0.5) ? frac(time * _AutoSpeed) : saturate(_Progress);

                // Non-linear blast wave deceleration curve (rapid initial detonation, slower expansion)
                float expansionCurve = 1.0 - pow(1.0 - rawProgress, 2.4);

                // Sample 2-octave outward-ejecting polar turbulence
                float2 scroll1 = float2(time * 0.12, -time * _RadialSpeed);
                float2 scroll2 = float2(-time * 0.2, -time * _RadialSpeed * 1.65);

                float n1 = PolarValueNoise(angle01, radius, _AngularTiling, _RadialTiling, scroll1);
                float n2 = PolarValueNoise(angle01, radius + (n1 - 0.5) * 0.1, _AngularTiling * 2.0, _RadialTiling * 2.2, scroll2);

                if (_UseNoiseTex > 0.5)
                {
                    float2 polarUV1 = float2(angle01 * round(_AngularTiling), radius * _RadialTiling) * _NoiseTex_ST.xy + _NoiseTex_ST.zw + scroll1;
                    float2 polarUV2 = float2(angle01 * round(_AngularTiling * 2.0), radius * _RadialTiling * 2.0) * _NoiseTex_ST.xy + _NoiseTex_ST.zw + scroll2;
                    float t1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, polarUV1).r;
                    float t2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, polarUV2).g;
                    n1 = lerp(n1, t1, _UseNoiseTex);
                    n2 = lerp(n2, t2, _UseNoiseTex);
                }

                float turbulence = saturate(n1 * 0.6 + n2 * 0.4);

                // Dynamic ring radius and width
                float currentRingWidth = _RingWidth * lerp(0.55, 1.15, expansionCurve);
                float frontRadius = expansionCurve * _MaxRadius;

                // Warp radius with polar turbulence so the blast wave has jagged solar-flare tongues
                float warpedRadius = radius - (turbulence - 0.5) * _DistortionStrength * saturate(radius * 2.0);

                // Normalized position inside the shockwave ring:
                // 1.0 = leading outer shock front, 0.0 = trailing inner edge
                float innerRadius = max(0.0, frontRadius - currentRingWidth);
                float ringPos = (warpedRadius - innerRadius) / max(currentRingWidth, 1e-4);

                // Sharp compression wave at the leading front (ringPos -> 1.0), smooth decay toward tail (ringPos -> 0.0)
                float leadingCutoff = 1.0 - smoothstep(0.88, 1.06, ringPos);
                float trailingRamp = pow(saturate(ringPos), _TrailingFalloff);
                float ringEnvelope = trailingRamp * leadingCutoff;

                // White-hot supersonic shock front highlight right at the leading edge
                float leadingEdgeMask = exp(-pow(max(0.0, 0.92 - ringPos) * _LeadingEdgeSharpness, 2.0)) * leadingCutoff * step(0.01, ringPos);

                // Molten orange turbulent middle band
                float middleMask = smoothstep(0.18, 0.78, ringPos) * leadingCutoff * (0.65 + 0.7 * turbulence);

                // Secondary concentric aftershock ripples inside the blast cavity
                float innerCavityMask = saturate(1.0 - warpedRadius / max(frontRadius, 1e-4)) * step(warpedRadius, frontRadius);
                float rippleWave = sin((frontRadius - warpedRadius) * 28.0 - time * 12.0) * 0.5 + 0.5;
                float aftershock = pow(rippleWave, 3.0) * innerCavityMask * _SecondaryWaveIntensity * n2;

                // 3-Tier HDR Color Synthesis: Crimson Tail -> Molten Orange Middle -> White-Hot Leading Edge
                float3 shockColor = _RimColor.rgb * (ringEnvelope * (0.5 + 0.8 * n1));
                shockColor = lerp(shockColor, _MainColor.rgb, saturate(middleMask));
                shockColor += _CoreColor.rgb * leadingEdgeMask;
                shockColor += _MainColor.rgb * aftershock;

                // Lifecycle energy dissipation (fades smoothly as _Progress approaches 1.0)
                float lifecycleFade = smoothstep(0.0, 0.06, rawProgress) * (1.0 - smoothstep(0.72, 1.0, rawProgress));

                // Quad boundary safety fade to guarantee zero square clipping on flat quads
                float quadEdgeFade = 1.0 - smoothstep(_OuterQuadFade * 0.85, _OuterQuadFade, radius);

                // Soft Depth Intersection with Scene Geometry
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1e-5);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = (unity_OrthoParams.w == 0.0)
                    ? LinearEyeDepth(rawDepth, _ZBufferParams)
                    : LinearDepthToEyeDepth(rawDepth);
                float fragmentEyeDepth = (unity_OrthoParams.w == 0.0)
                    ? input.screenPos.w
                    : LinearDepthToEyeDepth(input.positionCS.z);

                float depthDelta = max(0.0, sceneEyeDepth - fragmentEyeDepth);
                float depthFade = saturate(depthDelta / max(_DepthFade, 1e-4));

                // Final HDR Emission for URP Post-Processing Bloom
                float totalMask = saturate(ringEnvelope + leadingEdgeMask + aftershock) * lifecycleFade * quadEdgeFade * depthFade * input.color.a;
                float3 finalHDR = shockColor * _Intensity * input.color.rgb * lifecycleFade * quadEdgeFade * depthFade * input.color.a;

                finalHDR *= ComputeFogIntensity(input.fogFactor);

                return half4(finalHDR, totalMask);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
