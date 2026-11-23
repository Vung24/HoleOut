#ifndef HOLE_SORT_TOON_INCLUDED
#define HOLE_SORT_TOON_INCLUDED
// Simple two-tone toon lighting for URP, using the property names stored in the game materials.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
CBUFFER_START(UnityPerMaterial)
	float4 _BaseMap_ST;
	half4 _BaseColor, _HColor, _SColor, _EmissionColor, _RimColor, _OutlineColor;
	#if defined(HOLE_OUT_PALETTE_COLOR)
	half4 _PaletteColor;
	#endif
	half _Cutoff, _RampThreshold, _RampSmoothing, _UseEmission, _UseRim, _RimMin, _RimMax, _IndirectIntensity, _OutlineWidth;
CBUFFER_END

struct ToonAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
struct ToonVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; };

ToonVaryings ToonVert(ToonAttributes i)
{
	ToonVaryings o;
	o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
	o.positionCS = TransformWorldToHClip(o.positionWS);
	o.normalWS = TransformObjectToWorldNormal(i.normalOS);
	o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
	return o;
}

half4 ToonFrag(ToonVaryings i) : SV_Target
{
	half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
	#if defined(_ALPHATEST_ON)
	clip(albedo.a - _Cutoff);
	#endif
	#if defined(HOLE_OUT_PALETTE_COLOR)
	if (_PaletteColor.a > 0.5)
		return half4(_PaletteColor.rgb, albedo.a);
	#endif
	float3 n = normalize(i.normalWS);
	Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
	half ndl = dot(n, light.direction) * 0.5 + 0.5;
	half smoothing = max(_RampSmoothing, 0.001);
	half ramp = smoothstep(_RampThreshold - smoothing * 0.5, _RampThreshold + smoothing * 0.5, ndl * light.shadowAttenuation);
	half3 hColor = any(_HColor.rgb) ? _HColor.rgb : half3(1, 1, 1);
	half3 sColor = any(_SColor.rgb) ? _SColor.rgb : half3(0.6, 0.6, 0.7);
	half3 lit = lerp(sColor, hColor, ramp) * light.color;
	half3 ambient = SampleSH(n) * max(_IndirectIntensity, 0);
	half3 color = albedo.rgb * (lit + ambient);
	if (_UseRim > 0.5)
	{
		half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
		half rim = smoothstep(_RimMin, max(_RimMax, _RimMin + 0.001), 1 - saturate(dot(v, n)));
		color += _RimColor.rgb * rim;
	}
	if (_UseEmission > 0.5)
		color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
	return half4(color, albedo.a);
}

ToonVaryings OutlineVert(ToonAttributes i)
{
	ToonVaryings o;
	float3 positionOS = i.positionOS.xyz + normalize(i.normalOS) * _OutlineWidth * 0.01;
	o.positionWS = TransformObjectToWorld(positionOS);
	o.positionCS = TransformWorldToHClip(o.positionWS);
	o.normalWS = TransformObjectToWorldNormal(i.normalOS);
	o.uv = i.uv;
	return o;
}
half4 OutlineFrag(ToonVaryings i) : SV_Target { return half4(_OutlineColor.rgb, 1); }
#endif
