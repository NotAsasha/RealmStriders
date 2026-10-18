Shader "ProjectPortal/VFX/MoltenCraterDecal"
{
    Properties
    {
        [Header(HDR Thermal Magma Palette)]
        [HDR] _CoreColor ("Fresh Magma Core (White-Hot Plasma)", Color) = (8.0, 6.0, 2.0, 1.0)
        [HDR] _MainColor ("Molten Fissure Color (Solar Orange)", Color) = (5.0, 1.6, 0.1, 1.0)
        [HDR] _RimColor ("Cooling Ember Color (Fiery Crimson)", Color) = (1.5, 0.25, 0.03, 1.0)
        _AshColor ("Charred Scorch Ash Color", Color) = (0.035, 0.03, 0.028, 0.92)
        _Intensity ("Magma Emission Intensity", Range(0.1, 10.0)) = 1.25

        [Header(Thermal Cooling Lifecycle)]
        _CoolDown ("Cooling Progress (0 = Fresh Strike, 1 = Cold Ash)", Range(0.0, 1.0)) = 0.0
        _AshFadeOutStart ("Ash Opacity Fade Start (CoolDown)", Range(0.5, 0.95)) = 0.78
        _EmberPulseSpeed ("Residual Ember Pulse Speed", Range(0.0, 10.0)) = 2.5
        _EmberPulseAmount ("Residual Ember Pulse Strength", Range(0.0, 0.5)) = 0.15

        [Header(Magma Fissures and Crust Geometry)]
        [Toggle] _UseCraterTex ("Use Custom Fissure Mask Texture", Float) = 0.0
        _CraterTex ("Optional Custom Fissure Map (R: Cracks, G: Noise)", 2D) = "white" {}
        _FissureScale ("Voronoi Crust Cell Scale", Range(2.0, 24.0)) = 7.5
        _FissureSharpness ("Magma Crack Sharpness", Range(2.0, 30.0)) = 11.0
        _FissureWarp ("Tectonic Crack Turbulence Warp", Range(0.0, 1.0)) = 0.38
        _CenterPoolSize ("Central Molten Lava Pool Radius", Range(0.0, 0.7)) = 0.28
        _RadialSpokes ("Radial Fracture Spoke Influence", Range(0.0, 1.0)) = 0.45

        [Header(Radial Falloff and Terrain Blending)]
        _CraterRadius ("Overall Scorch Radius", Range(0.2, 1.0)) = 0.92
        _RadialSoftness ("Outer Scorch Edge Softness", Range(0.05, 0.8)) = 0.42
        _DepthFade ("Soft Terrain Intersection Distance", Range(0.01, 2.0)) = 0.25
        _ZOffsetFactor ("Polygon Z-Bias Factor (Anti Z-Fight)", Range(-5.0, 0.0)) = -1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "MoltenCraterDecalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset [_ZOffsetFactor], -1
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_CraterTex);
            SAMPLER(sampler_CraterTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _MainColor;
                float4 _RimColor;
                float4 _AshColor;
                float4 _CraterTex_ST;
                float _Intensity;
                float _CoolDown;
                float _AshFadeOutStart;
                float _EmberPulseSpeed;
                float _EmberPulseAmount;
                float _UseCraterTex;
                float _FissureScale;
                float _FissureSharpness;
                float _FissureWarp;
                float _CenterPoolSize;
                float _RadialSpokes;
                float _CraterRadius;
                float _RadialSoftness;
                float _DepthFade;
                float _ZOffsetFactor;
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

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float ValueNoise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = frac(sin(dot(i + float2(0.0, 0.0), float2(127.1, 311.7))) * 43758.5453);
                float b = frac(sin(dot(i + float2(1.0, 0.0), float2(127.1, 311.7))) * 43758.5453);
                float c = frac(sin(dot(i + float2(0.0, 1.0), float2(127.1, 311.7))) * 43758.5453);
                float d = frac(sin(dot(i + float2(1.0, 1.0), float2(127.1, 311.7))) * 43758.5453);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Two-pass Voronoi edge distance for authentic cooled basalt crust plates & glowing magma cracks
            // Returns: x = distance to crack edge (0 at crack center), y = cell interior distance
            float2 VoronoiCracks(float2 x)
            {
                float2 n = floor(x);
                float2 f = frac(x);

                float2 mg = 0.0;
                float2 mr = 0.0;
                float md = 8.0;

                [unroll]
                for (int j = -1; j <= 1; j++)
                {
                    [unroll]
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 g = float2((float)i, (float)j);
                        float2 o = Hash22(n + g);
                        float2 r = g + o - f;
                        float d = dot(r, r);

                        if (d < md)
                        {
                            md = d;
                            mr = r;
                            mg = g;
                        }
                    }
                }

                md = 8.0;
                [unroll]
                for (int k = -1; k <= 1; k++)
                {
                    [unroll]
                    for (int l = -1; l <= 1; l++)
                    {
                        float2 g = mg + float2((float)l, (float)k);
                        float2 o = Hash22(n + g);
                        float2 r = g + o - f;

                        if (dot(mr - r, mr - r) > 0.00001)
                        {
                            md = min(md, dot(0.5 * (mr + r), normalize(r - mr)));
                        }
                    }
                }

                return float2(md, length(mr));
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

                float2 centeredUV = (input.uv - 0.5) * 2.0;
                float radius = length(centeredUV);
                float angle = atan2(centeredUV.y, centeredUV.x);

                // 1. Domain-Warped Tectonic Magma Fissures
                float n1 = ValueNoise2D(centeredUV * 4.5);
                float n2 = ValueNoise2D(centeredUV * 9.5 + 17.3);
                float2 warpOffset = (float2(n1, n2) - 0.5) * _FissureWarp;

                float2 voronoi = VoronoiCracks((centeredUV + warpOffset) * _FissureScale);
                float crackEdgeDist = voronoi.x;

                // Convert cell boundary distance into razor-sharp glowing magma veins
                float primaryCracks = exp(-crackEdgeDist * _FissureSharpness);

                // Radial impact fracture spokes radiating from ground-zero center
                float spokeNoise = ValueNoise2D(float2(angle * 5.0, radius * 4.0 + n1 * 2.0));
                float radialFractures = pow(saturate(1.0 - abs(spokeNoise * 2.0 - 1.0)), 4.0) * (1.0 - smoothstep(0.1, _CraterRadius * 0.85, radius));
                float fissurePattern = saturate(primaryCracks + radialFractures * _RadialSpokes * (0.4 + 0.6 * primaryCracks));

                if (_UseCraterTex > 0.5)
                {
                    float2 texUV = input.uv * _CraterTex_ST.xy + _CraterTex_ST.zw + warpOffset * 0.1;
                    float2 customSample = SAMPLE_TEXTURE2D(_CraterTex, sampler_CraterTex, texUV).rg;
                    fissurePattern = lerp(fissurePattern, customSample.r, _UseCraterTex);
                    n2 = lerp(n2, customSample.g, _UseCraterTex);
                }

                // 2. Spatial Heat Distribution (Central Molten Lava Pool -> Radiating Fissures -> Outer Rim)
                float radialMask = 1.0 - smoothstep(0.0, _CraterRadius, radius);
                float centerPool = 1.0 - smoothstep(0.0, max(_CenterPoolSize, 1e-4), radius + (n1 - 0.5) * 0.14);

                // Combine central lava pool with surrounding fissure veins
                float spatialHeat = saturate(max(centerPool * 0.95, fissurePattern * pow(radialMask, 0.55)));
                spatialHeat *= (0.75 + 0.25 * n2);

                // 3. Thermal Cooling Curve Over Time (_CoolDown: 0 = White-Hot Impact -> Red Embers -> Black Ash)
                // Support driving cooldown via material property or particle vertex color alpha/red if needed
                float coolDown = saturate(_CoolDown);

                // Subtle convective pulse in the glowing magma before full solidification
                float emberPulse = 1.0 + sin(_Time.y * _EmberPulseSpeed + n1 * 6.28) * _EmberPulseAmount * (1.0 - coolDown);

                // Outer perimeter solidifies first; deep central fissures stay molten longest
                float coolingThreshold = coolDown * 1.35 - (1.0 - radialMask) * 0.22;
                float currentTemp = saturate((spatialHeat - coolingThreshold) / max(1.0 - coolDown * 0.55, 0.15));
                currentTemp = saturate(currentTemp * emberPulse);

                // 4. Blackbody Thermal Color Mapping (Ash -> Crimson -> Molten Orange -> White-Hot Core)
                float emberMask = smoothstep(0.04, 0.38, currentTemp);
                float moltenMask = smoothstep(0.35, 0.76, currentTemp);
                float whiteHotMask = smoothstep(0.72, 0.98, currentTemp);

                // Base charred basalt ash crust with subtle surface grain
                float3 crustColor = _AshColor.rgb * (0.65 + 0.7 * n2);

                float3 thermalEmission = lerp(crustColor, _RimColor.rgb * _Intensity, emberMask);
                thermalEmission = lerp(thermalEmission, _MainColor.rgb * _Intensity, moltenMask);
                thermalEmission = lerp(thermalEmission, _CoreColor.rgb * _Intensity, whiteHotMask);

                // 5. Soft Radial Alpha Falloff & Terrain Depth Blending
                float noisyRadius = radius + (n1 - 0.5) * 0.12 * smoothstep(0.15, 0.6, radius);
                float innerFadeStart = _CraterRadius * (1.0 - _RadialSoftness);
                float radialAlpha = 1.0 - smoothstep(innerFadeStart, _CraterRadius, noisyRadius);

                // Fade out cold ash at the very end of the decal lifecycle
                float ashLifecycleFade = 1.0 - smoothstep(_AshFadeOutStart, 1.0, coolDown);

                // Soft depth fade against terrain stones/slopes
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

                // Glowing magma cracks remain opaque; charred ash uses _AshColor.a * radialAlpha
                float surfaceAlpha = lerp(_AshColor.a, 1.0, emberMask);
                float finalAlpha = saturate(radialAlpha * surfaceAlpha * ashLifecycleFade * depthFade * input.color.a);

                float3 finalColor = thermalEmission * input.color.rgb;
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
