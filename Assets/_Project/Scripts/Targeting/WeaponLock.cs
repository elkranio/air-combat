using UnityEngine;

namespace AirCombat
{
    // Weapon-independent state: designation supplies hover, not cursor input.
    public sealed class WeaponLock
    {
        public PlayerHitPoints Target { get; private set; }
        public float Progress { get; private set; }
        public float LostTime { get; private set; }
        public bool IsAiming { get; private set; }
        public bool IsLocked => Alive(Target) && Progress >= 1f;

        public static bool Alive(PlayerHitPoints health) => health != null &&
            health.isActiveAndEnabled && health.IsAlive;

        public void Step(PlayerHitPoints hover, float dt, float acquire, float partialGrace, float fullGrace)
        {
            if (!Alive(Target)) Reset();
            if (!Alive(hover)) hover = null;
            dt = Mathf.Max(0f, dt);
            if (hover != null)
            {
                if (hover != Target)
                {
                    Reset();
                    Target = hover;
                }
                IsAiming = true;
                LostTime = 0f;
                Progress = Mathf.Min(1f, Progress + dt / Mathf.Max(.01f, acquire));
                return;
            }
            IsAiming = false;
            if (Target == null) return;
            LostTime += dt;
            if (LostTime >= (IsLocked ? fullGrace : partialGrace)) Reset();
        }

        public void Reset()
        {
            Target = null;
            Progress = LostTime = 0f;
            IsAiming = false;
        }
    }
}
