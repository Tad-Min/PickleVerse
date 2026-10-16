using UnityEngine;
using UnityEngine.UI;
namespace PickleVerse.Settings
{
    public sealed class UiPulseGraphic : MaskableGraphic
    {
        float progress;
        public float Progress {set{progress=value;SetVerticesDirty();}}
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;float center=Mathf.Lerp(-.35f,1.35f,progress),width=.08f;
            // Narrow diagonal strips clamped to the button; 32 vertices independent of its pixel resolution.
            var tint=color;tint.a*=Mathf.Sin(progress*Mathf.PI);
            for(int i=0;i<8;i++)
            {
                float y0=i/8f,y1=(i+1)/8f;
                float x0=Mathf.Clamp01(center+(y0-.5f)*.24f-width),x1=Mathf.Clamp01(center+(y0-.5f)*.24f+width);
                float x2=Mathf.Clamp01(center+(y1-.5f)*.24f+width),x3=Mathf.Clamp01(center+(y1-.5f)*.24f-width);int n=mesh.currentVertCount;
                mesh.AddVert(new Vector3(r.xMin+x0*r.width,r.yMin+y0*r.height,0),tint,Vector2.zero);mesh.AddVert(new Vector3(r.xMin+x1*r.width,r.yMin+y0*r.height,0),tint,Vector2.zero);
                mesh.AddVert(new Vector3(r.xMin+x2*r.width,r.yMin+y1*r.height,0),tint,Vector2.zero);mesh.AddVert(new Vector3(r.xMin+x3*r.width,r.yMin+y1*r.height,0),tint,Vector2.zero);
                mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);
            }
        }
    }
}
