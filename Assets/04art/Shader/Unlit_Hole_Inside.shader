// Hole interior: only drawn where the opening mask wrote the same stencil value.
Shader "Unlit/Hole_Inside" {
	Properties {
		_MainTex ("Texture", 2D) = "white" {}
		_Color ("Color", Vector) = (1,1,1,1)
		_Ref ("Ref", Range(0, 255)) = 1
		[HideInInspector] _HoleFloor ("Hole Floor", Float) = 0
		[Enum(Cull Off,0, Cull Front,1, Cull Back,2)] _CullMode ("Culling", Float) = 0
	}
	SubShader {
		Tags { "RenderType"="Opaque" "Queue"="Geometry+10" "RenderPipeline"="UniversalPipeline" }
		Pass {
			Tags { "LightMode"="UniversalForward" }
			Cull [_CullMode]
			ZTest Always
			ZWrite Off
			Stencil { Ref [_Ref] Comp Equal }
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST; half4 _Color; half _HoleFloor;
			CBUFFER_END
			struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
			struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionOS : TEXCOORD1; };
			V vert(A i) {
				V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = TRANSFORM_TEX(i.uv, _MainTex);
				o.positionOS = i.positionOS.xyz;
				return o;
			}
			half4 frag(V i) : SV_Target {
				half wallShade = lerp(0.35, 0.85, saturate(1 + i.positionOS.z / 4));
				// Darken the junction between the floor and wall even at a top-down camera angle.
				half floorShade = lerp(0.55, 0.25, smoothstep(0.5, 1.15, length(i.positionOS.xy)));
				half shade = lerp(wallShade, floorShade, _HoleFloor);
				return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color * half4(shade.xxx, 1);
			}
			ENDHLSL
		}
	}
}
