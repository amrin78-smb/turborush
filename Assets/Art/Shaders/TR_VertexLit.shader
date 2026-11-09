// Lean URP shader for all Turbo Rush geometry: vertex colour x texture x tint, half-lambert main light + flat
// ambient (SH), optional Blinn-Phong gloss (cars/water), paint mask (vertex alpha -> _Paint) and emission (UV2.x).
// No shadows, no additional lights, SRP-Batcher compatible.
Shader "TurboRush/VertexLit"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Paint ("Paint (vertex alpha mask)", Color) = (1,1,1,1)
        _Gloss ("Gloss", Range(0,1)) = 0
        _Emission ("Emission strength", Range(0,3)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _Paint;
                half _Gloss;
                half _Emission;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half2 fogEmit : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.color = i.color;
                o.fogEmit = half2(ComputeFogFactor(p.positionCS.z), i.uv2.x);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                half3 albedo = tex * _BaseColor.rgb * i.color.rgb * lerp(half3(1,1,1), _Paint.rgb, i.color.a);
                half3 n = normalize(i.normalWS);
                Light L = GetMainLight();
                half ndl = saturate(dot(n, L.direction) * 0.6h + 0.4h);
                half3 col = albedo * (SampleSH(n) + L.color * ndl);
                if (_Gloss > 0.001h)
                {
                    half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                    half3 h = normalize(L.direction + v);
                    half spec = pow(saturate(dot(n, h)), 40.0h) * _Gloss;
                    half rim = pow(1.0h - saturate(dot(n, v)), 4.0h) * 0.25h * _Gloss;
                    col += L.color * spec + rim;
                }
                col = lerp(col, albedo * _Emission, saturate(i.fogEmit.y));
                col = MixFog(col, i.fogEmit.x);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
