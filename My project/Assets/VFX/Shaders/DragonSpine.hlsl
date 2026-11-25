// PickleVerse VFX - the sine flight shared by the Dragon Ruby body and ribbons (data from DragonBallMotion).
#ifndef PICKLEVERSE_DRAGON_SPINE
#define PICKLEVERSE_DRAGON_SPINE

#define DRAGON_SPINE_POINTS 32
float4 _Spine[DRAGON_SPINE_POINTS];   // xyz ball path at i * step behind the ball, w lowest height allowed there
float4 _SpineInfo;                    // x step, y distance flown since the contact, z gap behind the ball, w covered distance
float4 _Wave;                         // x amplitude, y 2 pi / wavelength, z ramp
float4 _WaveAxis, _SideAxis;

float DragonEase(float x) { x = saturate(x); return x * x * (3 - 2 * x); }

// Same formula as DragonBallMotion.WaveOffset; sign -1 draws the mirrored (counter) wave.
float DragonWave(float behind, float sign)
{
    float travelled = _SpineInfo.y - behind;
    float open = DragonEase(behind / _Wave.z) * DragonEase(travelled / _Wave.z);
    return sign * _Wave.x * open * sin(_Wave.y * travelled);
}

float3 DragonPoint(float behind, float sign)
{
    float b = min(max(behind, 0), _SpineInfo.y);
    float f = clamp(b / _SpineInfo.x, 0, DRAGON_SPINE_POINTS - 1.001);
    int i = (int)f;
    float t = f - i;
    float4 p = lerp(_Spine[i], _Spine[i + 1], t);
    float3 w = p.xyz + _WaveAxis.xyz * DragonWave(b, sign);
    w.y = max(w.y, p.w);
    return w;
}

// Frame on the wave: forward along the flight, up in the wave plane (the dragon's back), side towards the camera.
void DragonFrame(float behind, float sign, out float3 origin, out float3 forward, out float3 up, out float3 side)
{
    const float h = 0.045;
    origin = DragonPoint(behind, sign);
    float3 d = DragonPoint(max(behind - h, 0), sign) - DragonPoint(behind + h, sign);
    float len = length(d);
    forward = len > 1e-4 ? d / len : normalize(cross(_SideAxis.xyz, _WaveAxis.xyz));
    side = _SideAxis.xyz - forward * dot(_SideAxis.xyz, forward);
    float sl = length(side);
    side = sl > 1e-3 ? side / sl : _SideAxis.xyz;
    up = cross(forward, side);
    if (dot(up, _WaveAxis.xyz) < 0) { up = -up; side = -side; }
}
#endif
