using System;
using UnityEngine;

namespace AirCombat
{
    // A mission-owned XZ operation rectangle, independent of visible terrain.
    // This is a plain helper: the launch sequence owns ordering, pause and retry.
    internal sealed class MissionBoundaryGuard
    {
        private readonly Rect bounds;
        private readonly float caution, critical;
        private readonly BoundaryWarningAudio audio;
        private float distance = float.PositiveInfinity;
        private int warningLevel;

        public MissionBoundaryGuard(Transform owner, Rect bounds, float caution, float critical)
        {
            if (bounds.width <= .1f || bounds.height <= .1f)
                throw new ArgumentException("Operation bounds must have positive dimensions.");
            this.bounds = bounds;
            this.caution = Mathf.Max(1f, caution);
            this.critical = Mathf.Clamp(critical, 1f, this.caution);
            audio = new BoundaryWarningAudio(owner);
        }

        internal Rect Bounds => bounds;
        internal float CautionDistance => caution;
        internal int WarningLevel => warningLevel;
        internal float WarningDistance => distance;

        public float Distance(Vector3 p) => Mathf.Min(
            Mathf.Min(p.x - bounds.xMin, bounds.xMax - p.x),
            Mathf.Min(p.z - bounds.yMin, bounds.yMax - p.z));

        // The rectangle is convex: an inside-to-inside segment cannot leave it.
        // For an outside endpoint, stop at the first edge, not at the overshoot.
        public bool TryClipExit(Vector3 from, Vector3 to, out Vector3 stop)
        {
            stop = to;
            if (Distance(to) > 0f && Distance(from) > 0f) return false;
            if (Distance(from) > 0f)
            {
                Vector3 delta = to - from;
                float t = 1f;
                if (to.x <= bounds.xMin) t = Mathf.Min(t, (bounds.xMin - from.x) / delta.x);
                if (to.x >= bounds.xMax) t = Mathf.Min(t, (bounds.xMax - from.x) / delta.x);
                if (to.z <= bounds.yMin) t = Mathf.Min(t, (bounds.yMin - from.z) / delta.z);
                if (to.z >= bounds.yMax) t = Mathf.Min(t, (bounds.yMax - from.z) / delta.z);
                stop = Vector3.Lerp(from, to, Mathf.Clamp01(t));
            }
            // Keep the final aircraft origin inside even with float rounding or
            // an invalid external teleport. The surrounding art covers the view.
            stop.x = Mathf.Clamp(stop.x, bounds.xMin + .01f, bounds.xMax - .01f);
            stop.z = Mathf.Clamp(stop.z, bounds.yMin + .01f, bounds.yMax - .01f);
            return true;
        }

        public void Tick(Vector3 position)
        {
            if (!Application.isFocused) { Clear(); return; }
            distance = Distance(position);
            int level = distance <= critical ? 2 : distance <= caution ? 1 : 0;
            if (level == 0) { Clear(); return; }
            bool changed = level != warningLevel;
            warningLevel = level;
            audio.Tick(level, changed);
        }

        public void Clear()
        {
            audio.Clear(warningLevel != 0);
            warningLevel = 0;
            distance = float.PositiveInfinity;
        }

        public void Dispose()
        {
            Clear();
            audio.Dispose();
        }
    }
}
