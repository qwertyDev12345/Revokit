using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cones
{
    public sealed partial class ConesApp
    {
        private WorkoutTimer workout;
        private bool trainingScreen;
        private Text workoutPhase, workoutClock, workoutSet, workoutPlay;
        private TrainingSettings runningSettings;
        private AudioSource cueSource;
        private AudioClip cueClip;
        private int priorSleepTimeout;
        private bool ownsSleepTimeout;

        public void ShowTrainingSettings()
        {
            BeginPage("Training settings", ShowTools);
            if (Plan.training == null) Plan.training = new TrainingSettings();
            var settings = Plan.training;
            string[] names = { "Preparation", "Work", "Rest", "Sets" };
            int[] values = { settings.preparation, settings.work, settings.rest, settings.sets };
            int[] minimum = { 0, 5, 0, 1 }, maximum = { 60, 3600, 600, 99 }, step = { 5, 5, 5, 1 };
            for (int i = 0; i < 4; i++)
            {
                int field = i;
                float top = 116 + i * 160;
                PageText(names[i] + ": " + values[i] + (i < 3 ? " s" : ""), top, 40, 25);
                var controls = PageRow(top + 52, new[] { "Decrease", "Increase" }, new Action[]
                { () => ChangeTrainingSetting(field, -step[field]), () => ChangeTrainingSetting(field, step[field]) });
                controls[0].interactable = values[i] > minimum[i]; controls[1].interactable = values[i] < maximum[i];
            }
            PageButton(settings.sound ? "Sound cues: ON" : "Sound cues: OFF", 782,
                () => { settings.sound = !settings.sound; Refresh(); ShowTrainingSettings(); });
            PageButton("Open workout", 892, () => ShowWorkout(Plan, drillTitle));
        }

        private void ChangeTrainingSetting(int field, int delta)
        {
            var s = Plan.training;
            if (field == 0) s.preparation = Mathf.Clamp(s.preparation + delta, 0, 60);
            if (field == 1) s.work = Mathf.Clamp(s.work + delta, 5, 3600);
            if (field == 2) s.rest = Mathf.Clamp(s.rest + delta, 0, 600);
            if (field == 3) s.sets = Mathf.Clamp(s.sets + delta, 1, 99);
            Refresh(); ShowTrainingSettings();
        }

        public void ShowWorkout(DrillPlan plan, string title, int sessionIndex = -1)
        {
            BeginPage("Training", sessionIndex >= 0 ? (Action)(() => ShowSession()) : ShowTrainingSettings);
            runningSettings = LocalWorkspace.Clone(plan.training ?? new TrainingSettings());
            workout = new WorkoutTimer(runningSettings);
            workout.PhaseChanged = PlayCue;
            trainingScreen = true; workoutTitle = title; workoutRecorded = false; workoutRecordId = null; workoutHistoryButton = null; workoutHint = null;
            var drillName = PageText(title, 94, 48, 25);
            drillName.alignment = TextAnchor.MiddleCenter; drillName.color = ConesVisuals.Muted;
            workoutSet = PageText("", 150, 44, 23); workoutSet.alignment = TextAnchor.MiddleCenter;
            var ringObject = new GameObject("Workout progress", typeof(RectTransform), typeof(CanvasRenderer), typeof(WorkoutRing));
            ringObject.transform.SetParent(page, false); workoutRing = ringObject.GetComponent<WorkoutRing>(); workoutRing.raycastTarget = false;
            var rr = (RectTransform)ringObject.transform;
            rr.anchorMin = rr.anchorMax = new Vector2(.5f, 1); rr.pivot = new Vector2(.5f, 1);
            rr.sizeDelta = new Vector2(350, 350); rr.anchoredPosition = new Vector2(0, -200);
            workoutPhase = PageText("READY", 286, 48, 29); workoutPhase.alignment = TextAnchor.MiddleCenter;
            workoutPhase.fontStyle = FontStyle.Bold;
            workoutClock = PageText("", 342, 104, 80); workoutClock.alignment = TextAnchor.MiddleCenter;
            workoutClock.fontStyle = FontStyle.Bold;
            phaseChips = new Text[3];
            string[] phaseNames = { "Prepare", "Work", "Rest" };
            int[] durations = { runningSettings.preparation, runningSettings.work, runningSettings.rest };
            for (int i = 0; i < 3; i++)
            {
                var chip = Card("Phase " + phaseNames[i], page); Top(chip, 564, 84);
                chip.anchorMin = new Vector2(i / 3f, 1); chip.anchorMax = new Vector2((i + 1) / 3f, 1);
                chip.offsetMin += new Vector2(i == 0 ? 0 : 5, 0); chip.offsetMax -= new Vector2(i == 2 ? 0 : 5, 0);
                phaseChips[i] = Label(phaseNames[i] + "\n" + durations[i] + "s", chip, 23, TextAnchor.MiddleCenter);
                Stretch(phaseChips[i].rectTransform);
            }
            var preview = PageBoard(plan, 668, 250); preview.Compact = true; preview.Refresh();
            var controls = PageRow(0, new[] { "Start", "Reset timer" }, new Action[]
            {
                () => { if (workout.Phase == WorkoutPhase.Done) { workoutRecorded = false; workoutRecordId = null; } workout.Toggle(); SetAwake(workout.Running); RefreshWorkout(); },
                () => { workoutRecorded = false; workoutRecordId = null; workout.Reset(); SetAwake(false); RefreshWorkout(); }
            });
            Bottom((RectTransform)controls[0].transform.parent, 142, 86);
            Primary(controls[0]);
            workoutPlay = controls[0].GetComponentInChildren<Text>();
            if (sessionIndex >= 0)
            {
                var navigation = PageRow(0, new[] { "Previous drill", "Next drill" }, new Action[]
                { () => StartSessionDrill(sessionIndex - 1), () => StartSessionDrill(sessionIndex + 1) });
                Bottom((RectTransform)navigation[0].transform.parent, 36, 86);
                navigation[0].interactable = sessionIndex > 0;
                navigation[1].interactable = sessionIndex + 1 < session.drills.Count;
            }
            else
            {
                var note = Label("Timer pauses when the app is backgrounded.", page, 20, TextAnchor.MiddleCenter);
                Bottom(note.rectTransform, 38, 66); workoutHint = note.gameObject;
                workoutHistoryButton = PageButton("View result", 0, () => ShowHistoryEntry(workoutRecordId));
                Bottom((RectTransform)workoutHistoryButton.transform, 36, 86); workoutHistoryButton.gameObject.SetActive(false);
            }
            RefreshWorkout();
        }

        private void RefreshWorkout()
        {
            if (!trainingScreen || workoutClock == null) return;
            int seconds = Mathf.CeilToInt(workout.Remaining);
            workoutClock.text = $"{seconds / 60:00}:{seconds % 60:00}";
            workoutPhase.text = workout.Phase == WorkoutPhase.Done ? "COMPLETE" : workout.Phase.ToString().ToUpperInvariant();
            workoutSet.text = $"Set {workout.Set}/{runningSettings.sets}" +
                (!workout.Running && workout.Phase != WorkoutPhase.Ready && workout.Phase != WorkoutPhase.Done ? " · Paused" : "");
            float duration = workout.Phase == WorkoutPhase.Prepare ? runningSettings.preparation
                : workout.Phase == WorkoutPhase.Rest ? runningSettings.rest : runningSettings.work;
            workoutRing.SetProgress(workout.Phase == WorkoutPhase.Ready ? 1 : workout.Remaining / Mathf.Max(1, duration));
            int activePhase = workout.Phase == WorkoutPhase.Prepare ? 0 : workout.Phase == WorkoutPhase.Work ? 1 : workout.Phase == WorkoutPhase.Rest ? 2 : -1;
            for (int i = 0; i < phaseChips.Length; i++)
            {
                phaseChips[i].color = i == activePhase ? ConesVisuals.Orange : ink;
                phaseChips[i].transform.parent.GetComponent<Image>().color = i == activePhase ? new Color32(255, 242, 234, 255) : Color.white;
            }
            if (workoutHistoryButton != null) workoutHistoryButton.gameObject.SetActive(workout.Phase == WorkoutPhase.Done && workoutRecorded);
            if (workoutHint != null) workoutHint.SetActive(workout.Phase != WorkoutPhase.Done || !workoutRecorded);
            workoutPlay.text = workout.Running ? "Pause" : workout.Phase == WorkoutPhase.Ready ? "Start"
                : workout.Phase == WorkoutPhase.Done ? "Repeat" : "Resume";
        }

        private void UpdateTraining()
        {
            if (!trainingScreen || workout == null) return;
            workout.Tick(Time.unscaledDeltaTime);
            RecordWorkout();
            if (!workout.Running) SetAwake(false);
            RefreshWorkout();
        }

        private void PauseTraining(bool leaveScreen = true)
        {
            if (workout != null) workout.Pause();
            RefreshWorkout();
            if (leaveScreen) trainingScreen = false;
            SetAwake(false);
        }

        private void SetAwake(bool active)
        {
            if (active && !ownsSleepTimeout)
            { priorSleepTimeout = Screen.sleepTimeout; Screen.sleepTimeout = SleepTimeout.NeverSleep; ownsSleepTimeout = true; }
            else if (!active && ownsSleepTimeout)
            { Screen.sleepTimeout = priorSleepTimeout; ownsSleepTimeout = false; }
        }

        private void PlayCue()
        {
            if (!runningSettings.sound) return;
            if (cueSource == null)
            {
                cueSource = gameObject.AddComponent<AudioSource>();
                cueSource.playOnAwake = false; cueSource.spatialBlend = 0; cueSource.volume = .45f;
                if (FindFirstObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
                int samples = 6615;
                var data = new float[samples];
                for (int i = 0; i < samples; i++) data[i] = Mathf.Sin(2 * Mathf.PI * 880 * i / 44100f) * Mathf.Sin(Mathf.PI * i / samples);
                cueClip = AudioClip.Create("Workout cue", samples, 1, 44100, false); cueClip.SetData(data, 0);
            }
            cueSource.PlayOneShot(cueClip);
        }

        private void OnDestroy()
        {
            StopSpeech(); coachClock.Stop();
            SetAwake(false);
            if (cueClip != null) Destroy(cueClip);
        }

        public void AddToSession(string title, DrillPlan plan)
        {
            if (session.drills.Count >= 20) { ShowMessage("Session full", "A session can contain up to 20 drills.", () => ShowSession()); return; }
            var changed = LocalWorkspace.Clone(session);
            changed.drills.Add(new SessionDrill { title = title, plan = LocalWorkspace.Clone(plan) });
            if (SaveSession(changed)) ShowSession((changed.drills.Count - 1) / 3);
        }

        private bool SaveSession(SessionPlan changed)
        {
            try
            {
                if (!changed.IsValid()) throw new ArgumentException("Invalid session.");
                if (blockedWorkspacePaths.Contains(WorkspacePath("session.json"))) throw new System.IO.IOException("Original workspace file could not be preserved.");
                LocalWorkspace.Write(WorkspacePath("session.json"), changed);
                session = changed;
                return true;
            }
            catch (Exception e) when (StorageError(e))
            { ShowMessage("Could not save session", "The session was not changed. Check available storage.", ShowHome); return false; }
        }

        public void ShowSession(int pageNumber = 0)
        {
            int pages = Mathf.Max(1, (session.drills.Count + 2) / 3);
            pageNumber = Mathf.Clamp(pageNumber, 0, pages - 1);
            BeginPage("Session plan", ShowHome);
            PageText(session.title, 104, 62, 26);
            PageRow(184, new[] { "Add saved drill", "Rename" }, new Action[] { () => ShowSessionPicker(), ShowSessionName });
            PageText($"{session.drills.Count}/20 drills · Page {pageNumber + 1}/{pages}", 292, 38, 22);
            if (session.drills.Count == 0) PageText("Add drills from My drills, or use Add to session in Drill tools.", 386, 118, 25);
            for (int i = 0; i < 3 && pageNumber * 3 + i < session.drills.Count; i++)
            {
                int index = pageNumber * 3 + i, currentPage = pageNumber;
                float top = 354 + i * 156;
                PageText($"{index + 1}. {session.drills[index].title}", top, 60, 23);
                var controls = PageRow(top + 62, new[] { "Up", "Down", "Remove" }, new Action[]
                {
                    () => MoveSessionDrill(index, -1, currentPage),
                    () => MoveSessionDrill(index, 1, currentPage),
                    () => ShowConfirm("Remove drill?", "Remove this drill from the session? The saved library drill is kept.", () =>
                    {
                        var changed = LocalWorkspace.Clone(session); changed.drills.RemoveAt(index);
                        if (SaveSession(changed)) ShowSession(currentPage);
                    }, () => ShowSession(currentPage))
                });
                controls[0].interactable = index > 0;
                controls[1].interactable = index + 1 < session.drills.Count;
            }
            int current = pageNumber;
            var pager = PageRow(836, new[] { "Previous", "Next" }, new Action[] { () => ShowSession(current - 1), () => ShowSession(current + 1) });
            pager[0].interactable = current > 0; pager[1].interactable = current + 1 < pages;
            PageButton("Start session", 942, () => StartSessionDrill(0), 86).interactable = session.drills.Count > 0;
        }

        public void MoveSessionDrill(int index, int delta, int pageNumber = 0)
        {
            int other = index + delta;
            if (index < 0 || index >= session.drills.Count || other < 0 || other >= session.drills.Count) return;
            var changed = LocalWorkspace.Clone(session);
            var item = changed.drills[index]; changed.drills[index] = changed.drills[other]; changed.drills[other] = item;
            if (SaveSession(changed)) ShowSession(pageNumber);
        }

        public void StartSessionDrill(int index)
        {
            if (index < 0 || index >= session.drills.Count) return;
            var item = session.drills[index];
            ShowWorkout(item.plan, $"{index + 1}/{session.drills.Count}: {item.title}", index);
        }

        public void ShowSessionPicker(int pageNumber = 0)
        {
            try
            {
                var entries = Library.List(out int skipped);
                int pages = Mathf.Max(1, (entries.Count + 3) / 4);
                pageNumber = Mathf.Clamp(pageNumber, 0, pages - 1);
                BeginPage("Add saved drill", () => ShowSession());
                PageText($"Choose a drill · Page {pageNumber + 1}/{pages}" + (skipped > 0 ? "\nUnreadable files were skipped." : ""), 112, 76, 22);
                if (entries.Count == 0) PageText("No saved drills yet. Save a drill in the editor first.", 250, 140, 25);
                for (int i = 0; i < 4 && pageNumber * 4 + i < entries.Count; i++)
                {
                    var entry = entries[pageNumber * 4 + i];
                    PageButton(entry.title, 216 + i * 144, () => AddToSession(entry.title, entry.plan), 112).interactable = session.drills.Count < 20;
                }
                int current = pageNumber;
                var pager = PageRow(850, new[] { "Previous", "Next" }, new Action[] { () => ShowSessionPicker(current - 1), () => ShowSessionPicker(current + 1) });
                pager[0].interactable = current > 0; pager[1].interactable = current + 1 < pages;
            }
            catch (Exception e) when (StorageError(e)) { ShowMessage("Library unavailable", "Could not read saved drills.", () => ShowSession()); }
        }

        private void ShowSessionName()
        {
            BeginPage("Session name", () => ShowSession());
            var input = PageInput("Name", session.title, 160);
            var error = PageText("", 326, 62, 22);
            PageButton("Save name", 420, () =>
            {
                string title = input.text.Trim();
                if (title.Length == 0) { error.text = "Please enter a name."; return; }
                var changed = LocalWorkspace.Clone(session); changed.title = title;
                if (SaveSession(changed)) ShowSession();
            });
        }
    }
}
