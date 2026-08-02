Shader "Custom/SubWooferAcousticShockwave"
{
    Properties
    {
        [HDR] _BaseColor("Wave Glow Color", Color) = (0.0, 0.6, 1.0, 0.5)
        
        [Header(Shockwave Refraction)]
        _DistortionStrength("Distortion Power", Range(0.0, 0.15)) = 0.04
        _ChromAb("RGB Split (Aberration)", Range(1.0, 3.0)) = 1.5
        
        [Header(Wave Motion)]
        _WaveSpeed("Wave Speed", Float) = 5.0
        _WaveFrequency("Wave Frequency", Float) = 12.0
        
        [Header(Smooth Blending)]
        _EdgeSoftness("Edge Smoothness (No Seams)", Range(0.5, 4.0)) = 1.8
        _IntersectionSoftness("Floor Intersection", Float) = 0.5
    }
    SubShader
    {
        // Рендеримо в прозорій черзі, АЛЕ після туману та пост-процесів
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent+100" 
            "RenderPipeline"="UniversalPipeline" 
        }
        LOD 100

        Pass
        {
            Name "AcousticRefraction"
            Tags { "LightMode"="UniversalForward" }

            // Використовуємо гібридне змішування: заломлений фон + світіння
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off 

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            // Обов'язково для захоплення екрана (туману та стін лабіринту)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 screenPos    : TEXCOORD0;
                float3 viewDirWS    : TEXCOORD1;
                float3 normalWS     : NORMAL;
                float2 uv           : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _DistortionStrength;
                float _ChromAb;
                float _WaveSpeed;
                float _WaveFrequency;
                float _EdgeSoftness;
                float _IntersectionSoftness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, float4(0,0,0,0));

                output.positionCS = vertexInput.positionCS;
                output.screenPos = ComputeScreenPos(vertexInput.positionCS);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.normalWS = normalInput.normalWS;
                output.uv = input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                
                // 1. Генерація акустичної хвилі від часу
                float waveRaw = sin(input.uv.y * _WaveFrequency - _Time.y * _WaveSpeed);
                // Робимо хвилю гострішою (ударною)
                float shockwave = sign(waveRaw) * pow(abs(waveRaw), 0.6);

                // 2. Розрахунок м'яких меж (щоб заломлення не обривалося квадратом на підлозі чи краях сфери)
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float meshEyeDepth = input.screenPos.w;
                float intersectionFade = saturate((sceneEyeDepth - meshEyeDepth) / _IntersectionSoftness);

                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(input.viewDirWS);
                // Спад сили ефекту від центру до країв сфери
                float centerDensity = saturate(dot(normal, viewDir));
                centerDensity = pow(centerDensity, _EdgeSoftness);

                // Загальна маска того, де простір може викривлятися (всередині зони)
                float distortionMask = centerDensity * intersectionFade;

                // 3. ВИКРИВЛЕННЯ ПРОСТОРУ + RGB SPLIT (Хроматична аберація)
                // Зміщуємо UV координати екрана у напрямку нормалей сфери в такти музики/хвилі
                float2 offsetDir = normal.xy * shockwave * _DistortionStrength * distortionMask;
                
                // Беремо 3 різні зміщення для Червоного, Зеленого та Синього каналів
                float2 uvR = screenUV + offsetDir;
                float2 uvG = screenUV + offsetDir * _ChromAb;
                float2 uvB = screenUV + offsetDir * (_ChromAb * 1.5);

                // Захист від артефакту "просвічування крізь стіни" (якщо зсунутий піксель ближче за сферу)
                if (LinearEyeDepth(SampleSceneDepth(uvG), _ZBufferParams) < meshEyeDepth)
                {
                    uvR = screenUV; uvG = screenUV; uvB = screenUV;
                }

                // Зчитуємо викривлений фон (разом із твоїм Volumetric Fog!)
                half r = SampleSceneColor(uvR).r;
                half g = SampleSceneColor(uvG).g;
                half b = SampleSceneColor(uvB).b;
                half3 refractedScene = half3(r, g, b);

                // 4. Додаємо неонове свічення самих ліній хвилі поверх викривленого фону
                float waveGlow = pow(saturate(waveRaw), 4.0);
                half3 finalRGB = refractedScene + (_BaseColor.rgb * waveGlow * distortionMask * 2.0);

                // Альфа контролює плавність переходу між реальним світом і нашою викривленою зоною
                // На краях сфери альфа стає 0, тому жодних швів чи різких обривів не буде!
                float finalAlpha = saturate(distortionMask * 1.5) * _BaseColor.a;

                return half4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }
}