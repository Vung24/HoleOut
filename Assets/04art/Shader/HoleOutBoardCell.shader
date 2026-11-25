// Ô bàn khối: cùng cách chiếu sáng với HoleOut/Surface, thêm đường viền tối dọc mép ô.
// Viền tính theo khoảng cách tới mép trong không gian vật thể, có độ rộng tối thiểu theo pixel
// nên lưới vẫn hiện khi màn hình nhỏ (khe hình học hẹp hơn 1px sẽ bị mất khi raster).
Shader "HoleOut/Board Cell"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Ambient ("Ambient", Color) = (0.62, 0.64, 0.68, 1)
        _LineColor ("Line Color", Color) = (0.09, 0.10, 0.22, 1)
        _HalfSize ("Cell Half Size", Float) = 0.6135
        _LineWidth ("Line Width (world, mỗi bên)", Float) = 0.004
        _MinPixels ("Line Min Pixels (mỗi bên)", Float) = 1.5
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
                float4 _LineColor;
                float _HalfSize;
                float _LineWidth;
                float _MinPixels;
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
                float2 cellPos : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.cellPos = input.positionOS.xz;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float ndotl = saturate(dot(n, normalize(_MainLightPosition.xyz)));
                float3 lighting = _Ambient.rgb + _MainLightColor.rgb * ndotl;

                // Khoảng tới mép gần nhất trên từng trục, chia cho độ rộng viền (tối thiểu _MinPixels pixel).
                float2 toEdge = _HalfSize - abs(input.cellPos);
                float2 pixel = max(fwidth(input.cellPos), 1e-5);
                float2 width = max(_LineWidth, pixel * _MinPixels);
                float2 t = saturate(toEdge / width);
                float inside = min(t.x, t.y);

                float3 color = lerp(_LineColor.rgb, _BaseColor.rgb, inside);
                return float4(color * lighting, 1);
            }
            ENDHLSL
        }
    }
}
