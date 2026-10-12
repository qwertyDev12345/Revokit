using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

namespace Cones
{
    public sealed partial class ConesApp
    {
        private ActivityLog activity = new ActivityLog();
        private CoachRoster roster = new CoachRoster();
        private Stopwatch coachClock = new Stopwatch();
        private bool coachScreen;
        private int coachIndex;
        private Text coachTime, coachPlay;
        private Button coachSave;
        private string activityError = "";
        private float retryRecordAt;
        private string workoutRecordId, workoutTitle;
        private bool workoutRecorded;
        private Button workoutHistoryButton;
        private GameObject workoutHint;

        private void LoadActivity()
        {
            activity = RecoverFile<ActivityLog>(WorkspacePath("history.json"), value => value.IsValid()) ?? new ActivityLog();
            roster = RecoverFile<CoachRoster>(WorkspacePath("coach.json"), value => value.IsValid()) ?? new CoachRoster();
        }
        public bool StoreRecord(TrainingRecord record)
        {
            if (record == null || !record.IsValid()) return false;
            if (activity.records.Exists(r => r.id == record.id)) return true;
            if (Time.unscaledTime < retryRecordAt) return false;
            var changed = LocalWorkspace.Clone(activity); changed.records.Insert(0, record);
            if (changed.records.Count > 200) changed.records.RemoveRange(200, changed.records.Count - 200);
            if (!SaveActivity(changed)) { retryRecordAt = Time.unscaledTime + 5; return false; }
            return true;
        }
        private bool SaveActivity(ActivityLog changed)
        {
            try
            {
                if (!changed.IsValid() || blockedWorkspacePaths.Contains(WorkspacePath("history.json"))) throw new System.IO.IOException("History is unavailable.");
                LocalWorkspace.Write(WorkspacePath("history.json"), changed); activity = changed; activityError = ""; return true;
            }
            catch (Exception e) when (StorageError(e)) { activityError = "Could not save history. Check available storage."; return false; }
        }
        public void ShowHistory(int pageNumber = 0)
        {
            int pages = Mathf.Max(1, (activity.records.Count + 3) / 4); pageNumber = Mathf.Clamp(pageNumber, 0, pages - 1);
            BeginPage("Training history", ShowTools);
            PageText(string.IsNullOrEmpty(activityError) ? $"Latest 200 completed activities · Page {pageNumber + 1}/{pages}" : activityError, 110, 100, 23);
            if (activity.records.Count == 0) PageText("Complete a workout, reaction drill, or coach attempt to see it here.", 270, 160, 26);
            for (int i = 0; i < 4 && pageNumber * 4 + i < activity.records.Count; i++)
            {
                var record = activity.records[pageNumber * 4 + i];
                string who = string.IsNullOrEmpty(record.participant) ? record.title : record.participant;
                string date = DateTime.Parse(record.utc).ToLocalTime().ToString("MMM d, HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                PageButton($"{who}\n{record.mode} · {FormatTime(record.seconds)} · {date}", 238 + i * 164, () => ShowHistoryEntry(record.id), 142);
            }
            int selected = pageNumber;
            var pager = PageRow(938, new[] { "Previous", "Next" }, new Action[] { () => ShowHistory(selected - 1), () => ShowHistory(selected + 1) });
            pager[0].interactable = selected > 0; pager[1].interactable = selected + 1 < pages;
        }
        public void ShowHistoryEntry(string id)
        {
            var record = activity.records.Find(r => r.id == id);
            if (record == null) { ShowHistory(); return; }
            BeginPage("Activity details", () => ShowHistory());
            PageText(record.title, 114, 90, 27);
            PageText($"{record.mode} · {FormatTime(record.seconds)} · {record.sets} set(s)\n" +
                (string.IsNullOrEmpty(record.participant) ? "" : record.participant + "\n") + DateTime.Parse(record.utc).ToLocalTime().ToString("MMM d, yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture), 216, 100, 23);
            var note = PageInput("Notes (up to 280 characters)", record.note, 348, 280);
            Top((RectTransform)note.transform, 390, 202); note.lineType = InputField.LineType.MultiLineNewline;
            note.textComponent.alignment = TextAnchor.UpperLeft;
            int difficulty = record.difficulty;
            Button rating = null;
            rating = PageButton(DifficultyLabel(difficulty), 628, () => { difficulty = (difficulty + 1) % 6; rating.GetComponentInChildren<Text>().text = DifficultyLabel(difficulty); });
            PageButton("Save notes", 750, () =>
            {
                if (UpdateRecord(id, note.text, difficulty)) ShowHistory();
                else ShowMessage("History unavailable", activityError, () => ShowHistoryEntry(id));
            });
            PageButton("Delete activity", 866, () => ShowConfirm("Delete activity?", "Delete this activity and its notes from this device?", () =>
            {
                var changed = LocalWorkspace.Clone(activity); changed.records.RemoveAll(r => r.id == id);
                if (SaveActivity(changed)) ShowHistory(); else ShowMessage("History unavailable", activityError, () => ShowHistoryEntry(id));
            }, () => ShowHistoryEntry(id)));
            PageText("Difficulty is your own rating: 1 easy, 5 very hard.", 978, 80, 21);
        }
        public bool UpdateRecord(string id, string note, int difficulty)
        {
            if (note == null || note.Length > 280 || difficulty < 0 || difficulty > 5) return false;
            var changed = LocalWorkspace.Clone(activity); var record = changed.records.Find(r => r.id == id);
            if (record == null) return false;
            record.note = note; record.difficulty = difficulty; return SaveActivity(changed);
        }
        private static string DifficultyLabel(int value) => value == 0 ? "Difficulty: not rated" : $"Difficulty: {value}/5";
        private static string FormatTime(float seconds) => $"{(int)seconds / 60:00}:{seconds % 60:00.00}";

        public bool AddAthlete(string name)
        {
            name = name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 40 || roster.names.Count >= 20 || roster.names.Exists(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))) return false;
            var changed = LocalWorkspace.Clone(roster); changed.names.Add(name); return SaveRoster(changed);
        }
        private bool SaveRoster(CoachRoster changed)
        {
            try
            {
                if (!changed.IsValid() || blockedWorkspacePaths.Contains(WorkspacePath("coach.json"))) throw new System.IO.IOException("Roster unavailable.");
                LocalWorkspace.Write(WorkspacePath("coach.json"), changed); roster = changed; return true;
            }
            catch (Exception e) when (StorageError(e)) { return false; }
        }
        public void ShowCoach()
        {
            BeginPage("Coach mode", ShowTools); coachScreen = true;
            coachIndex = Mathf.Clamp(coachIndex, 0, Mathf.Max(0, roster.names.Count - 1));
            var pager = PageRow(116, new[] { "Previous athlete", "Next athlete" }, new Action[] { () => SelectAthlete(coachIndex - 1), () => SelectAthlete(coachIndex + 1) });
            pager[0].interactable = coachIndex > 0; pager[1].interactable = coachIndex + 1 < roster.names.Count;
            var athlete = PageText(roster.names.Count == 0 ? "Add an athlete to begin" : roster.names[coachIndex], 234, 90, 33);
            athlete.alignment = TextAnchor.MiddleCenter;
            coachTime = PageText("00:00.00", 370, 124, 70); coachTime.alignment = TextAnchor.MiddleCenter; coachTime.fontStyle = FontStyle.Bold;
            var buttons = PageRow(536, new[] { "Start attempt", "Stop & save" }, new Action[]
            {
                () => { if (coachClock.IsRunning) coachClock.Stop(); else coachClock.Start(); SetAwake(coachClock.IsRunning); RefreshCoachClock(); },
                () => { if (SaveCoachAttempt()) ShowCoach(); else ShowMessage("Could not save", "The stopped attempt is kept. Check storage and try Stop & save again.", ShowCoach); }
            });
            buttons[0].interactable = roster.names.Count > 0; Primary(buttons[0]);
            coachPlay = buttons[0].GetComponentInChildren<Text>(); coachSave = buttons[1];
            PageButton("Discard timer", 646, () => ShowConfirm("Discard attempt?", "Reset this unsaved time? Existing results are kept.",
                () => { coachClock.Reset(); ShowCoach(); }, ShowCoach));
            var rosterActions = PageRow(758, new[] { "Add athlete", "Remove athlete" }, new Action[] { ShowAddAthlete, RemoveAthlete });
            rosterActions[0].interactable = roster.names.Count < 20; rosterActions[1].interactable = roster.names.Count > 0;
            PageButton("View results", 868, () => ShowHistory());
            PageText("Manual stopwatch. Start, pause, then Stop & save. Leaving this screen pauses the timer.", 980, 94, 21);
            RefreshCoachClock();
        }
        private void SelectAthlete(int index)
        {
            if (index < 0 || index >= roster.names.Count) return;
            Action select = () => { coachClock.Reset(); coachIndex = index; ShowCoach(); };
            if (coachClock.Elapsed.TotalSeconds > 0) ShowConfirm("Unsaved attempt", "Discard the current timer and switch athlete?", select, ShowCoach); else select();
        }
        private void ShowAddAthlete()
        {
            BeginPage("Add athlete", ShowCoach);
            var input = PageInput("Name", "", 152, 40);
            var error = PageText("", 310, 100, 23);
            PageButton("Add athlete", 462, () =>
            {
                if (!AddAthlete(input.text)) { error.text = "Use a unique name (1–40 characters), up to 20 athletes. Check storage if the name is valid."; return; }
                ShowCoach();
            });
        }
        private void RemoveAthlete()
        {
            if (roster.names.Count == 0) return;
            ShowConfirm("Remove athlete?", "Remove this athlete and discard their unsaved timer? Saved attempts stay in history.", () =>
            {
                var changed = LocalWorkspace.Clone(roster); changed.names.RemoveAt(coachIndex);
                if (SaveRoster(changed)) { coachClock.Reset(); ShowCoach(); }
                else ShowMessage("Could not save", "The roster was not changed. Check available storage.", ShowCoach);
            }, ShowCoach);
        }
        public bool SaveCoachAttempt()
        {
            coachClock.Stop(); SetAwake(false);
            if (roster.names.Count == 0 || coachClock.Elapsed.TotalSeconds <= 0) return false;
            if (!StoreRecord(new TrainingRecord { title = drillTitle, mode = "Coach", participant = roster.names[coachIndex], seconds = (float)coachClock.Elapsed.TotalSeconds, sets = 1 })) return false;
            coachClock.Reset(); return true;
        }
        private void RefreshCoachClock()
        {
            if (!coachScreen || coachTime == null || coachPlay == null) return;
            coachTime.text = FormatTime((float)coachClock.Elapsed.TotalSeconds);
            coachPlay.text = coachClock.IsRunning ? "Pause" : coachClock.Elapsed.TotalSeconds > 0 ? "Resume" : "Start";
            coachSave.interactable = roster.names.Count > 0 && coachClock.Elapsed.TotalSeconds > 0;
        }
        private void RecordWorkout()
        {
            if (workout.Phase != WorkoutPhase.Done || workoutRecorded) return;
            if (workoutRecordId == null) workoutRecordId = Guid.NewGuid().ToString("N");
            workoutRecorded = StoreRecord(new TrainingRecord { id = workoutRecordId, title = workoutTitle, mode = "Workout", sets = runningSettings.sets,
                seconds = runningSettings.preparation + runningSettings.work * runningSettings.sets + runningSettings.rest * (runningSettings.sets - 1) });
            if (workoutHistoryButton != null) workoutHistoryButton.gameObject.SetActive(true);
        }
    }
}
