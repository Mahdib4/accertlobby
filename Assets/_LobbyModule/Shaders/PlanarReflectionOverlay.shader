// ACCERTONE Lobby Module: adds a planar reflection (from PlanarFloorReflection) over the polished floor.
Shader "ACCERTONE/Planar Reflection Overlay"
{
    Properties
    {
        [NoScaleOffset] _ReflectionTex ("Reflection (set by script)", 2D) = "black" {}
        _Intensity ("Intensity", Range(0, 2)) = 0.9
        _BaseReflectance ("Base Reflectance", Range(0, 1)) = 0.07
        _FresnelPower ("Fresnel Power", Range(1, 8)) = 4
        _Blur ("Blur (mip)", Range(0, 6)) = 1.3
        _FadeCenter ("Fade Centre (xz)", Vector) = (0, 0, 0, 0)
        _FadeRadius ("Fade Radius", Float) = 13
        [Toggle] _FlipY ("Flip Y", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Offset -1, -1
        Cull Back

        Pass
        {
            Name "PlanarReflection"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);

            CBUFFER_START(UnityPerMaterial)
                half _Intensity;
                half _BaseReflectance;
                half _FresnelPower;
                half _Blur;
                float4 _FadeCenter;
                float _FadeRadius;
                half _FlipY;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.screenPos.xy / i.screenPos.w;
                if (_FlipY > 0.5) uv.y = 1.0 - uv.y;
                half3 refl = SAMPLE_TEXTURE2D_LOD(_ReflectionTex, sampler_ReflectionTex, uv, _Blur).rgb;

                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half ndv = saturate(viewDir.y);
                half fresnel = _BaseReflectance + (1.0 - _BaseReflectance) * pow(1.0 - ndv, _FresnelPower);
                half fade = 1.0 - smoothstep(_FadeRadius * 0.55, _FadeRadius, distance(i.positionWS.xz, _FadeCenter.xz));

                half3 c = refl * fresnel * fade * _Intensity;
                c = MixFogColor(c, half3(0, 0, 0), i.fogFactor);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
