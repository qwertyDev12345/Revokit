using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cones
{
    public sealed class FieldBoard : MaskableGraphic, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public DrillPlan Plan { get; set; }
        public bool RouteMode { get; set; }
        public bool Playing { get; set; }
        public int Selected { get; set; } = -1;
        public Vector2 Runner { get; set; }
        public Action Changed;
        public bool ReadOnly { get; set; }
        public bool Compact { get; set; }
        public bool TargetsOnly { get; set; }
        public Action<int> ConeTapped;
        public HashSet<int> Placed;
        private int dragging = -1;
        public bool IsDragging => dragging >= 0;
        private readonly List<Text> labels = new List<Text>();
        private Font labelFont;
        private static readonly Color Ink = new Color32(36, 47, 61, 255);
        private static readonly Color Orange = new Color32(255, 96, 12, 255);

        public Rect FieldRect
        {
            get
            {
                Rect r = rectTransform.rect;
                float scale = Mathf.Min(Mathf.Max(1, r.width - (Compact ? 20 : 96)) / Plan.width, Mathf.Max(1, r.height - (Compact ? 20 : 96)) / Plan.length);
                Vector2 size = new Vector2(Plan.width, Plan.length) * scale;
                return new Rect(r.center - size / 2, size);
            }
        }

        public Vector2 LocalPoint(Vector2 meters)
        {
            Rect r = FieldRect;
            return r.min + new Vector2(meters.x / Plan.width * r.width, meters.y / Plan.length * r.height);
        }

        private Vector2 Meters(Vector2 point)
        {
            Rect r = FieldRect;
            float x = Mathf.Clamp01((point.x - r.xMin) / r.width) * Plan.width;
            float y = Mathf.Clamp01((point.y - r.yMin) / r.height) * Plan.length;
            return new Vector2(Mathf.Clamp(Mathf.Round(x * 4) / 4, 0, Plan.width), Mathf.Clamp(Mathf.Round(y * 4) / 4, 0, Plan.length));
        }

        public void Refresh()
        {
            SetVerticesDirty();
            RefreshLabels();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (Plan != null && isActiveAndEnabled) RefreshLabels();
        }

        private void RefreshLabels()
        {
            if (Plan == null) return;
            if (labelFont == null) labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int segments = Mathf.Max(0, Plan.route.Count - 1) + (Plan.closed && Plan.route.Count > 1 ? 1 : 0);
            if (TargetsOnly) segments = 0;
            int needed = Compact ? 0 : Plan.cones.Count * 2 + segments + (Plan.obstacles?.Count ?? 0);
            while (labels.Count < needed)
            {
                var go = new GameObject("Map label", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(transform, false);
                var text = go.GetComponent<Text>();
                text.font = labelFont;
                text.fontSize = 20;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 12;
                text.resizeTextMaxSize = 20;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Ink;
                text.raycastTarget = false;
                text.supportRichText = false;
                text.rectTransform.sizeDelta = new Vector2(150, 48);
                labels.Add(text);
            }
            for (int i = 0; i < labels.Count; i++) labels[i].gameObject.SetActive(i < needed);
            if (Compact) return;
            for (int i = 0; i < Plan.cones.Count; i++)
            {
                string tag = TargetsOnly ? "" : Plan.StyleAt(i).label;
                labels[i].text = (i + 1).ToString();
                labels[i].fontSize = labels[i].resizeTextMaxSize = 20;
                labels[i].color = Color.white; labels[i].fontStyle = FontStyle.Bold;
                labels[i].rectTransform.sizeDelta = new Vector2(44, 44);
                labels[i].rectTransform.anchoredPosition = LocalPoint(Plan.cones[i]);
                var tagLabel = labels[Plan.cones.Count + i];
                tagLabel.text = tag; tagLabel.color = Ink; tagLabel.fontStyle = FontStyle.Bold;
                tagLabel.fontSize = tagLabel.resizeTextMaxSize = 20;
                tagLabel.rectTransform.sizeDelta = new Vector2(160, 40);
                Vector2 position = LocalPoint(Plan.cones[i]) + new Vector2(0, -40);
                Rect bounds = rectTransform.rect;
                tagLabel.rectTransform.anchoredPosition = new Vector2(Mathf.Clamp(position.x, bounds.xMin + 80, bounds.xMax - 80),
                    Mathf.Clamp(position.y, bounds.yMin + 20, bounds.yMax - 20));
            }
            for (int i = 1; i <= segments; i++)
            {
                Vector2 a = Plan.cones[Plan.route[i - 1]], b = Plan.cones[Plan.route[i % Plan.route.Count]];
                var label = labels[Plan.cones.Count * 2 + i - 1];
                var movement = Plan.MovementAt(i - 1);
                label.text = Vector2.Distance(a, b).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m"
                    + (movement == MovementKind.Run ? "" : " · " + DrillPlan.MovementName(movement));
                label.fontSize = label.resizeTextMaxSize = 16;
                label.fontStyle = FontStyle.Normal; label.color = Ink;
                label.rectTransform.sizeDelta = new Vector2(164, 28);
                label.rectTransform.anchoredPosition = LocalPoint((a + b) / 2) + new Vector2(0, 15);
            }
            if (Plan.obstacles != null) for (int i = 0; i < Plan.obstacles.Count; i++)
            {
                var o = Plan.obstacles[i]; var label = labels[Plan.cones.Count * 2 + segments + i];
                label.text = o.name; label.color = Ink; label.fontStyle = FontStyle.Bold;
                label.fontSize = label.resizeTextMaxSize = 16;
                label.rectTransform.sizeDelta = new Vector2(160, 36);
                var p = LocalPoint(new Vector2(o.x + o.width / 2, o.y + o.height)) + new Vector2(0, 18);
                Rect bounds = rectTransform.rect;
                label.rectTransform.anchoredPosition = new Vector2(Mathf.Clamp(p.x, bounds.xMin + 80, bounds.xMax - 80), Mathf.Clamp(p.y, bounds.yMin + 18, bounds.yMax - 18));
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Plan == null) return;
            Rect r = FieldRect;
            Quad(vh, rectTransform.rect, new Color32(227, 238, 221, 255));
            Quad(vh, r, new Color32(130, 171, 111, 255));
            if (Plan.obstacles != null) foreach (var o in Plan.obstacles)
            {
                Vector2 a = LocalPoint(new Vector2(o.x, o.y)), b = LocalPoint(new Vector2(o.x + o.width, o.y + o.height));
                Color zone = o.kind == ObstacleKind.Wall ? new Color32(116, 121, 130, 255)
                    : o.kind == ObstacleKind.Goal ? new Color32(137, 192, 229, 255) : new Color32(241, 164, 157, 255);
                Quad(vh, new Rect(a, b-a), zone);
                Line(vh, a, b, 2, Ink); Line(vh, new Vector2(a.x, b.y), new Vector2(b.x, a.y), 2, Ink);
            }
            Color grid = new Color32(176, 201, 163, 255);
            for (int x = 0; x <= Plan.width; x++) Line(vh, LocalPoint(new Vector2(x, 0)), LocalPoint(new Vector2(x, Plan.length)), 1, grid);
            for (int y = 0; y <= Plan.length; y++) Line(vh, LocalPoint(new Vector2(0, y)), LocalPoint(new Vector2(Plan.width, y)), 1, grid);
            Line(vh, r.min, new Vector2(r.xMax, r.yMin), 2, Color.white);
            Line(vh, new Vector2(r.xMax, r.yMin), r.max, 2, Color.white);
            Line(vh, r.max, new Vector2(r.xMin, r.yMax), 2, Color.white);
            Line(vh, new Vector2(r.xMin, r.yMax), r.min, 2, Color.white);
            for (int i = 1; !TargetsOnly && i < Plan.route.Count + (Plan.closed && Plan.route.Count > 1 ? 1 : 0); i++)
            {
                Vector2 a = LocalPoint(Plan.cones[Plan.route[i - 1]]), b = LocalPoint(Plan.cones[Plan.route[i % Plan.route.Count]]);
                Color routeColor = Plan.SegmentBlocked(i - 1) ? new Color32(190, 32, 37, 255) : Orange;
                float distance = Vector2.Distance(a, b);
                Vector2 dashDirection = (b - a).normalized;
                for (float step = 0; step < distance; step += 17)
                    Line(vh, a + dashDirection * step, a + dashDirection * Mathf.Min(step + 10, distance), Compact ? 2 : 3, routeColor);
                Vector2 direction = (b - a).normalized;
                Vector2 perpendicular = new Vector2(-direction.y, direction.x);
                Vector2 tip = Vector2.Lerp(a, b, 0.65f);
                Triangle(vh, tip, tip - direction * 13 + perpendicular * 6, tip - direction * 13 - perpendicular * 6, routeColor);
                if (!Compact)
                {
                    Vector2 middle = (a + b) / 2 + new Vector2(0, 15);
                    Quad(vh, new Rect(middle.x - 82, middle.y - 14, 164, 28), new Color32(245, 249, 241, 245));
                }
            }
            for (int i = 0; i < Plan.cones.Count; i++)
            {
                Vector2 p = LocalPoint(Plan.cones[i]);
                float radius = Compact ? 8 : 22;
                if (Placed != null && Placed.Contains(i)) Circle(vh, p, radius + 8, new Color32(37, 124, 68, 255));
                if (i == Selected) Circle(vh, p, radius + 7, Ink);
                Circle(vh, p, radius + 2, Color.white);
                Circle(vh, p, radius, ColorFor(Plan.StyleAt(i).color));
            }
            if (Playing)
            {
                Circle(vh, LocalPoint(Runner), 13, Ink);
                Circle(vh, LocalPoint(Runner), 7, Color.white);
            }
        }

        private static void Quad(VertexHelper vh, Rect r, Color color)
        {
            int n = vh.currentVertCount;
            vh.AddVert(new Vector2(r.xMin, r.yMin), color, Vector2.zero);
            vh.AddVert(new Vector2(r.xMin, r.yMax), color, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMax), color, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMin), color, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }

        private static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int n = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero); vh.AddVert(c, color, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2);
        }

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * width / 2;
            Triangle(vh, a - normal, a + normal, b + normal, color);
            Triangle(vh, a - normal, b + normal, b - normal, color);
        }

        private static void Circle(VertexHelper vh, Vector2 center, float radius, Color color)
        {
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI / 12, b = (i + 1) * Mathf.PI / 12;
                Triangle(vh, center, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, color);
            }
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (Playing || Plan == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, e.position, e.pressEventCamera, out Vector2 p);
            int nearest = -1;
            float distance = 38;
            for (int i = 0; i < Plan.cones.Count; i++)
            {
                float candidate = Vector2.Distance(p, LocalPoint(Plan.cones[i]));
                if (candidate < distance) { distance = candidate; nearest = i; }
            }
            Selected = nearest;
            if (ReadOnly) { Refresh(); ConeTapped?.Invoke(nearest); return; }
            if (RouteMode)
            {
                if (nearest >= 0 && Plan.route.Count < 100 && (Plan.route.Count == 0 || Plan.route[Plan.route.Count - 1] != nearest))
                    Plan.route.Add(nearest);
            }
            else dragging = nearest;
            Refresh();
            Changed?.Invoke();
        }

        public void OnDrag(PointerEventData e)
        {
            if (ReadOnly || Playing || RouteMode || dragging < 0 || dragging >= Plan.cones.Count) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, e.position, e.pressEventCamera, out Vector2 p);
            Plan.cones[dragging] = Meters(p);
            Refresh();
            Changed?.Invoke();
        }

        public void OnPointerUp(PointerEventData e)
        {
            dragging = -1;
            if (!ReadOnly) Changed?.Invoke();
        }

        public static Color ColorFor(int index) => index == 1 ? new Color32(45, 113, 203, 255)
            : index == 2 ? new Color32(33, 145, 91, 255) : index == 3 ? new Color32(200, 57, 59, 255) : Orange;
    }
}
