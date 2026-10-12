using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Cones
{
    [Serializable]
    public sealed class DraftState
    {
        public int version = 1;
        public string title, activeId, savedSnapshot;
        public DrillPlan plan;
        public bool IsValid() => version == 1 && !string.IsNullOrWhiteSpace(title) && title.Length <= 48
            && plan != null && plan.IsValid() && (string.IsNullOrEmpty(activeId) || Guid.TryParseExact(activeId, "N", out _));
    }

    [Serializable]
    public sealed class SessionPlan
    {
        public int version = 1;
        public string title = "My session";
        public List<SessionDrill> drills = new List<SessionDrill>();
        public bool IsValid()
        {
            if (version != 1 || string.IsNullOrWhiteSpace(title) || title.Length > 48 || drills == null || drills.Count > 20) return false;
            foreach (var drill in drills)
                if (drill == null || string.IsNullOrWhiteSpace(drill.title) || drill.title.Length > 48 || drill.plan == null || !drill.plan.IsValid()) return false;
            return true;
        }
    }

    [Serializable]
    public sealed class SessionDrill
    {
        public string title;
        public DrillPlan plan;
    }

    public static class LocalWorkspace
    {
        public static void Write<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(value, true));
            if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak");
            else File.Move(path + ".tmp", path);
        }

        public static T Read<T>(string path, Func<T, bool> valid) where T : class
        {
            if (new FileInfo(path).Length > 2097152) throw new InvalidDataException("Workspace file is too large.");
            var value = JsonUtility.FromJson<T>(File.ReadAllText(path));
            if (value == null || !valid(value)) throw new InvalidDataException("Invalid workspace file.");
            return value;
        }

        public static T Clone<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
    }
}
