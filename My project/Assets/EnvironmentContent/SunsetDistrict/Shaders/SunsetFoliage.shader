// Card-based foliage for the golden-hour district: alpha-tested leaf clusters with canopy-shaped normals,
// GPU wind, backlit translucency and per-tree hue variation. Vertex colour: R bend weight, G phase,
// B occlusion, A tone. One shader serves bark (opaque), leaves and the distant billboard LOD.
//
// _InstancingHint is deliberately outside UnityPerMaterial: that makes the shader SRP-Batcher
// incompatible so repeated trees draw with GPU instancing (one instanced draw per mesh/LOD) instead of
// one SRP-batched draw per tree.
Shader "PickleVerse/Environment/Sunset Foliage"
{
    Properties
    {
        _BaseMap("Atlas", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha cutoff", Range(0, 1)) = 0.45
        _WindStrength("Wind metres", Range(0, 0.4)) = 0.12
        _WindSpeed("Wind speed", Range(0, 3)) = 0.9
        _Translucency("Backlit translucency", Range(0, 1)) = 0.55
        _Billboard("Billboard LOD", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
        [Toggle(_ALPHATEST_ON)] _AlphaTest("Alpha test", Float) = 1
        [Toggle] _AlphaToMask("Alpha to coverage", Float) = 1
        [HideInInspector] _InstancingHint("Instancing hint", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        float _InstancingHint;
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff, _WindStrength, _WindSpeed, _Translucency, _Billboard, _Cull, _AlphaTest, _AlphaToMask;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float3 WindPosition(Attributes a)
        {
            float3 p = TransformObjectToWorld(a.positionOS.xyz);
            float3 origin = TransformObjectToWorld(float3(0, 0, 0));
            float phase = dot(origin.xz, float2(0.37, 0.61)) + a.color.g * 6.2832;
            float t = _Time.y * _WindSpeed;
            float gust = sin(t * 0.43 + origin.x * 0.05) * 0.5 + 0.5;
            float sway = sin(t * 1.3 + phase) * 0.6 + sin(t * 2.9 + phase * 1.7) * 0.25;
            float flutter = sin(t * 7.0 + phase * 3.1 + p.y * 2.0) * 0.15;
            float w = _WindStrength * a.color.r * (0.55 + 0.45 * gust);
            p.xz += float2(1.0, 0.45) * (w * (sway + flutter));
            p.y += w * flutter * 0.4;
            return p;
        }

        half Coverage(float2 uv)
        {
            half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a;
            half sharp = saturate((a - _Cutoff) / max(fwidth(a), 1e-4) + 0.5);
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
                half4 botanical : TEXCOORD3;      // x occlusion, y tone, z per-tree hue, w bend weight
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
                o.positionWS = WindPosition(a);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(a.normalOS);
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                half hue = frac(sin(dot(origin.xz, float2(12.9898, 78.233))) * 43758.5453);
                o.botanical = half4(a.color.b, a.color.a, hue, a.color.r);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half coverage = 1;
                #if defined(_ALPHATEST_ON)
                    coverage = Coverage(i.uv);
                #endif
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                // Gentle per-tree / per-cluster variation: some crowns lean yellow-olive, some deeper green.
                half3 warm = albedo * half3(1.10, 1.02, 0.80);
                half3 deep = albedo * half3(0.86, 0.98, 0.94);
                albedo = lerp(deep, warm, saturate(i.botanical.z * 0.7 + i.botanical.y * 0.3));
                half ao = lerp(0.45, 1.0, i.botanical.x);
                half3 n = normalize(i.normalWS);
                if (_Billboard < 0.5 && _Translucency <= 0) n *= IS_FRONT_VFACE(face, 1, -1);   // bark only
                half3 v = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 l = mainLight.direction;
                half wrap = saturate((dot(n, l) + 0.4) / 1.4);
                half lit = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                // Leaves glow when the low sun is behind them (golden-hour rim on every crown).
                half back = pow(saturate(dot(v, -l)), 3) * _Translucency * (0.35 + 0.65 * i.botanical.x);
                half3 lighting = mainLight.color * lit * (wrap * ao + back * half3(1.15, 1.0, 0.45));
                lighting += SampleSH(n) * ao;

                #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light al = GetAdditionalLight(lightIndex, i.positionWS);
                        lighting += al.color * (al.distanceAttenuation * saturate((dot(n, al.direction) + 0.4) / 1.4) * ao);
                    LIGHT_LOOP_END
                #endif
                return half4(MixFog(albedo * lighting, i.fog), coverage);
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
                float3 p = WindPosition(a);
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
                o.positionCS = TransformWorldToHClip(WindPosition(a));
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
                o.positionCS = TransformWorldToHClip(WindPosition(a));
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                o.normalWS = TransformObjectToWorldNormal(a.normalOS);
                return o;
            }
            half4 fragDN(V i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                #endif
                return half4(NormalizeNormalPerPixel(i.normalWS), 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
