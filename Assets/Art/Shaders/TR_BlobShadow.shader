// Soft round contact shadow under cars (multiply blend; analytic falloff, no texture).
Shader "TurboRush/BlobShadow"
{
    Properties { _Strength ("Strength", Range(0,1)) = 0.5 }
    SubShader
    {
        Tags { "Queue"="Transparent-50" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend DstColor Zero
        ZWrite Off
        Offset -1, -1
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half _Strength; CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv * 2 - 1; return o; }
            half4 frag(V i) : SV_Target
            {
                half r = saturate(1 - dot(i.uv, i.uv));
                half a = smoothstep(0, 0.8, r) * _Strength;
                return half4(1 - a, 1 - a, 1 - a, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
