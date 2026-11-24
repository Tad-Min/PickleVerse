// PickleVerse Art Direction — shared helpers (noise, world-space projection, wind) for every PickleVerse surface shader.
// Globals are published by PickleVerse.ArtDirection.ArtDirectionRuntime from the active Look; when the runtime is absent
// (_PV_LookOn == 0) every helper falls back to a neutral value, so shaders render correctly in a bare scene or test.
#ifndef PV_COMMON_INCLUDED
#define PV_COMMON_INCLUDED

half4 _PV_ShadowTint;      // rgb: cool tint applied to light the key does not reach, a: strength (0..1)
half4 _PV_RimTint;         // rgb: warm rim multiplier, a: global rim scale
half4 _PV_WindParams;      // xy: wind direction (xz), z: strength scale, w: speed scale
half4 _PV_AmbientBoost;    // rgb: multiplier on probe/trilight ambient, a: unused
half _PV_Wetness;          // 0 dry .. 1 soaked (weather)
half _PV_LookOn;           // 1 once the runtime has published a Look

// Sin-free hash (Dave Hoskins): stable for integer inputs on every GPU precision.
float PV_Hash12(float2 p)
{
    float3 p3 = frac(p.xyx * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// Smooth value noise in [0,1].
float PV_ValueNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = PV_Hash12(i), b = PV_Hash12(i + float2(1, 0)), c = PV_Hash12(i + float2(0, 1)), d = PV_Hash12(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Three octaves, normalised to [0,1]; cheap enough for per-pixel macro variation.
float PV_Fbm(float2 p)
{
    float v = PV_ValueNoise(p) * 0.5333;
    v += PV_ValueNoise(p * 2.03 + 17.1) * 0.2667;
    v += PV_ValueNoise(p * 4.11 + 41.7) * 0.1333;
    return v / 0.9333;
}

// World-space projection weights (triplanar), sharpened so mostly one projection is visible.
half3 PV_TriplanarWeights(half3 nWS)
{
    half3 w = pow(abs(nWS), 4.0);
    return w / max(w.x + w.y + w.z, 1e-4);
}

half3 PV_RimScale()
{
    return _PV_LookOn > 0.5 ? _PV_RimTint.rgb * _PV_RimTint.a : half3(1, 1, 1);
}

half3 PV_ShadowTintColor(half shade)
{
    return lerp(half3(1, 1, 1), _PV_ShadowTint.rgb, saturate(_PV_ShadowTint.a * shade));
}

half3 PV_AmbientScale()
{
    return _PV_LookOn > 0.5 ? _PV_AmbientBoost.rgb : half3(1, 1, 1);
}

// Gentle layered sway for foliage, flags and banners. weight is metres of displacement at full gust.
float3 PV_WindOffset(float3 positionWS, float3 originWS, half weight, half speedScale, half phaseSeed)
{
    half2 dir = _PV_WindParams.xy;
    half strength = _PV_WindParams.z, speed = _PV_WindParams.w;
    if (_PV_LookOn < 0.5 || dot(dir, dir) < 1e-4) { dir = half2(0.91, 0.41); strength = 1; speed = 1; }
    float t = _Time.y * speed * speedScale;
    float phase = dot(originWS.xz, float2(0.37, 0.61)) + phaseSeed * 6.2832;
    float gust = sin(t * 0.43 + originWS.x * 0.05) * 0.5 + 0.5;
    float sway = sin(t * 1.3 + phase) * 0.6 + sin(t * 2.9 + phase * 1.7) * 0.25;
    float flutter = sin(t * 7.0 + phase * 3.1 + positionWS.y * 2.0) * 0.15;
    float w = weight * strength * (0.55 + 0.45 * gust);
    return float3(dir.x * w * (sway + flutter), w * flutter * 0.4, dir.y * w * (sway + flutter));
}

#endif
