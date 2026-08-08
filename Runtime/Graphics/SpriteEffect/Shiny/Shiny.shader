 Shader "UI/Shiny"
 {
     Properties
     {
         [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
         _Color("Tint", Color) = (1,1,1,1)
         _ShineLocation("ShineLocation", Range(0,1)) = 0
         _ShineWidth("ShineWidth", Range(0,1)) = 0
         _Light("Light", float) = 0
		 _Angle("Angle", Range(1, 10)) = 1
		 _Alpha("Alpha", Range(0, 1)) = 1
         [MaterialToggle] PixelSnap("Pixel snap", Float) = 0

		 _StencilComp("Stencil Comparison", Float) = 8
		 _Stencil("Stencil ID", Float) = 0
		 _StencilOp("Stencil Operation", Float) = 0
		 _StencilWriteMask("Stencil Write Mask", Float) = 255
		 _StencilReadMask("Stencil Read Mask", Float) = 255

		 _ColorMask("Color Mask", Float) = 15
     }
 
     SubShader
     {
         Tags
		 {
			 "Queue" = "Transparent"
			 "IgnoreProjector" = "True"
			 "RenderType" = "Transparent"
			 "PreviewType" = "Plane"
			 "CanUseSpriteAtlas" = "True"
		 }

		 Stencil
		 {
			Ref[_Stencil]
			Comp[_StencilComp]
			Pass[_StencilOp]
			ReadMask[_StencilReadMask]
			WriteMask[_StencilWriteMask]
		 }
 
         Cull Off
         Lighting Off
         ZWrite Off
         Blend One OneMinusSrcAlpha
		 ColorMask[_ColorMask]
 
     Pass
     {
         CGPROGRAM
		 #pragma vertex vert
		 #pragma fragment frag
		 #pragma multi_compile _ PIXELSNAP_ON
		 #include "UnityCG.cginc"
 
		 struct appdata_t
		 {
			 float4 vertex   : POSITION;
			 float4 color    : COLOR;
			 float2 texcoord : TEXCOORD0;
		 };
 
		 struct v2f
		 {
			 float4 vertex   : SV_POSITION;
			 fixed4 color : COLOR;
			 float2 texcoord  : TEXCOORD0;
		 };
 
		 fixed4 _Color;
 
		 v2f vert(appdata_t IN)
		 {
			 v2f OUT;
			 OUT.vertex = UnityObjectToClipPos(IN.vertex);
			 OUT.texcoord = IN.texcoord;
			 OUT.color = IN.color * _Color;
			 #ifdef PIXELSNAP_ON
			 OUT.vertex = UnityPixelSnap(OUT.vertex);
			 #endif
 
			 return OUT;
		 }
 
		 sampler2D _MainTex;
		 sampler2D _AlphaTex;
		 float _AlphaSplitEnabled;
		 float _ShineLocation;
		 float _ShineWidth;
		 float _Light;
		 float _Angle;
		 float _Alpha;
		 float _T;
 
		 fixed4 SampleSpriteTexture(float2 uv)
		 {
			 fixed4 color = tex2D(_MainTex, uv);
 
			 #if UNITY_TEXTURE_ALPHASPLIT_ALLOWED
				if (_AlphaSplitEnabled)
					color.a = tex2D(_AlphaTex, uv).r;
			 #endif //UNITY_TEXTURE_ALPHASPLIT_ALLOWED

          
			 float lowLevel = _ShineLocation - _ShineWidth;
			 float highLevel = _ShineLocation + _ShineWidth;
			 float currentDistanceProjection = (uv.x * _Angle + uv.y / _Angle) / 2;
			 if (_ShineLocation != 0 && _ShineLocation != 1 && currentDistanceProjection > lowLevel && currentDistanceProjection < highLevel) 
			 {
				 float whitePower = 1- (abs(currentDistanceProjection - _ShineLocation ) / _ShineWidth);
				 float p = color.a * whitePower * _Light / 10;
				 color +=  p;
				 color.a = color.a * _Alpha + p * (1 - _Alpha);
			 }
			 else
				color.a = _Alpha;
          
			 return color;
		 }
 
		 fixed4 frag(v2f IN) : SV_Target
		 {
			 fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
			 c.rgb *= c.a;
 
			 return c;
		 }
		 ENDCG
		 }
     }
 }
