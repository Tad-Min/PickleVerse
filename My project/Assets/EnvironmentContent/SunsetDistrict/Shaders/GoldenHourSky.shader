// Procedural golden-hour skybox: layered gradient, sun glow that follows the main light, sun disc and
// drifting clouds lit warm on the sun side and lilac on the shade side. One small tiling cloud texture
// replaces the 8K equirect daytime sky for match scenes.
Shader "PickleVerse/Environment/Golden Hour Sky"
{
    Properties
    {
        _Zenith("Zenith", Color) = (0.23, 0.36, 0.64, 1)
        _Upper("Upper sky", Color) = (0.55, 0.62, 0.80, 1)
        _Horizon("Horizon", Color) = (1.0, 0.72, 0.52, 1)
        _SunGlow("Sun glow", Color) = (1.0, 0.55, 0.26, 1)
        _Ground("Below horizon", Color) = (0.62, 0.50, 0.44, 1)
        _SunSize("Sun angular radius", Range(0.005, 0.1)) = 0.028
        _SunIntensity("Sun disc intensity", Range(0, 8)) = 3
        [NoScaleOffset] _CloudTex("Clouds (R wisps, G banks, B detail)", 2D) = "black" {}
        _CloudScale("Cloud scale", Range(0.05, 2)) = 0.32
        _CloudSpeed("Cloud drift", Range(0, 0.05)) = 0.004
        _CloudCover("Cloud cover", Range(0, 1)) = 0.55
        _CloudLit("Cloud sun side", Color) = (1.0, 0.76, 0.58, 1)
        _CloudShade("Cloud shade side", Color) = (0.62, 0.52, 0.64, 1)
        _Exposure("Exposure", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_CloudTex); SAMPLER(sampler_CloudTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith, _Upper, _Horizon, _SunGlow, _Ground, _CloudLit, _CloudShade;
                half _SunSize, _SunIntensity, _CloudScale, _CloudSpeed, _CloudCover, _Exposure;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A a)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(a);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(a.positionOS.xyz);
                o.dir = a.positionOS.xyz;
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 s = normalize(_MainLightPosition.xyz);
                float up = saturate(d.y);
                half3 col = lerp(_Horizon.rgb, _Upper.rgb, smoothstep(0.0, 0.22, up));
                col = lerp(col, _Zenith.rgb, smoothstep(0.18, 0.85, up));
                float mu = dot(d, s);
                float sunSide = saturate(mu * 0.5 + 0.5);
                col = lerp(col, _SunGlow.rgb, pow(sunSide, 5) * (1 - smoothstep(0.0, 0.5, up)) * 0.85);
                col += _SunGlow.rgb * (pow(saturate(mu), 48) * 0.55 + pow(saturate(mu), 600) * 1.2);
                float disc = smoothstep(cos(_SunSize * 1.25), cos(_SunSize), mu);
                if (d.y > 0)
                {
                    float2 cuv = d.xz / (d.y + 0.11) * _CloudScale + _Time.y * _CloudSpeed;
                    half3 c = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, cuv).rgb;
                    half3 c2 = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, cuv * 0.37 + 0.19).rgb;
                    half dens = saturate(c.r * 0.75 + c2.g * 1.05 - (1 - _CloudCover) * 0.85);
                    dens *= smoothstep(0.0, 0.10, d.y) * (1 - 0.35 * smoothstep(0.5, 1.0, d.y));
                    half3 cloud = lerp(_CloudShade.rgb, _CloudLit.rgb, saturate(pow(sunSide, 2.2) * 1.3 + c.b * 0.25));
                    cloud += _SunGlow.rgb * pow(saturate(mu), 12) * (1 - dens) * 1.4;     // silver lining
                    col = lerp(col, cloud, dens * 0.9);
                    disc *= 1 - saturate(dens * 1.6);
                }
                col += disc * _SunIntensity * half3(1.0, 0.86, 0.62);
                col = lerp(col, _Ground.rgb, smoothstep(0.0, -0.06, d.y));
                return half4(col * _Exposure, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
