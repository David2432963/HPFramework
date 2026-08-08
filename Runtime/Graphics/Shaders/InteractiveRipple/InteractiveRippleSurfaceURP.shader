Shader "Base/InteractiveRippleSurfaceURP"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (0.45, 0.08, 0.35, 1)

        [HDR] _RippleColor("Ripple Color", Color) = (1.2, 0.25, 1.0, 1)
        _RippleTintStrength("Ripple Tint Strength", Range(0, 1)) = 0.45
        _RippleEmission("Ripple Emission", Range(0, 10)) = 1.5

        _RippleSpeed("Ripple Speed", Range(0.01, 10)) = 2.2
        _RippleWidth("Ripple Width", Range(0.01, 2)) = 0.28
        _RippleFrequency("Ripple Frequency", Range(0.1, 40)) = 12
        _RippleDamping("Ripple Damping", Range(0, 10)) = 1.4

        _Displacement("Displacement", Range(0, 1)) = 0.09
        _MaxCombinedDisplacement("Max Combined Displacement", Range(0, 2)) = 0.22
        _NormalSampleDistance("Normal Sample Distance", Range(0.005, 0.5)) = 0.05

        _SpecularStrength("Specular Strength", Range(0, 2)) = 0.4
        _SpecularPower("Specular Power", Range(1, 128)) = 32

        [HideInInspector] _EffectTime("Effect Time", Float) = 0
        [HideInInspector] _RippleCount("Ripple Count", Float) = 0
        [HideInInspector] _RippleAxisScale("Ripple Axis Scale", Vector) = (1, 1, 0, 0)
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
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define MAX_RIPPLES 8

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float2 planePositionOS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _RippleColor;

                float _RippleTintStrength;
                float _RippleEmission;

                float _RippleSpeed;
                float _RippleWidth;
                float _RippleFrequency;
                float _RippleDamping;

                float _Displacement;
                float _MaxCombinedDisplacement;
                float _NormalSampleDistance;

                float _SpecularStrength;
                float _SpecularPower;

                float _EffectTime;
                float _RippleCount;
                float4 _RippleAxisScale;
                float4 _RippleData[MAX_RIPPLES];
            CBUFFER_END

            void EvaluateRipples(float2 planePositionOS, out float finalHeight, out float finalMask)
            {
                finalHeight = 0.0;
                finalMask = 0.0;

                int rippleCount = min((int)round(_RippleCount), MAX_RIPPLES);
                float2 axisScale = max(abs(_RippleAxisScale.xy), float2(0.0001, 0.0001));
                float safeWidth = max(_RippleWidth, 0.0001);
                float safeDamping = max(_RippleDamping, 0.0);

                [unroll]
                for (int index = 0; index < MAX_RIPPLES; index++)
                {
                    if (index >= rippleCount) break;

                    float4 ripple = _RippleData[index];
                    float2 centerOS = ripple.xy;
                    float startTime = ripple.z;
                    float strength = ripple.w;

                    float age = _EffectTime - startTime;
                    float isAlive = step(0.0, age);
                    age = max(age, 0.0);

                    float2 deltaWS = (planePositionOS - centerOS) * axisScale;
                    float distanceWS = length(deltaWS);
                    float radius = age * _RippleSpeed;
                    float distanceFromRing = distanceWS - radius;

                    float normalizedDistance = distanceFromRing / safeWidth;
                    float ringEnvelope = exp(-normalizedDistance * normalizedDistance);
                    float ageFade = exp(-age * safeDamping);
                    float oscillation = cos(distanceFromRing * _RippleFrequency);

                    float rippleHeight = oscillation * ringEnvelope * ageFade * strength * _Displacement * isAlive;
                    finalHeight += rippleHeight;

                    float rippleMask = abs(oscillation) * ringEnvelope * ageFade * saturate(abs(strength)) * isAlive;
                    finalMask = max(finalMask, rippleMask);
                }

                finalHeight = clamp(finalHeight, -_MaxCombinedDisplacement, _MaxCombinedDisplacement);
                finalMask = saturate(finalMask);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 planePositionOS = input.positionOS.xz;
                float centerHeight, ignoredCenterMask;

                EvaluateRipples(planePositionOS, centerHeight, ignoredCenterMask);

                float2 axisScale = max(abs(_RippleAxisScale.xy), float2(0.0001, 0.0001));
                float sampleDistanceWS = max(_NormalSampleDistance, 0.0001);
                float localStepX = sampleDistanceWS / axisScale.x;
                float localStepZ = sampleDistanceWS / axisScale.y;

                float heightX, ignoredMaskX;
                EvaluateRipples(planePositionOS + float2(localStepX, 0.0), heightX, ignoredMaskX);

                float heightZ, ignoredMaskZ;
                EvaluateRipples(planePositionOS + float2(0.0, localStepZ), heightZ, ignoredMaskZ);

                float3 basePositionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 originalNormalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                float3 displacedPositionWS = basePositionWS + originalNormalWS * centerHeight;

                float3 tangentXWS = normalize(TransformObjectToWorldDir(float3(1.0, 0.0, 0.0)));
                float3 tangentZWS = normalize(TransformObjectToWorldDir(float3(0.0, 0.0, 1.0)));

                float slopeX = (heightX - centerHeight) / sampleDistanceWS;
                float slopeZ = (heightZ - centerHeight) / sampleDistanceWS;

                float3 displacedTangentXWS = tangentXWS + originalNormalWS * slopeX;
                float3 displacedTangentZWS = tangentZWS + originalNormalWS * slopeZ;

                float3 displacedNormalWS = normalize(cross(displacedTangentZWS, displacedTangentXWS));
                if (dot(displacedNormalWS, originalNormalWS) < 0.0) displacedNormalWS *= -1.0;

                output.positionCS = TransformWorldToHClip(displacedPositionWS);
                output.positionWS = displacedPositionWS;
                output.normalWS = displacedNormalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.planePositionOS = planePositionOS;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float ignoredHeight, rippleMask;
                EvaluateRipples(input.planePositionOS, ignoredHeight, rippleMask);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 baseColor = baseSample.rgb * _BaseColor.rgb;
                half3 surfaceColor = lerp(baseColor, _RippleColor.rgb, rippleMask * _RippleTintStrength);
                half3 normalWS = normalize(input.normalWS);
                
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                
                half attenuation = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half nDotL = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS);
                
                half3 diffuseLighting = surfaceColor * (ambient + mainLight.color * nDotL * attenuation);
                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirectionWS);
                
                half specular = pow(saturate(dot(normalWS, halfDirection)), max(_SpecularPower, 1.0)) * _SpecularStrength;
                half3 specularLighting = mainLight.color * specular * attenuation;
                half3 rippleEmission = _RippleColor.rgb * rippleMask * _RippleEmission;
                
                half3 finalColor = diffuseLighting + specularLighting + rippleEmission;
                finalColor = MixFog(finalColor, input.fogFactor);
                
                return half4(finalColor, baseSample.a * _BaseColor.a);
            }

            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
