using UnityEngine;
using UnityEngine.UI;
namespace PickleVerse.Settings
{
    public sealed class LoadingShade : MaskableGraphic
    {
        public Color bottom = new Color(.025f, .04f, .075f, .98f), top = new Color(.025f, .04f, .075f, 0);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            vh.AddVert(new Vector3(r.xMin, r.yMin), bottom, Vector2.zero);
            vh.AddVert(new Vector3(r.xMin, r.yMax), top, Vector2.up);
            vh.AddVert(new Vector3(r.xMax, r.yMax), top, Vector2.one);
            vh.AddVert(new Vector3(r.xMax, r.yMin), bottom, Vector2.right);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }
}
