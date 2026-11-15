// Shared surface shader for the golden-hour venue and district (PickleVerse).
// Albedo = detail map x vertex tint (tint stored with gamma 2 and squared here). Normal maps use a
// derivative tangent frame, so meshes need no tangents. The low sun's static shadows on the ground come
// from a baked top-down mask (global _PV_SunMask, set by MatchEnvironmentLighting) combined with the
// realtime shadow map with min(), so players and the ball still cast live shadows on Android.
Shader "PickleVerse/Environment/Sunset Surface"
{
    Properties
    {
        _BaseMap("Detail albedo", 2D) = "white" {}
        [Normal] _BumpMap("Normal map", 2D) = "bump" {}
        _BumpScale("Normal strength", Range(0, 2)) = 1
        _Smoothness("Smoothness", Range(0, 1)) = 0.3
        _Metallic("Metallic", Range(0, 1)) = 0
        _GlassSmoothness("Glass smoothness (albedo alpha)", Range(0, 1)) = 0
        _Cutoff("Alpha cutoff", Range(0, 1)) = 0.5
        _SunMask("Receives baked sun mask", Float) = 0
        _Glow("Night glow", Float) = 0
        _Water("Water ripples", Float) = 0
        _Unlit("Backdrop (unlit)", Float) = 0
        _Dither("Distance dither (fine weaves)", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Toggle(_NORMALMAP)] _UseNormalMap("Use normal map", Float) = 0
        [Toggle(_ALPHATEST_ON)] _AlphaTest("Alpha test", Float) = 0
        [Toggle] _AlphaToMask("Alpha to coverage", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        // Globals (not material properties): set by MatchEnvironmentLighting / StadiumEnvironment.
        TEXTURE2D(_PV_SunMask); SAMPLER(sampler_PV_SunMask);
        float4 _PV_SunMaskParams;      // x = 1 / span, y = district ground y, zw = mask origin xz
        float _PV_SunMaskOn, _PV_NightGlow;
        half4 _PV_SkyZenith, _PV_SkyHorizon;
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BumpMap_ST, _BaseMap_TexelSize;
            half _BumpScale, _Smoothness, _Metallic, _GlassSmoothness, _Cutoff;
            half _SunMask, _Glow, _Water, _Unlit, _Dither, _Cull, _UseNormalMap, _AlphaTest, _AlphaToMask;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        half AlphaCoverage(float2 uv, float4 positionCS)
        {
            half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a;
            // Sharpened alpha: crisp cut-outs without MSAA, anti-aliased edges with alpha-to-coverage.
            half sharp = saturate((a - _Cutoff) / max(fwidth(a), 1e-4) + 0.5);
            if (_Dither > 0.5)
            {
                // Fine weaves (chain-link, net) cannot be resolved once a pixel covers several texels:
                // switch to the mip's true average coverage and a stable screen-door pattern, so a far or
                // grazing fence reads as a see-through mesh instead of a solid wall (A2C smooths it with MSAA).
                float2 dx = ddx(uv * _BaseMap_TexelSize.zw), dy = ddy(uv * _BaseMap_TexelSize.zw);
                half far = saturate((sqrt(max(dot(dx, dx), dot(dy, dy))) - 1.5) * 0.25);
                sharp = lerp(sharp, a, far);
                half noise = frac(52.9829189 * frac(dot(floor(positionCS.xy), float2(0.06711056, 0.00583715))));
                clip(sharp - (noise * 0.98 + 0.01));
                return sharp;
            }
            clip(sharp - 0.02);
            return sharp;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            AlphaToMask [_AlphaToMask]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : TEXCOORD3;
                half fog : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes a)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(a);
                UNITY_TRANSFER_INSTANCE_ID(a, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(a.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(a.normalOS);
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                o.color = a.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half3 SkyReflection(half3 r)
            {
                half3 sky = lerp(_PV_SkyHorizon.rgb, _PV_SkyZenith.rgb, saturate(r.y * 1.6));
                return r.y < 0 ? _PV_SkyHorizon.rgb * 0.35 : sky;
            }

            half3 Specular(half3 n, half3 v, half3 l, half3 f0, half power)
            {
                half3 h = SafeNormalize(l + v);
                half nh = saturate(dot(n, h));
                half3 fresnel = f0 + (1 - f0) * pow(1 - saturate(dot(h, v)), 5);
                return fresnel * ((power + 8) * 0.125 * pow(nh, power));
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half coverage = 1;
                #if defined(_ALPHATEST_ON)
                    coverage = AlphaCoverage(i.uv, i.positionCS);
                #endif
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 albedo = tex.rgb * (i.color.rgb * i.color.rgb);
                half3 geometric = normalize(i.normalWS) * IS_FRONT_VFACE(face, 1, -1);
                half3 n = geometric;
                #if defined(_NORMALMAP)
                    float2 uvN = i.uv;
                    half3 tn;
                    if (_Water > 0.5)
                    {
                        float t = _Time.y;
                        half3 a = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvN * 1.7 + float2(t * 0.021, t * 0.013)), _BumpScale);
                        half3 b = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvN * 1.1 - float2(t * 0.017, -t * 0.024)), _BumpScale);
                        tn = normalize(half3(a.xy + b.xy, a.z * b.z));
                    }
                    else
                        tn = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvN), _BumpScale);
                    // Cotangent frame from screen-space derivatives (no mesh tangents needed).
                    float3 dp1 = ddx(i.positionWS), dp2 = ddy(i.positionWS);
                    float2 duv1 = ddx(uvN), duv2 = ddy(uvN);
                    float3 dp2perp = cross(dp2, geometric), dp1perp = cross(geometric, dp1);
                    float3 T = dp2perp * duv1.x + dp1perp * duv2.x;
                    float3 B = dp2perp * duv1.y + dp1perp * duv2.y;
                    float inv = rsqrt(max(max(dot(T, T), dot(B, B)), 1e-20));
                    n = normalize(T * (inv * tn.x) + B * (inv * tn.y) + geometric * tn.z);
                #endif

                if (_Unlit > 0.5)
                {
                    // Distant city backdrop: already hazy in the texture; dims at night, windows glow.
                    half3 backdrop = albedo * (1 - 0.88 * _PV_NightGlow) + albedo * saturate(tex.r - tex.b) * _PV_NightGlow * 1.5;
                    return half4(MixFog(backdrop, i.fog), coverage);
                }

                half smooth = _Smoothness;
                if (_GlassSmoothness > 0) smooth = lerp(_Smoothness, _GlassSmoothness, tex.a);
                half metallic = _Metallic;
                half3 diffuseColor = albedo * (1 - metallic);
                half3 f0 = lerp(half3(0.04, 0.04, 0.04), albedo, metallic);
                if (_Water > 0.5) { diffuseColor = albedo * 0.35; f0 = 0.02; }
                half power = exp2(10 * smooth + 1);
                half3 v = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half shadow = mainLight.shadowAttenuation;
                if (_SunMask > 0.5 && _PV_SunMaskOn > 0.5)
                {
                    float2 muv = 0.5 + (i.positionWS.xz - _PV_SunMaskParams.zw) * _PV_SunMaskParams.x;
                    half baked = SAMPLE_TEXTURE2D(_PV_SunMask, sampler_PV_SunMask, muv).r;
                    half w = saturate(1 - (i.positionWS.y - _PV_SunMaskParams.y - 0.55) * 3.3) * saturate((geometric.y - 0.6) * 5);
                    shadow = min(shadow, lerp(1, baked, w));
                }
                half nl = saturate(dot(n, mainLight.direction));
                half3 radiance = mainLight.color * (mainLight.distanceAttenuation * shadow * nl);
                half3 color = radiance * (diffuseColor + Specular(n, v, mainLight.direction, f0, power));

                #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light l = GetAdditionalLight(lightIndex, i.positionWS);
                        half lnl = saturate(dot(n, l.direction));
                        color += l.color * (l.distanceAttenuation * lnl) * (diffuseColor + Specular(n, v, l.direction, f0, power));
                    LIGHT_LOOP_END
                #endif

                // Ambient: probe/trilight diffuse + an analytic golden-hour sky reflection.
                color += SampleSH(n) * diffuseColor;
                half nv = saturate(dot(n, v));
                half3 envFresnel = f0 + (max(smooth.xxx, f0) - f0) * pow(1 - nv, 5);
                half envAmount = lerp(smooth * smooth, max(smooth, 0.55), metallic);
                if (_Water > 0.5) envAmount = 1;
                color += SkyReflection(reflect(-v, n)) * envFresnel * envAmount;

                // Night: lamp lenses glow; facades light a random set of windows.
                if (_Glow > 0)
                {
                    half lit = 1;
                    if (_GlassSmoothness > 0)
                    {
                        float2 cell = floor(i.uv * 8);
                        lit = step(0.55, frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453)) * tex.a;
                    }
                    color += albedo * (_Glow * _PV_NightGlow * 2.2 * lit);
                }
                return half4(MixFog(color, i.fog), coverage);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vertShadow(Attributes a)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(a);
                UNITY_TRANSFER_INSTANCE_ID(a, o);
                float3 p = TransformObjectToWorld(a.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(a.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 d = normalize(_LightPosition - p);
                #else
                    float3 d = _LightDirection;
                #endif
                float4 clip = TransformWorldToHClip(ApplyShadowBias(p, n, d));
                #if UNITY_REVERSED_Z
                    clip.z = min(clip.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    clip.z = max(clip.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = clip;
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                return o;
            }
            half4 fragShadow(V i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vertDepth(Attributes a)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(a);
                o.positionCS = TransformObjectToHClip(a.positionOS.xyz);
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                return o;
            }
            half4 fragDepth(V i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertDN
            #pragma fragment fragDN
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half3 normalWS : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vertDN(Attributes a)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(a);
                o.positionCS = TransformObjectToHClip(a.positionOS.xyz);
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                o.normalWS = TransformObjectToWorldNormal(a.normalOS);
                return o;
            }
            half4 fragDN(V i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                #endif
                return half4(NormalizeNormalPerPixel(i.normalWS * IS_FRONT_VFACE(face, 1, -1)), 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
