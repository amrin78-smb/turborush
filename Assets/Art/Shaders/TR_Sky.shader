// Gradient skybox with a soft sun. Colours are set per track at runtime.
Shader "TurboRush/Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0.16,0.45,0.92,1)
        _Horizon ("Horizon", Color) = (0.72,0.88,1,1)
        _Bottom ("Bottom", Color) = (0.55,0.75,0.95,1)
        _SunDir ("Sun direction", Vector) = (0.3,0.6,0.7,0)
        _SunColor ("Sun", Color) = (1,0.96,0.88,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Top; half4 _Horizon; half4 _Bottom; float4 _SunDir; half4 _SunColor;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };
            V vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.dir = i.positionOS.xyz; return o; }
            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                half3 c = d.y > 0 ? lerp(_Horizon.rgb, _Top.rgb, pow(saturate(d.y), 0.55)) : lerp(_Horizon.rgb, _Bottom.rgb, saturate(-d.y * 4.0));
                float s = saturate(dot(d, normalize(_SunDir.xyz)));
                c += _SunColor.rgb * (pow(s, 900.0) * 1.2 + pow(s, 12.0) * 0.18);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
