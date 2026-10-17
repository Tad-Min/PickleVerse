using UnityEngine;
using UnityEngine.Rendering;

namespace PickleVerse.LocalMatch
{
    /// <summary>One reusable, unlit mesh per actor. No Instantiate, particles, lights or GC per hit.</summary>
    public sealed class PaddleImpactVfx : MonoBehaviour
    {
        const int RingSegments = 24, Rays = 6;
        readonly Vector3[] vertices = new Vector3[RingSegments*4+Rays*3];
        readonly Color32[] colors = new Color32[RingSegments*4+Rays*3];
        Mesh mesh;
        MeshRenderer display;
        Transform effect;
        Camera view;
        float age, duration, size;
        Color tint;
        Vector3 direction;
        Vector3 contactPosition;
        public int PlayCount { get; private set; }
        public bool Visible => display && display.enabled;
        void Awake()
        {
            var material = Resources.Load<Material>("PaddleImpact");
            if (!material) return;
            var go = new GameObject("Paddle impact (pooled)", typeof(MeshFilter), typeof(MeshRenderer));
            go.layer=gameObject.layer;
            go.transform.SetParent(transform, false); effect = go.transform;
            mesh = new Mesh { name="Paddle impact mesh" }; mesh.MarkDynamic();
            var indices = new int[RingSegments*6+Rays*3];
            for (int i=0;i<RingSegments;i++)
            {
                int v=i*4,t=i*6;
                indices[t]=v;indices[t+1]=v+2;indices[t+2]=v+1;
                indices[t+3]=v+1;indices[t+4]=v+2;indices[t+5]=v+3;
            }
            for (int i=0;i<Rays*3;i++) indices[RingSegments*6+i]=RingSegments*4+i;
            mesh.vertices=vertices;mesh.colors32=colors;mesh.triangles=indices;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            display=go.GetComponent<MeshRenderer>();display.sharedMaterial=material;
            display.shadowCastingMode=ShadowCastingMode.Off;display.receiveShadows=false;
            display.lightProbeUsage=LightProbeUsage.Off;display.reflectionProbeUsage=ReflectionProbeUsage.Off;
            display.enabled=false;
        }
        public void Play(Vector3 position, ShotIntent shot, Vector3 outgoing)
        {
            if (!display) return;
            PlayCount++; age=0;duration=shot.power||shot.kind==ShotKind.Smash?.24f:.18f;
            size=shot.power||shot.kind==ShotKind.Smash?.40f:shot.kind==ShotKind.Dink||shot.kind==ShotKind.Reset?.20f:.28f;
            tint=shot.power||shot.kind==ShotKind.Smash?new Color(1,.81f,.37f):new Color(.64f,1,.94f);
            direction=outgoing; contactPosition=position;view=Camera.main;display.enabled=true;Draw();
        }
        public void Clear(){if(display)display.enabled=false;}
        void LateUpdate()
        { Advance(Time.deltaTime); }
        public void Advance(float dt)
        {
            if (!Visible) return;
            age+=dt;if(age>=duration){Clear();return;}Draw();
        }
        void Draw()
        {
            if (!view) view=Camera.main;
            effect.position=contactPosition-(view?view.transform.forward*.045f:Vector3.zero);
            if (view) effect.rotation=view.transform.rotation;
            // Cancel actor/root scale. Impact remains in metres, on the confirmed contact point.
            var s=transform.lossyScale;effect.localScale=new Vector3(1/Mathf.Max(.001f,s.x),1/Mathf.Max(.001f,s.y),1/Mathf.Max(.001f,s.z));
            float t=Mathf.Clamp01(age/duration),radius=Mathf.Lerp(.055f,size,1-(1-t)*(1-t));
            float width=Mathf.Lerp(.022f,.004f,t),alpha=(1-t)*(1-t);
            Color c=tint;c.a=alpha;
            for(int i=0;i<RingSegments;i++)
            {
                float a=i*Mathf.PI*2/RingSegments,b=(i+1)*Mathf.PI*2/RingSegments;
                Vector3 u=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0),v=new Vector3(Mathf.Cos(b),Mathf.Sin(b),0);
                int k=i*4;vertices[k]=u*(radius-width);vertices[k+1]=u*radius;vertices[k+2]=v*(radius-width);vertices[k+3]=v*radius;
                for(int j=0;j<4;j++)colors[k+j]=c;
            }
            Vector3 local=effect.InverseTransformDirection(direction);
            float angle=Mathf.Atan2(local.y,local.x);
            for(int i=0;i<Rays;i++)
            {
                float a=angle+i*Mathf.PI*2/Rays;
                Vector3 u=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0),side=new Vector3(-u.y,u.x,0);
                int k=RingSegments*4+i*3;
                vertices[k]=u*radius*.48f-side*width;vertices[k+1]=u*radius*.48f+side*width;
                vertices[k+2]=u*(radius*(i%2==0?1.55f:1.15f));
                colors[k]=colors[k+1]=c;Color tip=c;tip.a=0;colors[k+2]=tip;
            }
            mesh.vertices=vertices;mesh.colors32=colors;mesh.RecalculateBounds();
        }
        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}
