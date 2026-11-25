// PickleVerse - Dragon Ruby paddle. PBR with a clear coat over the ruby enamel, a second ruby layer seen through the surface
// (parallax), dark cavities from the baked occlusion, and embers that smoulder in the relief and in the dragon's eyes.
// Mask: R metallic, G occlusion, B ember, A smoothness.
Shader "PickleVerse/Paddle/Dragon Ruby"
{
    Properties
    {
        [MainTexture] _BaseMap("Colour", 2D) = "white" {}
        [Normal] _BumpMap("Normal", 2D) = "bump" {}
        _MaskMap("Metallic (R) Occlusion (G) Ember (B) Smoothness (A)", 2D) = "white" {}
        _BumpScale("Normal strength", Range(0, 2)) = 1
        _Cavity("Cavity depth", Range(0, 1)) = .5
        _Lift("Colour kept in the shade", Range(0, .5)) = .13
        _RubyDepth("Ruby inner depth", Range(0, .03)) = .012
        _Coat("Clear coat", Range(0, 1)) = .55
        [HDR] _Ember("Ember colour", Color) = (2.6, .62, .10, 1)
        [HDR] _Vein("Smoulder in the cavities", Color) = (.55, .04, .01, 1)
        [HDR] _Sheen("Warm rim", Color) = (.9, .16, .05, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #define _CLEARCOAT 1
            #define _NORMALMAP 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Ember, _Vein, _Sheen;
                half _BumpScale, _Cavity, _Lift, _RubyDepth, _Coat;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3; half fog : TEXCOORD4; float3 positionOS : TEXCOORD5;
            };
            Varyings vert(Attributes a)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(a);
                VertexPositionInputs p = GetVertexPositionInputs(a.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(a.normalOS, a.tangentOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = n.normalWS;
                o.tangentWS = half4(n.tangentWS, a.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                o.positionOS = a.positionOS.xyz;
                return o;
            }
            float Hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float Noise(float2 x)
            {
                float2 i = floor(x), f = frac(x);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x), lerp(Hash21(i + float2(0, 1)), Hash21(i + 1), f.x), f.y);
            }
            half4 frag(Varyings i) : SV_Target
            {
                half3 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, i.uv);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                half3 nGeo = normalize(i.normalWS), t = normalize(i.tangentWS.xyz), b = cross(nGeo, t) * i.tangentWS.w;
                half3x3 tbn = half3x3(t, b, nGeo);
                half3 n = normalize(mul(normalTS, tbn));
                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half facing = saturate(dot(n, v));
                // ruby: what is neither gold nor matt lacquer; a deeper layer shows through it and shifts with the view
                half ruby = smoothstep(.10, .30, color.r - color.g * 1.35) * (1 - mask.r);
                half3 viewTS = mul(tbn, v);
                float2 inside = i.uv - viewTS.xy / max(viewTS.z, .35) * _RubyDepth;
                half3 deep = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, inside).rgb;
                half3 albedo = lerp(color, color * .72 + deep * deep * half3(.75, .20, .16), ruby * facing * .8);
                albedo *= lerp(1, mask.g, _Cavity);
                // embers: breathing, drifting slowly up the paddle
                float2 flow = i.positionOS.xy * 9 + float2(0, -_Time.y * .22);
                half heat = saturate(Noise(flow) * .65 + Noise(flow * 2.7 + 3.1) * .35);
                half pulse = .62 + .38 * sin(_Time.y * 1.6 + heat * 6);
                half3 emission = mask.b * _Ember.rgb * pulse * color;
                emission += _Vein.rgb * smoothstep(.45, .85, heat) * pow(saturate(1 - mask.g), 1.5) * ruby;
                emission += _Sheen.rgb * pow(1 - facing, 4) * ruby * .35;
                emission += albedo * _Lift * lerp(1, mask.g, .6);   // no dark reflections on gold and ruby away from the sun

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.alpha = 1;
                surface.metallic = mask.r;
                surface.smoothness = mask.a;
                surface.normalTS = normalTS;
                surface.occlusion = lerp(1, mask.g, .7);
                surface.emission = emission;
                surface.clearCoatMask = _Coat * ruby * smoothstep(.45, .7, mask.a);
                surface.clearCoatSmoothness = .84;
                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.normalWS = n;
                input.viewDirectionWS = v;
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.tangentToWorld = tbn;
                input.bakedGI = SampleSH(n);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);
                half4 result = UniversalFragmentPBR(input, surface);
                result.rgb = MixFog(result.rgb, i.fog);
                return half4(result.rgb, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
