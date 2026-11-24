// PickleVerse VFX — additive particle for sparkles and glints. Particle colour (vertex colour) x flipbook sprite (UVs come
// from the Particle System's texture sheet animation) x HDR intensity so bloom picks it up; fog fades it with distance.
Shader "PickleVerse/VFX/Additive Flipbook"
{
    Properties
    {
        _BaseMap("Sprite / flipbook", 2D) = "white" {}
        [HDR] _Tint("Tint", Color) = (1, 1, 1, 1)
        _Intensity("Intensity", Range(0, 8)) = 2.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Sparkle"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Tint;
                half _Intensity;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };
            V vert(A a)
            {
                V o;
                o.positionCS = TransformObjectToHClip(a.positionOS.xyz);
                o.color = a.color * _Tint;
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half4 s = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 c = s.rgb * s.a * i.color.rgb * i.color.a * _Intensity;
                return half4(c * ComputeFogIntensity(i.fog), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
