Shader "UIBlur"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _BlurSize ("Blur Size", Range(0, 10)) = 2
        [Toggle] _UseHole ("Use Hole Masking", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        
        GrabPass { "_BackgroundTexture" }

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
                float4 grabPos : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
            };

            sampler2D _BackgroundTexture;
            float4 _BackgroundTexture_TexelSize;
            fixed4 _Color;
            half _BlurSize;
            half _UseHole;

            half4 _HoleCenters[8];
            half4 _HoleSizes[8];
            half _HoleRadii[8];
            int _HoleCount;

            v2f vert (appdata_t v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.grabPos = ComputeGrabScreenPos(o.vertex);
                o.worldPos = v.vertex;
                return o;
            }

            // Tối ưu hóa hàm RoundedBox (Giảm tính toán căn bậc 2)
            inline half sdRoundedBox(half2 p, half2 b, half r) {
                half2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }

            fixed4 frag (v2f i) : SV_Target {
                // Tối ưu hóa: Kiểm tra đục lỗ trước khi Blur
                if (_UseHole > 0.5) {
                    half finalDist = 10000.0;
                    for (int j = 0; j < _HoleCount; j++) {
                        half2 p = i.worldPos.xy - _HoleCenters[j].xy;
                        half2 b = _HoleSizes[j].xy * 0.5;
                        finalDist = min(finalDist, sdRoundedBox(p, b, _HoleRadii[j]));
                    }
                    // Dùng clip thay vì discard (clip thường nhanh hơn trên một số GPU)
                    clip(finalDist); 
                }

                float2 uv = i.grabPos.xy / i.grabPos.w;
                half2 res = _BackgroundTexture_TexelSize.xy * (_BlurSize + 1.0);
                
                // Kawase Blur: Chỉ 4-tap (Tiết kiệm hơn 50% tài nguyên so với 9-tap)
                fixed4 col = tex2D(_BackgroundTexture, uv + half2(res.x, res.y));
                col += tex2D(_BackgroundTexture, uv + half2(res.x, -res.y));
                col += tex2D(_BackgroundTexture, uv + half2(-res.x, res.y));
                col += tex2D(_BackgroundTexture, uv + half2(-res.x, -res.y));
                
                return (col * 0.25) * _Color;
            }
            ENDCG
        }
    }
}
