// Bề mặt màu phẳng cho bàn, khay chờ và khung tranh: diffuse theo đèn chính + ambient cố định.
// Chỉ đọc màu/hướng đèn chính và hằng số của material; không dùng SH, probe hay BRDF của URP Lit,
// nên mặt phẳng hiển thị ổn định từng khung hình.
Shader "HoleOut/Surface"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Ambient ("Ambient", Color) = (0.62, 0.64, 0.68, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _Ambient;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float ndotl = saturate(dot(n, normalize(_MainLightPosition.xyz)));
                float3 lighting = _Ambient.rgb + _MainLightColor.rgb * ndotl;
                return float4(_BaseColor.rgb * lighting, 1);
            }
            ENDHLSL
        }
    }
}
