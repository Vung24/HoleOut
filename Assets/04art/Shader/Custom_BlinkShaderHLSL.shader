// Texture with a periodic diagonal highlight sweeping across it.
Shader "Custom/BlinkShaderHLSL" {
	Properties {
		[NoScaleOffset] _MainTex ("MainTex", 2D) = "white" {}
		_Scroll ("Scroll", Range(-0.5, 1.5)) = 0.5
		_BlinkSpeed ("Blink Speed", Float) = 1.5
		_BlinkWidth ("Blink Width", Range(0.01, 1)) = 0.23
		_BlinkIntensity ("Blink Intensity", Range(0, 3)) = 1
		_Delay ("Delay Between Blinks", Float) = 1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
	}
	SubShader {
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
		Pass {
			Tags { "LightMode"="UniversalForward" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Off
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
			CBUFFER_START(UnityPerMaterial)
			half _Scroll, _BlinkSpeed, _BlinkWidth, _BlinkIntensity, _Delay;
			CBUFFER_END
			struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
			struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
			V vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; o.color = i.color; return o; }
			half4 frag(V i) : SV_Target {
				half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
				float speed = max(_BlinkSpeed, 0.01);
				float period = 1.0 + max(_Delay, 0) * speed;
				float t = frac(_Time.y * speed / period) * period - 0.5 + (_Scroll - 0.5);
				float d = abs((i.uv.x + i.uv.y) * 0.5 - t);
				c.rgb += smoothstep(_BlinkWidth, 0, d) * _BlinkIntensity * c.a;
				return c;
			}
			ENDHLSL
		}
	}
}
