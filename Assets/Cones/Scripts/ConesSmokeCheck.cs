#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cones
{
    public sealed class ConesSmokeCheck : MonoBehaviour
    {
        private void Awake() => Application.runInBackground = true;

        private IEnumerator Start()
        {
            var app = GetComponent<ConesApp>();
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-conesTestOutput");
            string output = index >= 0 && index + 1 < args.Length ? args[index + 1] : Application.persistentDataPath;
            Directory.CreateDirectory(output);
            string testRoot = Path.Combine(output, "run-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
            typeof(ConesApp).GetField("savePath", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(app, Path.Combine(testRoot, "legacy.json"));
            string error = null;
            Application.LogCallback capture = (message, trace, type) =>
            {
                if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) error = error ?? message + "\n" + trace;
            };
            Application.logMessageReceived += capture;
            var library = new DrillLibrary(Path.Combine(testRoot, "drills"));
            for (int i = 0; i < 5; i++) library.Save(null, "Saved drill " + (i + 1), DrillPlan.Template(i % 4));
            app.AddToSession("Zigzag", DrillPlan.Example());
            app.AddToSession("Square loop", DrillPlan.Template(2));
            var sampleRecord = new TrainingRecord { title = "Zigzag", mode = "Coach", participant = "Alex", seconds = 24.37f, sets = 1, note = "Keep turns controlled.", difficulty = 3 };
            app.StoreRecord(sampleRecord); app.AddAthlete("Alex");
            while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            app.SafePanel.simulatePhoneInsets = true;
            foreach (var size in new[] { new Vector2Int(375, 667), new Vector2Int(393, 852), new Vector2Int(430, 932) })
            {
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                yield return new WaitForSeconds(0.7f);
                app.SafePanel.Apply();
                app.UsePlan(DrillPlan.Example(), "Zigzag", null);
                app.SetMovement(0, MovementKind.Backpedal);
                app.SetMovement(1, MovementKind.SideStep);
                app.ApplyObstacle(-1, new FieldObstacle { name = "Wall 1", x = 2, y = 2, width = 1, height = 1 });
                app.ImportSharedText(JsonUtility.ToJson(new SharedDrill { title = "Shared zigzag", plan = app.Plan }));
                Action[] pages = { app.ShowHome, app.ShowTemplates, () => app.ShowLibrary(), () => app.ShowLibrary(1),
                    app.ShowRouteSettings, () => app.ShowRouteSteps(), app.ShowSaveDialog, app.ShowEditor,
                    app.ShowTools, app.ShowSetup, () => app.ShowConeDetails(0), () => app.ShowTemplateOptions(0),
                    app.ShowTrainingSettings, () => app.ShowWorkout(app.Plan, "Test workout"),
                    () => app.ShowSession(), () => app.ShowSessionPicker(), app.ShowExport, app.ShowFieldSettings,
                    () => { app.ShowEditor(); if (!app.Board.RouteMode) app.ToggleMode(); },
                    app.ShowVariations, () => app.ShowMovements(), () => app.ShowObstacles(), () => app.ShowObstacleEditor(0),
                    app.ShowReactionSettings, app.ShowReactionWorkout, app.ShowCoach, () => app.ShowHistory(),
                    () => app.ShowHistoryEntry(sampleRecord.id), app.ShowSharing, app.ShowImportPreview };
                string[] names = { "home", "templates", "library", "library-page-2", "route-settings", "steps", "save", "editor",
                    "tools", "setup", "cone-details", "customize", "training-settings", "workout", "session", "session-picker", "export", "field-settings", "route-editor",
                    "variations", "movements", "obstacles", "obstacle-editor", "reaction-settings", "reaction", "coach", "history", "activity", "sharing", "import" };
                for (int i = 0; i < pages.Length; i++)
                {
                    pages[i]();
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    try
                    {
                        AssertLayout(app);
                        ScreenCapture.CaptureScreenshot(Path.Combine(output, $"{names[i]}-{size.x}x{size.y}.png"));
                    }
                    catch (Exception e) { error = error ?? names[i] + ": " + e.ToString(); }
                    yield return new WaitForSeconds(0.2f);
                }
            }
            app.UsePlan(DrillPlan.Example(), "Interaction test", null);
            yield return null;
            Canvas.ForceUpdateCanvases();
            try
            {
                Vector2 before = app.Plan.cones[0];
                var pointer = new PointerEventData(EventSystem.current);
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, app.Board.transform.TransformPoint(app.Board.LocalPoint(before)));
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                Require(hits.Count > 0 && hits[0].gameObject == app.Board.gameObject, "Field cannot receive touch.");
                ExecuteEvents.Execute(app.Board.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, app.Board.transform.TransformPoint(app.Board.LocalPoint(new Vector2(2, 2))));
                ExecuteEvents.Execute(app.Board.gameObject, pointer, ExecuteEvents.dragHandler);
                ExecuteEvents.Execute(app.Board.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                Require(app.Plan.cones[0] == new Vector2(2, 2), "Cone drag failed.");
                app.UndoEdit(); Require(app.Plan.cones[0] == before, "Drag undo failed.");
                app.RedoEdit(); Require(app.Plan.cones[0] == new Vector2(2, 2), "Drag redo failed.");
                app.ShowHome(); Click(app, "Create a new drill");
                Require(app.GetComponentsInChildren<Text>().Any(t => t.text == "Unsaved changes"), "Missing discard confirmation.");
                Click(app, "Cancel"); app.ShowEditor();
                app.ClearRoute();
                for (int i = 0; i < 3; i++)
                {
                    pointer.position = RectTransformUtility.WorldToScreenPoint(null, app.Board.transform.TransformPoint(app.Board.LocalPoint(app.Plan.cones[i])));
                    ExecuteEvents.Execute(app.Board.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.Execute(app.Board.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                }
                Require(app.Plan.route.SequenceEqual(new[] { 0, 1, 2 }), "Route input failed.");
                app.ReverseRoute(); Require(app.Plan.route.SequenceEqual(new[] { 2, 1, 0 }), "Reverse failed.");
                app.MoveRouteStep(0, 1); Require(app.Plan.route.SequenceEqual(new[] { 1, 2, 0 }), "Reorder failed.");
                app.UndoEdit(); Require(app.Plan.route.SequenceEqual(new[] { 2, 1, 0 }), "Step undo failed.");
                app.ChangeSettings(0.5f, 1);
                Require(app.Plan.speed == 2.5f && app.Plan.rounds == 2, "Settings failed.");
                Click(app, "Closed loop: OFF"); Require(app.Plan.closed, "Close loop failed.");
                app.ShowEditor(); app.TogglePlayback();
                Require(app.Board.Playing, "Playback did not start.");
            }
            catch (Exception e) { error = error ?? e.ToString(); }
            yield return new WaitForSeconds(0.5f);
            try
            {
                Require(app.Board.Runner != app.Plan.cones[app.Plan.route[0]], "Runner did not move.");
                app.TogglePlayback();
                app.AddCone(); Require(app.Plan.cones.Count == 7, "Add cone failed.");
                app.RemoveCone(); Require(app.Plan.cones.Count == 6 && app.Plan.IsValid(), "Remove cone failed.");
                app.ShowSaveDialog();
                app.GetComponentInChildren<InputField>().text = "Saved route test";
                Click(app, "Save");
                var saved = library.List(out _).First(d => d.title == "Saved route test");
                Require(saved.plan.closed && saved.plan.rounds == 2 && saved.plan.speed == 2.5f, "Saved settings failed.");
                float distance = saved.plan.Distance;
                app.ClearRoute(); app.OpenSaved(saved.id);
                Require(Mathf.Abs(app.Plan.Distance - distance) < 0.001f, "Load failed.");
                app.SavePlan();
                Require(library.List(out _).Count == 6, "Save created an unwanted duplicate.");
                var copy = library.Save(null, "Copy", app.Plan);
                Require(copy.id != saved.id && library.List(out _).Count == 7, "Copy failed.");
                library.Delete(copy.id); Require(library.List(out _).Count == 6, "Delete failed.");
                string snapshot = JsonUtility.ToJson(app.Plan);
                File.WriteAllText(Path.Combine(testRoot, "drills", saved.id + ".json"), "{broken json");
                app.OpenSaved(saved.id);
                Require(JsonUtility.ToJson(app.Plan) == snapshot, "Invalid save replaced current drill.");
                Require(library.List(out int unreadable).Count == 5 && unreadable == 1, "Corrupt file handling failed.");
                app.UsePlan(DrillPlan.Template(2), "Loop test", null);
                app.Plan.speed = 6; app.Plan.rounds = 2; app.Refresh(); app.TogglePlayback();
                typeof(ConesApp).GetField("travelled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(app, app.Plan.Distance + 1f);
            }
            catch (Exception e) { error = error ?? e.ToString(); }
            yield return null;
            try
            {
                Require(app.Board.Playing && app.Board.Runner != app.Plan.cones[0], "Second round failed.");
                app.UsePlan(DrillPlan.Example(), "Feature checks", null);
                Require(app.ApplyConeDetails(0, new Vector2(1.33f, 2.66f), "Start", 1), "Precise editing rejected valid input.");
                Require(!app.ApplyConeDetails(0, new Vector2(99, 1), "Bad", 0), "Out-of-bounds input accepted.");
                Require(app.Plan.StyleAt(0).label == "Start" && app.Plan.StyleAt(0).color == 1, "Cone metadata failed.");
                app.UndoEdit(); Require(app.Plan.StyleAt(0).label == "", "Metadata undo failed.");
                app.RedoEdit(); Require(app.Plan.StyleAt(0).label == "Start", "Metadata redo failed.");
                app.SaveDraftNow();
                var draft = LocalWorkspace.Read<DraftState>(Path.Combine(testRoot, "draft.json"), value => value.IsValid());
                Require(draft.plan.StyleAt(0).label == "Start", "Draft did not save metadata.");
                app.ApplyConeDetails(0, new Vector2(2, 3), "Changed", 2);
                Require(app.RestoreDraft() && app.Plan.StyleAt(0).label == "Start", "Draft recovery failed.");
                app.SaveDraftNow();
                File.WriteAllText(Path.Combine(testRoot, "draft.json"), "{broken");
                Require(app.RestoreDraft(), "Draft backup recovery failed.");
                Require(Directory.GetFiles(testRoot, "draft.json.unreadable-*").Length == 1, "Corrupt draft was not preserved.");
                app.ShowSetup(); Click(app, "Mark placed");
                Require(app.GetComponentsInChildren<Text>().Any(t => t.text.StartsWith("1/6 placed")), "Setup checkmark failed.");
                Click(app, "Next cone"); Click(app, "Mark placed");
                Require(app.GetComponentsInChildren<Text>().Any(t => t.text.StartsWith("2/6 placed")), "Second setup checkmark failed.");
                app.MoveSessionDrill(1, -1);
                var session = LocalWorkspace.Read<SessionPlan>(Path.Combine(testRoot, "session.json"), value => value.IsValid());
                Require(session.drills.Count == 2 && session.drills[0].title == "Square loop", "Session reorder/persistence failed.");
                app.StartSessionDrill(0); Click(app, "Next drill");
                Require(app.GetComponentsInChildren<Text>().Any(t => t.text == "2/2: Zigzag"), "Session navigation failed.");
                app.Plan.training = new TrainingSettings { preparation = 0, work = 5, rest = 0, sets = 2, sound = false };
                app.ShowWorkout(app.Plan, "Timer check"); Click(app, "Start");
            }
            catch (Exception e) { error = error ?? e.ToString(); }
            yield return new WaitForSeconds(.2f);
            try
            {
                var timer = (WorkoutTimer)typeof(ConesApp).GetField("workout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(app);
                Require(timer.Running && timer.Remaining < 5, "Live workout timer did not tick.");
                app.SendMessage("OnApplicationPause", true);
                Require(!timer.Running, "Background timer was not paused.");
                Click(app, "Start"); // The button name remains Start while its caption changes to Resume.
                Require(timer.Running, "Workout resume failed.");
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "workout-running.png"));
            }
            catch (Exception e) { error = error ?? e.ToString(); }
            app.ShowExport(); app.ExportCurrentImage(false);
            float exportDeadline = Time.realtimeSinceStartup + 5;
            while (app.LastExportPath == null && Time.realtimeSinceStartup < exportDeadline) yield return null;
            try
            {
                Require(app.LastExportPath != null && File.Exists(app.LastExportPath), "PNG export did not complete.");
                byte[] png = File.ReadAllBytes(app.LastExportPath);
                Require(png.Length > 1000 && png[0] == 137 && png[1] == 80 && png[2] == 78 && png[3] == 71, "Invalid PNG.");
                File.Copy(app.LastExportPath, Path.Combine(output, "exported-drill.png"), true);
                app.ShowHome();
            }
            catch (Exception e) { error = error ?? e.ToString(); }
            try
            {
                app.UsePlan(DrillPlan.Example(), "Advanced checks", null);
                Vector2 initial = app.Plan.cones[0]; app.ApplyVariation(0); app.UndoEdit();
                Require(app.Plan.cones[0] == initial, "Mirror undo failed.");
                app.SetMovement(0, MovementKind.Dribble); app.UndoEdit();
                Require(app.Plan.MovementAt(0) == MovementKind.Run, "Movement undo failed.");
                app.RedoEdit(); Require(app.Plan.MovementAt(0) == MovementKind.Dribble, "Movement redo failed.");
                Require(app.ApplyObstacle(-1, new FieldObstacle { name = "Wall", x = 1, y = 1 }), "Valid obstacle rejected.");
                Require(!app.ApplyObstacle(-1, new FieldObstacle { x = 99 }), "Invalid obstacle accepted.");
                app.ExportSharedDrill();
                Require(File.Exists(app.LastSharedPath), "Editable export missing.");
                var transfer = SharedDrill.Parse(File.ReadAllText(app.LastSharedPath));
                Require(transfer.plan.obstacles.Count == 1 && transfer.plan.MovementAt(0) == MovementKind.Dribble, "Transfer lost metadata.");
                string current = JsonUtility.ToJson(app.Plan);
                Require(!app.ImportSharedText("{broken") && JsonUtility.ToJson(app.Plan) == current, "Invalid import replaced draft.");
                Require(app.ImportSharedFile(app.LastSharedPath) && JsonUtility.ToJson(app.Plan) == current, "Import preview modified draft.");
                int beforeCopies = library.List(out _).Count;
                Click(app, "Save a new library copy");
                Require(library.List(out _).Count == beforeCopies + 1, "Imported library copy missing.");
                Require(app.UpdateRecord(sampleRecord.id, "Updated notes", 4), "History notes save failed.");
                var history = LocalWorkspace.Read<ActivityLog>(Path.Combine(testRoot, "history.json"), value => value.IsValid());
                Require(history.records.Single(r => r.id == sampleRecord.id).difficulty == 4, "History rating persistence failed.");
                Require(!app.AddAthlete("alex"), "Duplicate athlete accepted.");
                app.ShowCoach(); Click(app, "Start attempt");
            }
            catch (Exception e) { error = error ?? e.ToString(); }
            yield return new WaitForSeconds(.2f);
            try
            {
                app.SendMessage("OnApplicationPause", true);
                var stopwatch = (System.Diagnostics.Stopwatch)typeof(ConesApp).GetField("coachClock", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(app);
                Require(!stopwatch.IsRunning && stopwatch.Elapsed.TotalSeconds > 0, "Coach background pause failed.");
                Require(app.SaveCoachAttempt(), "Coach result save failed.");
                app.Plan.reaction = new ReactionSettings { interval = 2, commands = 2, rounds = 1, voice = false };
                app.ShowReactionWorkout(); Click(app, "Start reaction");
                var reaction = (ReactionRun)typeof(ConesApp).GetField("reactionRun", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(app);
                reaction.Tick(4);
            }
            catch (Exception e) { error = error ?? e.ToString(); }
            yield return null;
            yield return null;
            try
            {
                var history = LocalWorkspace.Read<ActivityLog>(Path.Combine(testRoot, "history.json"), value => value.IsValid());
                Require(history.records.Count(r => r.mode == "Reaction") == 1, "Reaction result missing or duplicated.");
                Require(history.records.Count(r => r.mode == "Coach") == 2, "Coach result missing.");
                app.Plan.training = new TrainingSettings { preparation = 0, work = 5, rest = 0, sets = 1, sound = false };
                app.ShowWorkout(app.Plan, "Completed workout"); Click(app, "Start");
                var timer = (WorkoutTimer)typeof(ConesApp).GetField("workout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(app);
                timer.Tick(5);
            }
            catch (Exception e) { error = error ?? e.ToString(); }
            yield return null;
            yield return null;
            try
            {
                var history = LocalWorkspace.Read<ActivityLog>(Path.Combine(testRoot, "history.json"), value => value.IsValid());
                Require(history.records.Count(r => r.mode == "Workout") == 1, "Workout result missing or duplicated.");
            }
            catch (Exception e) { error = error ?? e.ToString(); }
            Application.logMessageReceived -= capture;
            File.WriteAllText(Path.Combine(output, "smoke-result.txt"), error ?? "PASS: 30 screens at 3 sizes; English/safe area; editing/undo; library; setup; metadata; draft/backup recovery; sessions; workout/pause; PNG; variations; movements; obstacles; editable export/import; coach/roster; reaction; persistent history/notes/ratings.");
            Application.Quit(error == null ? 0 : 1);
        }

        private static void AssertLayout(ConesApp app)
        {
            var corners = new Vector3[4];
            ((RectTransform)app.SafePanel.transform).GetWorldCorners(corners);
            Rect safeRect = new Rect(corners[0], corners[2] - corners[0]);
            foreach (var button in app.GetComponentsInChildren<Button>())
            {
                ((RectTransform)button.transform).GetWorldCorners(corners);
                Require(safeRect.Contains(corners[0]) && safeRect.Contains(corners[2]), "Outside safe area: " + button.name);
            }
            foreach (var label in app.GetComponentsInChildren<Text>())
            {
                Require(!System.Text.RegularExpressions.Regex.IsMatch(label.text, @"\p{IsCyrillic}"), "Non-English UI: " + label.text);
                Require(label.preferredHeight <= label.rectTransform.rect.height + 1, "Clipped text: " + label.text);
            }
        }

        private static void Click(ConesApp app, string name) => app.GetComponentsInChildren<Button>().Single(b => b.name == name).onClick.Invoke();
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
#endif
