using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.LocalMatch
{
    // Vector stars avoid depending on symbols missing from the Vietnamese font atlas.
    public sealed class TrainingStars : MaskableGraphic
    {
        [Range(0, 3)]
        public int stars;
        public void SetRating(int value)
        {
            stars = Mathf.Clamp(value, 0, 3);
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            float cell = rect.width / 3;
            float radius = Mathf.Min(cell * .4f, rect.height * .42f);
            for (int star = 0; star < 3; star++)
            {
                Vector2 center = new Vector2(rect.xMin + cell * (star + .5f), rect.center.y);
                Color tint = star < stars ? new Color(1, .81f, .35f) : new Color(.26f, .32f, .42f);
                int index = mesh.currentVertCount;
                mesh.AddVert(center, tint, Vector2.zero);
                for (int p = 0; p < 10; p++)
                {
                    float angle = (90 - p * 36) * Mathf.Deg2Rad;
                    float distance = p % 2 == 0 ? radius : radius * .45f;
                    mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance, tint, Vector2.zero);
                }
                for (int p = 0; p < 10; p++)
                    mesh.AddTriangle(index, index + 1 + p, index + 1 + (p + 1) % 10);
            }
        }
    }
}
