// PickleVerse VFX - black fire and ash of the Blue Phoenix effect (particles). Premultiplied like Dragon Fire, but the body
// of each sprite is dark ash that covers the court behind it, and only its embers glow, in the particle colour.
// Ash flakes: R marks the embers on the broken edges (_EmberRed 1). Black flames: the thin outer edge of a flame lick
// glows and its core stays black (_EmberEdge 1).
Shader "PickleVerse/VFX/Black Ash"
{
    Properties
    {
        _BaseMap("Sprite / flipbook", 2D) = "white" {}
        _Ash("Ash colour", Color) = (.010, .012, .018, 1)
        _Intensity("Ember glow", Range(0, 10)) = 3.5
        _Cover("Background cover", Range(0, 1)) = .85
        _EmberRed("Embers from the red channel", Range(0, 1)) = 1
        _EmberEdge("Embers on the outer edge", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+8" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Ash"
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
                half4 _Ash;
                half _Intensity, _Cover, _EmberRed, _EmberEdge;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };
            V vert(A a)
            {
                V o;
                o.positionCS = TransformObjectToHClip(a.positionOS.xyz);
                o.color = a.color;
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half4 s = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half a = s.a * i.color.a;
                half ember = saturate(s.r * _EmberRed + smoothstep(.7, .12, s.a) * _EmberEdge) * smoothstep(0, .06, s.a);
                half3 c = (_Ash.rgb * (1 - ember) + i.color.rgb * ember * _Intensity) * a;
                return half4(c * ComputeFogIntensity(i.fog), a * _Cover);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
