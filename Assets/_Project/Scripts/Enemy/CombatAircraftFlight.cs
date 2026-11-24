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
        private bool boostRequested, tightRequested;
        private bool variableManeuverability;
        public float CurrentSpeed => motor != null ? motor.CurrentSpeed : cruiseSpeed;

        // Production Tiger opt-in. Passive and legacy pilots keep their old motor.
        public void EnableVariableManeuverability()
        {
            variableManeuverability = true;
            breakTurn?.CancelBreak();
        }

        public void SetPilotCommand(float steering, bool wantsBoost, bool wantsTight = false)
        {
            steer = Mathf.Clamp(steering, -1f, 1f);
            boostRequested = wantsBoost;
            tightRequested = wantsTight;
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
            if (variableManeuverability)
            {
                // Same shared-resource rules and relative steering modifiers as
                // the player. Keep the Tiger's authored cruise/boost/yaw values.
                bool tight = tightRequested;
                bool thrust = !tight && boostRequested;
                bool powered = boost.TickResource(tight || thrust, thrust, dt);
                float targetSpeed = cruiseSpeed;
                float actualYaw = turnRate;
                float response = 140f;
                if (powered && tight)
                {
                    targetSpeed = cruiseSpeed * .5f;
                    actualYaw *= 2f;
                }
                else if (powered && thrust)
                {
                    targetSpeed = Mathf.Max(cruiseSpeed, boost.BoostSpeed);
                    actualYaw *= .6f;
                    response = Mathf.Max(response, boost.BoostAcceleration);
                }
                motor.Tick(targetSpeed, steer, response, 140f, actualYaw, maxBank, bankRate, dt);
                return;
            }
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
