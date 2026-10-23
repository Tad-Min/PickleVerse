using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.Settings
{
    /// <summary>Static inlaid court and guilloche lines, batched with the ordinary UI material.</summary>
    [AddComponentMenu("PickleVerse/UI/Lobby etching")]
    public sealed class LobbyEtching : MaskableGraphic
    {
        public bool compact;
        protected override void Awake() { base.Awake(); raycastTarget = false; }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r = rectTransform.rect;
            if (r.width < 8 || r.height < 8) return;
            float scale = canvas ? Mathf.Max(.01f, canvas.scaleFactor) : 1;
            float line = 1 / scale;
            var ink = new Color32(222, 202, 164, compact ? (byte)7 : (byte)9);
            float size = Mathf.Min(r.width * .66f, r.height * .92f);
            var center = new Vector2(r.xMax - size * .28f, r.center.y);
            // Two offset diamonds create a restrained engraved rhythm behind the content.
            for (int ring = 0; ring < (compact ? 2 : 5); ring++)
            {
                float a = size * (.22f + ring * .105f);
                Vector2 top = center + Vector2.up * a, right = center + Vector2.right * a;
                Vector2 bottom = center + Vector2.down * a, left = center + Vector2.left * a;
                Line(mesh, top, right, line, ink, r); Line(mesh, right, bottom, line, ink, r);
                Line(mesh, bottom, left, line, ink, r); Line(mesh, left, top, line, ink, r);
            }
            if (!compact)
            {
                float w = size * .24f, h = size * .48f;
                Vector2 a = center + new Vector2(-w, -h), b = center + new Vector2(w, h);
                Line(mesh, a, new Vector2(a.x,b.y), line, ink,r); Line(mesh,new Vector2(a.x,b.y),b,line,ink,r);
                Line(mesh,b,new Vector2(b.x,a.y),line,ink,r); Line(mesh,new Vector2(b.x,a.y),a,line,ink,r);
                foreach(float y in new[]{0f,-h*.318f,h*.318f})Line(mesh,new Vector2(a.x,center.y+y),new Vector2(b.x,center.y+y),line,ink,r);
                Line(mesh,new Vector2(center.x,a.y),new Vector2(center.x,center.y-h*.318f),line,ink,r);
                Line(mesh,new Vector2(center.x,b.y),new Vector2(center.x,center.y+h*.318f),line,ink,r);
            }
            // A short metallic rule gives the surface a clear visual origin.
            Line(mesh,new Vector2(r.xMin+22,r.yMax-1),new Vector2(r.xMin+Mathf.Min(126,r.width*.28f),r.yMax-1),line,new Color32(232,205,158,100),r);
        }
        static void Line(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color32 tint,Rect clip)
        {
            // Clip to the surface so the ornament never extends into neighbouring controls.
            Vector2 d=b-a; float start=0,end=1;
            if(!Clip(-d.x,a.x-clip.xMin,ref start,ref end)||!Clip(d.x,clip.xMax-a.x,ref start,ref end)||
               !Clip(-d.y,a.y-clip.yMin,ref start,ref end)||!Clip(d.y,clip.yMax-a.y,ref start,ref end))return;
            b=a+d*end;a+=d*start;var n=new Vector2(-d.y,d.x).normalized*(width*.5f);int i=mesh.currentVertCount;
            mesh.AddVert(a-n,tint,Vector2.zero);mesh.AddVert(a+n,tint,Vector2.zero);
            mesh.AddVert(b+n,tint,Vector2.zero);mesh.AddVert(b-n,tint,Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
        }
        static bool Clip(float p,float q,ref float a,ref float b)
        {
            if(Mathf.Abs(p)<.00001f)return q>=0;float t=q/p;
            if(p<0){if(t>b)return false;if(t>a)a=t;}else{if(t<a)return false;if(t<b)b=t;}return true;
        }
        public static LobbyEtching AddTo(RectTransform surface,bool small=false)
        {
            var old=surface.Find("Inlaid Courtwork");
            var rect=old?(RectTransform)old:new GameObject("Inlaid Courtwork",typeof(RectTransform),typeof(CanvasRenderer),typeof(LobbyEtching)).GetComponent<RectTransform>();
            rect.SetParent(surface,false);rect.SetAsFirstSibling();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var graphic=rect.GetComponent<LobbyEtching>();graphic.raycastTarget=false;graphic.compact=small;graphic.SetVerticesDirty();return graphic;
        }
    }
}
