Shader "HoleOut/Board Block"
{
    Properties
    {
        _Color ("Block Color", Color) = (0.02, 0.6, 1, 1)
        _HighlightColor ("Highlight", Color) = (0.0164, 0.492, 0.82, 1)
        _EdgeColor ("Edge", Color) = (0.0116, 0.348, 0.58, 1)
        _FrontColor ("Front", Color) = (0.0116, 0.348, 0.58, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite On
            ZTest LEqual
            Cull Back
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _HighlightColor, _EdgeColor, _FrontColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 color : COLOR;
                half3 normalWS : TEXCOORD0;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color.rgb;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                half3 side = n.z < -0.5 ? _FrontColor.rgb : _EdgeColor.rgb;
                half3 color = n.y > 0.95 ? _Color.rgb * input.color :
                    n.y > 0.1 ? _HighlightColor.rgb : side;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
