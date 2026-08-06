Shader "Base/ScrollingSurfaceURP"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        _Tiling("Tiling", Vector) = (1, 1, 0, 0)
        _ScrollSpeed("Scroll Speed", Vector) = (0.1, 0.0, 0, 0)
        _ManualOffset("Manual Offset", Vector) = (0, 0, 0, 0)
        _AnimationEnabled("Animation Enabled", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            
            // Hỗ trợ GPU Instancing
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Tiling)
                UNITY_DEFINE_INSTANCED_PROP(float4, _ScrollSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float4, _ManualOffset)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimationEnabled)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings Vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = input.uv;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 tiling = UNITY_ACCESS_INSTANCED_PROP(Props, _Tiling);
                float4 scrollSpeed = UNITY_ACCESS_INSTANCED_PROP(Props, _ScrollSpeed);
                float4 manualOffset = UNITY_ACCESS_INSTANCED_PROP(Props, _ManualOffset);
                float animEnabled = UNITY_ACCESS_INSTANCED_PROP(Props, _AnimationEnabled);
                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);

                float2 tiledUV = input.uv * tiling.xy;

                float2 animatedOffset =
                    _Time.y *
                    scrollSpeed.xy *
                    saturate(animEnabled);

                float2 finalUV = tiledUV + manualOffset.xy + animatedOffset;

                half4 textureColor = SAMPLE_TEXTURE2D(
                    _BaseMap,
                    sampler_BaseMap,
                    finalUV
                );

                return textureColor * baseColor;
            }

            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
