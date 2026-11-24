// PickleVerse VFX — glitter shell over the ball while a skin effect is active: faceted cells that twinkle (like cut gems
// catching light) plus a soft fresnel rim. Additive; _Fade (0..1) is driven per renderer through a MaterialPropertyBlock.
Shader "PickleVerse/VFX/Ball Glitter"
{
    Properties
    {
        [HDR] _ColorA("Facet colour A", Color) = (1, 0.92, 0.75, 1)
        [HDR] _ColorB("Facet colour B", Color) = (0.35, 1, 0.75, 1)
        _Cells("Facet density", Float) = 9
        _Speed("Twinkle speed", Float) = 6
        _Rim("Rim", Range(0, 2)) = 0.6
        _Intensity("Intensity", Range(0, 8)) = 2.4
        _Fade("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Glitter"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA, _ColorB;
                half _Cells, _Speed, _Rim, _Intensity, _Fade;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; float3 positionWS : TEXCOORD1; half3 normalWS : TEXCOORD2; };
            float Hash13(float3 p) { p = frac(p * 0.1031); p += dot(p, p.zyx + 31.32); return frac((p.x + p.y) * p.z); }
            V vert(A a)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(a);
                o.positionWS = TransformObjectToWorld(a.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.positionOS = a.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(a.normalOS);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float3 p = normalize(i.positionOS) * _Cells;      // cells stick to the spinning ball
                float3 cell = floor(p);
                half h = Hash13(cell);
                half t = frac(_Time.y * _Speed * (0.4 + h) + h * 13.7);
                half spark = pow(saturate(1 - abs(t - 0.5) * 2), 8) * step(0.55, h);
                half3 v = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = pow(1 - saturate(dot(normalize(i.normalWS), v)), 3) * _Rim;
                half3 c = lerp(_ColorA.rgb, _ColorB.rgb, step(0.8, h)) * spark + _ColorA.rgb * rim * 0.35;
                return half4(c * _Intensity * _Fade, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
