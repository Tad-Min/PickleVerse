using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.Settings
{
    public enum ArenaGlyph { Home, Progress, Career, Collection, Shop, Settings, Music, Book, Arrow, Previous, Next, Play, Pause, Close, Star, Orbit, RecordFrame, Run, Copy, Edit }

    /// <summary>Small vector symbols; retain sharp edges at different canvas scales.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiLineIcon : MaskableGraphic
    {
        public ArenaGlyph glyph;
        [Range(.025f, .12f)]
        public float stroke = .065f;
        public void SetGlyph(ArenaGlyph value)
        {
            if (glyph == value)
                return;
            glyph = value;
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            switch (glyph)
            {
                case ArenaGlyph.Copy:
                    Poly(mesh, new[] { new Vector2(-.3f, -.8f), new Vector2(.7f, -.8f), new Vector2(.7f, .3f), new Vector2(-.3f, .3f) }, true);
                    Line(mesh, -.7f, -.3f, -.7f, .75f); Line(mesh, -.7f, .75f, .3f, .75f); break;
                case ArenaGlyph.Edit:
                    Poly(mesh, new[] { new Vector2(-.7f, -.75f), new Vector2(-.55f, -.2f), new Vector2(.5f, .85f), new Vector2(.85f, .5f), new Vector2(-.2f, -.55f) }, true);
                    Line(mesh, .32f, .65f, .65f, .32f); Line(mesh, -.8f, -.85f, .7f, -.85f); break;
                case ArenaGlyph.Run:
                    Circle(mesh, .25f, .62f, .17f); Line(mesh, .12f, .32f, -.12f, -.12f); Line(mesh, .12f, .32f, .55f, .08f); Line(mesh, .55f, .08f, .78f, .3f);
                    Line(mesh, .12f, .32f, -.34f, .34f); Line(mesh, -.34f, .34f, -.57f, .02f); Line(mesh, -.12f, -.12f, .3f, -.4f); Line(mesh, .3f, -.4f, .12f, -.82f);
                    Line(mesh, -.12f, -.12f, -.44f, -.5f); Line(mesh, -.44f, -.5f, -.84f, -.4f); break;
                case ArenaGlyph.Home:
                    Line(mesh, -.82f, .05f, 0, .78f); Line(mesh, 0, .78f, .82f, .05f); Line(mesh, -.58f, .25f, -.58f, -.72f); Line(mesh, .58f, .25f, .58f, -.72f); Line(mesh, -.58f, -.72f, .58f, -.72f); Line(mesh, -.17f, -.72f, -.17f, -.2f); Line(mesh, -.17f, -.2f, .17f, -.2f); Line(mesh, .17f, -.2f, .17f, -.72f); break;
                case ArenaGlyph.Progress:
                    Line(mesh, -.7f, -.72f, -.7f, -.12f); Line(mesh, 0, -.72f, 0, .22f); Line(mesh, .7f, -.72f, .7f, .66f); Line(mesh, -.85f, -.83f, .85f, -.83f); Line(mesh, -.65f, .25f, .70f, .80f); break;
                case ArenaGlyph.Career:
                    Line(mesh, -.5f, .72f, .5f, .72f); Line(mesh, -.5f, .72f, -.32f, -.05f); Line(mesh, .5f, .72f, .32f, -.05f); Line(mesh, -.32f, -.05f, .32f, -.05f); Line(mesh, 0, -.05f, 0, -.72f); Line(mesh, -.42f, -.72f, .42f, -.72f); Line(mesh, -.5f, .55f, -.8f, .55f); Line(mesh, -.8f, .55f, -.7f, .02f); Line(mesh, -.7f, .02f, -.32f, -.05f); Line(mesh, .5f, .55f, .8f, .55f); Line(mesh, .8f, .55f, .7f, .02f); Line(mesh, .7f, .02f, .32f, -.05f); break;
                case ArenaGlyph.Collection:
                    Poly(mesh, new[] { new Vector2(-.65f, -.70f), new Vector2(-.65f, .65f), new Vector2(.42f, .65f), new Vector2(.42f, -.70f) }, true); Line(mesh, -.40f, .88f, .70f, .88f); Line(mesh, .70f, .88f, .70f, -.4f); Diamond(mesh, -.12f, 0, .28f); break;
                case ArenaGlyph.Shop:
                    Poly(mesh, new[] { new Vector2(-.68f, .25f), new Vector2(.68f, .25f), new Vector2(.80f, -.75f), new Vector2(-.80f, -.75f) }, true); Arc(mesh, 0, .25f, .4f, 0, 180); break;
                case ArenaGlyph.Settings:
                    Circle(mesh, 0, 0, .32f); Circle(mesh, 0, 0, .68f); for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4;
                        Line(mesh, Mathf.Cos(a) * .64f, Mathf.Sin(a) * .64f, Mathf.Cos(a) * .88f, Mathf.Sin(a) * .88f);
                    }
                    break;
                case ArenaGlyph.Music:
                    Line(mesh, -.18f, -.43f, -.18f, .63f); Line(mesh, -.18f, .63f, .65f, .83f); Line(mesh, .65f, .83f, .65f, -.18f); Circle(mesh, -.4f, -.46f, .22f); Circle(mesh, .43f, -.21f, .22f); break;
                case ArenaGlyph.Book:
                    Poly(mesh, new[] { new Vector2(0, -.72f), new Vector2(-.78f, -.52f), new Vector2(-.78f, .74f), new Vector2(0, .48f), new Vector2(.78f, .74f), new Vector2(.78f, -.52f) }, true); Line(mesh, 0, -.72f, 0, .48f); break;
                case ArenaGlyph.Arrow: Line(mesh, -.8f, 0, .72f, 0); Line(mesh, .18f, .6f, .78f, 0); Line(mesh, .78f, 0, .18f, -.6f); break;
                case ArenaGlyph.Previous: Triangle(mesh, true); Line(mesh, -.78f, .65f, -.78f, -.65f); break;
                case ArenaGlyph.Next: Triangle(mesh, false); Line(mesh, .78f, .65f, .78f, -.65f); break;
                case ArenaGlyph.Play: Triangle(mesh, false); break;
                case ArenaGlyph.Pause: Line(mesh, -.32f, -.7f, -.32f, .7f); Line(mesh, .32f, -.7f, .32f, .7f); break;
                case ArenaGlyph.Close: Line(mesh, -.65f, -.65f, .65f, .65f); Line(mesh, -.65f, .65f, .65f, -.65f); break;
                case ArenaGlyph.Star: Diamond(mesh, 0, 0, .82f); Line(mesh, -.82f, 0, .82f, 0); Line(mesh, 0, -.82f, 0, .82f); break;
                case ArenaGlyph.Orbit:
                    Circle(mesh, 0, 0, .94f); Circle(mesh, 0, 0, .80f); for (int i = 0; i < 12; i++)
                    {
                        float a = i * Mathf.PI / 6;
                        Line(mesh, Mathf.Cos(a) * .94f, Mathf.Sin(a) * .94f, Mathf.Cos(a), Mathf.Sin(a));
                    }
                    break;
                case ArenaGlyph.RecordFrame:
                    Circle(mesh, 0, 0, .94f); for (int i = 0; i < 12; i++)
                    {
                        float a = i * Mathf.PI / 6;
                        Line(mesh, Mathf.Cos(a) * .94f, Mathf.Sin(a) * .94f, Mathf.Cos(a), Mathf.Sin(a));
                    }
                    break;
            }
        }
        void Triangle(VertexHelper m, bool left)
        {
            float sign = left ? -1 : 1;
            Poly(m, new[] { new Vector2(-.4f * sign, .65f), new Vector2(.5f * sign, 0), new Vector2(-.4f * sign, -.65f) }, true);
        }
        void Diamond(VertexHelper m, float x, float y, float radius)
        {
            Poly(m, new[] { new Vector2(x, y + radius), new Vector2(x + radius, y), new Vector2(x, y - radius), new Vector2(x - radius, y) }, true);
        }
        void Circle(VertexHelper m, float x, float y, float radius)
        {
            Arc(m, x, y, radius, 0, 360);
        }
        void Arc(VertexHelper m, float x, float y, float radius, float from, float to)
        {
            for (int i = 0; i < 40; i++)
            {
                float a = Mathf.Lerp(from, to, i / 40f) * Mathf.Deg2Rad, b = Mathf.Lerp(from, to, (i + 1) / 40f) * Mathf.Deg2Rad;
                Line(m, x + radius * Mathf.Cos(a), y + radius * Mathf.Sin(a), x + radius * Mathf.Cos(b), y + radius * Mathf.Sin(b));
            }
        }
        void Poly(VertexHelper m, Vector2[] points, bool closed)
        {
            for (int i = 1; i < points.Length; i++)
                Line(m, points[i - 1].x, points[i - 1].y, points[i].x, points[i].y);
            if (closed)
                Line(m, points[points.Length - 1].x, points[points.Length - 1].y, points[0].x, points[0].y);
        }
        void Line(VertexHelper m, float ax, float ay, float bx, float by)
        {
            var r = rectTransform.rect;
            float size = Mathf.Min(r.width, r.height) * .46f;
            Vector2 a = r.center + new Vector2(ax, ay) * size, b = r.center + new Vector2(bx, by) * size;
            Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * (size * stroke * .5f);
            int index = m.currentVertCount;
            m.AddVert(a - n, color, Vector2.zero);
            m.AddVert(a + n, color, Vector2.zero);
            m.AddVert(b + n, color, Vector2.zero);
            m.AddVert(b - n, color, Vector2.zero);
            m.AddTriangle(index, index + 1, index + 2);
            m.AddTriangle(index, index + 2, index + 3);
        }
    }
}
