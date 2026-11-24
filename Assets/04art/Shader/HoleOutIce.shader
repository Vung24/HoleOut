// Vỏ băng trong suốt phủ khối trên bàn: viền sáng theo góc nhìn, bóng đèn chính, một dải sáng
// chạy chậm qua mặt băng, vết nứt hiện dần theo _Crack và chớp trắng theo _Flash.
// Vẽ trước chữ số của khối (Transparent-10) để số vẫn rõ khi nằm dưới lớp băng.
Shader "HoleOut/Ice"
{
    Properties
    {
        _Color ("Ice Color", Color) = (0.72, 0.9, 1, 0.38)
        _Ambient ("Ambient", Color) = (0.7, 0.74, 0.8, 1)
        _RimColor ("Rim Color", Color) = (1, 1, 1, 0.75)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.4
        _Gloss ("Gloss", Range(4, 128)) = 48
        _SpecStrength ("Specular", Range(0, 2)) = 0.7
        _SheenColor ("Sheen Color", Color) = (1, 1, 1, 0.45)
        _SheenSpeed ("Sheen Speed", Float) = 0.35
        _SheenPeriod ("Sheen Period", Float) = 3.2
        _SheenWidth ("Sheen Width", Range(0.01, 0.5)) = 0.12
        _CrackColor ("Crack Color", Color) = (1, 1, 1, 0.9)
        _CrackScale ("Crack Scale", Float) = 2.6
        _CrackWidth ("Crack Width", Range(0.005, 0.1)) = 0.03
        _Crack ("Crack", Range(0, 1)) = 0
        _Flash ("Flash", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "IgnoreProjector"="True" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _Ambient;
                float4 _RimColor;
                float _RimPower;
                float _Gloss;
                float _SpecStrength;
                float4 _SheenColor;
                float _SheenSpeed;
                float _SheenPeriod;
                float _SheenWidth;
                float4 _CrackColor;
                float _CrackScale;
                float _CrackWidth;
                float _Crack;
                float _Flash;
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
                float3 positionWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float3 normalOS : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                return output;
            }

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // Khoảng cách tới cạnh ô Voronoi gần nhất; chỉ cạnh của ô có hash < amount được vẽ,
            // nên vết nứt lan thêm khi amount tăng mà các vết cũ giữ nguyên chỗ.
            float CrackMask(float2 uv, float amount, float width)
            {
                float2 cell = floor(uv);
                float2 f = frac(uv);
                float d1 = 8, d2 = 8;
                float2 nearest = 0;
                for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 g = float2(x, y);
                        float2 o = Hash2(cell + g);
                        float d = length(g + o - f);
                        if (d < d1) { d2 = d1; d1 = d; nearest = cell + g; }
                        else if (d < d2) d2 = d;
                    }
                float active = step(Hash2(nearest + 17.3).x, amount);
                return active * (1 - smoothstep(width * 0.4, width, d2 - d1));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 l = normalize(_MainLightPosition.xyz);
                float ndotl = saturate(dot(n, l));
                float fresnel = pow(1 - saturate(dot(n, v)), _RimPower);
                float spec = pow(saturate(dot(n, normalize(l + v))), _Gloss) * _SpecStrength;

                // Dải sáng chéo trôi chậm theo thời gian.
                float s = frac((dot(input.positionOS.xz, float2(0.86, 0.5)) - _Time.y * _SheenSpeed) / _SheenPeriod);
                float sheen = smoothstep(0, _SheenWidth, s) * smoothstep(_SheenWidth * 2, _SheenWidth, s);

                // Mặt trên dùng xz, mặt bên dùng mặt phẳng của nó.
                float3 an = abs(input.normalOS);
                float2 uv = an.y > 0.5 ? input.positionOS.xz : (an.x > 0.5 ? input.positionOS.zy : input.positionOS.xy);
                float crack = _Crack > 0 ? CrackMask(uv * _CrackScale, _Crack, _CrackWidth) : 0;

                float3 color = _Color.rgb * (_Ambient.rgb + _MainLightColor.rgb * ndotl * 0.5);
                color += _RimColor.rgb * fresnel * _RimColor.a + spec + _SheenColor.rgb * sheen * _SheenColor.a;
                color = lerp(color, _CrackColor.rgb, crack * _CrackColor.a);
                float alpha = _Color.a + fresnel * _RimColor.a * 0.6 + spec * 0.5 + sheen * _SheenColor.a * 0.5 + crack * _CrackColor.a;
                color = lerp(color, 1, _Flash * 0.7);
                alpha = saturate(alpha + _Flash * 0.35);
                return float4(color, alpha);
            }
            ENDHLSL
        }
    }
}
