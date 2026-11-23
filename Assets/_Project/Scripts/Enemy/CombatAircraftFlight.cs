using UnityEngine;

namespace AirCombat
{
    // Shared aircraft motor with pilot commands rather than keyboard or AI rules.
    // AIR-01 supplies a passive test pilot; AIR-02 can supply a combat pilot.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    public sealed class CombatAircraftFlight : MonoBehaviour
    {
        [SerializeField] private JetAssembly aircraft;
        [SerializeField] private Afterburner boost;
        [SerializeField] private BreakTurn breakTurn;
        [SerializeField] private float cruiseSpeed = 70f;
        [SerializeField] private float acceleration = 35f;
        [SerializeField] private float deceleration = 45f;
        [SerializeField] private float turnRate = 125f;
        [SerializeField] private float maxBank = 40f;
        [SerializeField] private float bankRate = 160f;
        private AircraftMotor motor;
        private float steer;
        private bool boostRequested;
        public float CurrentSpeed => motor != null ? motor.CurrentSpeed : cruiseSpeed;

        public void SetPilotCommand(float steering, bool wantsBoost)
        {
            steer = Mathf.Clamp(steering, -1f, 1f);
            boostRequested = wantsBoost;
        }

        private void Awake()
        {
            if (aircraft == null || !aircraft.IsConfigured || boost == null || breakTurn == null)
            {
                Debug.LogError("CombatAircraftFlight: missing aircraft, boost or break turn.", this);
                enabled = false;
                return;
            }
            motor = new AircraftMotor(transform, aircraft.VisualRoot, cruiseSpeed);
            breakTurn.Configure(boost, aircraft.VisualRoot);
        }

        private void Update()
        {
            if (motor == null) return;
            float dt = Time.deltaTime;
            float steering = steer, yaw = turnRate;
            bool breaking = breakTurn.TryGetFlightCommand(dt, out float breakSteer, out float breakRate);
            if (breaking) { steering = breakSteer; yaw = breakRate; }
            bool boosting = boost.Tick(!breaking && boostRequested, dt);
            float speed = boosting || breaking ? Mathf.Max(cruiseSpeed, boost.BoostSpeed) : cruiseSpeed;
            float accel = boosting || breaking ? Mathf.Max(acceleration, boost.BoostAcceleration) : acceleration;
            motor.Tick(speed, steering, accel, deceleration, yaw, maxBank, bankRate, dt);
        }
    }
}
