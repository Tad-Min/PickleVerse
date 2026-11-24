// PickleVerse VFX — paddle-skin ball trail ribbon (TrailRenderer). Additive; colour from the trail gradient (vertex colour),
// a faceted ribbon texture that slides along the trail, and hashed twinkles so the ribbon glitters as it fades.
Shader "PickleVerse/VFX/Skin Trail"
{
    Properties
    {
        _BaseMap("Ribbon (RGB value, A coverage)", 2D) = "white" {}
        [HDR] _Tint("Tint", Color) = (1, 1, 1, 1)
        _Intensity("Intensity", Range(0, 6)) = 2.2
        _Scroll("Facet scroll speed", Float) = 1.6
        _Twinkle("Twinkle amount", Range(0, 1)) = 0.6
        _TwinkleDensity("Twinkle cells along the trail", Float) = 40
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Trail"
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
                half _Intensity, _Scroll, _Twinkle, _TwinkleDensity;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };
            float Hash11(float p) { p = frac(p * 0.1031); p *= p + 33.33; p *= p + p; return frac(p); }
            V vert(A a)
            {
                V o;
                o.positionCS = TransformObjectToHClip(a.positionOS.xyz);
                o.color = a.color * _Tint;
                o.uv = a.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                // u: 0 at the ball (head) -> 1 at the tail (Stretch texture mode); v: across the ribbon.
                float2 uv = float2(i.uv.x * _BaseMap_ST.x - _Time.y * _Scroll, i.uv.y);
                half4 rib = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                float cell = floor(i.uv.x * _TwinkleDensity);
                half phase = Hash11(cell * 7.13 + floor(_Time.y * 9.0));
                half twinkle = 1 + _Twinkle * (smoothstep(0.82, 1.0, phase) * 2.5 - 0.35);
                half core = saturate(1 - abs(i.uv.y - 0.5) * 2);
                half3 c = i.color.rgb * rib.rgb * rib.a * twinkle * _Intensity * i.color.a;
                c += i.color.a * core * core * 0.35 * (1 - i.uv.x);   // white-hot head
                return half4(c * ComputeFogIntensity(i.fog), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
