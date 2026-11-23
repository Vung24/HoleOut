Shader "HoleOut/Cube Preview"
{
    Properties
    {
        _Color ("Block Color", Color) = (1, 1, 1, 1)
        _HighlightColor ("Highlight", Color) = (1, 1, 1, 1)
        _EdgeColor ("Edge", Color) = (0.82, 0.82, 0.82, 1)
        _FrontColor ("Front", Color) = (0.58, 0.58, 0.58, 1)
        _GradientStrength ("Surface Lighting", Range(0, 2)) = 1
        [HideInInspector] _FaceFrame ("Face Frame", Vector) = (0, 1, 2, 2)
        [HideInInspector] _BaseVertexGreen ("Base Vertex Green", Float) = 0
        [HideInInspector] _GradientHeight ("Gradient Height", Float) = 0.42
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _HighlightColor, _EdgeColor, _FrontColor;
                half _BaseVertexGreen;
                half _GradientStrength;
                float _GradientHeight;
                float4 _FaceFrame;
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
                float3 positionOS : TEXCOORD0;
                float up : TEXCOORD1;
                float4 color : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.up = input.normalOS.y;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_BaseVertexGreen <= 0) return half4(input.color.rgb, 1);

                half height = saturate(input.positionOS.y / max(_GradientHeight, 0.01));
                // Orient the reflection along the board even when the mesh is set sideways.
                float2 forward = _FaceFrame.xy;
                float2 xz = input.positionOS.xz;
                float2 p = float2(dot(xz, float2(forward.y, -forward.x)), dot(xz, forward)) * _FaceFrame.zw;
                // Khoảng cách tới mép tính theo ô để dải sáng/tối giữ độ rộng như ô tranh với mọi cỡ khối.
                float2 halfCells = 1 / max(_FaceFrame.zw, 1e-3);
                float toSide = (1 - abs(p.x)) * halfCells.x;
                float toBack = (1 - p.y) * halfCells.y;
                float toFront = (1 + p.y) * halfCells.y;
                // Mặt trên giữ đúng màu thân của tranh; chỉ mép sau sáng, mép bên tối và một vệt bóng gần mép trước.
                half back = 1 - smoothstep(0.08, 0.24, toBack);
                half side = 1 - smoothstep(0.08, 0.22, toSide);
                half glint = smoothstep(0.06, 0.1, toFront) * (1 - smoothstep(0.15, 0.22, toFront))
                    * smoothstep(0.12, 0.26, toSide);
                half3 face = lerp(_Color.rgb, _HighlightColor.rgb, saturate(back * 0.45 * _GradientStrength));
                face = lerp(face, _EdgeColor.rgb, side * 0.6);
                face = lerp(face, _HighlightColor.rgb, saturate(glint * 0.85 * _GradientStrength));
                half3 wall = lerp(_FrontColor.rgb, _EdgeColor.rgb, height);
                half3 painted = lerp(wall, face, smoothstep(0.15, 0.97, input.up));

                // Keep the rim in the same color family and let the opening fall into shade.
                half shade = input.color.g / _BaseVertexGreen;
                half3 recess = _FrontColor.rgb * lerp(0.32, 1, smoothstep(0.32, 0.78, shade));
                recess = lerp(recess, _EdgeColor.rgb, smoothstep(0.7, 0.93, shade));
                half3 color = lerp(recess, painted, smoothstep(0.82, 0.995, shade));
                color = lerp(color, _HighlightColor.rgb, smoothstep(1.02, 1.18, shade) * 0.13);                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
