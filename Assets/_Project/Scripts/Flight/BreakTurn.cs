using UnityEngine;

namespace AirCombat
{
    // Reusable real heading change and short powered escape.
    // No teleport, sideways translation, invulnerability or separate cooldown.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10)] // Visual bank runs before the later camera offset.
    public sealed class BreakTurn : MonoBehaviour
    {
        [SerializeField, Min(.05f)] private float turnDuration = .55f;
        [SerializeField, Min(.05f)] private float burstDuration = 1f;
        [SerializeField, Range(0f, 85f)] private float breakBankAngle = 70f;
        [SerializeField, Range(1f, 180f)] private float turnAngle = 90f;
        [SerializeField, Range(.01f, 1f)] private float fuelCost01 = 1f;

        private Afterburner boost;
        private AfterburnerVfx flames;
        private Transform visual;
        private Quaternion visualRestRotation;
        private bool breaking;
        private int direction;
        private float elapsed;

        public bool IsBreaking => breaking;

        public void Configure(Afterburner tank, Transform aircraftVisual)
        {
            boost = tank;
            flames = GetComponentInChildren<AfterburnerVfx>(true);
            visual = aircraftVisual;
            visualRestRotation = visual != null ? visual.localRotation : Quaternion.identity;
            CancelBreak();
        }

        public bool TryBegin(int side)
        {
            if (!isActiveAndEnabled || breaking || side == 0 || boost == null) return false;
            bool paid = fuelCost01 >= 1f ? boost.TryConsumeFullCharge()
                : boost.TryConsumeCharge(fuelCost01);
            if (!paid) return false;
            direction = side < 0 ? -1 : 1;
            elapsed = 0f;
            breaking = true;
            if (flames != null) flames.SetManeuverFlames(true);
            return true;
        }

        public void CancelBreak()
        {
            breaking = false;
            elapsed = 0f;
            if (flames != null) flames.SetManeuverFlames(false);
        }

        // Called by the owning flight controller exactly once per frame, before its motor Tick.
        // Supply a variable yaw rate so the *real* plane reaches the configured angle smoothly.
        // Normal WASD/AI steering is suppressed for the configured maneuver duration.
        public bool TryGetFlightCommand(float dt, out float steer, out float yawRate)
        {
            steer = 0f;
            yawRate = 0f;
            if (!breaking) return false;
            if (dt <= 0f) return true;

            float turnTime = Mathf.Max(.05f, turnDuration);
            float endTime = Mathf.Max(turnTime, burstDuration);
            float next = Mathf.Min(endTime, elapsed + dt);
            float from = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / turnTime));
            float to = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(next / turnTime));
            float yawDelta = turnAngle * (to - from);
            steer = yawDelta > 0.00001f ? direction : 0f;
            yawRate = Mathf.Abs(yawDelta) / dt;
            elapsed = next;
            return true;
        }

        private void LateUpdate()
        {
            if (!breaking) return;
            // AircraftMotor wrote the ordinary bank during its Update. Override it for
            // the break, then restore normal banking without any axial spin.
            if (visual != null)
            {
                float turnTime = Mathf.Max(.05f, turnDuration);
                float endTime = Mathf.Max(turnTime, burstDuration);
                float bankIn = Mathf.Min(.16f, turnTime * .5f);
                float weight = elapsed < bankIn
                    ? Mathf.SmoothStep(0f, 1f, elapsed / bankIn)
                    : elapsed < turnTime ? 1f
                    : Mathf.SmoothStep(1f, 0f, (elapsed - turnTime) / (endTime - turnTime + .0001f));
                Quaternion breakBank = Quaternion.AngleAxis(-direction * breakBankAngle, Vector3.forward)
                    * visualRestRotation;
                visual.localRotation = Quaternion.Slerp(visual.localRotation, breakBank, weight);
            }
            if (elapsed >= Mathf.Max(turnDuration, burstDuration)) CancelBreak();
        }

        private void OnDisable() { CancelBreak(); }
    }
}
