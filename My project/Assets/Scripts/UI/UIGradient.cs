using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [AddComponentMenu("UI/Effects/Gradient")]
    public class UIGradient : BaseMeshEffect
    {
        public Color colorLeft = Color.white;
        public Color colorRight = Color.white;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;

            var count = vh.currentVertCount;
            if (count == 0) return;

            var vertexs = new UIVertex[count];
            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vertexs[i], i);
            }

            float leftX = vertexs[0].position.x;
            float rightX = vertexs[0].position.x;

            for (int i = 1; i < count; i++)
            {
                float x = vertexs[i].position.x;
                if (x < leftX) leftX = x;
                else if (x > rightX) rightX = x;
            }

            float width = rightX - leftX;

            for (int i = 0; i < count; i++)
            {
                var vertex = vertexs[i];
                float normalizedX = (vertex.position.x - leftX) / width;
                vertex.color = Color.Lerp(colorLeft, colorRight, normalizedX);
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
