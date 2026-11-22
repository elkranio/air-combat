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
        public Transform Visual => visual; // Used by isolated Weapon Lab break-turn presentation.
        private AircraftMotor motor;
        private WeaponLabBreakTurn breakTurn;

        // Runtime-only Lab injection; null in Sandbox.
        public void ConfigureBreakTurn(WeaponLabBreakTurn maneuver) { breakTurn = maneuver; }

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
            float actualTurnRate = turnRate;
            float breakSteer = 0f, breakTurnRate = 0f;
            bool breaking = breakTurn != null && breakTurn.TryGetFlightCommand(
                Time.deltaTime, out breakSteer, out breakTurnRate);
            if (breaking) { steer = breakSteer; actualTurnRate = breakTurnRate; }

            // Break turn already paid the entire tank. No extra charge drain or
            // Space boost during the maneuver; grant the motor a short powered burst.
            bool boost = afterburner != null && afterburner.Tick(
                !breaking && keyboard != null && Application.isFocused && keyboard.spaceKey.isPressed,
                Time.deltaTime);
            if (boost || breaking) targetSpeed = Mathf.Max(targetSpeed, afterburner.BoostSpeed);
            float accel = boost || breaking
                ? Mathf.Max(acceleration, afterburner.BoostAcceleration) : acceleration;
            motor.Tick(targetSpeed, steer, accel, deceleration,
                actualTurnRate, maxBank, bankRate, Time.deltaTime);
        }
    }
}
