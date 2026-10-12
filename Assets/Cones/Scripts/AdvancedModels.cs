using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cones
{
    public enum MovementKind { Run, SideStep, Backpedal, Dribble }
    [Serializable] public sealed class RouteLeg
    {
        public int from, to;
        public MovementKind movement;
    }
    public enum ObstacleKind { Wall, Goal, NoGo }
    [Serializable] public sealed class FieldObstacle
    {
        public string name = "Obstacle";
        public ObstacleKind kind;
        public float x, y, width = 1, height = 1;
        public bool IsValid(float fieldWidth, float fieldHeight) => !string.IsNullOrWhiteSpace(name) && name.Length <= 24
            && (int)kind >= 0 && (int)kind <= 2 && Finite(x) && Finite(y) && Finite(width) && Finite(height)
            && x >= 0 && y >= 0 && width >= .1f && height >= .1f && x + width <= fieldWidth + .001f && y + height <= fieldHeight + .001f;
        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        public bool Contains(Vector2 p) => p.x >= x && p.x <= x + width && p.y >= y && p.y <= y + height;
        public bool Crosses(Vector2 a, Vector2 b)
        {
            float low = 0, high = 1;
            return Clip(a.x, b.x - a.x, x, x + width, ref low, ref high)
                && Clip(a.y, b.y - a.y, y, y + height, ref low, ref high);
        }
        private static bool Clip(float origin, float delta, float min, float max, ref float low, ref float high)
        {
            if (Mathf.Abs(delta) < .00001f) return origin >= min && origin <= max;
            float a = (min - origin) / delta, b = (max - origin) / delta;
            if (a > b) { float swap = a; a = b; b = swap; }
            low = Mathf.Max(low, a); high = Mathf.Min(high, b); return low <= high;
        }
    }

    [Serializable] public sealed class ReactionSettings
    {
        public int interval = 4, commands = 10, rounds = 2;
        public bool colors, allowRepeat, voice = true;
        public bool IsValid() => interval >= 2 && interval <= 30 && commands >= 1 && commands <= 100 && rounds >= 1 && rounds <= 20;
    }
    public sealed class ReactionRun
    {
        private readonly System.Random random;
        private readonly int choices;
        public readonly ReactionSettings Settings;
        public int Target { get; private set; } = -1;
        public int Issued { get; private set; }
        public bool Running { get; private set; }
        public bool Complete { get; private set; }
        public float Remaining { get; private set; }
        public int Round => Mathf.Min(Settings.rounds, (Mathf.Max(1, Issued) - 1) / Settings.commands + 1);
        public ReactionRun(ReactionSettings settings, int choices, int seed)
        {
            if (settings == null || !settings.IsValid() || choices < 1) throw new ArgumentException("Invalid reaction setup.");
            Settings = LocalWorkspace.Clone(settings); this.choices = choices; random = new System.Random(seed);
            Remaining = settings.interval;
        }
        public void Toggle()
        {
            if (Complete) return;
            if (Issued == 0) Next();
            Running = !Running;
        }
        public void Pause() => Running = false;
        public void Tick(float elapsed)
        {
            if (!Running || elapsed < 0 || float.IsNaN(elapsed) || float.IsInfinity(elapsed)) return;
            while (Running && elapsed >= Remaining)
            {
                elapsed -= Remaining;
                if (Issued >= Settings.commands * Settings.rounds) { Complete = true; Running = false; Remaining = 0; }
                else Next();
            }
            if (Running) Remaining -= elapsed;
        }
        private void Next()
        {
            int next = random.Next(choices);
            if (!Settings.allowRepeat && choices > 1 && next == Target) next = (next + 1 + random.Next(choices - 1)) % choices;
            Target = next; Issued++; Remaining = Settings.interval;
        }
    }

    [Serializable] public sealed class TrainingRecord
    {
        public string id = Guid.NewGuid().ToString("N"), title, mode, participant = "", utc = DateTime.UtcNow.ToString("o"), note = "";
        public float seconds;
        public int sets, difficulty;
        public bool IsValid() => Guid.TryParseExact(id, "N", out _) && !string.IsNullOrWhiteSpace(title) && title.Length <= 100
            && (mode == "Workout" || mode == "Reaction" || mode == "Coach") && participant != null && participant.Length <= 40
            && DateTime.TryParse(utc, out _) && note != null && note.Length <= 280 && seconds >= 0 && seconds <= 1000000
            && !float.IsNaN(seconds) && !float.IsInfinity(seconds) && sets >= 0 && sets <= 2000 && difficulty >= 0 && difficulty <= 5;
    }
    [Serializable] public sealed class ActivityLog
    {
        public int version = 1;
        public List<TrainingRecord> records = new List<TrainingRecord>();
        public bool IsValid()
        {
            if (version != 1 || records == null || records.Count > 200) return false;
            var ids = new HashSet<string>();
            foreach (var r in records) if (r == null || !r.IsValid() || !ids.Add(r.id)) return false;
            return true;
        }
    }
    [Serializable] public sealed class CoachRoster
    {
        public int version = 1;
        public List<string> names = new List<string>();
        public bool IsValid()
        {
            if (version != 1 || names == null || names.Count > 20) return false;
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in names) if (string.IsNullOrWhiteSpace(name) || name.Length > 40 || !unique.Add(name)) return false;
            return true;
        }
    }
    [Serializable] public sealed class SharedDrill
    {
        public string format = "cones-drill";
        public int version = 1;
        public string title;
        public DrillPlan plan;
        public bool IsValid() => format == "cones-drill" && version == 1 && !string.IsNullOrWhiteSpace(title) && title.Length <= 48 && plan != null && plan.IsValid();
        public static SharedDrill Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 262144) throw new ArgumentException("Invalid drill file size.");
            var value = JsonUtility.FromJson<SharedDrill>(text);
            if (value == null || !value.IsValid()) throw new ArgumentException("Invalid drill file.");
            return value;
        }
    }
}
