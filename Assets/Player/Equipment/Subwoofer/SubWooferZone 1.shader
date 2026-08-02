Shader "Custom/SubWooferZone"
{
    Properties
    {
        [HDR] _BaseColor("Base Color (Glow)", Color) = (0.0, 0.5, 1.0, 0.3)
        _WaveSpeed("Wave Speed", Float) = 3.0
        _WaveFrequency("Wave Frequency", Float) = 8.0
        _IntersectionSoftness("Intersection Softness", Float) = 0.5
        _EdgeFade("Edge Fade Power", Float) = 2.0
    }
    SubShader
    {
        // Встановлюємо теги для прозорого об'єкта в URP
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            // Адитивне змішування (ідеально для енергетичних та звукових ефектів)
            Blend SrcAlpha One
            ZWrite Off
            Cull Off // Бачимо сферу і зсередини, і ззовні

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Обов'язково підключаємо бібліотеку для роботи з глибиною сцени
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
                float _WaveSpeed;
                float _WaveFrequency;
                float _IntersectionSoftness;
                float _EdgeFade;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // Трансформація координат
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, float4(0,0,0,0));

                output.positionCS = vertexInput.positionCS;
                // Обчислюємо екранні координати для порівняння глибини
                output.screenPos = ComputeScreenPos(vertexInput.positionCS);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.normalWS = normalInput.normalWS;
                output.uv = input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. М'який перетин із землею/стінами (Depth Blending)
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float meshEyeDepth = input.screenPos.w;
                
                float depthDiff = sceneEyeDepth - meshEyeDepth;
                // Якщо об'єкт торкається іншої геометрії, ефект плавно затухає
                float intersectionFade = saturate(depthDiff / _IntersectionSoftness);

                // 2. Пульсуючі звукові хвилі (Синусоїда по UV координаті від часу)
                float wave = sin(input.uv.y * _WaveFrequency - _Time.y * _WaveSpeed) * 0.5 + 0.5;
                // Робимо смужки хвиль чіткішими
                wave = pow(wave, 5.0);

                // 3. Ефект Френеля (Підсвічування контурів сфери)
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(input.viewDirWS);
                float fresnel = 1.0 - saturate(dot(normal, viewDir));
                fresnel = pow(fresnel, _EdgeFade);

                // Комбінуємо контур сфери, звукові хвилі та м'який дотик до підлоги
                float finalAlpha = (fresnel + wave * 0.4) * intersectionFade * _BaseColor.a;

                // Повертаємо підсумковий колір
                return half4(_BaseColor.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
}