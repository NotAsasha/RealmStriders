Shader "ProjectPortal/VFX/FieryOrbitalBeam"
{
    Properties
    {
        [Header(HDR Thermal Palette)]
        [HDR] _CoreColor ("Core Color (White-Hot Plasma)", Color) = (8.0, 6.0, 2.0, 1.0)
        [HDR] _MainColor ("Main Body Color (Molten Orange)", Color) = (5.0, 1.6, 0.1, 1.0)
        [HDR] _RimColor ("Outer Rim Color (Fiery Crimson)", Color) = (1.5, 0.25, 0.03, 1.0)
        _Intensity ("Global Emission Intensity", Range(0.1, 10.0)) = 1.0

        [Header(Dual Core and Radial Profile)]
        _CoreRadius ("Inner Core Tightness", Range(0.1, 8.0)) = 2.6
        _CoreSharpness ("Core Transition Sharpness", Range(0.5, 8.0)) = 2.2
        _CoreTurbulence ("Core Turbulence Influence", Range(0.0, 1.0)) = 0.35

        [Header(Supersonic Vertical Noise)]
        [Toggle] _UseNoiseTex ("Use Custom Noise Texture", Float) = 0.0
        _NoiseTex ("Optional Custom Noise (R: Oct1, G: Oct2)", 2D) = "white" {}
        _NoiseScale1 ("Octave 1 Scale (Radial, Vertical)", Vector) = (4.0, 2.5, 0.0, 0.0)
        _NoiseScale2 ("Octave 2 Scale (Radial, Vertical)", Vector) = (8.0, 6.0, 0.0, 0.0)
        _Speed ("Primary Downward Stream Speed", Range(0.0, 25.0)) = 7.5
        _SecondarySpeedRatio ("Octave 2 Speed Multiplier", Range(0.5, 4.0)) = 1.65
        _TurbulenceDistortion ("Flame UV Warp Strength", Range(0.0, 1.0)) = 0.28
        _VertexDisplacement ("Radial Plasma Pulse Displacement", Range(0.0, 0.5)) = 0.04

        [Header(Fresnel and Grazing Edge Breakup)]
        _FresnelPower ("Fresnel Falloff Power", Range(0.5, 10.0)) = 2.5
        _EdgeBreakup ("Grazing Edge Flame Breakup", Range(0.0, 2.0)) = 0.85
        _EdgeClipThreshold ("Flame Border Erosion Cutoff", Range(0.0, 0.8)) = 0.18

        [Header(Height Dissolve and Ground Intersection)]
        _SkyFadeStart ("Sky Fade Start Height (UV.y)", Range(0.5, 1.0)) = 0.78
        _SkyFadeEnd ("Sky Fade End Height (UV.y)", Range(0.6, 1.0)) = 0.99
        _GroundFlareBoost ("Ground Impact Flare Boost", Range(0.0, 4.0)) = 1.5
        _GroundFlareHeight ("Ground Impact Flare Height (UV.y)", Range(0.01, 0.5)) = 0.12
        _DepthFade ("Soft Depth Intersection Distance", Range(0.01, 10.0)) = 1.25
        _StrikeWidthScale ("Beam Width Multiplier (Lifecycle)", Range(0.0, 2.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "IgnoreProjector" = "True"
            "PreviewType" = "Cylinder"
        }

        Pass
        {
            Name "FieryOrbitalBeamForward"
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
                float4 _NoiseScale1;
                float4 _NoiseScale2;
                float _Intensity;
                float _CoreRadius;
                float _CoreSharpness;
                float _CoreTurbulence;
                float _UseNoiseTex;
                float _Speed;
                float _SecondarySpeedRatio;
                float _TurbulenceDistortion;
                float _VertexDisplacement;
                float _FresnelPower;
                float _EdgeBreakup;
                float _EdgeClipThreshold;
                float _SkyFadeStart;
                float _SkyFadeEnd;
                float _GroundFlareBoost;
                float _GroundFlareHeight;
                float _DepthFade;
                float _StrikeWidthScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 screenPos  : TEXCOORD3;
                half4  color      : TEXCOORD4;
                half   fogFactor  : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Fast, deterministic hash for seamless cylindrical value noise
            float Hash21PeriodicX(float2 p, float periodX)
            {
                p.x = fmod(p.x, periodX);
                if (p.x < 0.0) p.x += periodX;
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Seamless 2D Value Noise that wraps cleanly around cylinder UV.x (0..1)
            float CylindricalValueNoise(float2 uv, float2 scale, float2 scrollOffset)
            {
                float periodX = max(1.0, round(scale.x));
                float2 p = float2(uv.x * periodX, uv.y * scale.y) + scrollOffset;

                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash21PeriodicX(i + float2(0.0, 0.0), periodX);
                float b = Hash21PeriodicX(i + float2(1.0, 0.0), periodX);
                float c = Hash21PeriodicX(i + float2(0.0, 1.0), periodX);
                float d = Hash21PeriodicX(i + float2(1.0, 1.0), periodX);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Compute dual-octave vertical scrolling plasma noise
            // Note: UV.y = 0 at ground, 1 at sky. Adding _Time.y * _Speed scrolls noise downward.
            float2 SampleBeamTurbulence(float2 uv, float time)
            {
                float scroll1 = time * _Speed;
                float scroll2 = time * _Speed * _SecondarySpeedRatio;

                // Counter-rotating slight helical drift for realistic solar plasma vortex
                float2 offset1 = float2(time * 0.15, scroll1);
                float2 offset2 = float2(-time * 0.25, scroll2);

                float n1 = CylindricalValueNoise(uv, _NoiseScale1.xy, offset1);
                float2 warpedUV = uv + (n1 - 0.5) * (_TurbulenceDistortion * 0.15);
                float n2 = CylindricalValueNoise(warpedUV, _NoiseScale2.xy, offset2);

                if (_UseNoiseTex > 0.5)
                {
                    float2 texUV1 = uv * _NoiseScale1.xy * _NoiseTex_ST.xy + _NoiseTex_ST.zw + offset1;
                    float2 texUV2 = warpedUV * _NoiseScale2.xy * _NoiseTex_ST.xy + _NoiseTex_ST.zw + offset2;
                    float t1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, texUV1).r;
                    float t2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, texUV2).g;
                    n1 = lerp(n1, t1, _UseNoiseTex);
                    n2 = lerp(n2, t2, _UseNoiseTex);
                }

                return float2(n1, n2);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float time = _Time.y;

                // Subtle high-speed radial vertex pulse along the column
                float pulseWave = sin(input.uv.y * 24.0 + time * _Speed * 2.5) * 0.5 + 0.5;
                float skyTaper = 1.0 - smoothstep(_SkyFadeStart, _SkyFadeEnd, input.uv.y);
                float3 posOS = input.positionOS.xyz;

                // Scale radial XZ components for lifecycle beam width and turbulence pulse
                posOS.xz *= _StrikeWidthScale;
                posOS += input.normalOS * (pulseWave * _VertexDisplacement * skyTaper * _StrikeWidthScale);

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(posInputs.positionCS);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);

                return output;
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                float time = _Time.y;

                // 1. Two-Octave Vertical Turbulent Noise (Downward Supersonic Stream)
                float2 octaves = SampleBeamTurbulence(uv, time);
                float primaryNoise = octaves.x;
                float detailNoise = octaves.y;
                float combinedNoise = saturate(primaryNoise * 0.62 + detailNoise * 0.38);
                float flameRidge = 1.0 - abs(combinedNoise * 2.0 - 1.0); // Ridged plasma filaments
                float plasmaPattern = lerp(combinedNoise, flameRidge, 0.45);

                // 2. View Vector & Double-Sided Normal Alignment
                float3 normalWS = normalize(input.normalWS);
                normalWS = IS_FRONT_VFACE(isFrontFace, normalWS, -normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Grazing angle factor: 1.0 at center of cylinder, 0.0 at silhouette edges
                float NdotV = saturate(dot(normalWS, viewDirWS));
                float fresnel = 1.0 - NdotV;

                // 3. Fresnel & Grazing Edge Breakup (Jagged Flame Borders)
                float edgeMask = pow(NdotV, _FresnelPower);
                float erodedEdge = saturate(edgeMask + (plasmaPattern - 0.5) * _EdgeBreakup * fresnel);
                float flameAlpha = smoothstep(_EdgeClipThreshold, _EdgeClipThreshold + 0.35, erodedEdge);

                // 4. Dual Core Energy Profile (White-Hot Core -> Molten Orange Body -> Crimson Rim)
                float coreWeight = pow(NdotV, _CoreRadius);
                coreWeight = saturate(coreWeight + (detailNoise - 0.5) * _CoreTurbulence * coreWeight);
                float coreFactor = pow(coreWeight, _CoreSharpness);

                float bodyFactor = smoothstep(0.1, 0.75, erodedEdge * (0.65 + 0.7 * plasmaPattern));

                float3 thermalColor = _RimColor.rgb;
                thermalColor = lerp(thermalColor, _MainColor.rgb, bodyFactor);
                thermalColor = lerp(thermalColor, _CoreColor.rgb, coreFactor);

                // Add high-frequency solar flare streaks inside the main body
                float flareStreaks = pow(saturate(primaryNoise * detailNoise * 1.8), 2.0);
                thermalColor += _CoreColor.rgb * (flareStreaks * bodyFactor * 0.45);

                // 5. Height Dissolve / Fade (Sky Entry & Ground Impact Flare)
                float heightDissolveNoise = lerp(uv.y, uv.y + (primaryNoise - 0.5) * 0.15, 0.6);
                float skyFade = 1.0 - smoothstep(_SkyFadeStart, _SkyFadeEnd, heightDissolveNoise);

                float groundFlare = 1.0 - smoothstep(0.0, _GroundFlareHeight, uv.y);
                thermalColor += _CoreColor.rgb * (groundFlare * groundFlare * _GroundFlareBoost);

                // 6. Soft Depth Intersection (_CameraDepthTexture)
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

                // Boost contact glow slightly right above the terrain intersection before fading to 0
                float contactGlow = smoothstep(0.0, 0.25, depthFade) * (1.0 - smoothstep(0.25, 1.0, depthFade));
                thermalColor += _MainColor.rgb * (contactGlow * 0.75);

                // Final Composite (Additive Blend One One)
                float totalMask = flameAlpha * skyFade * depthFade * input.color.a;
                float3 finalHDR = thermalColor * _Intensity * input.color.rgb * totalMask;

                // Additive fog attenuation (fade emission toward black in distant fog)
                finalHDR *= ComputeFogIntensity(input.fogFactor);

                return half4(finalHDR, saturate(totalMask));
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
