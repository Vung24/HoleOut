Shader "HoleOut/Picture Cell"
{
    Properties
    {
        _BaseColor ("Body", Color) = (1, 0.5, 0, 1)
        _HighlightColor ("Highlight", Color) = (1, 0.73, 0.2, 1)
        _EdgeColor ("Edge", Color) = (0.9, 0.3, 0, 1)
        _FrontColor ("Front", Color) = (0.7, 0.2, 0, 1)
        [Header(Shape)]
        _HalfSize ("Mesh Half Width", Float) = 0.39
        _Height ("Mesh Height", Float) = 0.92
        [Header(Shading)]
        _BackRim ("Back Rim Light", Range(0, 1)) = 0.45
        _SideShade ("Side Shade", Range(0, 1)) = 0.55
        _GlintWidth ("Glint Width", Range(0.1, 1)) = 0.68
        _GlintStrength ("Glint Strength", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _HighlightColor, _EdgeColor, _FrontColor;
                float _HalfSize, _Height;
                half _BackRim, _SideShade, _GlintWidth, _GlintStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                // Vị trí trong ô theo trục thế giới: x trái/phải, y đáy→đỉnh (0→1), z trước→sau (-1→1).
                float3 cellPos : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                // Mesh ô dựng đứng theo trục z của nó (gốc ở đáy), nên z của mesh là chiều cao trên tranh.
                float3 origin = UNITY_MATRIX_M._m03_m13_m23;
                float halfX = length(UNITY_MATRIX_M._m00_m10_m20) * _HalfSize;
                float halfZ = length(UNITY_MATRIX_M._m01_m11_m21) * _HalfSize;
                float height = length(UNITY_MATRIX_M._m02_m12_m22) * _Height;
                float3 d = positionWS - origin;
                output.cellPos = float3(d.x / max(halfX, 1e-4), d.y / max(height, 1e-4), d.z / max(halfZ, 1e-4));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                float3 p = input.cellPos;

                // Mặt trên: màu thân phẳng, mép sau sáng nhẹ, hai mép bên tối dần để ra khối bo tròn.
                half3 color = _BaseColor.rgb;
                half back = smoothstep(0.6, 0.95, p.z);
                color = lerp(color, _HighlightColor.rgb, back * _BackRim);
                half side = smoothstep(0.7, 1.0, abs(p.x));
                color = lerp(color, _EdgeColor.rgb, side * _SideShade);

                // Vệt sáng ngang trên mép trước của mặt trên (giống ánh đèn phản chiếu lên khối nhựa).
                half glintBand = smoothstep(-1.0, -0.9, p.z) * (1 - smoothstep(-0.74, -0.6, p.z));
                half glintSpan = 1 - smoothstep(_GlintWidth - 0.15, _GlintWidth, abs(p.x));
                half glintTop = smoothstep(0.75, 0.9, p.y);
                color = lerp(color, _HighlightColor.rgb, glintBand * glintSpan * glintTop * _GlintStrength);

                // Các mặt bên/đáy cạnh: màu cạnh; mặt trước tối dần xuống chân ô.
                half up = saturate(n.y);
                half3 wall = n.z < -0.4 ? lerp(_FrontColor.rgb, _EdgeColor.rgb, saturate(p.y)) : _EdgeColor.rgb;
                half wallMask = 1 - smoothstep(0.35, 0.8, up);
                color = lerp(color, wall, wallMask);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
