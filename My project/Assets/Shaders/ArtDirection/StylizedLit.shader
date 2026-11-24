// PickleVerse Art Direction — uber shader for every opaque/cut-out 3D surface (Art Bible §02 Material Language).
// Stylized PBR (GGX + wrapped diffuse + cool shadow tint + warm fresnel rim + edge highlight) with optional:
//   base gradient, world-space detail (triplanar), procedural noise variation, wet surface, wind deformation,
//   and the court feature (zone tint, zone roughness, play-wear and scuffs, grain) for the competition surface.
// SRP Batcher compatible: every material property lives in UnityPerMaterial in every pass.
// Property names are a superset of PickleVerse/Environment/Sunset Surface so those materials switch without loss.
Shader "PickleVerse/Stylized Lit"
{
    Properties
    {
        [Header(Base)]
        _BaseMap("Base Color", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _VertexTint("Vertex colour tint (stored gamma 2)", Float) = 0
        [Toggle(_NORMALMAP)] _UseNormalMap("Use normal map", Float) = 0
        [Normal] _BumpMap("Normal", 2D) = "bump" {}
        _BumpScale("Normal strength", Range(0, 2)) = 1
        [Toggle(_MASKMAP)] _UseMaskMap("Use mask map", Float) = 0
        _MaskMap("Mask (R metal, G AO, B edge, A smooth)", 2D) = "white" {}
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.35
        _SmoothnessRange("Smoothness clamp (min, max)", Vector) = (0.04, 0.88, 0, 0)
        _OcclusionStrength("Occlusion", Range(0, 1)) = 1
        [Toggle(_EMISSION)] _UseEmission("Use emission", Float) = 0
        _EmissionMap("Emission", 2D) = "white" {}
        [HDR] _EmissionColor("Emission colour", Color) = (0, 0, 0, 1)
        _Cutoff("Alpha cutoff", Range(0, 1)) = 0.5

        [Header(Stylized light response)]
        _Wrap("Soft terminator (wrap)", Range(0, 0.6)) = 0.25
        _ShadowTintStrength("Cool shadow tint", Range(0, 1.5)) = 1
        _RimColor("Rim colour", Color) = (1, 0.82, 0.62, 1)
        _RimStrength("Rim strength", Range(0, 2)) = 0.2
        _RimPower("Rim power", Range(1, 8)) = 4
        _EdgeHighlight("Edge highlight", Range(0, 1)) = 0.25
        _SpecularScale("Specular scale", Range(0, 2)) = 1

        [Header(Gradient)]
        _GradientColor("Base gradient colour", Color) = (0.55, 0.56, 0.64, 1)
        _GradientRange("Gradient object Y (min, max)", Vector) = (0, 1, 0, 0)
        _GradientStrength("Gradient strength", Range(0, 1)) = 0

        [Header(World space detail and variation)]
        [Toggle(_WORLD_DETAIL)] _UseWorldDetail("World-space detail", Float) = 0
        _DetailMap("Detail (grey, 0.5 neutral)", 2D) = "grey" {}
        _DetailScale("Detail metres per tile", Float) = 2
        _DetailStrength("Detail strength", Range(0, 1)) = 0.5
        _NoiseScale("Noise frequency (1/m)", Float) = 0.15
        _NoiseAlbedo("Noise albedo variation", Range(0, 0.5)) = 0
        _NoiseSmoothness("Noise smoothness variation", Range(0, 0.5)) = 0

        [Header(Weather and wind)]
        [Toggle(_WETNESS)] _UseWetness("Wet surface", Float) = 0
        _WetResponse("Wet response", Range(0, 1)) = 1
        _Puddles("Puddle amount", Range(0, 1)) = 0.35
        [Toggle(_WIND)] _UseWind("Wind deformation", Float) = 0
        _WindWeight("Wind metres", Range(0, 0.5)) = 0.08
        _WindSpeedScale("Wind speed", Range(0, 3)) = 1

        [Header(Court surface)]
        [Toggle(_COURT)] _UseCourt("Court feature", Float) = 0
        _CourtParams("Court centre XZ, half width, half length", Vector) = (0, 0, 3.05, 6.705)
        _KitchenDepth("Kitchen depth (m)", Float) = 2.134
        _KitchenTint("Kitchen tint", Color) = (0.93, 0.95, 1.0, 1)
        _CourtWear("Play wear", Range(0, 1)) = 0.55
        _CourtGrain("Grain normal", Range(0, 1)) = 0.35
        _SurroundTint("Surround colour (a = strength)", Color) = (0.17, 0.37, 0.42, 0)
        _SurroundExtent("Surround half extents XZ", Vector) = (9.25, 13.2, 0, 0)

        [Header(Venue compatibility)]
        _GlassSmoothness("Glass smoothness (albedo alpha)", Range(0, 1)) = 0
        _SunMask("Receives baked sun mask", Float) = 0
        _Glow("Night glow", Float) = 0
        _Water("Water ripples", Float) = 0
        _Unlit("Backdrop (unlit)", Float) = 0
        _Dither("Distance dither (fine weaves)", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Toggle(_ALPHATEST_ON)] _AlphaTest("Alpha test", Float) = 0
        [Toggle] _AlphaToMask("Alpha to coverage", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "PVCommon.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
        TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);
        // Venue globals (not material properties): set by MatchEnvironmentLighting / StadiumEnvironment.
        TEXTURE2D(_PV_SunMask); SAMPLER(sampler_PV_SunMask);
        float4 _PV_SunMaskParams;
        float _PV_SunMaskOn, _PV_NightLightsOn, _PV_NightGlow;
        half4 _PV_LampColor;

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BumpMap_ST, _BaseMap_TexelSize;
            half4 _BaseColor, _EmissionColor, _RimColor, _GradientColor, _KitchenTint, _SurroundTint;
            float4 _SmoothnessRange, _GradientRange, _CourtParams, _SurroundExtent;
            half _VertexTint, _BumpScale, _Metallic, _Smoothness, _OcclusionStrength, _Cutoff;
            half _Wrap, _ShadowTintStrength, _RimStrength, _RimPower, _EdgeHighlight, _SpecularScale, _GradientStrength;
            float _DetailScale, _NoiseScale, _KitchenDepth;
            half _DetailStrength, _NoiseAlbedo, _NoiseSmoothness, _WetResponse, _Puddles, _WindWeight, _WindSpeedScale, _CourtWear, _CourtGrain;
            half _GlassSmoothness, _SunMask, _Glow, _Water, _Unlit, _Dither, _Cull, _AlphaTest, _AlphaToMask;
            half _UseNormalMap, _UseMaskMap, _UseEmission, _UseWorldDetail, _UseWetness, _UseWind, _UseCourt;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float2 uv1 : TEXCOORD1;
            float2 uv2 : TEXCOORD2;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float3 PV_DeformedWS(Attributes a)
        {
            float3 p = TransformObjectToWorld(a.positionOS.xyz);
            #if defined(_WIND)
                // Weight: vertex colour R (painted bend) x height above the pivot (flags, banners, plants).
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                half bend = a.color.r * saturate(a.positionOS.y * 0.6);
                p += PV_WindOffset(p, origin, _WindWeight * bend, _WindSpeedScale, a.color.g);
            #endif
            return p;
        }

        half PV_Coverage(float2 uv, float4 positionCS)
        {
            half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a;
            half sharp = saturate((a - _Cutoff) / max(fwidth(a), 1e-4) + 0.5);
            if (_Dither > 0.5)
            {
                // Fine weaves (chain-link, net): resolve to the mip's average coverage with a stable screen-door
                // pattern once a pixel covers several texels, so far fences stay see-through, not a solid wall.
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
            #pragma shader_feature_local _MASKMAP
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WORLD_DETAIL
            #pragma shader_feature_local _WETNESS
            #pragma shader_feature_local _WIND
            #pragma shader_feature_local _COURT
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "PVStylizedLighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : TEXCOORD3;
                half fog : TEXCOORD4;
                float heightOS : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes a)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(a);
                UNITY_TRANSFER_INSTANCE_ID(a, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = PV_DeformedWS(a);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(a.normalOS);
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                o.color = a.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.heightOS = a.positionOS.y;
                return o;
            }

            // Normal perturbation from a cotangent frame built from screen derivatives (no mesh tangents needed).
            half3 PerturbNormal(half3 geometric, float3 positionWS, float2 uv, half3 tn)
            {
                float3 dp1 = ddx(positionWS), dp2 = ddy(positionWS);
                float2 duv1 = ddx(uv), duv2 = ddy(uv);
                float3 dp2perp = cross(dp2, geometric), dp1perp = cross(geometric, dp1);
                float3 T = dp2perp * duv1.x + dp1perp * duv2.x;
                float3 B = dp2perp * duv1.y + dp1perp * duv2.y;
                float inv = rsqrt(max(max(dot(T, T), dot(B, B)), 1e-20));
                return normalize(T * (inv * tn.x) + B * (inv * tn.y) + geometric * tn.z);
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half coverage = 1;
                #if defined(_ALPHATEST_ON)
                    coverage = PV_Coverage(i.uv, i.positionCS);
                #endif
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb;
                if (_VertexTint > 0.5) albedo *= i.color.rgb * i.color.rgb;
                half3 geometric = normalize(i.normalWS) * IS_FRONT_VFACE(face, 1, -1);
                half3 n = geometric;

                // ---- masks: metallic, occlusion, edge, smoothness
                half metallic = _Metallic, smooth = _Smoothness, occlusion = 1, edge = 0;
                #if defined(_MASKMAP)
                    half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, i.uv);
                    metallic *= mask.r;
                    occlusion = lerp(1, mask.g, _OcclusionStrength);
                    edge = mask.b;
                    smooth *= mask.a;
                #endif
                if (_GlassSmoothness > 0) smooth = lerp(smooth, _GlassSmoothness, tex.a);

                // ---- normal
                #if defined(_NORMALMAP)
                    half3 tn;
                    if (_Water > 0.5)
                    {
                        float t = _Time.y;
                        half3 na = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv * 1.7 + float2(t * 0.021, t * 0.013)), _BumpScale);
                        half3 nb = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv * 1.1 - float2(t * 0.017, -t * 0.024)), _BumpScale);
                        tn = normalize(half3(na.xy + nb.xy, na.z * nb.z));
                    }
                    else
                        tn = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                    n = PerturbNormal(geometric, i.positionWS, i.uv, tn);
                #endif

                // ---- base gradient (grounding): darker, cooler toward the base of the object
                if (_GradientStrength > 0)
                {
                    half g = saturate((i.heightOS - _GradientRange.x) / max(_GradientRange.y - _GradientRange.x, 1e-3));
                    albedo *= lerp(half3(1, 1, 1), _GradientColor.rgb, (1 - g) * (1 - g) * _GradientStrength);
                }

                // ---- world-space detail (triplanar), breaks tiling on large surfaces
                #if defined(_WORLD_DETAIL)
                    half3 w = PV_TriplanarWeights(geometric);
                    float3 dp = i.positionWS / max(_DetailScale, 0.01);
                    half d = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, dp.zy).r * w.x
                           + SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, dp.xz).r * w.y
                           + SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, dp.xy).r * w.z;
                    albedo *= lerp(1, d * 2, _DetailStrength);
                    smooth *= lerp(1, saturate(1.35 - d * 0.7), _DetailStrength * 0.5);
                #endif

                // ---- procedural macro variation (no texture): subtle value and roughness drift across a surface
                if (_NoiseAlbedo + _NoiseSmoothness > 0)
                {
                    half nz = PV_Fbm(i.positionWS.xz * _NoiseScale + i.positionWS.y * 0.37 * _NoiseScale);
                    albedo *= 1 + (nz - 0.5) * 2 * _NoiseAlbedo;
                    smooth += (nz - 0.5) * 2 * _NoiseSmoothness;
                }

                // ---- court feature: zone tint, zone roughness, play wear, scuffs and acrylic grain
                #if defined(_COURT)
                {
                    float2 c = i.positionWS.xz - _CourtParams.xy;
                    float ax = abs(c.x), az = abs(c.y);
                    half inPlay = (1 - smoothstep(_CourtParams.z - 0.02, _CourtParams.z + 0.02, ax)) * (1 - smoothstep(_CourtParams.w - 0.02, _CourtParams.w + 0.02, az));
                    half kitchen = inPlay * (1 - smoothstep(_KitchenDepth - 0.015, _KitchenDepth + 0.015, az));
                    // Wear where players stand and shuffle: serve/return boxes behind the baseline-centre, the dink line,
                    // and a faint lane between them. Broken up by noise so it reads as use, not as a painted gradient.
                    half baseline = exp(-Sq((az - (_CourtParams.w - 0.35)) / 0.8)) * exp(-Sq(ax / 1.5));
                    half dinkLine = exp(-Sq((az - (_KitchenDepth + 0.3)) / 0.35)) * exp(-Sq(ax / 2.2));
                    half lane = exp(-Sq(ax / 1.0)) * smoothstep(_KitchenDepth, _CourtParams.w, az) * 0.25;
                    half grime = PV_Fbm(c * 2.3 + 3.1);
                    half wear = saturate((baseline + dinkLine * 0.7 + lane) * smoothstep(0.25, 0.75, grime) * 1.4) * _CourtWear * inPlay;
                    half scuff = smoothstep(0.7, 0.93, PV_ValueNoise(float2(c.x * 7.0 + c.y * 1.3, c.y * 1.1) + 11.0)) * wear;
                    // Run-off around the court (inside the venue's court slab only): palette surround colour.
                    half surround = (1 - inPlay) * (1 - smoothstep(_SurroundExtent.x - 0.05, _SurroundExtent.x, ax)) * (1 - smoothstep(_SurroundExtent.y - 0.05, _SurroundExtent.y, az));
                    albedo = lerp(albedo, _SurroundTint.rgb * tex.rgb * 1.18, surround * _SurroundTint.a);
                    albedo *= lerp(half3(1, 1, 1), _KitchenTint.rgb, kitchen);
                    half3 worn = albedo * 1.07 + 0.006;
                    albedo = lerp(albedo, lerp(worn, Luminance(worn).xxx, 0.08), saturate(wear * 0.45));
                    albedo *= 1 - scuff * 0.16;
                    smooth += kitchen * 0.03 + wear * 0.14 - scuff * 0.12 + (1 - inPlay) * -0.05;
                    // Acrylic grain: high-frequency sand-texture normal from hashed noise slopes (no texture).
                    float2 gp = i.positionWS.xz * 140.0;
                    half g0 = PV_ValueNoise(gp), gx = PV_ValueNoise(gp + float2(0.35, 0)), gz = PV_ValueNoise(gp + float2(0, 0.35));
                    half fade = saturate(1 - length(fwidth(gp)) * 0.6);
                    n = normalize(n + half3(g0 - gx, 0, g0 - gz) * (_CourtGrain * 0.35 * fade) * (1 - wear * 0.6));
                }
                #endif

                // ---- weather: darken porous albedo, raise smoothness, flatten normals in puddles
                #if defined(_WETNESS)
                {
                    half wet = saturate(_PV_Wetness * _WetResponse);
                    half puddle = smoothstep(1 - _Puddles, 1 - _Puddles + 0.12, PV_Fbm(i.positionWS.xz * 0.45)) * saturate(geometric.y * 4 - 3) * wet;
                    albedo *= lerp(1, 0.62, wet * (1 - metallic));
                    smooth = lerp(smooth, 0.93, saturate(wet * 0.6 + puddle));
                    n = normalize(lerp(n, geometric, puddle));
                }
                #endif

                smooth = clamp(smooth, _SmoothnessRange.x, _SmoothnessRange.y);
                half3 v = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));

                if (_Unlit > 0.5)
                {
                    // Distant city backdrop: already hazy in the texture; dims at night while window dots glow through.
                    half3 backdrop = albedo * (1 - 0.9 * _PV_NightGlow);
                    half3 windows = albedo * saturate(tex.r - tex.b) * (_PV_NightGlow * 2.0);
                    return half4(MixFog(backdrop, i.fog) + windows * (ComputeFogIntensity(i.fog) * 0.55 + 0.45), coverage);
                }

                // ---- venue baked shadows / night pools (golden-hour district)
                half bakedShadow = 1, pools = 0;
                if (_SunMask > 0.5 && _PV_SunMaskOn + _PV_NightLightsOn > 0.001)
                {
                    float2 muv = 0.5 + (i.positionWS.xz - _PV_SunMaskParams.zw) * _PV_SunMaskParams.x;
                    half2 baked = SAMPLE_TEXTURE2D(_PV_SunMask, sampler_PV_SunMask, muv).rg;
                    half wm = saturate(1 - (i.positionWS.y - _PV_SunMaskParams.y - 0.55) * 3.3) * saturate((geometric.y - 0.6) * 5);
                    bakedShadow = lerp(1, baked.r, wm * _PV_SunMaskOn);
                    pools = baked.g * baked.g * wm * _PV_NightLightsOn;
                }

                // ---- edge highlight: authored mask (B) plus screen-space curvature of the geometric normal
                half curvature = saturate(length(fwidth(geometric)) / max(length(fwidth(i.positionWS)), 1e-4) * 0.045);
                edge = saturate(edge + curvature) * _EdgeHighlight;

                PVSurface s;
                s.albedo = albedo;
                s.metallic = metallic;
                s.smoothness = smooth;
                s.occlusion = occlusion;
                s.normalWS = n;
                s.emission = 0;
                s.wrap = _Wrap;
                s.shadowTint = _ShadowTintStrength;
                s.rimColor = _RimColor.rgb;
                s.rimStrength = _RimStrength;
                s.rimPower = _RimPower;
                s.specularScale = _SpecularScale;
                s.edge = edge;
                if (_Water > 0.5) { s.albedo *= 0.35; s.smoothness = 0.95; }

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                half keyLit;
                half3 color = PV_Shade(s, i.positionWS, i.positionCS, v, shadowCoord, bakedShadow, keyLit);
                color += pools * _PV_LampColor.rgb * (albedo * (1 - metallic) + 0.04 * (smooth * 2.5));

                // Emission is added after fog with partial attenuation so lamps and windows read through haze.
                half3 emission = 0;
                #if defined(_EMISSION)
                    emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
                #endif
                if (_Glow > 0 && _PV_NightGlow > 0)
                {
                    half glow = _Glow * _PV_NightGlow;
                    if (_GlassSmoothness > 0)
                    {
                        // Facade: one window per 3 m bay, vertex alpha seeds each building -> irregular lit pattern.
                        float2 grid = i.uv * 8;
                        float2 cell = floor(grid), f = frac(grid);
                        float seed = floor(i.color.a * 255 + 0.5);
                        half h = PV_Hash12(cell + float2(seed * 17, seed * 31));
                        half h2 = PV_Hash12(cell.yx + float2(seed * 7 + 101, seed * 3 + 57));
                        half lit = step(0.75, h) * tex.a;
                        half3 tint = h2 < 0.6 ? half3(1.0, 0.64, 0.32) : (h2 < 0.88 ? half3(1.0, 0.83, 0.60) : half3(0.60, 0.74, 1.0));
                        half level = 0.55 + 0.85 * frac(h2 * 3.71);
                        half interior = lerp(0.7, 1.15, f.y);
                        half curtain = (h2 > 0.42 && h2 < 0.55) ? lerp(0.35, 1.0, smoothstep(0.3, 0.7, f.x)) : 1;
                        emission += tint * (lit * level * interior * curtain * glow * 1.35);
                    }
                    else
                        emission += albedo * (glow * 2.2);
                }
                return half4(MixFog(color, i.fog) + emission * (ComputeFogIntensity(i.fog) * 0.55 + 0.45), coverage);
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
            #pragma shader_feature_local _WIND
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vertShadow(Attributes a)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(a);
                UNITY_TRANSFER_INSTANCE_ID(a, o);
                float3 p = PV_DeformedWS(a);
                float3 nrm = TransformObjectToWorldNormal(a.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 d = normalize(_LightPosition - p);
                #else
                    float3 d = _LightDirection;
                #endif
                float4 clip = TransformWorldToHClip(ApplyShadowBias(p, nrm, d));
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
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
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
            #pragma shader_feature_local _WIND
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vertDepth(Attributes a)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(a);
                UNITY_TRANSFER_INSTANCE_ID(a, o);
                o.positionCS = TransformWorldToHClip(PV_DeformedWS(a));
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                return o;
            }
            half4 fragDepth(V i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
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
            #pragma shader_feature_local _WIND
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half3 normalWS : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vertDN(Attributes a)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(a);
                UNITY_TRANSFER_INSTANCE_ID(a, o);
                o.positionCS = TransformWorldToHClip(PV_DeformedWS(a));
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                o.normalWS = TransformObjectToWorldNormal(a.normalOS);
                return o;
            }
            half4 fragDN(V i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return half4(NormalizeNormalPerPixel(i.normalWS * IS_FRONT_VFACE(face, 1, -1)), 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertMeta
            #pragma fragment fragMeta
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature EDITOR_VISUALIZATION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : TEXCOORD1; };
            V vertMeta(Attributes a)
            {
                V o = (V)0;
                o.positionCS = UnityMetaVertexPosition(a.positionOS.xyz, a.uv1, a.uv2);
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                o.color = a.color;
                return o;
            }
            half4 fragMeta(V i) : SV_Target
            {
                UnityMetaInput meta = (UnityMetaInput)0;
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                if (_VertexTint > 0.5) albedo *= i.color.rgb * i.color.rgb;
                meta.Albedo = albedo;
                #if defined(_EMISSION)
                    meta.Emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
                #endif
                return UnityMetaFragment(meta);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
