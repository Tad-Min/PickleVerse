// Night glare around lamp heads and floodlights: camera-facing additive sprites expanded in the vertex
// shader. All lamps of a venue live in one static mesh (one draw call); every vertex of a sprite holds the
// lamp centre, UV0 = sprite corner, UV1 = (radius in metres, strength). Sprites collapse to nothing while
// _PV_NightGlow is 0, so they cost nothing by day. A minimum angular size keeps distant lamps readable,
// as in AAA night scenes, without any realtime light or bloom pass.
Shader "PickleVerse/Environment/Lamp Halo"
{
    Properties
    {
        _Color("Glow colour", Color) = (1.0, 0.70, 0.40, 1)
        _Intensity("Intensity", Range(0, 4)) = 1.1
        _MinAngular("Minimum size (radians)", Range(0, 0.05)) = 0.012
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Halo"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _PV_NightGlow;
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity, _MinAngular;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 corner : TEXCOORD0; float2 lamp : TEXCOORD1; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half strength : TEXCOORD1; half fog : TEXCOORD2; };
            V vert(A a)
            {
                V o;
                float3 centre = TransformObjectToWorld(a.positionOS.xyz);
                float3 toCamera = _WorldSpaceCameraPos - centre;
                float distance = max(length(toCamera), 0.01);
                // Pull the sprite towards the camera so it is not swallowed by the lamp head geometry.
                centre += toCamera / distance * min(0.5, distance * 0.08);
                float radius = max(a.lamp.x, distance * _MinAngular) * _PV_NightGlow;
                float2 corner = a.corner * 2 - 1;
                float3 p = centre + (UNITY_MATRIX_V[0].xyz * corner.x + UNITY_MATRIX_V[1].xyz * corner.y) * radius;
                o.positionCS = TransformWorldToHClip(p);
                o.uv = corner;
                o.strength = a.lamp.y * _PV_NightGlow;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half r2 = dot(i.uv, i.uv);
                half glow = exp(-r2 * 5.0) * 0.55 + exp(-r2 * 42.0) * 1.1;      // soft bloom + hot core
                half3 c = _Color.rgb * (glow * i.strength * _Intensity * saturate(1 - r2));
                return half4(c * (ComputeFogIntensity(i.fog) * 0.6 + 0.4), 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
