using System;

namespace Cones
{
    [Serializable]
    public sealed class TrainingSettings
    {
        public int preparation = 5, work = 30, rest = 15, sets = 3;
        public bool sound = true;
        public bool IsValid() => preparation >= 0 && preparation <= 60 && work >= 5 && work <= 3600
            && rest >= 0 && rest <= 600 && sets >= 1 && sets <= 99;
    }

    public enum WorkoutPhase { Ready, Prepare, Work, Rest, Done }

    public sealed class WorkoutTimer
    {
        public WorkoutPhase Phase { get; private set; } = WorkoutPhase.Ready;
        public float Remaining { get; private set; }
        public int Set { get; private set; } = 1;
        public bool Running { get; private set; }
        public Action PhaseChanged;
        private readonly TrainingSettings settings;

        public WorkoutTimer(TrainingSettings settings)
        {
            if (settings == null || !settings.IsValid()) throw new ArgumentException("Invalid training settings.");
            this.settings = new TrainingSettings { preparation = settings.preparation, work = settings.work,
                rest = settings.rest, sets = settings.sets, sound = settings.sound };
            Remaining = settings.preparation > 0 ? settings.preparation : settings.work;
        }

        public void Toggle()
        {
            if (Phase == WorkoutPhase.Done) Reset();
            if (Phase == WorkoutPhase.Ready) Change(settings.preparation > 0 ? WorkoutPhase.Prepare : WorkoutPhase.Work);
            Running = !Running;
        }

        public void Pause() => Running = false;
        public void Reset()
        {
            Running = false; Set = 1; Phase = WorkoutPhase.Ready;
            Remaining = settings.preparation > 0 ? settings.preparation : settings.work;
        }

        public void Tick(float seconds)
        {
            if (!Running || seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            while (Running && seconds >= Remaining)
            {
                seconds -= Remaining;
                if (Phase == WorkoutPhase.Prepare) Change(WorkoutPhase.Work);
                else if (Phase == WorkoutPhase.Work)
                {
                    if (Set >= settings.sets) { Change(WorkoutPhase.Done); Running = false; }
                    else if (settings.rest > 0) Change(WorkoutPhase.Rest);
                    else { Set++; Change(WorkoutPhase.Work); }
                }
                else if (Phase == WorkoutPhase.Rest) { Set++; Change(WorkoutPhase.Work); }
            }
            if (Running) Remaining -= seconds;
        }

        private void Change(WorkoutPhase phase)
        {
            Phase = phase;
            Remaining = phase == WorkoutPhase.Prepare ? settings.preparation : phase == WorkoutPhase.Work ? settings.work
                : phase == WorkoutPhase.Rest ? settings.rest : 0;
            PhaseChanged?.Invoke();
        }
    }
}
