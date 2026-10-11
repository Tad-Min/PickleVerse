Shader "PickleVerse/Paddle/Gemstone"
{
 Properties
 {
  _BaseMap("Color",2D)="white"{}
  _MetallicRoughnessMap("Metallic Roughness",2D)="white"{}
  _Glitter("Gem Glint",Range(0,.5))=.24
 }
 SubShader
 {
  Tags{"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
  Pass
  {
   Name "ForwardLit" Tags{"LightMode"="UniversalForward"}
   Cull Off
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex vert
   #pragma fragment frag
   // This vertex stage has no light-dependent output. Keep light variants in
   // the fragment stage to avoid redundant legacy-target FXC compilations.
   #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_MetallicRoughnessMap);SAMPLER(sampler_MetallicRoughnessMap);
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;float _Glitter;
   CBUFFER_END
   struct Attributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
   struct Varyings{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;half fog:TEXCOORD3;};
   Varyings vert(Attributes a){Varyings o;VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
   half4 frag(Varyings i):SV_Target
   {
    half3 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;half3 mr=SAMPLE_TEXTURE2D(_MetallicRoughnessMap,sampler_MetallicRoughnessMap,i.uv).rgb;
    half emerald=smoothstep(.035,.10,color.g-max(color.r,color.b))*step(.08,color.g);
    half diamond=smoothstep(.55,.80,min(color.r,min(color.g,color.b)))*(1-smoothstep(.04,.10,max(color.r,max(color.g,color.b))-min(color.r,min(color.g,color.b))));
    half gems=saturate(emerald+diamond);half3 n=NormalizeNormalPerPixel(i.normalWS);half3 v=GetWorldSpaceNormalizeViewDir(i.positionWS);
    Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));half spec=pow(saturate(dot(n,normalize(v+light.direction))),72);
    float grain=frac(sin(dot(floor(i.uv*220),float2(12.9898,78.233)))*43758.5453);
    half pulse=pow(saturate(sin(_Time.y*1.8+grain*40)*.5+.5),12)*step(.91,grain);
    SurfaceData surface=(SurfaceData)0;surface.albedo=color;surface.alpha=1;surface.metallic=lerp(mr.b,.12,gems);surface.smoothness=lerp(1-mr.g,.94,gems);surface.normalTS=half3(0,0,1);surface.occlusion=1;surface.emission=gems*_Glitter*(spec*.65+pulse*.16)*half3(.9,1,.94);
    InputData input=(InputData)0;input.positionWS=i.positionWS;input.normalWS=n;input.viewDirectionWS=v;input.shadowCoord=TransformWorldToShadowCoord(i.positionWS);input.bakedGI=SampleSH(n);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);input.shadowMask=half4(1,1,1,1);
    half4 result=UniversalFragmentPBR(input,surface);result.rgb=MixFog(result.rgb,i.fog);return result;
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
