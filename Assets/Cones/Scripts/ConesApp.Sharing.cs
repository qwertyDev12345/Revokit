using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Cones
{
    public sealed partial class ConesApp
    {
        private SharedDrill pendingImport;
        public string LastSharedPath { get; private set; }
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void ConesShareFile(string path);
        [DllImport("__Internal")] private static extern void ConesPickDrill();
#endif
        public void ShowSharing()
        {
            BeginPage("Share drill", ShowTools);
            PageText("Send a .cones file to keep the drill editable. It includes cones, movements, obstacles and training settings.", 118, 150, 26);
            PageButton("Export editable file", 302, ExportSharedDrill);
#if UNITY_IOS && !UNITY_EDITOR
            PageButton("Import from Files", 430, () => ConesPickDrill());
            PageText("Open Revokit on the receiving device, then choose Import from Files. You can review the drill before replacing your draft.", 572, 160, 24);
#else
            var path = PageInput("File path (.cones or JSON)", "", 442, 512);
            PageButton("Import file", 604, () => ImportSharedFile(path.text));
            PageText("On iPhone, Import from Files opens the system file picker. Desktop preview accepts a local file path.", 742, 144, 23);
#endif
        }
        public void ExportSharedDrill()
        {
            try
            {
                Plan.ReconcileLegs();
                var data = new SharedDrill { title = drillTitle, plan = LocalWorkspace.Clone(Plan) };
                if (!data.IsValid()) throw new ArgumentException("The current drill is invalid.");
                string folder = WorkspacePath("Exports"); Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "drill-" + Guid.NewGuid().ToString("N") + ".cones");
                File.WriteAllText(path, JsonUtility.ToJson(data, true)); LastSharedPath = path;
                BeginPage("Drill exported", ShowSharing);
                PageText("Your editable drill file is ready.", 138, 100, 27);
                PageText(Path.GetFileName(path), 282, 100, 22);
#if UNITY_IOS && !UNITY_EDITOR
                PageButton("Share file", 448, () => ConesShareFile(path));
#else
                PageButton("Open export folder", 448, () => Application.OpenURL(new Uri(folder).AbsoluteUri));
#endif
                PageButton("Back to editor", 566, ShowEditor);
            }
            catch (Exception e) when (StorageError(e)) { ShowMessage("Export failed", "Could not save the drill file. Check available storage.", ShowSharing); }
        }
        public bool ImportSharedFile(string path)
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 262144) throw new ArgumentException("Invalid file.");
                return ImportSharedText(File.ReadAllText(path));
            }
            catch (Exception e) when (StorageError(e)) { ShowMessage("Import failed", "Choose a valid .cones file up to 256 KB. Your current drill was kept.", ShowSharing); return false; }
        }
        [UnityEngine.Scripting.Preserve]
        public void ReceiveDrillFile(string json) => ImportSharedText(json);
        [UnityEngine.Scripting.Preserve]
        public void ReceiveDrillError(string ignored) => ShowMessage("Import unavailable", "The selected file could not be read. Choose a .cones file up to 256 KB.", ShowSharing);
        public bool ImportSharedText(string text)
        {
            try
            {
                var candidate = SharedDrill.Parse(text); candidate.plan.ReconcileLegs(); pendingImport = candidate;
                ShowImportPreview(); return true;
            }
            catch (Exception e) when (StorageError(e)) { ShowMessage("Invalid drill file", "This file is damaged, unsupported, or contains invalid values. Your current drill was kept.", ShowSharing); return false; }
        }
        public void ShowImportPreview()
        {
            if (pendingImport == null) { ShowSharing(); return; }
            BeginPage("Import preview", ShowSharing);
            PageText(pendingImport.title, 112, 90, 28);
            PageText($"{pendingImport.plan.cones.Count} cones · {pendingImport.plan.width:0.##} × {pendingImport.plan.length:0.##} m\n{pendingImport.plan.ObstacleWarnings} obstacle conflict(s)", 210, 80, 23);
            PageBoard(pendingImport.plan, 316, 272);
            var open = PageButton("Open imported drill", 0, () => ConfirmReplace(() =>
                UsePlan(LocalWorkspace.Clone(pendingImport.plan), pendingImport.title, null), ShowImportPreview));
            Bottom((RectTransform)open.transform, 148, 90); Primary(open);
            var save = PageButton("Save a new library copy", 0, () => StorageAction(() =>
            {
                Library.Save(null, pendingImport.title, pendingImport.plan);
                ShowMessage("Drill saved", "A new copy was added to My drills. Your editing draft was kept.", () => ShowLibrary());
            }));
            Bottom((RectTransform)save.transform, 36, 90);
        }
    }
}
