using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    [DisallowMultipleComponent]
    public sealed class FlightController : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private Afterburner afterburner;

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

        public float CurrentSpeed => motor != null ? motor.CurrentSpeed : cruiseSpeed;
        private AircraftMotor motor;

        private void OnValidate()
        {
            cruiseSpeed = Mathf.Max(minSpeed, cruiseSpeed);
            maxSpeed = Mathf.Max(cruiseSpeed, maxSpeed);
        }

        private void Awake()
        {
            motor = new AircraftMotor(transform, visual, Mathf.Clamp(cruiseSpeed, minSpeed, maxSpeed));
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
            bool boost = afterburner != null && afterburner.Tick(
                keyboard != null && Application.isFocused && keyboard.spaceKey.isPressed, Time.deltaTime);
            if (boost) targetSpeed = Mathf.Max(targetSpeed, afterburner.BoostSpeed);
            float accel = boost ? Mathf.Max(acceleration, afterburner.BoostAcceleration) : acceleration;
            motor.Tick(targetSpeed, steer, accel, deceleration,
                turnRate, maxBank, bankRate, Time.deltaTime);
        }
    }
}
