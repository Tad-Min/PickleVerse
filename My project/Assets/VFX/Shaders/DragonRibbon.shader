// PickleVerse VFX - the burning sine curve the Dragon Ruby dragon leaves behind. A flat strip (x = 0..1 along the path,
// y = -1..1 across) is laid on the same wave as the dragon; _Sign -1 draws the mirrored golden counter-wave. Premultiplied,
// so red fire stays red over the blue court.
Shader "PickleVerse/VFX/Dragon Ribbon"
{
    Properties
    {
        _BaseMap("Fire (RGB value, A coverage)", 2D) = "white" {}
        [HDR] _Hot("Hot colour", Color) = (4, 2.2, .7, 1)
        [HDR] _Mid("Mid colour", Color) = (2.4, .28, .05, 1)
        [HDR] _Tail("Tail colour", Color) = (.7, .02, .02, 1)
        _Width("Width (m)", Float) = .11
        _Sign("Wave sign", Float) = 1
        _Scroll("Flow speed", Float) = 1.5
        _Start("Start behind the ball (m)", Float) = .1
        _Cover("Background cover", Range(0, 1)) = .8
        _Fade("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Ribbon"
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
            #include "DragonSpine.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Hot, _Mid, _Tail;
                float _Width, _Sign, _Scroll, _Start, _Cover, _Fade;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 uv : TEXCOORD0; float2 life : TEXCOORD1; half fog : TEXCOORD2; };
            V vert(A a)
            {
                V o;
                float u = a.positionOS.x;
                float behind = lerp(_Start, _SpineInfo.w, u);
                float3 origin, forward, up, side;
                DragonFrame(behind, _Sign, origin, forward, up, side);
                float width = _Width * (1 - .65 * u) * DragonEase(u * 9);
                float3 p = origin + up * a.positionOS.y * width * .5;
                o.positionCS = TransformWorldToHClip(p);
                o.uv = float3(behind, a.positionOS.y * .5 + .5, u);
                o.life = float2(DragonEase((_SpineInfo.y - behind) / .18), 0);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float travelled = _SpineInfo.y - i.uv.x;             // pattern fixed in the world, plus its own flow
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, float2(travelled * _BaseMap_ST.x + _Time.y * _Scroll, i.uv.y));
                half core = saturate(1 - abs(i.uv.y - .5) * 2);
                half u = i.uv.z;
                half3 c = lerp(_Hot.rgb, _Mid.rgb, smoothstep(0, .35, u));
                c = lerp(c, _Tail.rgb, smoothstep(.35, 1, u));
                half a = saturate(t.a * (.5 + .9 * t.r) * core * 1.35) * (1 - u) * i.life.x * _Fade;
                half heart = core * core * core * (1 - u) * (1 - u) * .22 * i.life.x * _Fade;
                c = c * a + _Hot.rgb * heart;
                return half4(c * ComputeFogIntensity(i.fog), saturate(a * 1.4 + heart) * _Cover);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
