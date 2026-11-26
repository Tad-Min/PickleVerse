// PickleVerse VFX - fire sprites of the Dragon Ruby and Blue Phoenix effects (particles and the ball trail). Premultiplied: the flame covers
// what is behind it (_Cover), so red fire stays red over the blue court instead of adding up to pink.
Shader "PickleVerse/VFX/Dragon Fire"
{
    Properties
    {
        _BaseMap("Sprite / flipbook", 2D) = "white" {}
        _Intensity("Intensity", Range(0, 8)) = 2.2
        _Cover("Background cover", Range(0, 1)) = .75
        _Hot("White-hot core", Range(0, 1)) = .5
        _HotColor("Core colour (linear rgb)", Vector) = (1, .86, .55, 1)
        _Scroll("Texture flow (trail)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Fire"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
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
                half4 _HotColor;
                half _Intensity, _Cover, _Hot, _Scroll;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };
            V vert(A a)
            {
                V o;
                o.positionCS = TransformObjectToHClip(a.positionOS.xyz);
                o.color = a.color;
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap) - float2(_Time.y * _Scroll, 0);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half4 s = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half a = s.a * i.color.a;
                half value = max(s.r, max(s.g, s.b));
                half3 c = s.rgb * lerp(i.color.rgb, _HotColor.rgb, value * value * _Hot) * _Intensity;
                return half4(c * a * ComputeFogIntensity(i.fog), a * _Cover);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
