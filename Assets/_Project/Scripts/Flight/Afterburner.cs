using UnityEngine;

namespace AirCombat
{
    // Shared resource; the owning controller supplies a held/released request once per frame.
    [DisallowMultipleComponent]
    public sealed class Afterburner : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float boostSpeed = 110f;
        [SerializeField, Min(0.1f)] private float boostAcceleration = 180f;
        [SerializeField, Min(0.01f)] private float capacitySeconds = 2f;
        [SerializeField, Min(0.01f)] private float rechargeSeconds = 5f;
        [SerializeField, Min(0f)] private float rechargeDelay = 0.4f;

        public float BoostSpeed => boostSpeed;
        public float BoostAcceleration => boostAcceleration;
        public float Charge01 => Mathf.Clamp01(charge / Mathf.Max(0.01f, capacitySeconds));
        public bool IsActive { get; private set; }
        private float charge;
        private float delayRemaining;
        private bool wasHeld;

        private void OnEnable()
        {
            charge = Mathf.Max(0.01f, capacitySeconds);
            delayRemaining = 0f;
            wasHeld = false;
            IsActive = false;
        }

        private void OnDisable() { IsActive = false; }

        // Legacy full-tank maneuver retains its original boost restriction.
        public bool TryConsumeFullCharge()
        {
            return !IsActive && TryConsumeCharge(1f);
        }

        // Atomic fractional tank cost; the maneuver can take over from active boost.
        // No extra cooldown beyond the existing resource recharge delay.
        public bool TryConsumeCharge(float fraction)
        {
            float capacity = Mathf.Max(0.01f, capacitySeconds);
            float cost = capacity * Mathf.Clamp01(fraction);
            if (!isActiveAndEnabled || charge < cost - 0.0001f) return false;
            charge = Mathf.Max(0f, charge - cost);
            delayRemaining = rechargeDelay;
            wasHeld = false;
            IsActive = false;
            return true;
        }

        public bool Tick(bool held, float deltaTime)
        {
            if (!isActiveAndEnabled) return false;
            float capacity = Mathf.Max(0.01f, capacitySeconds);
            charge = Mathf.Clamp(charge, 0f, capacity);
            float dt = Mathf.Max(0f, deltaTime);
            IsActive = held && charge > 0f;
            if (held)
            {
                if (IsActive) charge = Mathf.Max(0f, charge - dt);
                // At zero, a held request cannot recharge or pulse the engine.
            }
            else
            {
                if (wasHeld) delayRemaining = rechargeDelay;
                float waiting = Mathf.Min(delayRemaining, dt);
                delayRemaining -= waiting;
                charge = Mathf.Min(capacity, charge + (dt - waiting) * capacity / Mathf.Max(0.01f, rechargeSeconds));
            }
            wasHeld = held;
            return IsActive;
        }
    }
}
