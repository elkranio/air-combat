using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    [DisallowMultipleComponent]
    public sealed class FlightController : MonoBehaviour
    {
        [SerializeField] private Transform visual;

        [Header("Speed (units per second)")]
        [SerializeField, Min(0.1f)] private float minSpeed = 25f;
        [SerializeField, Min(0.1f)] private float cruiseSpeed = 45f;
        [SerializeField, Min(0.1f)] private float maxSpeed = 70f;
        [SerializeField, Min(0.1f)] private float acceleration = 35f;
        [SerializeField, Min(0.1f)] private float deceleration = 45f;

        [Header("Steering")]
        [SerializeField, Min(1f)] private float turnRate = 85f;
        [SerializeField, Range(0f, 80f)] private float maxBank = 40f;
        [SerializeField, Min(1f)] private float bankRate = 160f;

        public float CurrentSpeed { get; private set; }
        private Quaternion visualRestRotation;
        private float bank;

        private void OnValidate()
        {
            cruiseSpeed = Mathf.Max(minSpeed, cruiseSpeed);
            maxSpeed = Mathf.Max(cruiseSpeed, maxSpeed);
        }

        private void Awake()
        {
            CurrentSpeed = Mathf.Clamp(cruiseSpeed, minSpeed, maxSpeed);
            if (visual != null)
                visualRestRotation = visual.localRotation;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            float throttle = 0f;
            float steer = 0f;
            if (keyboard != null)
            {
                throttle = (keyboard.wKey.isPressed ? 1f : 0f)
                    - (keyboard.sKey.isPressed ? 1f : 0f);
                steer = (keyboard.dKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed ? 1f : 0f);
            }

            var targetSpeed = throttle > 0f ? maxSpeed : throttle < 0f ? minSpeed : cruiseSpeed;
            var speedRate = targetSpeed > CurrentSpeed ? acceleration : deceleration;
            var dt = Time.deltaTime;
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, speedRate * dt);

            // Only yaw the gameplay root: altitude stays fixed and banking is cosmetic.
            transform.Rotate(0f, steer * turnRate * dt, 0f, Space.World);
            transform.position += transform.forward * (CurrentSpeed * dt);

            if (visual != null)
            {
                bank = Mathf.MoveTowards(bank, -steer * maxBank, bankRate * dt);
                visual.localRotation = Quaternion.AngleAxis(bank, Vector3.forward) * visualRestRotation;
            }
        }
    }
}
