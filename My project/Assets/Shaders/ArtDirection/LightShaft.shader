// PickleVerse Art Direction — soft volumetric light shaft card (Art Bible §04: subtle atmosphere, never over the play area).
// Additive, double-sided; fades when seen edge-on, when the camera is close, at night (_PV_NightGlow) and with distance fog.
Shader "PickleVerse/FX/Light Shaft"
{
    Properties
    {
        _BaseMap("Shaft mask", 2D) = "white" {}
        [HDR] _BaseColor("Colour", Color) = (1, 0.86, 0.66, 1)
        _Intensity("Intensity", Range(0, 2)) = 0.18
        _NearFade("Camera near fade (m)", Float) = 4
        _Drift("Dust drift", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Shaft"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PVCommon.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float _PV_NightGlow;
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Intensity, _NearFade, _Drift;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; half3 normalWS : TEXCOORD2; half fog : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vert(A a)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(a);
                UNITY_TRANSFER_INSTANCE_ID(a, o);
                o.positionWS = TransformObjectToWorld(a.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(a.normalOS);
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half mask = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a;
                float3 toCam = _WorldSpaceCameraPos - i.positionWS;
                half facing = saturate(abs(dot(normalize(toCam), normalize(i.normalWS))) * 1.6 - 0.15);
                half nearFade = saturate(length(toCam) / max(_NearFade, 0.01) - 0.4);
                // Slow dust drift inside the beam so it never reads as a flat card.
                half dust = 1 - _Drift + _Drift * PV_ValueNoise(i.positionWS.xy * 2.3 + float2(0, _Time.y * 0.08));
                half day = 1 - saturate(_PV_NightGlow);
                half3 c = _BaseColor.rgb * (mask * facing * nearFade * dust * day * _Intensity);
                return half4(c * ComputeFogIntensity(i.fog), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
