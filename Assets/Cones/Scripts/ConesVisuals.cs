using UnityEngine;
using UnityEngine.UI;

namespace Cones
{
    // Code-drawn UI stays crisp at any phone resolution; only the hero is raster artwork.
    public static class ConesVisuals
    {
        public static readonly Color Orange = new Color32(255, 96, 12, 255);
        public static readonly Color Muted = new Color32(94, 104, 116, 255);
        public static readonly Color Border = new Color32(217, 222, 228, 255);
        private static Sprite rounded;
        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.name = "UI rounded rectangle";
                texture.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(18 - (x + .5f), x + .5f - 46);
                    float dy = Mathf.Max(18 - (y + .5f), y + .5f - 46);
                    float d = new Vector2(Mathf.Max(0, dx), Mathf.Max(0, dy)).magnitude;
                    texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(18.5f - d)));
                }
                texture.Apply(false, true);
                rounded = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0,
                    SpriteMeshType.FullRect, new Vector4(20, 20, 20, 20));
                return rounded;
            }
        }
    }

    public sealed class WorkoutRing : MaskableGraphic
    {
        private float progress = 1;
        public void SetProgress(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Abs(value - progress) < .001f) return;
            progress = value; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f - 10;
            DrawArc(vh, radius, 1, new Color32(235, 237, 240, 255));
            DrawArc(vh, radius, progress, ConesVisuals.Orange);
        }
        private void DrawArc(VertexHelper vh, float radius, float amount, Color tint)
        {
            int segments = Mathf.CeilToInt(120 * amount);
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.PI * .5f - Mathf.PI * 2 * amount * i / segments;
                float b = Mathf.PI * .5f - Mathf.PI * 2 * amount * (i + 1) / segments;
                Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), q = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                int n = vh.currentVertCount;
                Vector2 center = rectTransform.rect.center;
                vh.AddVert(center + p * radius, tint, Vector2.zero); vh.AddVert(center + p * (radius - 14), tint, Vector2.zero);
                vh.AddVert(center + q * (radius - 14), tint, Vector2.zero); vh.AddVert(center + q * radius, tint, Vector2.zero);
                vh.AddTriangle(n, n+1, n+2); vh.AddTriangle(n, n+2, n+3);
            }
        }
    }

    public sealed class ConesIcon : MaskableGraphic
    {
        public string Kind { get; set; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Kind == "Home")
            { Line(vh, 3, 12, 12, 3); Line(vh, 12, 3, 21, 12); Line(vh, 5, 11, 5, 21); Line(vh, 19, 11, 19, 21); Line(vh, 5, 21, 19, 21); Line(vh, 10, 21, 10, 15); Line(vh, 10, 15, 14, 15); Line(vh, 14, 15, 14, 21); }
            else if (Kind == "Session")
            { for (int y = 5; y <= 19; y += 7) { Line(vh, 3, y, 5, y); Line(vh, 9, y, 21, y); } }
            else if (Kind == "Folder")
            { Line(vh, 3, 6, 9, 6); Line(vh, 9, 6, 12, 9); Line(vh, 12, 9, 21, 9); Line(vh, 21, 9, 21, 20); Line(vh, 21, 20, 3, 20); Line(vh, 3, 20, 3, 6); Line(vh, 3, 11, 21, 11); }
            else
            { Line(vh, 5, 20, 10, 3); Line(vh, 10, 3, 14, 3); Line(vh, 14, 3, 19, 20); Line(vh, 3, 21, 21, 21); Line(vh, 7, 15, 17, 15); Line(vh, 9, 9, 15, 9); }
        }
        private void Line(VertexHelper vh, float x1, float y1, float x2, float y2)
        {
            Rect r = rectTransform.rect;
            Vector2 a = r.min + new Vector2(x1 / 24 * r.width, (24 - y1) / 24 * r.height);
            Vector2 b = r.min + new Vector2(x2 / 24 * r.width, (24 - y2) / 24 * r.height);
            Vector2 normal = new Vector2(-(b-a).y, (b-a).x).normalized * Mathf.Min(r.width, r.height) / 28;
            int n = vh.currentVertCount;
            vh.AddVert(a-normal, color, Vector2.zero); vh.AddVert(a+normal, color, Vector2.zero);
            vh.AddVert(b+normal, color, Vector2.zero); vh.AddVert(b-normal, color, Vector2.zero);
            vh.AddTriangle(n, n+1, n+2); vh.AddTriangle(n, n+2, n+3);
        }
    }
}
