// Match-found stage: self-lit surfaces (key, fill, rim and a front hero key) so the intro looks the same
// whatever scene the ready check opens over. Light directions/colours are globals set by MatchFoundStage.
Shader "PickleVerse/MatchFound/Stage Lit"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0,2)) = 1
        _Gloss ("Gloss", Range(2,256)) = 48
        _Spec ("Specular", Range(0,2)) = .5
        _Rim ("Rim strength", Range(0,3)) = 1
        _RimPower ("Rim power", Range(.5,8)) = 3
        _Emission ("Emission", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _BumpMap;
            float4 _MainTex_ST, _Color, _Emission;
            float _BumpScale, _Gloss, _Spec, _Rim, _RimPower;
            float4 _MFKeyDir, _MFKeyColor, _MFFillDir, _MFFillColor, _MFRimColor, _MFAmbient, _MFHeroDir, _MFHeroColor;
            float _MFFlash;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float4 tangent:TANGENT; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; float3 n:TEXCOORD2; float3 t:TEXCOORD3; float3 b:TEXCOORD4; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.n = UnityObjectToWorldNormal(v.normal);
                o.t = UnityObjectToWorldDir(v.tangent.xyz);
                o.b = cross(o.n, o.t) * v.tangent.w * unity_WorldTransformParams.w;
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float3 tn = UnpackNormal(tex2D(_BumpMap, i.uv));
                tn.xy *= _BumpScale;
                float3 n = normalize(tn.x * i.t + tn.y * i.b + tn.z * i.n);
                float3 v = normalize(_WorldSpaceCameraPos - i.world);
                float4 albedo = tex2D(_MainTex, i.uv) * _Color;
                float3 key = _MFKeyDir.xyz, fill = _MFFillDir.xyz, hero = _MFHeroDir.xyz;
                float3 light = _MFAmbient.rgb
                    + _MFKeyColor.rgb * saturate(dot(n, key))
                    + _MFFillColor.rgb * (dot(n, fill) * .5 + .5)
                    + _MFHeroColor.rgb * saturate(dot(n, hero));
                float3 spec = (_MFKeyColor.rgb * pow(saturate(dot(n, normalize(key + v))), _Gloss)
                    + _MFHeroColor.rgb * pow(saturate(dot(n, normalize(hero + v))), _Gloss)) * _Spec;
                float rim = pow(1 - saturate(dot(n, v)), _RimPower) * _Rim;
                float3 c = albedo.rgb * light + spec + _MFRimColor.rgb * rim + _Emission.rgb;
                return float4(c * (1 + _MFFlash), 1);
            }
            ENDCG
        }
    }
}
