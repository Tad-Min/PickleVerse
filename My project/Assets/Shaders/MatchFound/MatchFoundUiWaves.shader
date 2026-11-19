// Energy waves around an avatar that has accepted the match (uGUI). Graphic colour alpha is the intensity;
// _Start is the time (in _MFTime seconds) the waves were released so every avatar pulses from its own moment.
Shader "PickleVerse/UI/Energy Waves"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _NoiseTex ("Noise", 2D) = "gray" {}
        _Start ("Start time", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            sampler2D _NoiseTex;
            float _Start, _MFTime;
            float4 _ClipRect;
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world = v.vertex; o.vertex = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = v.uv;
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float2 p = (i.uv - .5) * 2;
                float r = length(p);
                float2 dir = p / max(r, 1e-4);
                float t = _MFTime;
                float wob = (tex2D(_NoiseTex, dir * .4 + float2(t * .05, _Start * .13)).r - .5) * .14;
                float phase = (r + wob) * 3 - (t - _Start) * 1.25;
                float tri = 1 - abs(frac(phase) - .5) * 2;
                float waves = pow(tri, 5);
                float streams = smoothstep(.5, .8, tex2D(_NoiseTex, p * 1.2 - dir * t * .28 + _Start).g);
                float density = saturate((waves * .8 + streams * .6) * smoothstep(.36, .5, r) * (1 - smoothstep(.55, 1, r)));
                float3 c = lerp(float3(.01, .10, .45), float3(.18, .52, 1), saturate((density - .3) / .38));
                c = lerp(c, float3(.82, .94, 1), saturate((density - .68) / .32));
                float4 col = float4(c, density * i.color.a);
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.world.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - .001);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
