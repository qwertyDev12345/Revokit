using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cones
{
    [Serializable]
    public sealed partial class DrillPlan
    {
        public int version = 1;
        public float width = 5;
        public float length = 8;
        public float speed = 2;
        public int rounds = 1;
        public bool closed;
        public List<ConeStyle> styles = new List<ConeStyle>();
        public TrainingSettings training = new TrainingSettings();
        public List<Vector2> cones = new List<Vector2>();
        public List<int> route = new List<int>();
        public List<RouteLeg> legs = new List<RouteLeg>();
        public List<FieldObstacle> obstacles = new List<FieldObstacle>();
        public ReactionSettings reaction = new ReactionSettings();

        public static DrillPlan Example()
        {
            var plan = new DrillPlan();
            for (int i = 0; i < 6; i++)
            {
                plan.cones.Add(new Vector2(i % 2 == 0 ? 1 : 4, 1 + i * 1.2f));
                plan.route.Add(i);
            }
            return plan;
        }

        public bool IsValid()
        {
            if (version != 1 || !Finite(width) || !Finite(length) || width < 2 || width > 40 || length < 2 || length > 40
                || !Finite(speed) || speed < 0.5f || speed > 6 || rounds < 1 || rounds > 20
                || cones == null || route == null || cones.Count > 24 || route.Count > 100) return false;
            if (training != null && !training.IsValid()) return false;
            if (reaction != null && !reaction.IsValid()) return false;
            if (obstacles != null)
            {
                if (obstacles.Count > 20) return false;
                foreach (var obstacle in obstacles) if (obstacle == null || !obstacle.IsValid(width, length)) return false;
            }
            if (legs != null)
            {
                if (legs.Count > 100) return false;
                foreach (var leg in legs) if (leg == null || leg.from < 0 || leg.from >= cones.Count || leg.to < 0 || leg.to >= cones.Count
                    || (int)leg.movement < 0 || (int)leg.movement > 3) return false;
            }
            if (styles != null)
            {
                if (styles.Count > cones.Count) return false;
                foreach (var style in styles)
                    if (style == null || style.color < 0 || style.color > 3 || style.label == null || style.label.Length > 16) return false;
            }
            foreach (var p in cones)
                if (!Finite(p.x) || !Finite(p.y) || p.x < 0 || p.x > width || p.y < 0 || p.y > length) return false;
            foreach (int index in route)
                if (index < 0 || index >= cones.Count) return false;
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public void Resize(float newWidth, float newLength)
        {
            newWidth = Mathf.Clamp(newWidth, 2, 40);
            newLength = Mathf.Clamp(newLength, 2, 40);
            for (int i = 0; i < cones.Count; i++)
                cones[i] = new Vector2(cones[i].x * newWidth / width, cones[i].y * newLength / length);
            if (obstacles != null) foreach (var o in obstacles)
            {
                o.x *= newWidth / width; o.y *= newLength / length;
                o.width = Mathf.Max(.1f, o.width * newWidth / width); o.height = Mathf.Max(.1f, o.height * newLength / length);
                o.x = Mathf.Min(o.x, newWidth - o.width); o.y = Mathf.Min(o.y, newLength - o.height);
            }
            width = newWidth;
            length = newLength;
        }

        public void RemoveCone(int index)
        {
            if (index < 0 || index >= cones.Count) return;
            cones.RemoveAt(index);
            if (legs != null)
            {
                legs.RemoveAll(l => l.from == index || l.to == index);
                foreach (var leg in legs) { if (leg.from > index) leg.from--; if (leg.to > index) leg.to--; }
            }
            if (styles != null && index < styles.Count) styles.RemoveAt(index);
            route.RemoveAll(value => value == index);
            for (int i = 0; i < route.Count; i++) if (route[i] > index) route[i]--;
        }

        public float Distance
        {
            get
            {
                float result = 0;
                for (int i = 1; i < route.Count; i++) result += Vector2.Distance(cones[route[i - 1]], cones[route[i]]);
                if (closed && route.Count > 1) result += Vector2.Distance(cones[route[route.Count - 1]], cones[route[0]]);
                return result;
            }
        }

        public Vector2 PositionAt(float distance)
        {
            if (route.Count == 0) return Vector2.zero;
            for (int i = 1; i < route.Count + (closed && route.Count > 1 ? 1 : 0); i++)
            {
                Vector2 a = cones[route[i - 1]], b = cones[route[i % route.Count]];
                float segment = Vector2.Distance(a, b);
                if (segment < 0.0001f) continue;
                if (distance <= segment) return Vector2.Lerp(a, b, Mathf.Clamp01(distance / segment));
                distance -= segment;
            }
            return cones[route[closed ? 0 : route.Count - 1]];
        }

        public static DrillPlan Template(int index)
        {
            if (index == 0) return Example();
            var plan = new DrillPlan();
            if (index == 1)
            {
                plan.cones.AddRange(new[] { new Vector2(2.5f, 1), new Vector2(2.5f, 3), new Vector2(2.5f, 5), new Vector2(2.5f, 7) });
                plan.route.AddRange(new[] { 0, 1, 0, 2, 0, 3, 0 });
            }
            else if (index == 2)
            {
                plan.width = plan.length = 6;
                plan.cones.AddRange(new[] { new Vector2(1, 1), new Vector2(5, 1), new Vector2(5, 5), new Vector2(1, 5) });
                plan.route.AddRange(new[] { 0, 1, 2, 3 });
                plan.closed = true;
            }
            else
            {
                plan.width = plan.length = 8;
                plan.cones.AddRange(new[] { new Vector2(4, 1), new Vector2(4, 5), new Vector2(1, 5), new Vector2(7, 5) });
                plan.route.AddRange(new[] { 0, 1, 2, 3, 1, 0 });
            }
            return plan;
        }

        public ConeStyle StyleAt(int index) => styles != null && index < styles.Count ? styles[index] : new ConeStyle();

        public void EnsureMetadata()
        {
            if (styles == null) styles = new List<ConeStyle>();
            while (styles.Count < cones.Count) styles.Add(new ConeStyle());
            if (training == null) training = new TrainingSettings();
        }

        public static DrillPlan AdaptTemplate(int template, int count, float width, float length)
        {
            count = Mathf.Clamp(count, template >= 2 ? 4 : 2, 24);
            var plan = new DrillPlan { width = Mathf.Clamp(width, 2, 40), length = Mathf.Clamp(length, 2, 40) };
            float w = plan.width, h = plan.length;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                Vector2 p;
                if (template == 0) p = new Vector2(i % 2 == 0 ? w * .2f : w * .8f, h * (.1f + .8f * t));
                else if (template == 1) p = new Vector2(w * .5f, h * (.1f + .8f * t));
                else if (template == 2)
                {
                    int side = 0, offset = i;
                    int onSide = count / 4 + (side < count % 4 ? 1 : 0);
                    while (offset >= onSide)
                    {
                        offset -= onSide; side++;
                        onSide = count / 4 + (side < count % 4 ? 1 : 0);
                    }
                    float u = offset / (float)onSide;
                    p = side == 0 ? new Vector2(.1f + .8f * u, .1f) : side == 1 ? new Vector2(.9f, .1f + .8f * u)
                        : side == 2 ? new Vector2(.9f - .8f * u, .9f) : new Vector2(.1f, .9f - .8f * u);
                    p = Vector2.Scale(p, new Vector2(w, h));
                }
                else
                {
                    if (i == 0) p = new Vector2(w * .5f, h * .1f);
                    else if (i == 1) p = new Vector2(w * .5f, h * .75f);
                    else if (i == 2) p = new Vector2(w * .1f, h * .75f);
                    else if (i == 3) p = new Vector2(w * .9f, h * .75f);
                    else p = new Vector2(w * .5f, h * (.1f + .65f * (i - 3f) / (count - 3f)));
                }
                plan.cones.Add(p);
                if (template == 1)
                {
                    if (i == 0) plan.route.Add(0);
                    else { plan.route.Add(i); plan.route.Add(0); }
                }
                else plan.route.Add(i);
            }
            if (template == 2) plan.closed = true;
            if (template == 3)
            {
                plan.route.Clear(); plan.route.Add(0);
                for (int i = 4; i < count; i++) plan.route.Add(i);
                plan.route.AddRange(new[] { 1, 2, 3, 1 });
                for (int i = count - 1; i >= 4; i--) plan.route.Add(i);
                plan.route.Add(0);
            }
            return plan;
        }
    }

    [Serializable]
    public sealed class ConeStyle
    {
        public string label = "";
        public int color;
    }
}
