// ACCERTONE: soft light shaft for overhead showroom spots (URP). Apply to an open cone whose UV.y runs 0 (source) -> 1 (floor).
Shader "ACCERTONE/Light Beam"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 0.08
        _Falloff ("Length Falloff", Range(0.1, 8)) = 1.6
        _EdgeSoftness ("Edge Softness", Range(0.1, 8)) = 2.2
        _SourceFade ("Source Fade", Range(1, 40)) = 10
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Beam"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Falloff;
                half _EdgeSoftness;
                half _SourceFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half edge = pow(saturate(abs(dot(normalize(i.normalWS), viewDir))), _EdgeSoftness);
                half along = pow(saturate(1.0 - i.uv.y), _Falloff);
                half source = saturate(i.uv.y * _SourceFade);
                half3 c = _Color.rgb * (_Intensity * edge * along * source);
                c = MixFogColor(c, half3(0, 0, 0), i.fogFactor);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
