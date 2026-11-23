Shader "HoleOut/Board Gate"
{
    Properties
    {
        _BaseColor ("Gate Color", Color) = (0.4, 0.48, 0.72, 1)
        _ArrowColor ("Arrow Face Color", Color) = (1, 0.98, 0.93, 1)
        _ArrowRoundness ("Arrow Roundness", Range(0.006, 0.024)) = 0.017
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ArrowColor;
                float _ArrowRoundness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 gatePosition : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalOS : TEXCOORD0;
                float2 gatePosition : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalOS = input.normalOS;
                output.gatePosition = input.gatePosition;
                return output;
            }

            float SegmentDistanceSquared(float2 position, float2 start, float2 end)
            {
                float2 offset = position - start;
                float2 edge = end - start;
                float2 nearest = offset - edge * saturate(dot(offset, edge) / dot(edge, edge));
                return dot(nearest, nearest);
            }

            float ArrowDistance(float2 position)
            {
                // Gate meshes point along local +Z on every side of the board.
                float2 p = float2(abs(position.x), position.y);
                const float2 tip = float2(0, 0.067);
                const float2 wing = float2(0.116, -0.052);
                const float2 notch = float2(0, -0.012);
                float distanceSquared = min(
                    SegmentDistanceSquared(p, tip, wing),
                    SegmentDistanceSquared(p, notch, wing));
                float across = p.x / wing.x;
                bool inside = p.y >= lerp(notch.y, wing.y, across)
                    && p.y <= lerp(tip.y, wing.y, across);
                return sqrt(distanceSquared) * (inside ? -1 : 1) - _ArrowRoundness;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalOS);
                half face = smoothstep(0.1, 0.97, normal.y);
                half outerBevel = smoothstep(0.035, 0.116, input.gatePosition.y);
                half edgeLight = smoothstep(0.35, 0.68, normal.z) * (1 - smoothstep(0.68, 0.95, normal.z));
                half3 color = _BaseColor.rgb * lerp(0.48, lerp(0.92, 1.05, outerBevel), face);
                color += _BaseColor.rgb * (edgeLight * 0.28);

                float distance = ArrowDistance(input.gatePosition);
                float antialias = max(0.003, fwidth(distance) * 0.55);
                half arrow = 1 - smoothstep(-antialias, antialias, distance);
                const float outlineWidth = 0.008;
                half outline = 1 - smoothstep(outlineWidth - antialias, outlineWidth + antialias, distance);
                float shadowDistance = ArrowDistance(input.gatePosition + float2(0, 0.009));
                half shadow = 1 - smoothstep(-antialias, 0.006 + antialias, shadowDistance);
                half lightGate = smoothstep(0.62, 0.92, min(_BaseColor.r, min(_BaseColor.g, _BaseColor.b)));
                half3 arrowColor = lerp(_ArrowColor.rgb, half3(0.53, 0.49, 0.72), lightGate * 0.85);
                half tipLight = smoothstep(-0.056, 0.065, input.gatePosition.y);
                arrowColor *= lerp(0.86, 1.0, tipLight);
                color *= 1 - shadow * face * 0.33;
                half3 outlineColor = lerp(_BaseColor.rgb * 0.45, half3(0.22, 0.18, 0.34), 0.55);
                color = lerp(color, outlineColor, outline * face * 0.78);
                color = lerp(color, arrowColor, arrow * face);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
