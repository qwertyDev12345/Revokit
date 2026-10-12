using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Cones
{
    [Serializable]
    public sealed class SavedDrill
    {
        public int version = 1;
        public string id;
        public string title;
        public string updatedUtc;
        public DrillPlan plan;
    }

    public sealed class DrillLibrary
    {
        private readonly string folder;
        public DrillLibrary(string folder) => this.folder = folder;

        public List<SavedDrill> List(out int unreadable)
        {
            unreadable = 0;
            var entries = new List<SavedDrill>();
            if (!Directory.Exists(folder)) return entries;
            foreach (string path in Directory.GetFiles(folder, "*.json"))
            {
                try { entries.Add(Read(Path.GetFileNameWithoutExtension(path))); }
                catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException) { unreadable++; }
            }
            entries.Sort((a, b) => string.CompareOrdinal(b.updatedUtc, a.updatedUtc));
            return entries;
        }

        public SavedDrill Read(string id)
        {
            string path = FilePath(id);
            if (new FileInfo(path).Length > 131072) throw new InvalidDataException("Drill file is too large.");
            var entry = JsonUtility.FromJson<SavedDrill>(File.ReadAllText(path));
            if (entry == null || entry.version != 1 || entry.id != id || string.IsNullOrWhiteSpace(entry.title)
                || entry.title.Length > 48 || entry.plan == null || !entry.plan.IsValid())
                throw new InvalidDataException("Invalid drill file.");
            return entry;
        }

        public SavedDrill Save(string id, string title, DrillPlan plan)
        {
            if (plan == null || !plan.IsValid()) throw new InvalidDataException("Invalid drill data.");
            title = (title ?? "").Trim();
            if (title.Length == 0 || title.Length > 48) throw new ArgumentException("Enter a name between 1 and 48 characters.");
            if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString("N");
            string path = FilePath(id);
            Directory.CreateDirectory(folder);
            var entry = new SavedDrill { id = id, title = title, updatedUtc = DateTime.UtcNow.ToString("O"), plan = plan };
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(entry, true));
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
            return entry;
        }

        public void Delete(string id)
        {
            string path = FilePath(id);
            File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }

        private string FilePath(string id)
        {
            if (id == null || id.Length != 32 || !Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Invalid drill ID.");
            return Path.Combine(folder, id + ".json");
        }
    }
}
