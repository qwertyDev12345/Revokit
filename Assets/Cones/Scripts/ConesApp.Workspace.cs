using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Cones
{
    public sealed partial class ConesApp
    {
        private bool workspaceReady, draftPending;
        private float draftDelay;
        private string recoveryNotice = "";
        private readonly HashSet<string> blockedWorkspacePaths = new HashSet<string>();
        private SessionPlan session = new SessionPlan();
        private readonly HashSet<int> placedCones = new HashSet<int>();
        private string setupSnapshot;
        private string WorkspacePath(string name) => Path.Combine(Path.GetDirectoryName(savePath), name);

        private void InitializeWorkspace()
        {
            bool testing = Array.IndexOf(Environment.GetCommandLineArgs(), "-conesSmokeTest") >= 0;
            if (!testing)
            {
                RestoreDraft();
                LoadActivity();
                session = RecoverFile<SessionPlan>(WorkspacePath("session.json"), value => value.IsValid()) ?? new SessionPlan();
            }
            workspaceReady = true;
        }

        public bool RestoreDraft()
        {
            var draft = RecoverFile<DraftState>(WorkspacePath("draft.json"), value => value.IsValid());
            if (draft == null) return false;
            ResetPlayback();
            Plan = draft.plan; Board.Plan = Plan; Board.Selected = -1;
            drillTitle = draft.title; activeId = draft.activeId;
            savedSnapshot = draft.savedSnapshot; lastSnapshot = JsonUtility.ToJson(Plan);
            undoHistory.Clear(); redoHistory.Clear();
            recoveryNotice = "Recovered your last draft.\nSaved library drills were not changed.";
            Refresh();
            return true;
        }

        private T RecoverFile<T>(string path, Func<T, bool> valid) where T : class
        {
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return null;
            try { if (File.Exists(path)) return LocalWorkspace.Read(path, valid); }
            catch (Exception e) when (StorageError(e))
            {
                recoveryNotice = "A workspace file could not be read.\nThe original file has been retained.";
                try { File.Copy(path, path + ".unreadable-" + Guid.NewGuid().ToString("N")); }
                catch (Exception copyError) when (StorageError(copyError)) { blockedWorkspacePaths.Add(path); return null; }
            }
            try { return File.Exists(path + ".bak") ? LocalWorkspace.Read(path + ".bak", valid) : null; }
            catch (Exception e) when (StorageError(e)) { return null; }
        }

        private void MarkDraftDirty() { draftPending = true; draftDelay = .7f; }

        public void SaveDraftNow()
        {
            if (!workspaceReady || Plan == null || !Plan.IsValid()) return;
            if (blockedWorkspacePaths.Contains(WorkspacePath("draft.json"))) return;
            try
            {
                LocalWorkspace.Write(WorkspacePath("draft.json"), new DraftState
                { title = drillTitle, activeId = activeId, savedSnapshot = savedSnapshot, plan = Plan });
                draftPending = false;
            }
            catch (Exception e) when (StorageError(e))
            {
                recoveryNotice = "Draft could not be saved.\nCheck available storage.";
                if (content.gameObject.activeSelf) Say(recoveryNotice);
                draftDelay = 5;
            }
        }

        private void UpdateFeatures()
        {
            if (draftPending && (draftDelay -= Time.unscaledDeltaTime) <= 0) SaveDraftNow();
            UpdateTraining();
            UpdateAdvanced();
        }

        public void ShowTools()
        {
            BeginPage("Drill tools", ShowEditor);
            string[][] labels = {
                new[] { "Setup mode", "Cone details" }, new[] { "Route settings", "Variations" },
                new[] { "Movement types", "Obstacles" }, new[] { "Training mode", "Reaction mode" },
                new[] { "Coach mode", "History" }, new[] { "Export image", "Share drill" }, new[] { "Add to session", "Session plan" } };
            Action[][] actions = {
                new Action[] { ShowSetup, () => ShowConeDetails(Board.Selected >= 0 ? Board.Selected : 0) },
                new Action[] { ShowRouteSettings, ShowVariations }, new Action[] { () => ShowMovements(), () => ShowObstacles() },
                new Action[] { ShowTrainingSettings, ShowReactionSettings }, new Action[] { () => ShowCoach(), () => ShowHistory() },
                new Action[] { ShowExport, ShowSharing }, new Action[] { () => AddToSession(drillTitle, Plan), () => ShowSession() } };
            for (int i = 0; i < labels.Length; i++) PageRow(130 + i * 112, labels[i], actions[i]);
            PageText("Drafts save automatically. Save keeps a named drill in My drills.", 946, 90, 22);
        }

        private InputField PageInput(string title, string value, float top, int limit = 48)
        {
            PageText(title, top, 34, 23);
            var box = Panel(title + " input", page, buttonColor);
            Top(box, top + 42, 86);
            box.GetComponent<Image>().raycastTarget = true;
            var label = Label("", box, 25, TextAnchor.MiddleLeft);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(14, 8); label.rectTransform.offsetMax = new Vector2(-14, -8);
            var input = box.gameObject.AddComponent<InputField>();
            input.textComponent = label; input.targetGraphic = box.GetComponent<Image>();
            input.characterLimit = limit; input.lineType = InputField.LineType.SingleLine; input.text = value;
            return input;
        }

        private FieldBoard PageBoard(DrillPlan plan, float top, float bottom, Transform parent = null)
        {
            var go = new GameObject("Drill diagram", typeof(RectTransform), typeof(CanvasRenderer), typeof(FieldBoard));
            go.transform.SetParent(parent == null ? page : parent, false);
            var board = go.GetComponent<FieldBoard>();
            board.Plan = plan; board.ReadOnly = true;
            board.rectTransform.anchorMin = Vector2.zero; board.rectTransform.anchorMax = Vector2.one;
            board.rectTransform.offsetMin = new Vector2(0, bottom); board.rectTransform.offsetMax = new Vector2(0, -top);
            board.Refresh();
            return board;
        }

        public void ShowSetup()
        {
            BeginPage("Setup mode", ShowTools);
            string fingerprint = JsonUtility.ToJson(Plan);
            if (setupSnapshot != fingerprint) { placedCones.Clear(); setupSnapshot = fingerprint; }
            var count = PageText("", 108, 44, 24);
            var board = PageBoard(Plan, 162, 278);
            board.Placed = placedCones;
            board.Selected = Plan.cones.Count > 0 ? 0 : -1;
            var info = Label("", page, 23, TextAnchor.MiddleLeft);
            Bottom(info.rectTransform, 122, 150);
            var actions = PageRow(0, new[] { "Mark placed", "Next cone" }, new Action[] { () => { }, () => { } });
            var actionsRoot = (RectTransform)actions[0].transform.parent;
            Bottom(actionsRoot, 20, 86);
            Action refresh = () =>
            {
                count.text = $"{placedCones.Count}/{Plan.cones.Count} placed · {Plan.width:0.##} × {Plan.length:0.##} m";
                int selected = board.Selected;
                if (selected < 0)
                {
                    info.text = "No cone selected. Tap a cone on the diagram.";
                    actions[0].interactable = false;
                }
                else
                {
                    Vector2 p = Plan.cones[selected];
                    info.text = $"Cone {selected + 1} {Plan.StyleAt(selected).label}\nLeft: {p.x:0.##} m · Right: {Plan.width - p.x:0.##} m\nBottom: {p.y:0.##} m · Top: {Plan.length - p.y:0.##} m";
                    actions[0].interactable = true;
                    actions[0].GetComponentInChildren<Text>().text = placedCones.Contains(selected) ? "Mark unplaced" : "Mark placed";
                }
                actions[1].interactable = Plan.cones.Count > 1;
                board.Refresh();
            };
            actions[0].onClick.RemoveAllListeners();
            actions[0].onClick.AddListener(() =>
            {
                if (board.Selected < 0) return;
                if (!placedCones.Add(board.Selected)) placedCones.Remove(board.Selected);
                refresh();
            });
            actions[1].onClick.RemoveAllListeners();
            actions[1].onClick.AddListener(() => { board.Selected = (board.Selected + 1) % Plan.cones.Count; refresh(); });
            board.ConeTapped = _ => refresh();
            refresh();
        }

        public void ShowConeDetails(int index)
        {
            if (Plan.cones.Count == 0) { ShowMessage("No cones", "Add a cone in the editor first.", ShowTools); return; }
            index = Mathf.Clamp(index, 0, Plan.cones.Count - 1);
            BeginPage("Cone details", ShowTools);
            var pager = PageRow(110, new[] { "Previous cone", "Next cone" }, new Action[]
            { () => ShowConeDetails(index - 1), () => ShowConeDetails(index + 1) });
            pager[0].interactable = index > 0; pager[1].interactable = index + 1 < Plan.cones.Count;
            var x = PageInput($"Cone {index + 1}: X from left (m)", Plan.cones[index].x.ToString("0.##", CultureInfo.InvariantCulture), 220, 8);
            var y = PageInput("Y from bottom (m)", Plan.cones[index].y.ToString("0.##", CultureInfo.InvariantCulture), 374, 8);
            var tag = PageInput("Label (up to 16 characters)", Plan.StyleAt(index).label, 528, 16);
            int selectedColor = Plan.StyleAt(index).color;
            string[] colors = { "Orange", "Blue", "Green", "Red" };
            var colorLabel = PageText("Color: " + colors[selectedColor], 684, 36, 22);
            var picks = PageRow(734, colors, new Action[]
            {
                () => { selectedColor = 0; colorLabel.text = "Color: Orange"; },
                () => { selectedColor = 1; colorLabel.text = "Color: Blue"; },
                () => { selectedColor = 2; colorLabel.text = "Color: Green"; },
                () => { selectedColor = 3; colorLabel.text = "Color: Red"; }
            });
            var error = PageText("", 836, 60, 21);
            PageButton("Apply changes", 918, () =>
            {
                if (!TryNumber(x.text, out float px) || !TryNumber(y.text, out float py) || px < 0 || px > Plan.width || py < 0 || py > Plan.length)
                { error.text = $"Enter coordinates within {Plan.width:0.##} × {Plan.length:0.##} m."; return; }
                ApplyConeDetails(index, new Vector2(px, py), tag.text.Trim(), selectedColor);
                ShowEditor();
            });
        }

        public bool ApplyConeDetails(int index, Vector2 position, string label, int color)
        {
            if (index < 0 || index >= Plan.cones.Count || float.IsNaN(position.x) || float.IsNaN(position.y)
                || position.x < 0 || position.x > Plan.width || position.y < 0 || position.y > Plan.length
                || label == null || label.Length > 16 || color < 0 || color > 3) return false;
            ResetPlayback(); Plan.EnsureMetadata();
            Plan.cones[index] = position; Plan.styles[index] = new ConeStyle { label = label, color = color };
            Board.Selected = index; Refresh();
            return true;
        }

        private static bool TryNumber(string value, out float number)
        {
            return float.TryParse(value.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
                && !float.IsNaN(number) && !float.IsInfinity(number);
        }

        public void ShowTemplateOptions(int template)
        {
            BeginPage("Customize drill", ShowTemplates);
            PageText(TemplateNames[template], 108, 60, 28);
            var count = PageInput("Available cones", template == 0 ? "6" : "4", 208, 2);
            var width = PageInput("Field width (m)", "5", 376, 5);
            var length = PageInput("Field length (m)", "8", 544, 5);
            int minimum = template >= 2 ? 4 : 2;
            var error = PageText($"Use {minimum}–24 cones. Field dimensions: 2–40 m.", 708, 94, 22);
            PageButton("Create layout", 846, () =>
            {
                if (!int.TryParse(count.text, out int n) || n < minimum || n > 24 || !TryNumber(width.text, out float w)
                    || !TryNumber(length.text, out float h) || w < 2 || w > 40 || h < 2 || h > 40)
                { error.text = $"Enter {minimum}–24 cones and dimensions between 2 and 40 m."; return; }
                var adapted = DrillPlan.AdaptTemplate(template, n, w, h);
                ConfirmReplace(() => UsePlan(adapted, TemplateNames[template], null), () => ShowTemplateOptions(template));
            });
        }
    }
}
