// PickleVerse Art Direction — "Stylized PBR" lighting model shared by the uber shader (and usable by foliage/FX).
//  - GGX specular from URP's BRDF (physically based highlights, controlled roughness range).
//  - Wrapped diffuse: the terminator rolls off softly instead of cutting to black (soft, cinematic light).
//  - Warm key / cool shadow: everything the key light does not reach is tinted by the Look's cool shadow colour,
//    so shadows stay readable (detail kept) but read cool next to warm sunlit planes.
//  - Fresnel rim tinted warm, masked by how much the surface faces the key: a soft warm edge, never a halo.
// Requires Lighting.hlsl and PVCommon.hlsl.
#ifndef PV_STYLIZED_LIGHTING_INCLUDED
#define PV_STYLIZED_LIGHTING_INCLUDED

struct PVSurface
{
    half3 albedo;
    half metallic;
    half smoothness;
    half occlusion;
    half3 normalWS;
    half3 emission;
    half wrap;            // 0 = Lambert, 0.5 = very soft terminator
    half shadowTint;      // material multiplier on the Look's cool shadow tint
    half3 rimColor;
    half rimStrength;
    half rimPower;
    half specularScale;
    half edge;            // 0..1 convex-edge highlight amount
};

half PV_WrapDiffuse(half ndl, half wrap)
{
    // Normalised wrap lighting (energy roughly preserved for wrap <= 0.5).
    return saturate((ndl + wrap) / ((1.0 + wrap) * (1.0 + wrap)));
}

// One light's contribution. Returns diffuse+specular radiance; 'lit' receives how lit the point is (0..1).
half3 PV_DirectLight(BRDFData brdf, PVSurface s, Light light, half3 v, out half lit)
{
    half ndl = dot(s.normalWS, light.direction);
    half atten = light.distanceAttenuation * light.shadowAttenuation;
    half diffuse = PV_WrapDiffuse(ndl, s.wrap);
    lit = diffuse * light.shadowAttenuation;
    half3 radiance = light.color * light.distanceAttenuation;
    half3 spec = brdf.specular * DirectBRDFSpecular(brdf, s.normalWS, light.direction, v) * (saturate(ndl) * s.specularScale);
    return radiance * (brdf.diffuse * diffuse * light.shadowAttenuation + spec * light.shadowAttenuation);
}

half3 PV_Shade(PVSurface s, float3 positionWS, float4 positionCS, half3 v, float4 shadowCoord, half bakedShadow, out half keyLit)
{
    half alpha = 1;
    BRDFData brdf;
    InitializeBRDFData(s.albedo, s.metallic, half3(0, 0, 0), s.smoothness, alpha, brdf);

    float2 screenUV = GetNormalizedScreenSpaceUV(positionCS);
    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(screenUV);
    half occlusion = min(s.occlusion, ao.indirectAmbientOcclusion);

    Light mainLight = GetMainLight(shadowCoord);
    mainLight.shadowAttenuation = min(mainLight.shadowAttenuation, bakedShadow);
    #if defined(_SCREEN_SPACE_OCCLUSION)
        mainLight.color *= ao.directAmbientOcclusion;
    #endif
    half lit;
    half3 color = PV_DirectLight(brdf, s, mainLight, v, lit);
    keyLit = lit;

    #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)
        InputData inputData = (InputData)0;
        inputData.positionWS = positionWS;
        inputData.normalizedScreenSpaceUV = screenUV;
        uint pixelLightCount = GetAdditionalLightsCount();
        half pointLit;
        LIGHT_LOOP_BEGIN(pixelLightCount)
            Light l = GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1));
            color += PV_DirectLight(brdf, s, l, v, pointLit);
        LIGHT_LOOP_END
    #endif

    // Ambient: probes / trilight, tinted cool where the key does not reach (warm highlights + cool shadows).
    half3 shadowTint = lerp(half3(1, 1, 1), PV_ShadowTintColor(1 - lit), s.shadowTint);
    half3 indirectDiffuse = SampleSH(s.normalWS) * PV_AmbientScale() * shadowTint;
    half3 r = reflect(-v, s.normalWS);
    half3 indirectSpecular = GlossyEnvironmentReflection(r, brdf.perceptualRoughness, 1);
    half fresnelTerm = Pow4(1.0 - saturate(dot(s.normalWS, v)));
    color += EnvironmentBRDF(brdf, indirectDiffuse, indirectSpecular, fresnelTerm) * occlusion;

    // Warm fresnel rim, strongest on edges turned towards the key light; a quarter remains in shade (backlight).
    half nv = saturate(dot(s.normalWS, v));
    half rim = pow(1.0 - nv, s.rimPower) * s.rimStrength;
    half rimMask = lerp(0.25, 1.0, saturate(dot(s.normalWS, mainLight.direction) * 0.5 + 0.55)) * lerp(0.35, 1.0, mainLight.shadowAttenuation);
    color += s.rimColor * PV_RimScale() * (rim * rimMask) * (Luminance(mainLight.color) * 0.35 + Luminance(indirectDiffuse) + 0.02) * occlusion;

    // Convex edge highlight (bevels catch light): lifts the edge toward the key colour.
    color += s.edge * (mainLight.color * (0.12 * lit) + indirectDiffuse * 0.25) * (0.35 + 0.65 * s.albedo);

    return color + s.emission;
}

#endif
