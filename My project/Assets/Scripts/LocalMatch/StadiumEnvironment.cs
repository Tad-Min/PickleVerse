using UnityEngine;
using PickleVerse.Rules;
using System.Linq;

namespace PickleVerse.LocalMatch
{
    /// <summary>The imported stadium is calibrated to the shared metre-based rules plane.</summary>
    public sealed class StadiumEnvironment:MonoBehaviour
    {
        public const string SourceAsset="Assets/Models/Stadium_Enhanced/Stadium_Enhanced.glb";
        public Light[] poleLights,roofLights;
        // All scanned lamp anchors remain available. One source per Y cluster and two per roof bank
        // illuminate the same fixture group; the other lamp faces use the shared emissive material.
        public Light[] poleIllumination,roofIllumination;
        public float roofNightIntensity=110;
        public Renderer[] poleEmitters,roofEmitters,decorEmitters;
        public Material lightsOn,lightsOff;
        public MeshFilter competitionSurface,kitchenSurface,courtLines,netStructure;
        public Transform nearEntrance,farEntrance,nearResult,farResult;
        [Range(0,1)]public float nightWeight;
        float applied=-1;bool previousRoof;
        /// <summary>Authoring utility: retain the scanned anchors, group closely spaced lamp heads.</summary>
        public void ConfigureFixtureGroups()
        {
            poleIllumination=poleLights.GroupBy(l=>(Mathf.Abs(l.transform.localPosition.z)<11?"near":"far")+"_"+Mathf.Sign(l.transform.localPosition.x)+"_"+Mathf.Sign(l.transform.localPosition.z))
                .Select(g=>g.OrderByDescending(l=>l.shadows!=LightShadows.None).ThenBy(l=>l.transform.localPosition.sqrMagnitude).First()).ToArray();
            roofIllumination=roofLights.GroupBy(l=>
            {
                var p=l.transform.localPosition;
                if(Mathf.Abs(p.z)<11)return "side"+Mathf.Sign(p.x)+"_"+Mathf.Sign(p.z);
                if(Mathf.Abs(p.x)<5.5f)return "end"+Mathf.Sign(p.z);
                return "corner"+Mathf.Sign(p.x)+"_"+Mathf.Sign(p.z);
            }).SelectMany(g=>
            {
                var row=g.OrderBy(l=>l.transform.localPosition.x).ThenBy(l=>l.transform.localPosition.z).ToArray();
                return g.Key.StartsWith("corner")?new[]{row[row.Length/2]}:new[]{row[row.Length/3],row[row.Length*2/3]};
            }).ToArray();
            foreach(var light in roofIllumination)
            {
                var p=light.transform.position;
                light.transform.LookAt(new Vector3(Mathf.Clamp(p.x*.08f,-1.6f,1.6f),0,Mathf.Clamp(p.z*.25f,-4.8f,4.8f)));
                light.range=32;light.spotAngle=50;light.innerSpotAngle=32.5f;light.shadows=LightShadows.None;
            }
            applied=-1;
        }
        public void ApplyNight(float night)
        {
            night=Mathf.Clamp01(night);
            if(applied>=0&&Mathf.Abs(applied-night)<.003f)return;
            bool first=applied<0;nightWeight=night;applied=night;bool roof=night>.025f;
            foreach(var l in poleLights)if(l)l.enabled=false;
            foreach(var l in roofLights)if(l)l.enabled=false;
            var poles=poleIllumination!=null&&poleIllumination.Length>0?poleIllumination:poleLights;
            var roofs=roofIllumination!=null&&roofIllumination.Length>0?roofIllumination:roofLights;
            foreach(var l in poles)if(l){l.enabled=true;l.intensity=Mathf.Lerp(8f,65f,night);}
            foreach(var l in roofs)if(l){l.enabled=roof;l.intensity=roofNightIntensity*night;}
            if(lightsOn&&lightsOff)
            {
                if(first)foreach(var r in poleEmitters)if(r)r.sharedMaterial=lightsOn;
                if(first||previousRoof!=roof)
                {
                    foreach(var r in roofEmitters)if(r)r.sharedMaterial=roof?lightsOn:lightsOff;
                    foreach(var r in decorEmitters)if(r)r.sharedMaterial=roof?lightsOn:lightsOff;
                }
            }
            previousRoof=roof;
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color=Color.cyan;Gizmos.DrawWireCube(Vector3.up*.015f,new Vector3(Court.HalfWidth*2,.03f,Court.HalfLength*2));
            Gizmos.color=Color.yellow;foreach(int end in new[]{-1,1})Gizmos.DrawWireCube(new Vector3(0,.02f,end*Court.Kitchen*.5f),new Vector3(Court.HalfWidth*2,.03f,Court.Kitchen));
            Gizmos.color=Color.white;for(int i=0;i<32;i++){float a=Mathf.Lerp(-Court.NetHalfSpan,Court.NetHalfSpan,i/32f),b=Mathf.Lerp(-Court.NetHalfSpan,Court.NetHalfSpan,(i+1)/32f);Gizmos.DrawLine(new Vector3(a,Court.NetHeight(a),0),new Vector3(b,Court.NetHeight(b),0));}
        }
    }
}
