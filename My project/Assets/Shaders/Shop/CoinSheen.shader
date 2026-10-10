Shader "PickleVerse/UI/Coin Sheen"
{
 Properties
 {
  [PerRendererData] _MainTex("Sprite",2D)="white"{}
  _Color("Tint",Color)=(1,1,1,1)
  _SweepColor("Sheen",Color)=(1,.95,.7,.4)
  _Period("Period",Float)=1
  _StencilComp("Stencil Comparison",Float)=8
  _Stencil("Stencil ID",Float)=0
  _StencilOp("Stencil Operation",Float)=0
  _StencilWriteMask("Stencil Write Mask",Float)=255
  _StencilReadMask("Stencil Read Mask",Float)=255
  _ColorMask("Color Mask",Float)=15
  [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Alpha Clip",Float)=0
 }
 SubShader
 {
  Tags{"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True"}
  Stencil{Ref[_Stencil] Comp[_StencilComp] Pass[_StencilOp] ReadMask[_StencilReadMask] WriteMask[_StencilWriteMask]}
  Cull Off Lighting Off ZWrite Off ZTest[unity_GUIZTestMode]
  Blend SrcAlpha OneMinusSrcAlpha ColorMask[_ColorMask]
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   struct appdata{float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
   struct v2f{float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 world:TEXCOORD1;};
   sampler2D _MainTex;float4 _Color,_SweepColor,_ClipRect,_TextureSampleAdd;float _Period;
   v2f vert(appdata i){v2f o;o.world=i.vertex;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.color=i.color*_Color;return o;}
   fixed4 frag(v2f i):SV_Target
   {
    fixed4 c=(tex2D(_MainTex,i.uv)+_TextureSampleAdd)*i.color;
    float t=frac(_Time.y/max(.1,_Period));float progress=t/.38;
    float band=pow(saturate(1-abs(i.uv.x+i.uv.y*.28-lerp(-.25,1.55,progress))/.13),2)*step(t,.38);
    c.rgb+=_SweepColor.rgb*_SweepColor.a*band;
    #ifdef UNITY_UI_CLIP_RECT
     c.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
    #endif
    #ifdef UNITY_UI_ALPHACLIP
     clip(c.a-.001);
    #endif
    return c;
   }
   ENDCG
  }
 }
}
