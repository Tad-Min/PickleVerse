using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.FrontEnd
{
    [AddComponentMenu("UI/Effects/PickleVerse Gradient")]
    public sealed class UIGradient : BaseMeshEffect
    {
        public Color colorLeft = Color.white, colorRight = Color.white;
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || mesh.currentVertCount == 0)
                return;
            UIVertex vertex = default;
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                min = Mathf.Min(min, vertex.position.x);
                max = Mathf.Max(max, vertex.position.x);
            }
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                vertex.color = Color.Lerp(colorLeft, colorRight, max > min ? (vertex.position.x - min) / (max - min) : 0);
                mesh.SetUIVertex(vertex, i);
            }
        }
    }
}
