using UnityEngine;

namespace AirCombat
{
    // Read-only unit state. Missing or uninitialized targets are not kills;
    // a successful escape is not a destruction either.
    public enum ObjectiveTargetState { Pending, Patrolling, Charging, Escaping, Destroyed, Escaped }

    public interface IObjectiveTarget
    {
        ObjectiveTargetState ObjectiveState { get; }
    }

    public readonly struct ObjectiveProgress
    {
        public int Destroyed { get; }
        public int Total { get; }
        public bool IsReady { get; }
        public bool Complete => IsReady && Total > 0 && Destroyed == Total;

        internal ObjectiveProgress(int destroyed, int total, bool ready)
        {
            Destroyed = destroyed;
            Total = total;
            IsReady = ready;
        }
    }

    public interface IPrimaryObjectiveSource
    {
        ObjectiveProgress PrimaryProgress { get; }
    }

    // The composition chooses the required targets. Read their current state
    // rather than counting events; accepted retry automatically resets progress.
    internal sealed class DestroyTargetsObjective
    {
        private readonly MonoBehaviour[] targets;

        internal DestroyTargetsObjective(MonoBehaviour[] targets)
        {
            this.targets = (MonoBehaviour[])targets.Clone();
        }

        internal ObjectiveProgress Read()
        {
            int destroyed = 0;
            bool ready = targets.Length > 0;
            for (int i = 0; i < targets.Length; i++)
            {
                MonoBehaviour component = targets[i];
                if (component == null || !(component is IObjectiveTarget target))
                { ready = false; continue; }
                // Duplicated authoring must not count the same kill twice.
                bool duplicate = false;
                for (int j = 0; j < i; j++)
                    if (targets[j] == component) { duplicate = true; break; }
                if (duplicate) { ready = false; continue; }
                ObjectiveTargetState state = target.ObjectiveState;
                if (state == ObjectiveTargetState.Pending) ready = false;
                if (state == ObjectiveTargetState.Destroyed) destroyed++;
            }
            return new ObjectiveProgress(destroyed, targets.Length, ready);
        }
    }

    // Shared presentation mapping; callers choose their own objective title.
    internal static class ObjectiveStatusText
    {
        internal static bool IsActive(ObjectiveTargetState state) =>
            state == ObjectiveTargetState.Patrolling || state == ObjectiveTargetState.Charging ||
            state == ObjectiveTargetState.Escaping;

        internal static string For(ObjectiveTargetState state)
        {
            switch (state)
            {
                case ObjectiveTargetState.Destroyed: return "DESTROYED";
                case ObjectiveTargetState.Escaped: return "TARGET ESCAPED";
                case ObjectiveTargetState.Escaping: return "ESCAPING";
                case ObjectiveTargetState.Charging: return "ENGINES CHARGING";
                case ObjectiveTargetState.Patrolling: return "PATROLLING";
                default: return "PENDING";
            }
        }
    }
}
