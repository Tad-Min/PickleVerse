using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.LocalMatch
{
    // Four small, reusable meshes; no particle system, render texture or per-frame allocations.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TouchWaveGraphic : MaskableGraphic
    {
        public TouchShotDirection direction;
        public float progress;
        public bool serving;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int i = 0; i < 4; i++)
            {
                if (serving && i == 0) continue;
                bool selected = (int)direction == i + 1;
                float radius = selected ? 66 + progress * 20 : i == 3 ? 74 : 57;
                Arc(vh, i, radius, selected ? 13 : 7, selected ? .88f : .25f);
                if (selected) Arc(vh, i, radius + 10, 3, .18f);
            }
        }
        void Arc(VertexHelper vh, int side, float radius, float width, float alpha)
        {
            float angle = side == 0 ? 90 : side == 1 ? 180 : side == 2 ? 0 : 270;
            Color tint = new Color(1,1,1,alpha);
            int start = vh.currentVertCount;
            const int segments = 16;
            for (int j = 0; j <= segments; j++)
            {
                float a = (angle - 36 + 72f * j / segments) * Mathf.Deg2Rad;
                Vector2 unit = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(unit * (radius-width*.5f), tint, Vector2.zero);
                vh.AddVert(unit * (radius+width*.5f), tint, Vector2.zero);
                if (j == segments) continue;
                int n = start+j*2;
                vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n+1,n+3,n+2);
            }
        }
    }
}
