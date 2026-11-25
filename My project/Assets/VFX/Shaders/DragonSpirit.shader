// PickleVerse VFX - the red dragon that chases the ball (Dragon Ruby paddle). The mesh is straight (x = distance behind the
// ball, y = back, z = side) and is bent along the sine flight in the vertex stage. Vertex colour: rgb albedo, a glow.
Shader "PickleVerse/VFX/Dragon Spirit"
{
    Properties
    {
        [HDR] _Tint("Tint", Color) = (1, 1, 1, 1)
        _Light("Light response", Range(0, 2)) = .85
        _Glow("Glow", Range(0, 6)) = 1.7
        [HDR] _Rim("Rim fire", Color) = (2.3, .30, .04, 1)
        [HDR] _Burn("Dissolve edge", Color) = (5, 1.2, .14, 1)
        _ScaleSize("Scale size along the body (m)", Float) = .034
        _Length("Dragon length (m)", Float) = 2.36
        _Fade("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest+20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Spirit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "DragonSpine.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint, _Rim, _Burn;
                half _Light, _Glow;
                float _ScaleSize, _Length, _Fade;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half4 color : COLOR; float4 body : TEXCOORD2; half fog : TEXCOORD3; };

            float Hash(float3 p) { p = frac(p * .3183099 + .1); p *= 17; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float Noise(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x), lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x), lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            V vert(A a)
            {
                V o;
                float s = a.positionOS.x;
                float behind = max(s, 0) + _SpineInfo.z;
                float3 origin, forward, up, side;
                DragonFrame(behind, 1, origin, forward, up, side);
                // the part that has not left the paddle yet folds into the contact point
                float grown = DragonEase((_SpineInfo.y - behind) / .22);
                float flutter = sin(_Time.y * 13 + s * 9) * .010 * saturate(length(a.positionOS.yz) / .09 - .6);
                float3 p = origin - forward * min(s, 0) + (up * (a.positionOS.y + flutter) + side * a.positionOS.z) * grown;
                o.positionWS = p;
                o.positionCS = TransformWorldToHClip(p);
                o.normalWS = normalize(-forward * a.normalOS.x + up * a.normalOS.y + side * a.normalOS.z);
                o.color = a.color;
                o.body = float4(a.positionOS.xyz, grown);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float s = i.body.x;
                // dissolve: not grown yet, burning tail, fading out
                float n = Noise(float3(s * 20 + _Time.y * 3, i.body.yz * 34));
                float tail = smoothstep(_Length * .80, _Length * 1.04, s) * .85;
                float cover = min(i.body.w * 1.15, 1) * (1 - tail) * _Fade;
                float cut = cover * 1.18 - n - .06;
                clip(cut);
                half3 n0 = normalize(i.normalWS);
                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                // overlapping scales on the ruby trunk
                half trunk = step(.15, s) * step(s, 2.26) * saturate(1 - i.color.a * 2.2) * step(i.color.g, .2);
                float2 q = float2(s / _ScaleSize, atan2(i.body.z, i.body.y) * 2.4);
                q.x += .5 * fmod(floor(abs(q.y)), 2);
                float2 c = frac(q) - .5;
                half d = abs(c.x) + abs(c.y);
                half3 albedo = i.color.rgb * _Tint.rgb;
                albedo *= lerp(1, lerp(1.35, .22, smoothstep(.28, .62, d)), trunk);
                albedo += trunk * smoothstep(.34, .05, length(c - float2(-.12, .10))) * half3(.30, .05, .02);
                Light l = GetMainLight();
                half wrap = saturate(dot(n0, l.direction) * .5 + .5);
                half spec = pow(saturate(dot(n0, normalize(l.direction + v))), 42);
                half rim = pow(1 - saturate(dot(n0, v)), 2.6);
                half flame = .72 + .28 * sin(_Time.y * 17 - s * 15 + n * 5);
                half3 lit = albedo * (l.color * wrap * .85 + .40) * _Light + spec * l.color * .55;
                half3 col = lit + albedo * i.color.a * _Glow * flame + _Rim.rgb * rim * (.42 + .4 * i.color.a) * flame;
                half burn = 1 - smoothstep(0, .13, cut);
                col = lerp(col, _Burn.rgb, burn * .9);
                return half4(MixFog(col, i.fog), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
