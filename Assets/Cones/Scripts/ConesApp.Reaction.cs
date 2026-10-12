using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

namespace Cones
{
    public sealed partial class ConesApp
    {
        private ReactionRun reactionRun;
        private bool reactionScreen, reactionRecorded;
        private string reactionTitle;
        private List<int> reactionTargets;
        private Text reactionCommand, reactionProgress, reactionClock, reactionPlay;
        private FieldBoard reactionBoard;
        private int spokenCommand;
        private string reactionRecordId;
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void ConesSpeak(string text);
        [DllImport("__Internal")] private static extern void ConesStopSpeaking();
#endif
        public void ShowReactionSettings()
        {
            BeginPage("Reaction mode", ShowTools);
            if (Plan.reaction == null) Plan.reaction = new ReactionSettings();
            var s = Plan.reaction;
            PageText("Follow a random cone number or color. The phone calls each target at your chosen interval.", 108, 100, 24);
            var interval = PageInput("Seconds per command (2–30)", s.interval.ToString(), 236, 2);
            var commands = PageInput("Commands per round (1–100)", s.commands.ToString(), 382, 3);
            var rounds = PageInput("Rounds (1–20)", s.rounds.ToString(), 528, 2);
            bool colors = s.colors, repeat = s.allowRepeat, voice = s.voice;
            Button mode = null, repeats = null, speech = null;
            mode = PageButton(colors ? "Targets: colors" : "Targets: numbers", 678,
                () => { colors = !colors; mode.GetComponentInChildren<Text>().text = colors ? "Targets: colors" : "Targets: numbers"; }, 76);
            repeats = PageButton(repeat ? "Repeats: allowed" : "Repeats: avoid", 766,
                () => { repeat = !repeat; repeats.GetComponentInChildren<Text>().text = repeat ? "Repeats: allowed" : "Repeats: avoid"; }, 76);
            speech = PageButton(voice ? "Voice: ON" : "Voice: OFF", 854,
                () => { voice = !voice; speech.GetComponentInChildren<Text>().text = voice ? "Voice: ON" : "Voice: OFF"; }, 76);
            var error = PageText("", 936, 40, 19);
            var start = PageButton("Open reaction drill", 982, () =>
            {
                var settings = new ReactionSettings { colors = colors, allowRepeat = repeat, voice = voice };
                if (!int.TryParse(interval.text, out settings.interval) || !int.TryParse(commands.text, out settings.commands)
                    || !int.TryParse(rounds.text, out settings.rounds) || !settings.IsValid()) { error.text = "Check the ranges above."; return; }
                if (Plan.cones.Count == 0) { error.text = "Add at least one cone first."; return; }
                Plan.reaction = settings; Refresh(); ShowReactionWorkout();
            }, 86); Primary(start);
        }
        public void ShowReactionWorkout()
        {
            if (Plan.cones.Count == 0) { ShowMessage("No targets", "Add cones before starting a reaction drill.", ShowReactionSettings); return; }
            BeginPage("Reaction drill", ShowReactionSettings);
            reactionTitle = drillTitle; reactionTargets = new List<int>();
            var settings = Plan.reaction ?? new ReactionSettings();
            for (int i = 0; i < Plan.cones.Count; i++)
            {
                int target = settings.colors ? Plan.StyleAt(i).color : i;
                if (!reactionTargets.Contains(target)) reactionTargets.Add(target);
            }
            reactionRun = new ReactionRun(settings, reactionTargets.Count, Environment.TickCount);
            reactionScreen = true; reactionRecorded = false; reactionRecordId = null; spokenCommand = 0;
            reactionProgress = PageText("", 114, 56, 25); reactionProgress.alignment = TextAnchor.MiddleCenter;
            reactionCommand = PageText("Ready", 208, 122, 58); reactionCommand.alignment = TextAnchor.MiddleCenter; reactionCommand.fontStyle = FontStyle.Bold;
            reactionClock = PageText("", 350, 64, 35); reactionClock.alignment = TextAnchor.MiddleCenter;
            reactionBoard = PageBoard(LocalWorkspace.Clone(Plan), 430, 360); reactionBoard.TargetsOnly = true; reactionBoard.Refresh();
            var controls = PageRow(0, new[] { "Start reaction", "Restart" }, new Action[]
            {
                () => { reactionRun.Toggle(); SetAwake(reactionRun.Running); if (!reactionRun.Running) StopSpeech(); RefreshReaction(); },
                ShowReactionWorkout
            });
            Bottom((RectTransform)controls[0].transform.parent, 196, 86); Primary(controls[0]);
            reactionPlay = controls[0].GetComponentInChildren<Text>();
            var history = PageButton("Training history", 0, () => ShowHistory()); Bottom((RectTransform)history.transform, 94, 86);
            var note = Label("Pauses in the background. Colors refer to any cone of that color.", page, 19, TextAnchor.MiddleCenter);
            Bottom(note.rectTransform, 8, 72);
            RefreshReaction();
        }
        private void RefreshReaction()
        {
            if (!reactionScreen || reactionRun == null || reactionCommand == null) return;
            var run = reactionRun;
            reactionProgress.text = $"Round {run.Round}/{run.Settings.rounds} · Command {run.Issued}/{run.Settings.commands * run.Settings.rounds}";
            string[] colors = { "Orange", "Blue", "Green", "Red" };
            if (run.Complete)
            {
                reactionCommand.text = "Complete"; reactionCommand.color = ink;
                reactionClock.text = reactionRecorded ? "Saved to history" : "Saving result...";
            }
            else if (run.Target >= 0)
            {
                int target = reactionTargets[run.Target];
                reactionCommand.text = run.Settings.colors ? colors[target] : "Cone " + (target + 1);
                reactionCommand.color = run.Settings.colors ? FieldBoard.ColorFor(target) : ConesVisuals.Orange;
                reactionClock.text = (run.Running ? "Next in " : "Paused · ") + Mathf.CeilToInt(run.Remaining) + "s";
                int selected = target;
                if (run.Settings.colors)
                    for (int i = 0; i < reactionBoard.Plan.cones.Count; i++)
                        if (reactionBoard.Plan.StyleAt(i).color == target) { selected = i; break; }
                if (reactionBoard.Selected != selected) { reactionBoard.Selected = selected; reactionBoard.Refresh(); }
                if (spokenCommand != run.Issued && run.Running)
                {
                    spokenCommand = run.Issued;
                    if (run.Settings.voice) Speak(reactionCommand.text);
                }
            }
            else reactionClock.text = "Tap Start when ready";
            reactionPlay.text = run.Complete ? "Finished" : run.Running ? "Pause" : run.Issued == 0 ? "Start" : "Resume";
            reactionPlay.GetComponentInParent<Button>().interactable = !run.Complete;
        }
        private void Speak(string text)
        {
#if UNITY_IOS && !UNITY_EDITOR
            ConesSpeak(text);
#else
            // Desktop preview uses a tone; iPhone uses the native English speech voice.
            if (runningSettings == null) runningSettings = new TrainingSettings();
            bool sound = runningSettings.sound; runningSettings.sound = true; PlayCue(); runningSettings.sound = sound;
#endif
        }
        private void StopSpeech()
        {
#if UNITY_IOS && !UNITY_EDITOR
            ConesStopSpeaking();
#endif
        }
        private void PauseAdvanced(bool leaveScreen)
        {
            if (reactionRun != null) reactionRun.Pause();
            if (coachClock != null) coachClock.Stop();
            StopSpeech(); SetAwake(false);
            if (leaveScreen) { reactionScreen = false; coachScreen = false; }
            else { RefreshReaction(); RefreshCoachClock(); }
        }
        private void UpdateAdvanced()
        {
            if (reactionScreen && reactionRun != null)
            {
                reactionRun.Tick(Time.unscaledDeltaTime);
                if (reactionRun.Complete && !reactionRecorded)
                {
                    if (reactionRecordId == null) reactionRecordId = Guid.NewGuid().ToString("N");
                    reactionRecorded = StoreRecord(new TrainingRecord { id = reactionRecordId, title = reactionTitle, mode = "Reaction",
                        seconds = reactionRun.Settings.interval * reactionRun.Settings.commands * reactionRun.Settings.rounds, sets = reactionRun.Settings.rounds });
                    SetAwake(false);
                }
                RefreshReaction();
            }
            RefreshCoachClock();
        }
    }
}
