// Toon lighting for URP (two-tone ramp, rim, emission).
Shader "Toony Colors Pro 2/Hybrid Shader 2" {
	Properties {
		[Enum(Front, 2, Back, 1, Both, 0)] _Cull ("Render Face", Float) = 2
		_ZWrite ("Depth Write", Float) = 1
		[Toggle(_ALPHATEST_ON)] _UseAlphaTest ("Alpha Clipping", Float) = 0
		_Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
		_BaseColor ("Color", Vector) = (1,1,1,1)
		[HideInInspector] _PaletteColor ("Palette Color", Color) = (0,0,0,0)
		_BaseMap ("Albedo", 2D) = "white" {}
		_HColor ("Highlight Color", Vector) = (1,1,1,1)
		_SColor ("Shadow Color", Vector) = (0.2,0.2,0.2,1)
		[Toggle(TCP2_SHADOW_LIGHT_COLOR)] _ShadowColorLightAtten ("Main Light affects Shadow Color", Float) = 1
		[Toggle(TCP2_SHADOW_TEXTURE)] _UseShadowTexture ("Enable Shadow Albedo Texture", Float) = 0
		[NoScaleOffset] _ShadowBaseMap ("Shadow Albedo", 2D) = "gray" {}
		_RampType ("Ramp Type", Float) = 0
		_Ramp ("Ramp Texture (RGB)", 2D) = "gray" {}
		_RampScale ("Scale", Float) = 1
		_RampOffset ("Offset", Float) = 0
		[PowerSlider(0.415)] _RampThreshold ("Threshold", Range(0.01, 1)) = 0.75
		_RampSmoothing ("Smoothing", Range(0, 1)) = 0.1
		[IntRange] _RampBands ("Bands Count", Range(1, 20)) = 4
		_RampBandsSmoothing ("Bands Smoothing", Range(0, 1)) = 0.1
		_UseNormalMap ("Normal Mapping", Float) = 0
		_BumpMap ("Normal Map", 2D) = "bump" {}
		_BumpScale ("Scale", Range(-1, 1)) = 1
		_UseSpecular ("Specular", Float) = 0
		_SpecularType ("Type", Float) = 0
		[HDR] _SpecularColor ("Color", Vector) = (0.75,0.75,0.75,1)
		[PowerSlider(5.0)] _SpecularToonSize ("Size", Range(0.001, 1)) = 0.25
		_SpecularToonSmoothness ("Smoothing", Range(0, 1)) = 0.05
		_SpecularRoughness ("Roughness", Range(0, 1)) = 0.5
		[Enum(Disabled,0,Albedo Alpha,1,Custom R,2,Custom G,3,Custom B,4,Custom A,5)] _SpecularMapType ("Specular Map#Specular Map (A)", Float) = 0
		[NoScaleOffset] _SpecGlossMap ("Specular Texture", 2D) = "white" {}
		_UseEmission ("Emission", Float) = 0
		[Enum(No Texture,5,R,0,G,1,B,2,A,3,RGB,4)] _EmissionChannel ("Texture Channel", Float) = 4
		_EmissionMap ("Texture#Texture (A)", 2D) = "white" {}
		_EmissionColor ("Color", Vector) = (1,1,0,1)
		_UseRim ("Rim Lighting", Float) = 0
		[HDR] _RimColor ("Color", Vector) = (0.8,0.8,0.8,0.5)
		_RimMin ("Min", Range(0, 2)) = 0.5
		_RimMax ("Max", Range(0, 2)) = 1
		[Toggle(TCP2_RIM_LIGHTING_LIGHTMASK)] _UseRimLightMask ("Light-based Mask", Float) = 1
		_UseMatCap ("MatCap", Float) = 0
		[Enum(Additive,0,Replace,1)] _MatCapType ("MatCap Blending", Float) = 0
		[NoScaleOffset] _MatCapTex ("Texture", 2D) = "black" {}
		[HDR] _MatCapColor ("Color", Vector) = (1,1,1,1)
		[Toggle(TCP2_MATCAP_MASK)] _UseMatCapMask ("Enable Mask", Float) = 0
		[NoScaleOffset] _MatCapMask ("Mask Texture#Mask Texture (A)", 2D) = "black" {}
		[Enum(R,0,G,1,B,2,A,3)] _MatCapMaskChannel ("Texture Channel", Float) = 0
		_IndirectIntensity ("Strength", Range(0, 1)) = 1
		_SingleIndirectColor ("Single Indirect Color", Float) = 0
		_UseReflections ("Indirect Specular (Environment Reflections)", Float) = 0
		_ReflectionColor ("Color", Vector) = (1,1,1,1)
		_ReflectionSmoothness ("Smoothness", Range(0, 1)) = 0.5
		_ReflectionMapType ("Reflection Map", Float) = 0
		[NoScaleOffset] _ReflectionTex ("Reflection Texture#Reflection Texture (A)", 2D) = "white" {}
		[Toggle(TCP2_REFLECTIONS_FRESNEL)] _UseFresnelReflections ("Fresnel Reflections", Float) = 1
		_FresnelMin ("Fresnel Min", Range(0, 2)) = 0
		_FresnelMax ("Fresnel Max", Range(0, 2)) = 1.5
		_UseOcclusion ("Occlusion", Float) = 0
		_OcclusionStrength ("Strength", Range(0, 1)) = 1
		[NoScaleOffset] _OcclusionMap ("Texture#Texture (A)", 2D) = "white" {}
		[Enum(Albedo Alpha,0,Custom R,1,Custom G,2,Custom B,3,Custom A,4)] _OcclusionChannel ("Texture Channel", Float) = 0
		_UseOutline ("Outline", Float) = 0
		[HDR] _OutlineColor ("Color", Vector) = (0,0,0,1)
		_OutlineTextureType ("Texture", Float) = 0
		_OutlineTextureLOD ("Texture LOD", Range(0, 8)) = 5
		_OutlineWidth ("Width", Range(0, 10)) = 1
		_OutlinePixelSizeType ("Pixel Size", Float) = 0
		_OutlineMinWidth ("Minimum Width (Pixels)", Float) = 1
		_OutlineMaxWidth ("Maximum Width (Pixels)", Float) = 1
		_NormalsSource ("Outline Normals Source", Float) = 0
		_NormalsUVType ("UV Data Type", Float) = 0
		_OutlineLightingTypeURP ("Lighting", Float) = 0
		_OutlineLightingType ("Lighting", Float) = 0
		_DirectIntensityOutline ("Direct Strength", Range(0, 1)) = 1
		_IndirectIntensityOutline ("Indirect Strength", Range(0, 1)) = 0
		[ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadowsOff ("Receive Shadows", Float) = 1
		[HideInInspector] _RenderingMode ("rendering mode", Float) = 0
		[HideInInspector] _SrcBlend ("blending source", Float) = 1
		[HideInInspector] _DstBlend ("blending destination", Float) = 0
		[HideInInspector] _UseMobileMode ("Mobile mode", Float) = 0
	}
	SubShader {
		Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
		Pass {
			Name "ForwardLit"
			Tags { "LightMode"="UniversalForward" }
			Cull [_Cull]
			ZWrite [_ZWrite]
			HLSLPROGRAM
			#pragma vertex ToonVert
			#pragma fragment ToonFrag
			#pragma shader_feature_local _ALPHATEST_ON
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _SHADOWS_SOFT
			#define HOLE_OUT_PALETTE_COLOR
			#include "HoleSortToon.hlsl"
			ENDHLSL
		}
		Pass {
			Name "DepthOnly"
			Tags { "LightMode"="DepthOnly" }
			ColorMask 0
			HLSLPROGRAM
			#pragma vertex ToonVert
			#pragma fragment DepthFrag
			#define HOLE_OUT_PALETTE_COLOR
			#include "HoleSortToon.hlsl"
			half4 DepthFrag(ToonVaryings i) : SV_Target { return 0; }
			ENDHLSL
		}
	}
}
