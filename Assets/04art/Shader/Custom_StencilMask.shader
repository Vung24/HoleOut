// Invisible mask that marks the hole opening in the stencil buffer.
Shader "Custom/StencilMask" {
	Properties {
		_Ref ("Ref", Range(0, 255)) = 1
		[Enum(Cull Off,0, Cull Front,1, Cull Back,2)] _CullMode ("Culling", Float) = 0
	}
	SubShader {
		Tags { "RenderType"="Opaque" "Queue"="Geometry+5" "RenderPipeline"="UniversalPipeline" }
		Pass {
			Tags { "LightMode"="UniversalForward" }
			Cull [_CullMode]
			ZTest LEqual
			ZWrite Off
			ColorMask 0
			Stencil { Ref [_Ref] Comp Always Pass Replace }
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			float4 vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
			half4 frag() : SV_Target { return 0; }
			ENDHLSL
		}
	}
}
