// Match-found stage effects on camera-facing quads (uv 0..1):
//   _Mode 0  smoke aura: tileable noise pushed outward and upward, blue core fading through navy to black
//   _Mode 1  ring burst: a soft glowing ring at the edge of the quad
//   _Mode 2  backdrop: dark arena gradient with out-of-focus stadium glows
//   _Mode 3  spark: a soft streak along uv.y
//   _Mode 4  solid: flat tint (court lines)
// Two texture taps plus one warped tap per pixel keeps the smoke cheap enough for phones.
Shader "PickleVerse/MatchFound/Energy"
{
    Properties
    {
        _NoiseTex ("Noise (tileable RGB fBm)", 2D) = "gray" {}
        _Mode ("Mode", Float) = 0
        _Intensity ("Intensity", Float) = 1
        _Color ("Tint", Color) = (.62,.82,1,1)
        _Seed ("Seed", Float) = 0
        _Aspect ("Quad aspect (h/w)", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off
        // Colour blends normally; alpha accumulates so the stage texture stays opaque under the UI.
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _NoiseTex;
            float4 _Color;
            float _Mode, _Intensity, _Seed, _Aspect, _MFTime;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            float3 SmokeRamp(float x)
            {
                float3 c = lerp(float3(0, 0, 0), float3(0, .005, .02), saturate(x / .30));
                c = lerp(c, float3(.01, .07, .36), saturate((x - .30) / .22));
                c = lerp(c, float3(.10, .38, 1.0), saturate((x - .52) / .26));
                return lerp(c, float3(.70, .88, 1.0), saturate((x - .78) / .22));
            }

            float Fbm(float2 q)
            {
                float n1 = tex2D(_NoiseTex, q * .5).r;
                float n2 = tex2D(_NoiseTex, q * 1.15 + float2(.37, .11)).g;
                float n3 = tex2D(_NoiseTex, (q + (n1 - .5) * .35) * .9).b;
                return n1 * .45 + n2 * .25 + n3 * .30;
            }

            float4 Smoke(float2 uv)
            {
                float2 p = (uv - .5) * 2;
                float r = length(p);
                float2 dir = p / max(r, 1e-4);
                float t = _MFTime + _Seed;
                // Two-phase flow map: smoke keeps drifting outward and upward but never stretches into streaks.
                float ph0 = frac(t * .11), ph1 = frac(t * .11 + .5);
                float w0 = 1 - abs(1 - 2 * ph0);
                float2 flow = dir * .30 + float2(0, .22);
                float2 q = p * .85 + float2(t * .012, _Seed * .37);
                float mix = lerp(Fbm(q - flow * ph1 + float2(.37, .21)), Fbm(q - flow * ph0), w0);
                float smoke = smoothstep(.44, .80, mix);
                float mask = 1 - smoothstep(.32, 1, length(p * float2(1, .92)));
                float core = 1 - smoothstep(0, .6, r);
                float density = saturate((smoke + core * .28) * mask);
                float tone = density * (1.08 - r * .55);
                float energy = density * _Intensity;
                float3 c = SmokeRamp(tone) * (.45 + 1.3 * energy);
                return float4(min(c, 1), saturate(energy * 1.25));
            }

            float4 Ring(float2 uv)
            {
                float r = length((uv - .5) * 2);
                float band = exp(-abs(r - .82) * 38) + .35 * exp(-abs(r - .82) * 9);
                return float4(_Color.rgb * (1 + band), saturate(band * _Intensity));
            }

            float4 Backdrop(float2 uv)
            {
                float2 p = uv - float2(.5, .44);
                float3 c = lerp(float3(.002, .004, .01), float3(.016, .04, .06), saturate(1 - length(p * float2(1.0, 1.25)) * 1.25));
                // soft stadium lights, out of focus
                const float3 gold = float3(1, .80, .52), teal = float3(.55, .85, .95);
                c += gold * .10 * exp(-dot(uv - float2(.10, .62), uv - float2(.10, .62)) * 90);
                c += gold * .07 * exp(-dot(uv - float2(.86, .60), uv - float2(.86, .60)) * 70);
                c += teal * .06 * exp(-dot(uv - float2(.95, .70), uv - float2(.95, .70)) * 60);
                c += teal * .05 * exp(-dot(uv - float2(.24, .80), uv - float2(.24, .80)) * 120);
                c += gold * .05 * exp(-dot(uv - float2(.64, .78), uv - float2(.64, .78)) * 140);
                return float4(c, 1);
            }

            float4 Spark(float2 uv)
            {
                float across = 1 - abs(uv.x * 2 - 1);
                float along = smoothstep(0, .25, uv.y) * (1 - smoothstep(.6, 1, uv.y));
                float a = pow(across, 3) * along * _Intensity;
                return float4(_Color.rgb * 1.4, saturate(a));
            }

            float4 frag(v2f i) : SV_Target
            {
                if (_Mode < .5) return Smoke(i.uv);
                if (_Mode < 1.5) return Ring(i.uv);
                if (_Mode < 2.5) return Backdrop(i.uv);
                if (_Mode < 3.5) return Spark(i.uv);
                return float4(_Color.rgb, _Color.a * _Intensity);
            }
            ENDCG
        }
    }
}
