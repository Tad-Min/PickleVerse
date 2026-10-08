Shader "PickleVerse/Home/Panoramic Blend"
{
    Properties
    {
        _DayTex ("Day Panorama", 2D) = "white" {}
        _NightTex ("Night Panorama", 2D) = "black" {}
        _NightBlend ("Night Blend", Range(0,1)) = 0
        _DayExposure ("Day Exposure", Range(0,2)) = 1
        _NightExposure ("Night Exposure", Range(0,2)) = .7
        _DayRotation ("Day Rotation", Float) = 0
        _NightRotation ("Night Rotation", Float) = 35
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _DayTex, _NightTex;
            float _NightBlend, _DayExposure, _NightExposure, _DayRotation, _NightRotation;
            struct Input { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varying { float4 vertex : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varying vert(Input v)
            {
                Varying o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.direction = v.vertex.xyz;
                return o;
            }
            half4 frag(Varying i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 dir = normalize(i.direction);
                float2 uv = float2(.5 - atan2(dir.z, dir.x) / (2 * UNITY_PI), 1 - acos(clamp(dir.y,-1,1)) / UNITY_PI);
                half3 day = tex2D(_DayTex,float2(frac(uv.x + _DayRotation / 360),uv.y)).rgb * _DayExposure;
                half3 night = tex2D(_NightTex,float2(frac(uv.x + _NightRotation / 360),uv.y)).rgb * _NightExposure;
                return half4(lerp(day,night,saturate(_NightBlend)),1);
            }
            ENDCG
        }
    }
    Fallback Off
}
