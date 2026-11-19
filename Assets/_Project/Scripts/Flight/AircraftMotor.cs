using UnityEngine;

namespace AirCombat
{
    // Shared movement state. Controllers supply intent and tuning; the motor never reads input.
    public sealed class AircraftMotor
    {
        private readonly Transform root;
        private readonly Transform visual;
        private readonly Quaternion visualRestRotation;
        private float bank;

        public float CurrentSpeed { get; private set; }

        public AircraftMotor(Transform root, Transform visual, float initialSpeed)
        {
            this.root = root;
            this.visual = visual;
            visualRestRotation = visual != null ? visual.localRotation : Quaternion.identity;
            CurrentSpeed = initialSpeed;
        }

        public void Tick(float targetSpeed, float steer, float acceleration, float deceleration,
            float turnRate, float maxBank, float bankRate, float deltaTime)
        {
            steer = Mathf.Clamp(steer, -1f, 1f);
            var speedRate = targetSpeed > CurrentSpeed ? acceleration : deceleration;
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, speedRate * deltaTime);

            // Gameplay stays in XZ. Only the visual banks.
            root.Rotate(0f, steer * turnRate * deltaTime, 0f, Space.World);
            root.position += root.forward * (CurrentSpeed * deltaTime);

            if (visual != null)
            {
                bank = Mathf.MoveTowards(bank, -steer * maxBank, bankRate * deltaTime);
                visual.localRotation = Quaternion.AngleAxis(bank, Vector3.forward) * visualRestRotation;
            }
        }
    }
}
