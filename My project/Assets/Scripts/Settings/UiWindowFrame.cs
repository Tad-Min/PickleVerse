using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.Settings
{
    /// <summary>
    /// The PickleVerse window chrome in one mesh: title bar, accent line, rounded blue border and the
    /// corner fill that hides the square window corners on systems that do not round them.
    /// Only the title bar receives raycasts, so the game underneath stays clickable.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiWindowFrame : MaskableGraphic, ICanvasRaycastFilter
    {
        public float barHeight = DesktopWindow.TitleBarHeight, radius = 8, border = 2;
        public Color barTop = new Color32(27, 42, 68, 250), barBottom = new Color32(15, 25, 44, 250);
        public Color edge = DesktopWindow.Accent, innerEdge = new Color32(9, 16, 30, 170), corner = new Color32(7, 12, 22, 255);
        const int Segments = 8;

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var local))
                return false;
            var r = rectTransform.rect;
            return local.y >= r.yMax - barHeight && r.Contains(local);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var r = rectTransform.rect;
            if (r.width < 4 * radius || r.height < barHeight + 2 * radius)
                return;
            float rad = Mathf.Max(0, radius);
            Corners(mesh, r, rad);
            Bar(mesh, r, rad);
            Accent(mesh, r);
            Ring(mesh, r, rad, border, edge);
            var inner = new Rect(r.xMin + border, r.yMin + border, r.width - 2 * border, r.height - 2 * border);
            Ring(mesh, inner, Mathf.Max(0, rad - border), 1, innerEdge);
        }

        // Fills between each square corner and the rounded outline.
        void Corners(VertexHelper mesh, Rect r, float rad)
        {
            if (rad <= 0)
                return;
            for (int c = 0; c < 4; c++)
            {
                var square = CornerPoint(r, c);
                int start = mesh.currentVertCount;
                mesh.AddVert(square, corner, Vector2.zero);
                for (int i = 0; i <= Segments; i++)
                    mesh.AddVert(Arc(r, rad, c, i / (float)Segments), corner, Vector2.zero);
                for (int i = 0; i < Segments; i++)
                    mesh.AddTriangle(start, start + 1 + i, start + 2 + i);
            }
        }

        // Title bar with rounded top corners and a vertical gradient.
        void Bar(VertexHelper mesh, Rect r, float rad)
        {
            float bottom = r.yMax - barHeight;
            int centre = mesh.currentVertCount;
            mesh.AddVert(new Vector2(r.center.x, bottom + barHeight * .5f), Color.Lerp(barBottom, barTop, .5f), Vector2.zero);
            int first = mesh.currentVertCount;
            AddBarVert(mesh, new Vector2(r.xMin, bottom), bottom);
            for (int i = 0; i <= Segments; i++)
                AddBarVert(mesh, Arc(r, rad, 1, i / (float)Segments), bottom);
            for (int i = 0; i <= Segments; i++)
                AddBarVert(mesh, Arc(r, rad, 2, i / (float)Segments), bottom);
            AddBarVert(mesh, new Vector2(r.xMax, bottom), bottom);
            int last = mesh.currentVertCount - 1;
            for (int i = first; i < last; i++)
                mesh.AddTriangle(centre, i, i + 1);
            mesh.AddTriangle(centre, last, first);
        }
        void AddBarVert(VertexHelper mesh, Vector2 p, float bottom)
        {
            mesh.AddVert(p, Color.Lerp(barBottom, barTop, Mathf.InverseLerp(bottom, bottom + barHeight, p.y)), Vector2.zero);
        }

        // A blue hairline under the title bar that fades towards the window edges.
        void Accent(VertexHelper mesh, Rect r)
        {
            float y = r.yMax - barHeight;
            var clear = edge;
            clear.a = 0;
            float[] xs = { r.xMin + border, r.xMin + r.width * .22f, r.xMax - r.width * .22f, r.xMax - border };
            Color[] colours = { clear, edge, edge, clear };
            int start = mesh.currentVertCount;
            for (int i = 0; i < xs.Length; i++)
            {
                mesh.AddVert(new Vector2(xs[i], y), colours[i], Vector2.zero);
                mesh.AddVert(new Vector2(xs[i], y + 1.2f), colours[i], Vector2.zero);
            }
            for (int i = 0; i < xs.Length - 1; i++)
            {
                int a = start + i * 2;
                mesh.AddTriangle(a, a + 1, a + 3);
                mesh.AddTriangle(a, a + 3, a + 2);
            }
        }

        // Rounded rectangle outline of the given thickness, inside r.
        void Ring(VertexHelper mesh, Rect r, float rad, float thickness, Color colour)
        {
            if (thickness <= 0 || colour.a <= 0)
                return;
            var inner = new Rect(r.xMin + thickness, r.yMin + thickness, r.width - 2 * thickness, r.height - 2 * thickness);
            float innerRad = Mathf.Max(0, rad - thickness);
            int start = mesh.currentVertCount, count = 0;
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= Segments; i++)
                {
                    float t = i / (float)Segments;
                    mesh.AddVert(Arc(r, rad, c, t), colour, Vector2.zero);
                    mesh.AddVert(Arc(inner, innerRad, c, t), colour, Vector2.zero);
                    count++;
                }
            for (int i = 0; i < count; i++)
            {
                int a = start + i * 2, b = start + (i + 1) % count * 2;
                mesh.AddTriangle(a, a + 1, b + 1);
                mesh.AddTriangle(a, b + 1, b);
            }
        }

        // Corners in order: bottom-left, top-left, top-right, bottom-right (clockwise around the window).
        static Vector2 CornerPoint(Rect r, int c) => c == 0 ? new Vector2(r.xMin, r.yMin) : c == 1 ? new Vector2(r.xMin, r.yMax) : c == 2 ? new Vector2(r.xMax, r.yMax) : new Vector2(r.xMax, r.yMin);
        static readonly float[] startAngles = { 270, 180, 90, 0 };
        // Each corner sweeps 90 degrees clockwise: bottom-left goes from the bottom edge to the left edge, and so on.
        static Vector2 Arc(Rect r, float rad, int c, float t)
        {
            var centre = new Vector2(c < 2 ? r.xMin + rad : r.xMax - rad, c == 1 || c == 2 ? r.yMax - rad : r.yMin + rad);
            float angle = (startAngles[c] - 90 * t) * Mathf.Deg2Rad;
            return centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * rad;
        }
    }
}
