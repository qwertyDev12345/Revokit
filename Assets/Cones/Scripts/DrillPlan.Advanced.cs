using System.Collections.Generic;
using UnityEngine;

namespace Cones
{
    public sealed partial class DrillPlan
    {
        public int SegmentCount => Mathf.Max(0, route.Count - 1) + (closed && route.Count > 1 ? 1 : 0);
        public void ReconcileLegs()
        {
            var prior = legs ?? new List<RouteLeg>(); var used = new HashSet<int>(); var current = new List<RouteLeg>();
            for (int i = 0; i < SegmentCount; i++)
            {
                int a = route[i], b = route[(i + 1) % route.Count];
                var leg = new RouteLeg { from = a, to = b };
                for (int j = 0; j < prior.Count; j++)
                    if (!used.Contains(j) && prior[j].from == a && prior[j].to == b)
                    { leg.movement = prior[j].movement; used.Add(j); break; }
                current.Add(leg);
            }
            legs = current;
        }
        public void Reverse()
        {
            ReconcileLegs(); legs.Reverse();
            foreach (var leg in legs) { int swap = leg.from; leg.from = leg.to; leg.to = swap; }
            route.Reverse(); ReconcileLegs();
        }
        public void Mirror(bool horizontal)
        {
            for (int i = 0; i < cones.Count; i++) cones[i] = horizontal ? new Vector2(width - cones[i].x, cones[i].y) : new Vector2(cones[i].x, length - cones[i].y);
            if (obstacles != null) foreach (var o in obstacles)
                if (horizontal) o.x = Mathf.Max(0, width - o.x - o.width); else o.y = Mathf.Max(0, length - o.y - o.height);
        }
        public void StartAt(int step)
        {
            if (step < 0 || step >= route.Count) return;
            var rotated = new List<int>();
            for (int i = 0; i < route.Count; i++) rotated.Add(route[(i + step) % route.Count]);
            route = rotated; ReconcileLegs();
        }
        public bool SegmentBlocked(int segment)
        {
            if (obstacles == null || segment < 0 || segment >= SegmentCount) return false;
            foreach (var o in obstacles) if (o.Crosses(cones[route[segment]], cones[route[(segment + 1) % route.Count]])) return true;
            return false;
        }
        public int ObstacleWarnings
        {
            get
            {
                int count = 0;
                for (int i = 0; i < SegmentCount; i++) if (SegmentBlocked(i)) count++;
                if (obstacles != null) foreach (var p in cones) foreach (var o in obstacles) if (o.Contains(p)) { count++; break; }
                return count;
            }
        }
        public static string MovementName(MovementKind kind) => kind == MovementKind.SideStep ? "Side step" : kind == MovementKind.Backpedal ? "Backpedal" : kind == MovementKind.Dribble ? "Dribble" : "Run";
        public MovementKind MovementAt(int step) => legs != null && step < legs.Count ? legs[step].movement : MovementKind.Run;
    }
}
