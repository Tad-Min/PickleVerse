// PickleVerse - Blue Phoenix paddle. PBR with a clear coat over the black lacquer and the blue enamel, dark cavities from the
// baked occlusion, and electric blue fire that breathes, drifts up the paddle and crackles along the flames and feathers.
// Mask: R metallic, G occlusion, B glow (blue fire), A smoothness.
Shader "PickleVerse/Paddle/Blue Phoenix"
{
    Properties
    {
        [MainTexture] _BaseMap("Colour", 2D) = "white" {}
        [Normal] _BumpMap("Normal", 2D) = "bump" {}
        _MaskMap("Metallic (R) Occlusion (G) Glow (B) Smoothness (A)", 2D) = "white" {}
        _BumpScale("Normal strength", Range(0, 2)) = 1
        _Cavity("Cavity depth", Range(0, 1)) = .55
        _Lift("Colour kept in the shade", Range(0, .5)) = .10
        _Coat("Clear coat", Range(0, 1)) = .6
        _Glow("Blue fire glow", Range(0, 6)) = 2.2
        [HDR] _Spark("Crackle colour", Color) = (.9, 2.4, 6, 1)
        _Crackle("Crackle amount", Range(0, 1)) = .55
        [HDR] _Sheen("Blue rim", Color) = (.10, .45, 1.6, 1)
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
            #pragma multi_compile_instancing
            #define _CLEARCOAT 1
            #define _NORMALMAP 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Spark, _Sheen;
                half _BumpScale, _Cavity, _Lift, _Coat, _Glow, _Crackle;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3; half fog : TEXCOORD4; float3 positionOS : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            Varyings vert(Attributes a)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(a);
                UNITY_TRANSFER_INSTANCE_ID(a, o);
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
                UNITY_SETUP_INSTANCE_ID(i);
                half3 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, i.uv);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                half3 nGeo = normalize(i.normalWS), t = normalize(i.tangentWS.xyz), b = cross(nGeo, t) * i.tangentWS.w;
                half3x3 tbn = half3x3(t, b, nGeo);
                half3 n = normalize(mul(normalTS, tbn));
                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half facing = saturate(dot(n, v));
                half fire = mask.b;
                half3 albedo = color * lerp(1, mask.g, _Cavity);
                // blue fire: breathing glow that drifts up the paddle, with thin electric threads running through it
                float2 flow = i.positionOS.xy * 11 + float2(0, -_Time.y * .35);
                half heat = saturate(Noise(flow) * .6 + Noise(flow * 2.3 + 5.7) * .4);
                half breath = .70 + .30 * sin(_Time.y * 2.1 + heat * 6.2832);
                float2 q = i.positionOS.xy * 46 + float2(_Time.y * .9, -_Time.y * 1.7);
                half thread = 1 - abs(Noise(q) * 2 - 1);
                thread = pow(saturate(thread), 14) * smoothstep(.45, .8, Noise(i.positionOS.xy * 7 + _Time.y * .6));
                half3 emission = fire * color * _Glow * breath * (.75 + .5 * heat);
                emission += _Spark.rgb * thread * fire * _Crackle;
                emission += _Sheen.rgb * pow(1 - facing, 4) * (.25 + fire) * .35;
                emission += albedo * _Lift * lerp(1, mask.g, .6);   // no dead black on the lacquer away from the sun

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.alpha = 1;
                surface.metallic = mask.r;
                surface.smoothness = mask.a;
                surface.normalTS = normalTS;
                surface.occlusion = lerp(1, mask.g, .7);
                surface.emission = emission;
                surface.clearCoatMask = _Coat * smoothstep(.62, .82, mask.a);
                surface.clearCoatSmoothness = .86;
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
