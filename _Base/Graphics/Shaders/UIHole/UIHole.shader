Shader "UIHole"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Softness ("Softness", Range(0, 100)) = 10
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
            };

            fixed4 _Color;
            float _Softness;

            // Multi-hole data
            float4 _HoleCenters[8];
            float4 _HoleSizes[8];
            float _HoleRadii[8];
            int _HoleCount;

            v2f vert (appdata_t v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.worldPos = v.vertex;
                return o;
            }

            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }

            fixed4 frag (v2f i) : SV_Target {
                float finalDist = 1e10; // Start with a very large distance

                for (int j = 0; j < _HoleCount; j++)
                {
                    float2 p = i.worldPos.xy - _HoleCenters[j].xy;
                    float2 b = _HoleSizes[j].xy * 0.5;
                    float dist = sdRoundedBox(p, b, _HoleRadii[j]);
                    
                    // Combine holes (Union of distances)
                    finalDist = min(finalDist, dist);
                }
                
                float alpha = smoothstep(-_Softness, 0, finalDist);
                return fixed4(_Color.rgb, _Color.a * alpha);
            }
            ENDCG
        }
    }
}
