Shader "CharacterCandidates/AnimMapPreview"
{
    Properties
    {
        _AnimMap ("Anim Map", 2D) = "black" {}
        _BoundsMin ("Bounds Min", Vector) = (-0.2, -0.1, -0.2, 0)
        _BoundsMax ("Bounds Max", Vector) = (1.5, 1.2, 1.6, 0)
        _FrameCount ("Frame Count", Float) = 16
        _Speed ("Speed (Loops/Sec)", Float) = 1.0
        _Color ("Color", Color) = (0.86, 0.88, 0.92, 1)
        [ToggleUI] _EnableAnim ("Enable Animation", Float) = 1
        [ToggleUI] _InterpolateFrames ("Interpolate Frames", Float) = 1
        [ToggleUI] _FlipV ("Flip V", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "DisableBatching" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_AnimMap);
            SAMPLER(sampler_AnimMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _AnimMap_TexelSize;
                float4 _BoundsMin;
                float4 _BoundsMax;
                float _FrameCount;
                float _Speed;
                half4 _Color;
                float _EnableAnim;
                float _InterpolateFrames;
                float _FlipV;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                uint vertexID     : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 posOS = input.positionOS;

                if (_EnableAnim > 0.5)
                {
                    float texWidth = max(_AnimMap_TexelSize.z, 1.0);
                    float texHeight = max(_AnimMap_TexelSize.w, 1.0);
                    float u = (float(input.vertexID) + 0.5) / texWidth;
                    float frameCount = clamp(floor(_FrameCount + 0.5), 1.0, texHeight);

                    float phase = frac(_Time.y * _Speed);
                    float framePos = phase * frameCount;
                    float f0 = min(floor(framePos), frameCount - 1.0);
                    float f1 = (f0 + 1.0 >= frameCount) ? 0.0 : (f0 + 1.0);
                    float blend = frac(framePos);

                    float v0 = (f0 + 0.5) / texHeight;
                    float v1 = (f1 + 0.5) / texHeight;
                    if (_FlipV > 0.5)
                    {
                        v0 = 1.0 - v0;
                        v1 = 1.0 - v1;
                    }

                    float3 sample0 = SAMPLE_TEXTURE2D_LOD(_AnimMap, sampler_AnimMap, float2(u, v0), 0.0).rgb;
                    float3 encodedPos = sample0;
                    if (_InterpolateFrames > 0.5 && frameCount > 1.0)
                    {
                        float3 sample1 = SAMPLE_TEXTURE2D_LOD(_AnimMap, sampler_AnimMap, float2(u, v1), 0.0).rgb;
                        encodedPos = lerp(sample0, sample1, blend);
                    }

                    posOS = lerp(_BoundsMin.xyz, _BoundsMax.xyz, encodedPos);
                }

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = SafeNormalize(input.normalWS);

                if (_EnableAnim > 0.5)
                {
                    float3 dpdx = ddx(input.positionWS);
                    float3 dpdy = ddy(input.positionWS);
                    float3 crossProd = cross(dpdx, dpdy);
                    float lenSq = dot(crossProd, crossProd);
                    if (lenSq > 1e-10)
                    {
                        float3 faceNormal = crossProd * rsqrt(lenSq);
                        float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                        if (dot(faceNormal, viewDirWS) < 0.0)
                        {
                            faceNormal = -faceNormal;
                        }
                        normalWS = faceNormal;
                    }
                }

                Light mainLight = GetMainLight();
                half ndotl = saturate((half)dot(normalWS, (float3)mainLight.direction));
                half3 ambient = half3(0.25, 0.27, 0.32);
                half3 lighting = ambient + mainLight.color * (ndotl * 0.75 + 0.05);
                half3 color = _Color.rgb * lighting;
                return half4(color, _Color.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
