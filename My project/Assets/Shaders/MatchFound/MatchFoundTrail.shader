// Comet tail behind the ball (TrailRenderer: uv.x runs from the ball to the tail, vertex colour carries the fade).
Shader "PickleVerse/MatchFound/Trail"
{
    Properties { _Color ("Tint", Color) = (1,.9,.55,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off
        Blend SrcAlpha One, Zero One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = v.uv; return o; }
            float4 frag(v2f i) : SV_Target
            {
                float across = 1 - abs(i.uv.y * 2 - 1);
                float a = pow(saturate(1 - i.uv.x), 1.8) * smoothstep(0, .6, across) * i.color.a;
                return float4(_Color.rgb * i.color.rgb, a * .55);
            }
            ENDCG
        }
    }
}
