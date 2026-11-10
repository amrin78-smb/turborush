// Additive glow for power-up effects: turbo flames (flat) and the shield bubble (_Rim = 1: bright edges, clear middle).
// Vertex colour × _Color, no textures, no lighting, no depth writes. Cheap on GLES3 and Vulkan.
Shader "TurboRush/Glow"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Rim ("Rim (0 = flat, 1 = bubble)", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Back
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Rim;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; };
            V vert(A i)
            {
                V o;
                float3 p = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(p);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewWS = GetWorldSpaceViewDir(p);
                o.color = i.color;
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half ndv = saturate(abs(dot(normalize(i.normalWS), normalize(i.viewWS))));
                half rim = lerp(1.0h, (1.0h - ndv) * (1.0h - ndv) * 1.7h + 0.06h, _Rim);
                return half4(i.color.rgb * _Color.rgb * (_Color.a * rim), 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
