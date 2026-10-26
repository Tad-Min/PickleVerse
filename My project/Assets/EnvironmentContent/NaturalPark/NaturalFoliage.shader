Shader "PickleVerse/Environment/Natural Foliage"
{
 Properties
 {
  _BaseMap("Botanical surface",2D)="white"{}
  _BaseColor("Tint",Color)=(1,1,1,1)
  _WindStrength("Wind metres",Range(0,.2))=.07
  _WindSpeed("Wind speed",Range(0,3))=.8
  _Translucency("Thin leaf light",Range(0,.5))=.18
  _Grass("Grass distance thinning",Float)=0
  _Cull("Cull",Float)=0
 }
 SubShader
 {
  Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
  Cull [_Cull]
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
  CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST;half4 _BaseColor;
   float _WindStrength,_WindSpeed,_Translucency,_Grass,_Cull;
  CBUFFER_END
  struct Attributes
  {
   float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR;
   UNITY_VERTEX_INPUT_INSTANCE_ID
  };
  // R=bending weight (zero at the root), G=phase, B=occlusion, A=tone.
  // Leaves/blades have real opaque outlines: no transparent-card overdraw.
  float3 WindPosition(Attributes a)
  {
   float3 p=TransformObjectToWorld(a.positionOS.xyz);
   float phase=dot(p.xz,float2(.67,.83))+a.color.g*6.283;
   float t=_Time.y*_WindSpeed;
   float wave=sin(t*1.7+phase)*.65+sin(t*3.1+phase*1.9)*.35;
   float distanceFade=1-smoothstep(24,38,distance(_WorldSpaceCameraPos.xz,p.xz));
   p.y-=a.positionOS.y*_Grass*(1-distanceFade);
   p.xz+=float2(1,.42)*(_WindStrength*a.color.r*wave);
   return p;
  }
  ENDHLSL
  Pass
  {
   Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma multi_compile_fog
   #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   struct Varyings
   {
    float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;
    float2 uv:TEXCOORD2;half3 botanical:TEXCOORD3;half fog:TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO
   };
   Varyings vert(Attributes a)
   {
    Varyings o=(Varyings)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionWS=WindPosition(a);o.positionCS=TransformWorldToHClip(o.positionWS);
    o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.uv=TRANSFORM_TEX(a.uv,_BaseMap);
    o.botanical=half3(a.color.b,a.color.a,a.color.r);o.fog=ComputeFogFactor(o.positionCS.z);return o;
   }
   half Diffuse(half3 n,half3 l){return saturate((dot(n,l)+.2)/1.2) + _Translucency*saturate(-dot(n,l));}
   half4 frag(Varyings i, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
   {
    UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half3 n=normalize(i.normalWS)*IS_FRONT_VFACE(face,1,-1);
    half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
    albedo*=lerp(.82,1.12,i.botanical.y);
    Light main=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    half3 lighting=lerp(SampleSH(n),SampleSH(half3(0,1,0)),.35)*lerp(.72,1,i.botanical.x);
    lighting+=main.color*Diffuse(n,main.direction)*main.distanceAttenuation*main.shadowAttenuation;
    #if defined(_ADDITIONAL_LIGHTS)
     InputData inputData=(InputData)0;inputData.positionWS=i.positionWS;
     inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
     uint pixelLightCount=GetAdditionalLightsCount();
     LIGHT_LOOP_BEGIN(pixelLightCount)
      Light l=GetAdditionalLight(lightIndex,i.positionWS);
      lighting+=l.color*Diffuse(n,l.direction)*l.distanceAttenuation;
     LIGHT_LOOP_END
    #endif
    return half4(MixFog(albedo*lighting,i.fog),1);
   }
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"}
   ZWrite On ZTest LEqual ColorMask 0
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex vertShadow
   #pragma fragment fragDepth
   #pragma multi_compile_instancing
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
   float3 _LightDirection;float3 _LightPosition;
   float4 vertShadow(Attributes a):SV_POSITION
   {
    UNITY_SETUP_INSTANCE_ID(a);float3 p=WindPosition(a);float3 n=TransformObjectToWorldNormal(a.normalOS);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
     float3 direction=normalize(_LightPosition-p);
    #else
     float3 direction=_LightDirection;
    #endif
    float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,direction));
    #if UNITY_REVERSED_Z
     clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE);
    #else
     clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE);
    #endif
    return clip;
   }
   half4 fragDepth():SV_Target{return 0;}
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly" Tags {"LightMode"="DepthOnly"}
   ZWrite On ColorMask R
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex vertDepth
   #pragma fragment fragDepth
   #pragma multi_compile_instancing
   float4 vertDepth(Attributes a):SV_POSITION{UNITY_SETUP_INSTANCE_ID(a);return TransformWorldToHClip(WindPosition(a));}
   half4 fragDepth():SV_Target{return 0;}
   ENDHLSL
  }
 }
 FallBack Off
}
