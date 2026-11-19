using UnityEngine;

namespace AirCombat
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    public sealed class EnemyFlightController : MonoBehaviour
    {
        public enum FlightState { Cruise, CatchUp }

        [SerializeField] private Transform target;
        [SerializeField] private Transform visual;

        [Header("Distance (horizontal units)")]
        [SerializeField, Min(1f)] private float catchUpDistance = 180f;
        [SerializeField, Min(1f)] private float resumeCruiseDistance = 100f;

        [Header("Speed (units per second)")]
        [SerializeField, Min(0.1f)] private float cruiseSpeed = 40f;
        [SerializeField, Min(0.1f)] private float boostSpeed = 80f;
        [SerializeField, Min(0.1f)] private float acceleration = 35f;
        [SerializeField, Min(0.1f)] private float deceleration = 45f;

        [Header("Steering")]
        [SerializeField, Min(1f)] private float turnRate = 85f;
        [SerializeField, Range(0f, 80f)] private float maxBank = 40f;
        [SerializeField, Min(1f)] private float bankRate = 160f;
        [SerializeField, Range(0f, 1f)] private float cruiseSteering = 0.25f;
        [SerializeField, Min(0.1f)] private float maneuverPeriod = 12f;
        [SerializeField, Min(1f)] private float fullSteerAngle = 30f;

        public FlightState State { get; private set; }
        public float CurrentSpeed => motor != null ? motor.CurrentSpeed : cruiseSpeed;

        private AircraftMotor motor;
        private float cruiseTime;
        private Quaternion visualRestRotation;

        private void OnValidate()
        {
            catchUpDistance = Mathf.Max(2f, catchUpDistance);
            resumeCruiseDistance = Mathf.Clamp(resumeCruiseDistance, 1f, catchUpDistance - 1f);
            boostSpeed = Mathf.Max(cruiseSpeed, boostSpeed);
        }

        private void Awake()
        {
            visualRestRotation = visual != null ? visual.localRotation : Quaternion.identity;
            ResetFlight();
        }

        public void Respawn(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            ResetFlight();
            gameObject.SetActive(true);
        }

        private void ResetFlight()
        {
            State = FlightState.Cruise;
            cruiseTime = 0f;
            if (visual != null) visual.localRotation = visualRestRotation;
            motor = new AircraftMotor(transform, visual, cruiseSpeed);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var toTarget = target != null ? target.position - transform.position : Vector3.zero;
            toTarget.y = 0f;
            var distance = toTarget.magnitude;

            // Hysteresis prevents boost from toggling around one threshold.
            if (target == null || (State == FlightState.CatchUp && distance <= resumeCruiseDistance))
            {
                if (State != FlightState.Cruise)
                    cruiseTime = 0f;
                State = FlightState.Cruise;
            }
            else if (State == FlightState.Cruise && distance >= catchUpDistance)
                State = FlightState.CatchUp;

            float steer;
            float speed;
            if (State == FlightState.CatchUp)
            {
                var headingError = Vector3.SignedAngle(transform.forward, toTarget, Vector3.up);
                steer = Mathf.Clamp(headingError / fullSteerAngle, -1f, 1f);
                speed = boostSpeed;
            }
            else
            {
                // Repeatable gentle turns, no random inputs or instant heading changes.
                cruiseTime += dt;
                steer = Mathf.Sin(cruiseTime * (2f * Mathf.PI / maneuverPeriod)) * cruiseSteering;
                speed = cruiseSpeed;
            }

            motor.Tick(speed, steer, acceleration, deceleration,
                turnRate, maxBank, bankRate, dt);
        }
    }
}
