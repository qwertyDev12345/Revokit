using System;
using Cones;
using UnityEngine;

public static class ConesAdvancedChecks
{
    public static void Run()
    {
        var p = DrillPlan.Example(); p.ReconcileLegs();
        p.legs[0].movement = MovementKind.Backpedal;
        p.Reverse();
        Check(p.legs[p.legs.Count - 1].movement == MovementKind.Backpedal, "Reverse preserves movement");
        p.Reverse(); Check(p.legs[0].movement == MovementKind.Backpedal, "Double reverse");
        var first = p.cones[0]; p.Mirror(true); p.Mirror(true);
        Check(Vector2.Distance(first, p.cones[0]) < .001f, "Double mirror");
        var zone = new FieldObstacle { name = "Wall", x = 1, y = 1, width = 1, height = 2 };
        Check(zone.Crosses(new Vector2(0, 2), new Vector2(4, 2)), "Crossing zone");
        Check(!zone.Crosses(Vector2.zero, new Vector2(4, 0)), "Clear segment");
        Check(zone.Crosses(new Vector2(1, 1), new Vector2(1, 1)), "Zero-length inside zone");
        Check(zone.Crosses(new Vector2(0, 1), new Vector2(4, 1)), "Touching zone boundary");
        p.obstacles.Add(zone); Check(p.ObstacleWarnings > 0, "Conflict warning");
        p.Resize(10, 16); Check(p.IsValid() && zone.width == 2 && zone.height == 4, "Resize obstacles");
        p.StartAt(2); Check(p.route[0] == 2 && p.IsValid(), "Rotate start");
        p.RemoveCone(1); p.ReconcileLegs(); Check(p.IsValid(), "Remove cone with movements");
        var shared = SharedDrill.Parse(JsonUtility.ToJson(new SharedDrill { title = "Transfer", plan = p }));
        Check(shared.plan.obstacles.Count == 1 && shared.plan.legs.Count == p.legs.Count, "Editable transfer round trip");
        bool rejected = false;
        try { SharedDrill.Parse("{\"version\":99}"); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Reject unsupported transfer");
        var run = new ReactionRun(new ReactionSettings { interval = 2, commands = 4, rounds = 2 }, 3, 42);
        run.Toggle(); int last = run.Target;
        run.Pause(); run.Tick(100); Check(run.Issued == 1 && run.Remaining == 2, "Reaction pause");
        run.Toggle();
        for (int i = 1; i < 8; i++) { run.Tick(2); Check(run.Target != last, "Avoid immediate repeats"); last = run.Target; }
        Check(run.Round == 2 && !run.Complete, "Final command remains active");
        run.Tick(2); Check(run.Complete && !run.Running && run.Issued == 8, "Reaction completion");
        var single = new ReactionRun(new ReactionSettings { interval = 2, commands = 2, rounds = 1 }, 1, 1);
        single.Toggle(); single.Tick(100); Check(single.Complete, "Single target catch-up");
        var legacy = JsonUtility.FromJson<DrillPlan>("{\"version\":1,\"width\":5,\"length\":8,\"cones\":[],\"route\":[]}");
        legacy.ReconcileLegs(); Check(legacy.IsValid(), "Legacy drill migration");
    }
    private static void Check(bool valid, string message) { if (!valid) throw new Exception("Advanced check: " + message); }
}
