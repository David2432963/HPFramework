Shader "KeyboardEscape/ScrollingArrowTrail"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _SpriteUvRect ("Sprite UV Rect", Vector) = (0, 0, 1, 1)
        _TrailSize ("Trail Size", Vector) = (1, 1, 0, 0)
        _TileSize ("Tile Size", Vector) = (1, 1, 0, 0)
        _TrailAxis ("Trail Axis", Float) = 1
        _ScrollDirection ("Scroll Direction", Float) = 1
        _ScrollSpeed ("Scroll Speed", Float) = 1
        _EdgeFadePortion ("Edge Fade Portion", Range(0, 0.5)) = 0.1
        _Flip ("Flip", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _SpriteUvRect;
                float4 _TrailSize;
                float4 _TileSize;
                float _TrailAxis;
                float _ScrollDirection;
                float _ScrollSpeed;
                float _EdgeFadePortion;
                float4 _Flip;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 positionOS : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xy;
                output.color = input.color;
                return output;
            }

            float GetCoord01(float position, float size)
            {
                float start = -size * 0.5;
                return saturate((position - start) / max(size, 0.0001));
            }

            float ComputeEdgeFade(float coord01)
            {
                float fade = saturate(_EdgeFadePortion);
                if (fade <= 0.0001)
                {
                    return 1.0;
                }

                float fadeIn = smoothstep(0.0, fade, coord01);
                float fadeOut = smoothstep(0.0, fade, 1.0 - coord01);
                return fadeIn * fadeOut;
            }

            float2 ApplyFlip(float2 uv)
            {
                uv.x = lerp(uv.x, 1.0 - uv.x, saturate(_Flip.x));
                uv.y = lerp(uv.y, 1.0 - uv.y, saturate(_Flip.y));
                return uv;
            }

            float2 GetTileUv(float2 positionOS, out float fadeCoord)
            {
                bool useLocalY = _TrailAxis > 0.5;

                float axisPosition = useLocalY ? positionOS.y : positionOS.x;
                float crossPosition = useLocalY ? positionOS.x : positionOS.y;
                float axisSize = useLocalY ? _TrailSize.y : _TrailSize.x;
                float crossSize = useLocalY ? _TrailSize.x : _TrailSize.y;
                float tileAxisSize = useLocalY ? _TileSize.y : _TileSize.x;

                fadeCoord = GetCoord01(axisPosition, axisSize);

                float axisStart = -axisSize * 0.5;
                float tileCoord = (axisPosition - axisStart) / max(tileAxisSize, 0.0001);
                float tileAxisUv = frac(tileCoord + (_Time.y * _ScrollSpeed * _ScrollDirection));
                float crossUv = GetCoord01(crossPosition, crossSize);

                float2 tileUv = useLocalY
                    ? float2(crossUv, tileAxisUv)
                    : float2(tileAxisUv, crossUv);

                return ApplyFlip(tileUv);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float fadeCoord;
                float2 tileUv = GetTileUv(input.positionOS, fadeCoord);
                float2 sampleUv = _SpriteUvRect.xy + tileUv * _SpriteUvRect.zw;
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUv) * _Color * input.color;
                color.a *= ComputeEdgeFade(fadeCoord);
                return color;
            }
            ENDHLSL
        }
    }
}
