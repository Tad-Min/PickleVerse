// PickleVerse VFX - the blue phoenix that flies after the ball (Blue Phoenix paddle). The mesh is built in spine space
// (x = distance behind the ball, y = across the spread wings, z = the phoenix's back). The body and wings ride one rigid
// frame on the sine flight, where the wings unfold as the bird leaves the paddle and then flap; the long tail is bent along
// the ball's path like the dragon of the Dragon Ruby paddle. Feather tips char into black ash with blue embers on the
// burning edge and crumble away. UV0 feather atlas (rgb brightness, a coverage); UV1 x flap weight, y place along the
// feather; vertex colour rgb glow colour, a glow strength.
Shader "PickleVerse/VFX/Phoenix Spirit"
{
    Properties
    {
        _BaseMap("Feather atlas", 2D) = "white" {}
        [HDR] _Deep("Deep colour", Color) = (.01, .05, .45, 1)
        _Glow("Glow", Range(0, 8)) = 2.6
        [HDR] _Rim("Rim light", Color) = (.35, 1.2, 4.0, 1)
        [HDR] _Ember("Burning edge", Color) = (.55, 2.0, 6.0, 1)
        _Ash("Ash colour", Color) = (.010, .012, .018, 1)
        _AshReach("Ash creeping in from the tips", Range(0, 1)) = .38
        _Flap("Wing beat (degrees)", Range(0, 80)) = 34
        _FlapSpeed("Wing beat speed (rad/s)", Float) = 12
        _Dihedral("Wings raised (degrees)", Range(-30, 40)) = 10
        _Roll("Roll towards the camera (degrees)", Range(-80, 80)) = 0
        _Anchor("Body frame behind the beak (m)", Float) = .2
        _BodyEnd("Tail starts following the path (m)", Float) = .34
        _Unfold("Flight that spreads the wings (m)", Float) = .55
        _Scale("Size of the phoenix", Range(.5, 2)) = 1
        _Fade("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest+20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Spirit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
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
                half4 _Deep, _Rim, _Ember, _Ash;
                half _Glow, _AshReach;
                float _Flap, _FlapSpeed, _Dihedral, _Roll, _Anchor, _BodyEnd, _Unfold, _Scale, _Fade;
            CBUFFER_END
            #define SHOULDER (0.045 * _Scale)
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; float2 uv : TEXCOORD0; float2 attr : TEXCOORD1; };
            struct V
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half4 color : COLOR;
                float2 uv : TEXCOORD2; float4 info : TEXCOORD3; half fog : TEXCOORD4;
            };

            float Hash(float3 p) { p = frac(p * .3183099 + .1); p *= 17; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float Noise(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x), lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x), lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float2 Turn(float2 v, float a) { float c = cos(a), s = sin(a); return float2(v.x * c - v.y * s, v.x * s + v.y * c); }

            V vert(A a)
            {
                V o;
                float3 lp = a.positionOS.xyz * _Scale;
                float anchor = _Anchor * _Scale, bodyEnd = _BodyEnd * _Scale;
                float3 ln = a.normalOS;
                float s = lp.x;
                float flown = _SpineInfo.y;
                float open = DragonEase(flown / _Unfold);
                // wings: they open out of the body, then beat; the outer feathers lag behind the inner ones
                if (a.attr.x > 0)
                {
                    float side = lp.y < 0 ? -1 : 1;
                    float r = (abs(lp.y) - SHOULDER) * lerp(.22, 1, open);
                    float beat = sin(_Time.y * _FlapSpeed - r * 2.6 + 1.2);
                    float ang = radians(_Dihedral) + radians(_Flap) * beat * (.55 + .45 * a.attr.x) * open + radians(-55) * (1 - open);
                    float2 rel = Turn(float2(r, lp.z), ang);
                    lp.y = side * (SHOULDER + rel.x);
                    lp.z = rel.y;
                    float2 rn = Turn(float2(ln.y * side, ln.z), ang);
                    ln.y = rn.x * side;
                    ln.z = rn.y;
                }
                // the tail ripples as it trails
                float tail = smoothstep(bodyEnd, bodyEnd + .5, s);
                lp.z += sin(_Time.y * 7.5 - s * 5.5) * .035 * tail;
                lp.y += sin(_Time.y * 4.1 - s * 3.2) * .03 * tail;
                // roll the cross-section so the wings are seen a little from the side
                float roll = radians(_Roll);
                float2 yz = Turn(lp.yz, roll);
                float2 nyz = Turn(ln.yz, roll);

                // body and wings: one rigid frame on the flight, bursting out of the paddle
                float3 oA, fA, uA, sA;
                DragonFrame(anchor + _SpineInfo.z, 1, oA, fA, uA, sA);
                float burst = lerp(.18, 1, DragonEase(flown / .35));
                float3 pA = oA + (fA * (anchor - s) + uA * yz.x + sA * yz.y) * burst;
                float3 nA = -fA * ln.x + uA * nyz.x + sA * nyz.y;
                // tail: bent along the path; what has not left the paddle yet folds into the contact point
                float behind = max(s, 0) + _SpineInfo.z;
                float3 oS, fS, uS, sS;
                DragonFrame(behind, 1, oS, fS, uS, sS);
                float grown = DragonEase((flown - behind) / .22);
                float3 pS = oS - fS * min(s, 0) + (uS * yz.x + sS * yz.y) * grown;
                float3 nS = -fS * ln.x + uS * nyz.x + sS * nyz.y;
                float k = smoothstep(bodyEnd - .06, bodyEnd + .24, s);
                float3 p = lerp(pA, pS, k);
                o.positionWS = p;
                o.positionCS = TransformWorldToHClip(p);
                o.normalWS = normalize(lerp(nA, nS, k));
                o.color = a.color;
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                o.info = float4(s, a.attr.y, lerp(burst, grown, k), a.attr.x);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(V i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                float s = i.info.x, along = i.info.y;
                float n = Noise(i.positionWS * 16 + float3(0, _Time.y * 1.4, 0));
                float n2 = Noise(i.positionWS * 41 - _Time.y * 2.1);
                // charring: a burning line breathes in and out from the feather tips; behind it the feather is ash
                float burnLine = 1 - _AshReach * (.55 + .45 * sin(_Time.y * 2.2 + s * 3.1));
                float burnt = smoothstep(burnLine - .12, burnLine + .06, along + (n - .5) * .32);
                // holes crumble into the ash; the whole bird dissolves the same way when it fades
                float cut = t.a - .42 - burnt * smoothstep(.35, .8, n2) * .9 - (1 - _Fade) * (1.25 - n * .6) - (1 - i.info.z) * (1 - n) * .8;
                clip(cut);
                half3 nrm = normalize(i.normalWS) * IS_FRONT_VFACE(face, 1, -1);
                half3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half value = t.r;
                half flicker = .82 + .18 * sin(_Time.y * 19 + s * 13 + n * 6);
                half3 c = lerp(_Deep.rgb, i.color.rgb, saturate(value * 1.15)) * _Glow * (.55 + .45 * i.color.a) * flicker;
                c += _Rim.rgb * pow(1 - saturate(abs(dot(nrm, view))), 2.2) * (.45 + .55 * value);
                half edge = 1 - smoothstep(0, .10, cut);                       // rims of the holes and of the dissolve
                half front_ = saturate(1 - abs(burnt - .5) * 2.2);              // the burning line itself
                c = lerp(c, _Ash.rgb * (.6 + .8 * n2), burnt * .94);
                c += _Ember.rgb * (front_ * .9 + edge * (burnt * .9 + (1 - _Fade) * 1.2)) * (.6 + .4 * n2);
                return half4(MixFog(c, i.fog), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
