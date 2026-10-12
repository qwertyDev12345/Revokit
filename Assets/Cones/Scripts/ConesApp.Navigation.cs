using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Cones
{
    public sealed partial class ConesApp
    {
        private RectTransform page;
        private Button undoEditButton, redoEditButton;
        private readonly List<string> undoHistory = new List<string>();
        private readonly List<string> redoHistory = new List<string>();
        private string lastSnapshot, savedSnapshot, activeId;
        private string drillTitle = "Zigzag";
        private static readonly string[] TemplateNames = { "Zigzag", "Shuttle runs", "Square loop", "T route" };
        private static readonly string[] TemplateNotes =
        {
            "6 cones · 5 × 8 m · Alternating turns",
            "4 cones · 5 × 8 m · Return to the start",
            "4 cones · 6 × 6 m · Closed route",
            "4 cones · 8 × 8 m · Center and side points"
        };
        private DrillLibrary Library => new DrillLibrary(Path.Combine(Path.GetDirectoryName(savePath), "drills"));
        public bool HasUnsavedChanges => JsonUtility.ToJson(Plan) != savedSnapshot;

        public void ShowHome() => BuildHomeDesign();

        public void ShowEditor()
        {
            ClosePage();
            content.gameObject.SetActive(true);
            Refresh();
            Say(Board.RouteMode ? "Tap cones in movement order." : "Grid: 1 m · Drag cones to move them.");
        }

        public void ShowTemplates()
        {
            BeginPage("Ready-made drills", ShowHome);
            PageText("Choose a starting layout. Every cone and route can be edited.", 110, 76, 23);
            for (int i = 0; i < TemplateNames.Length; i++)
            {
                int template = i;
                var card = PageButton(TemplateNames[i], 212 + i * 170, () => ShowTemplateOptions(template), 148);
                card.GetComponentInChildren<Text>().text = "";
                var preview = PageBoard(DrillPlan.Template(i), 14, 14, card.transform);
                preview.Compact = true; preview.raycastTarget = false;
                preview.rectTransform.anchorMax = new Vector2(.26f, 1); preview.Refresh();
                var title = CardText(TemplateNames[i], card.transform, 16, 48, 26, true);
                title.rectTransform.anchorMin = new Vector2(.27f, 1);
                var notes = CardText(TemplateNotes[i], card.transform, 68, 64, 21);
                notes.color = ConesVisuals.Muted; notes.rectTransform.anchorMin = new Vector2(.27f, 1);
            }
        }

        public void UsePlan(DrillPlan plan, string title, string id)
        {
            ResetPlayback();
            Plan = plan; Plan.ReconcileLegs();
            drillTitle = title;
            activeId = id;
            Board.Plan = Plan; Board.Selected = -1; Board.RouteMode = false;
            undoHistory.Clear(); redoHistory.Clear();
            lastSnapshot = savedSnapshot = JsonUtility.ToJson(Plan);
            MarkDraftDirty();
            ShowEditor();
        }

        public void ShowLibrary(int pageNumber = 0)
        {
            List<SavedDrill> entries;
            int unreadable;
            try { entries = Library.List(out unreadable); }
            catch (Exception e) when (StorageError(e))
            { ShowMessage("Library unavailable", "The saved drills could not be read. Please try again.", ShowHome); return; }
            int pages = Mathf.Max(1, (entries.Count + 2) / 3);
            pageNumber = Mathf.Clamp(pageNumber, 0, pages - 1);
            BeginPage("My drills", ShowHome);
            PageText($"{entries.Count} saved drill(s) · Page {pageNumber + 1}/{pages}" +
                (unreadable > 0 ? $"\n{unreadable} unreadable file(s) skipped." : ""), 104, 70, 22);
            if (entries.Count == 0)
                PageText("Your library is empty.\nCreate a drill, then tap Save in the editor.", 220, 140, 25);
            for (int i = 0; i < 3 && pageNumber * 3 + i < entries.Count; i++)
            {
                var entry = entries[pageNumber * 3 + i];
                int currentPage = pageNumber;
                float y = 200 + i * 214;
                PageText(entry.title, y, 62, 25);
                PageText($"{entry.plan.cones.Count} cones · {entry.plan.Distance:0.0} m · {entry.plan.rounds} round(s)", y + 64, 30, 20);
                PageRow(y + 100, new[] { "Open", "Duplicate", "Delete" }, new Action[]
                {
                    () => ConfirmReplace(() => OpenSaved(entry.id), () => ShowLibrary(currentPage)),
                    () => StorageAction(() => { Library.Save(null, CopyTitle(entry.title), entry.plan); ShowLibrary(); }),
                    () => ShowConfirm("Delete drill?", $"Delete \"{entry.title}\" from this device? This cannot be undone.", () => StorageAction(() =>
                    {
                        Library.Delete(entry.id);
                        if (activeId == entry.id) { activeId = null; savedSnapshot = null; }
                        ShowLibrary(currentPage);
                    }), () => ShowLibrary(currentPage))
                });
            }
            int selectedPage = pageNumber;
            var paging = PageRow(862, new[] { "Previous", "Next" }, new Action[]
            { () => ShowLibrary(selectedPage - 1), () => ShowLibrary(selectedPage + 1) });
            paging[0].interactable = pageNumber > 0; paging[1].interactable = pageNumber + 1 < pages;
            if (File.Exists(savePath)) PageButton("Import previous single-slot save", 970,
                () => ConfirmReplace(LoadPlan, () => ShowLibrary(selectedPage)), 76);
        }

        public void OpenSaved(string id)
        {
            StorageAction(() =>
            {
                var entry = Library.Read(id);
                UsePlan(entry.plan, entry.title, entry.id);
            });
        }

        public void ShowSaveDialog()
        {
            BeginPage("Save drill", ShowEditor);
            PageText("Drill name", 124, 40, 24);
            var box = Panel("Drill name input", page, buttonColor);
            Top(box, 182, 90);
            box.GetComponent<Image>().raycastTarget = true;
            var text = Label("", box, 25, TextAnchor.MiddleLeft);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(16, 8); text.rectTransform.offsetMax = new Vector2(-16, -8);
            var field = box.gameObject.AddComponent<InputField>();
            field.textComponent = text; field.targetGraphic = box.GetComponent<Image>();
            field.characterLimit = 48; field.lineType = InputField.LineType.SingleLine;
            field.text = drillTitle;
            PageText(activeId == null ? "Save this drill to your local library." : "Save updates the opened drill. Save a copy keeps both versions.", 306, 94, 23);
            var error = PageText("", 418, 60, 22);
            Action<bool> save = copy =>
            {
                string title = field.text.Trim();
                if (title.Length == 0) { error.text = "Please enter a drill name."; return; }
                string priorId = activeId, priorTitle = drillTitle;
                try
                {
                    var entry = Library.Save(copy ? null : activeId, title, Plan);
                    activeId = entry.id; drillTitle = title; savedSnapshot = JsonUtility.ToJson(Plan);
                    MarkDraftDirty();
                    ShowEditor(); Say("Saved to My drills.");
                }
                catch (Exception e) when (StorageError(e))
                { activeId = priorId; drillTitle = priorTitle; error.text = "Could not save. Check available storage."; }
            };
            PageButton("Save", 506, () => save(false));
            PageButton("Save a copy", 616, () => save(true));
        }

        public void ShowRouteSettings()
        {
            BeginPage("Route settings", ShowEditor);
            PageText($"Preview speed: {Plan.speed:0.0} m/s", 116, 42, 25);
            PageRow(176, new[] { "Slower −0.5", "Faster +0.5" }, new Action[]
            { () => ChangeSettings(-0.5f, 0), () => ChangeSettings(0.5f, 0) });
            PageText($"Rounds: {Plan.rounds} · Total: {Plan.Distance * Plan.rounds:0.0} m", 292, 42, 25);
            PageRow(352, new[] { "One fewer", "One more" }, new Action[]
            { () => ChangeSettings(0, -1), () => ChangeSettings(0, 1) });
            PageButton(Plan.closed ? "Closed loop: ON" : "Closed loop: OFF", 462, () =>
            { ResetPlayback(); Plan.closed = !Plan.closed; Refresh(); ShowRouteSettings(); });
            PageButton("Reverse route direction", 572, () => { ReverseRoute(); ShowRouteSettings(); });
            PageButton("Edit route steps", 682, () => ShowRouteSteps());
            PageButton("Clear route", 792, () => ShowConfirm("Clear route?", "Remove all route steps? Cone positions are kept. You can undo this in the editor.",
                () => { ClearRoute(); ShowEditor(); }, ShowRouteSettings));
            PageText($"Estimated preview: {Plan.Distance * Plan.rounds / Plan.speed:0.0} s\n" +
                (Plan.closed ? "A closed loop connects the last cone to the first." : "Each round restarts at the first cone."), 906, 92, 21);
        }

        public void ChangeSettings(float speedDelta, int roundsDelta)
        {
            ResetPlayback();
            Plan.speed = Mathf.Clamp(Plan.speed + speedDelta, 0.5f, 6);
            Plan.rounds = Mathf.Clamp(Plan.rounds + roundsDelta, 1, 20);
            Refresh(); ShowRouteSettings();
        }

        public void ReverseRoute() { ResetPlayback(); Plan.Reverse(); Refresh(); }

        public void ShowRouteSteps(int pageNumber = 0)
        {
            int pages = Mathf.Max(1, (Plan.route.Count + 3) / 4);
            pageNumber = Mathf.Clamp(pageNumber, 0, pages - 1);
            BeginPage("Route steps", ShowRouteSettings);
            PageText($"{Plan.route.Count}/100 steps · Page {pageNumber + 1}/{pages}\nUse route mode in the editor to add steps.", 110, 76, 22);
            if (Plan.route.Count == 0) PageText("No steps yet. Tap cones in route mode to begin.", 230, 100, 24);
            for (int i = 0; i < 4 && pageNumber * 4 + i < Plan.route.Count; i++)
            {
                int step = pageNumber * 4 + i, currentPage = pageNumber;
                float y = 218 + i * 166;
                PageText($"Step {step + 1} → Cone {Plan.route[step] + 1}", y, 42, 25);
                var buttons = PageRow(y + 52, new[] { "Move up", "Move down", "Remove" }, new Action[]
                {
                    () => { MoveRouteStep(step, -1); ShowRouteSteps(currentPage); },
                    () => { MoveRouteStep(step, 1); ShowRouteSteps(currentPage); },
                    () => { ResetPlayback(); Plan.route.RemoveAt(step); Refresh(); ShowRouteSteps(currentPage); }
                });
                buttons[0].interactable = step > 0; buttons[1].interactable = step < Plan.route.Count - 1;
            }
            int selectedPage = pageNumber;
            var paging = PageRow(918, new[] { "Previous", "Next" }, new Action[]
            { () => ShowRouteSteps(selectedPage - 1), () => ShowRouteSteps(selectedPage + 1) });
            paging[0].interactable = pageNumber > 0; paging[1].interactable = pageNumber + 1 < pages;
        }

        public void MoveRouteStep(int index, int delta)
        {
            int other = index + delta;
            if (index < 0 || index >= Plan.route.Count || other < 0 || other >= Plan.route.Count) return;
            int cone = Plan.route[index]; Plan.route[index] = Plan.route[other]; Plan.route[other] = cone;
            ResetPlayback(); Refresh();
        }

        private void TrackEdit()
        {
            if (Board == null || Board.IsDragging) return;
            string snapshot = JsonUtility.ToJson(Plan);
            if (lastSnapshot != null && lastSnapshot != snapshot)
            {
                undoHistory.Add(lastSnapshot);
                if (undoHistory.Count > 50) undoHistory.RemoveAt(0);
                redoHistory.Clear();
                MarkDraftDirty();
            }
            lastSnapshot = snapshot;
            if (undoEditButton != null) undoEditButton.interactable = undoHistory.Count > 0;
            if (redoEditButton != null) redoEditButton.interactable = redoHistory.Count > 0;
        }

        public void UndoEdit() => RestoreHistory(undoHistory, redoHistory);
        public void RedoEdit() => RestoreHistory(redoHistory, undoHistory);

        private void RestoreHistory(List<string> source, List<string> destination)
        {
            if (source.Count == 0) return;
            ResetPlayback();
            destination.Add(JsonUtility.ToJson(Plan));
            lastSnapshot = source[source.Count - 1]; source.RemoveAt(source.Count - 1);
            Plan = JsonUtility.FromJson<DrillPlan>(lastSnapshot);
            Board.Plan = Plan; Board.Selected = -1;
            MarkDraftDirty();
            Refresh();
            Say("Edit restored.");
        }

        private void ConfirmReplace(Action next, Action cancel)
        {
            if (!HasUnsavedChanges) { next(); return; }
            ShowConfirm("Unsaved changes", "Replace the current drill and discard its unsaved changes? Choose Cancel to keep editing or save it first.", next, cancel);
        }

        private void ShowConfirm(string title, string message, Action confirm, Action cancel)
        {
            BeginPage(title, cancel);
            PageText(message, 150, 200, 26);
            PageButton("Confirm", 402, confirm);
            PageButton("Cancel", 516, cancel);
        }

        private void ShowMessage(string title, string message, Action back)
        {
            BeginPage(title, back); PageText(message, 150, 180, 26); PageButton("Back", 402, back);
        }

        private void StorageAction(Action action)
        {
            try { action(); }
            catch (Exception e) when (StorageError(e))
            { ShowMessage("Storage error", "This operation could not be completed. Your current drill was kept.", () => ShowLibrary()); }
        }

        private static bool StorageError(Exception e) => e is IOException || e is UnauthorizedAccessException || e is ArgumentException;
        private static string CopyTitle(string value) => (value.Length > 41 ? value.Substring(0, 41) : value) + " (copy)";

        private void BeginPage(string title, Action back)
        {
            if (Board.Playing) { paused = true; Refresh(); }
            ClosePage();
            content.gameObject.SetActive(false);
            page = new GameObject("Page - " + title, typeof(RectTransform)).GetComponent<RectTransform>();
            page.SetParent(SafePanel.transform, false);
            page.anchorMin = new Vector2(0.5f, 0); page.anchorMax = new Vector2(0.5f, 1);
            page.sizeDelta = content.sizeDelta; page.anchoredPosition = Vector2.zero;
            if (back != null)
            {
                var button = MakeButton("Back", page, back);
                Top((RectTransform)button.transform, 0, 86);
                ((RectTransform)button.transform).anchorMax = new Vector2(0.23f, 1);
            }
            var heading = PageText(title, 0, 86, 30);
            heading.fontStyle = FontStyle.Bold;
            if (back != null) heading.rectTransform.anchorMin = new Vector2(0.27f, 1);
        }

        private void ClosePage()
        {
            PauseTraining();
            PauseAdvanced(true);
            if (page == null) return;
            page.gameObject.SetActive(false);
            Destroy(page.gameObject);
            page = null;
        }

        private Text PageText(string value, float top, float height, int size)
        {
            var label = Label(value, page, size, TextAnchor.MiddleLeft);
            Top(label.rectTransform, top, height);
            return label;
        }

        private Button PageButton(string value, float top, Action action, float height = 90)
        {
            var button = MakeButton(value, page, action);
            Top((RectTransform)button.transform, top, height);
            if (value == "Save" || value == "Apply changes" || value == "Create layout" || value == "Open workout"
                || value == "Start session" || value == "Save PNG" || value == "Save and share" || value == "Confirm") Primary(button);
            return button;
        }

        private Button[] PageRow(float top, string[] labels, Action[] actions)
        {
            var editorContent = content;
            content = page;
            var result = Row(top, 86, labels, actions);
            content = editorContent;
            return result;
        }
    }
}
