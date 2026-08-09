Shader "Base/BubblingSurfaceURP"
{
    Properties
    {
        [MainColor] _BaseColorLow("Base Color Low", Color) = (0.12, 0.02, 0.12, 1)
        _BaseColorHigh("Base Color High", Color) = (0.8, 0.12, 0.55, 1)
        [HDR] _BubbleColor("Bubble Color", Color) = (1.5, 0.35, 1.2, 1)

        _NoiseTiling("Noise Tiling", Vector) = (4, 4, 0, 0)
        _NoiseSpeedA("Noise Speed A", Vector) = (0.08, 0.025, 0, 0)
        _NoiseSpeedB("Noise Speed B", Vector) = (-0.035, 0.06, 0, 0)
        _SecondNoiseScale("Second Noise Scale", Float) = 1.85

        _Displacement("Displacement", Range(0, 1)) = 0.12
        _WaveContribution("Wave Contribution", Range(0, 1)) = 0.15
        _WaveFrequency("Wave Frequency", Float) = 10
        _WaveSpeed("Wave Speed", Float) = 1.2

        _BubbleDensity("Bubble Density", Range(1, 30)) = 8
        _BubbleSpeed("Bubble Cycles Per Second", Range(0, 5)) = 0.45
        _BubbleChance("Bubble Chance", Range(0, 1)) = 0.45
        _BubbleMinRadius("Bubble Min Radius", Range(0.01, 0.5)) = 0.04
        _BubbleMaxRadius("Bubble Max Radius", Range(0.01, 0.7)) = 0.34
        _BubbleRingWidth("Bubble Ring Width", Range(0.002, 0.15)) = 0.035
        _BubbleEmission("Bubble Emission", Range(0, 10)) = 2

        _SpecularStrength("Specular Strength", Range(0, 2)) = 0.45
        _SpecularPower("Specular Power", Range(1, 128)) = 32

        _SurfaceSize("Surface Size", Vector) = (10, 10, 0, 0)
        _NormalSampleDistance("Normal Sample Distance", Range(0.001, 0.1)) = 0.01
        [HideInInspector] _EffectTime("Effect Time", Float) = 0
        [HideInInspector] _EffectTimeReference("Effect Time Reference", Float) = 0
        [HideInInspector] _EffectTimeScale("Effect Time Scale", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColorLow;
                float4 _BaseColorHigh;
                float4 _BubbleColor;

                float4 _NoiseTiling;
                float4 _NoiseSpeedA;
                float4 _NoiseSpeedB;
                float _SecondNoiseScale;

                float _Displacement;
                float _WaveContribution;
                float _WaveFrequency;
                float _WaveSpeed;

                float _BubbleDensity;
                float _BubbleSpeed;
                float _BubbleChance;
                float _BubbleMinRadius;
                float _BubbleMaxRadius;
                float _BubbleRingWidth;
                float _BubbleEmission;

                float _SpecularStrength;
                float _SpecularPower;

                float4 _SurfaceSize;
                float _NormalSampleDistance;
                float _EffectTime;
                float _EffectTimeReference;
                float _EffectTimeScale;
            CBUFFER_END

            float GetCurrentEffectTime()
            {
                float elapsed = max(_Time.y - _EffectTimeReference, 0.0);
                return _EffectTime + elapsed * _EffectTimeScale;
            }

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(
                    float3(p.xyx) * float3(0.1031, 0.1030, 0.0973)
                );

                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                float2 smoothLocal = local * local * (3.0 - 2.0 * local);

                float bottomLeft = Hash21(cell);
                float bottomRight = Hash21(cell + float2(1.0, 0.0));
                float topLeft = Hash21(cell + float2(0.0, 1.0));
                float topRight = Hash21(cell + float2(1.0, 1.0));

                float bottom = lerp(bottomLeft, bottomRight, smoothLocal.x);
                float top = lerp(topLeft, topRight, smoothLocal.x);

                return lerp(bottom, top, smoothLocal.y);
            }

            float GetSurfaceSignal(float2 uv, float effectTime)
            {
                float2 uvA =
                    uv * _NoiseTiling.xy +
                    effectTime * _NoiseSpeedA.xy;

                float2 uvB =
                    uv * _NoiseTiling.xy * max(_SecondNoiseScale, 0.001) +
                    effectTime * _NoiseSpeedB.xy;

                float noiseA = ValueNoise(uvA);
                float noiseB = ValueNoise(uvB);

                return noiseA * 0.65 + noiseB * 0.35;
            }

            float GetSurfaceHeight(float2 uv, float effectTime)
            {
                float signal = GetSurfaceSignal(uv, effectTime);
                float centeredNoise = signal * 2.0 - 1.0;

                float wave = sin(
                    (uv.x + uv.y) * _WaveFrequency +
                    effectTime * _WaveSpeed
                );

                return (
                    centeredNoise +
                    wave * _WaveContribution
                ) * _Displacement;
            }

            float GetBubbleMask(float2 uv, float effectTime)
            {
                float2 bubbleUV = uv * max(_BubbleDensity, 0.001);
                float2 baseCell = floor(bubbleUV);
                float2 localPosition = frac(bubbleUV);

                float finalMask = 0.0;
                float speed = max(_BubbleSpeed, 0.0001);
                float ringWidth = max(_BubbleRingWidth, 0.0001);

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbour = float2(x, y);
                        float2 cellId = baseCell + neighbour;

                        float basePhase = Hash21(cellId + 13.71);
                        float lifeValue = effectTime * speed + basePhase;
                        float cycle = floor(lifeValue);
                        float life = frac(lifeValue);

                        float2 randomPosition = Hash22(
                            cellId + cycle * 17.19
                        );

                        float chanceRandom = Hash21(
                            cellId + cycle * 29.73
                        );

                        float isActive = step(
                            1.0 - saturate(_BubbleChance),
                            chanceRandom
                        );

                        float randomSize = lerp(
                            0.75,
                            1.25,
                            Hash21(cellId + cycle * 41.11)
                        );

                        float2 bubbleCenter =
                            neighbour +
                            lerp(0.15, 0.85, randomPosition);

                        float distanceToCenter = length(
                            localPosition - bubbleCenter
                        );

                        float radius = lerp(
                            _BubbleMinRadius,
                            _BubbleMaxRadius,
                            life
                        ) * randomSize;

                        float ringDistance = abs(
                            distanceToCenter - radius
                        );

                        float ring = 1.0 - smoothstep(
                            ringWidth,
                            ringWidth * 2.0,
                            ringDistance
                        );

                        float fadeIn = smoothstep(0.0, 0.08, life);
                        float fadeOut = 1.0 - smoothstep(0.68, 1.0, life);
                        float lifeFade = fadeIn * fadeOut;

                        finalMask = max(
                            finalMask,
                            ring * lifeFade * isActive
                        );
                    }
                }

                return saturate(finalMask);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float effectTime = GetCurrentEffectTime();

                float sampleDistance = max(
                    _NormalSampleDistance,
                    0.0001
                );

                float currentHeight = GetSurfaceHeight(
                    input.uv,
                    effectTime
                );

                float heightU = GetSurfaceHeight(
                    input.uv + float2(sampleDistance, 0.0),
                    effectTime
                );

                float heightV = GetSurfaceHeight(
                    input.uv + float2(0.0, sampleDistance),
                    effectTime
                );

                float3 normalOS = normalize(input.normalOS);
                float3 tangentOS = normalize(input.tangentOS.xyz);
                float3 bitangentOS = normalize(
                    cross(normalOS, tangentOS) * input.tangentOS.w
                );

                float2 safeSurfaceSize = max(
                    _SurfaceSize.xy,
                    float2(0.001, 0.001)
                );

                float3 tangentStep =
                    tangentOS *
                    safeSurfaceSize.x *
                    sampleDistance;

                float3 bitangentStep =
                    bitangentOS *
                    safeSurfaceSize.y *
                    sampleDistance;

                float3 displacedTangent =
                    tangentStep +
                    normalOS * (heightU - currentHeight);

                float3 displacedBitangent =
                    bitangentStep +
                    normalOS * (heightV - currentHeight);

                float3 displacedNormalOS = normalize(
                    cross(displacedBitangent, displacedTangent)
                );

                float3 displacedPositionOS =
                    input.positionOS.xyz +
                    normalOS * currentHeight;

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(displacedPositionOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(
                    displacedNormalOS
                );
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(
                    positionInputs.positionCS.z
                );

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                float effectTime = GetCurrentEffectTime();

                float surfaceSignal = GetSurfaceSignal(
                    input.uv,
                    effectTime
                );

                float colorBlend = smoothstep(
                    0.18,
                    0.82,
                    surfaceSignal
                );

                half3 baseColor = lerp(
                    _BaseColorLow.rgb,
                    _BaseColorHigh.rgb,
                    colorBlend
                );

                float bubbleMask = GetBubbleMask(
                    input.uv,
                    effectTime
                );

                float4 shadowCoord = TransformWorldToShadowCoord(
                    input.positionWS
                );

                Light mainLight = GetMainLight(shadowCoord);

                half distanceAndShadow =
                    mainLight.distanceAttenuation *
                    mainLight.shadowAttenuation;

                half nDotL = saturate(
                    dot(normalWS, mainLight.direction)
                );

                half3 ambient = SampleSH(normalWS);

                half3 diffuseLighting =
                    baseColor *
                    (
                        ambient +
                        mainLight.color *
                        nDotL *
                        distanceAndShadow
                    );

                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(
                    input.positionWS
                );

                half3 halfDirection = SafeNormalize(
                    mainLight.direction + viewDirectionWS
                );

                half specular = pow(
                    saturate(dot(normalWS, halfDirection)),
                    max(_SpecularPower, 1.0)
                ) * _SpecularStrength;

                half3 specularLighting =
                    mainLight.color *
                    specular *
                    distanceAndShadow;

                half3 bubbleEmission =
                    _BubbleColor.rgb *
                    bubbleMask *
                    _BubbleEmission;

                half3 finalColor =
                    diffuseLighting +
                    specularLighting +
                    bubbleEmission;

                finalColor = MixFog(
                    finalColor,
                    input.fogFactor
                );

                return half4(finalColor, 1.0);
            }

            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
