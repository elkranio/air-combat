using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    [DisallowMultipleComponent]
    public sealed class FlightController : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private Afterburner afterburner;

        // PlayerJet only. Sandbox and other legacy scene controllers keep W/S throttle + Space boost.
        [SerializeField] private bool useFixedCruiseWBoost;

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
        public float CruiseSpeed => Mathf.Clamp(cruiseSpeed, minSpeed, maxSpeed);
        public Transform Visual => visual; // Shared with optional flight maneuvers.
#if UNITY_EDITOR
        // Explicit opt-in from Mission01LaunchSequence only; absent from builds.
        public bool DebugFastTravel { get; set; }
#endif
        private AircraftMotor motor;
        private BreakTurn breakTurn;
        private bool variableManeuverability;
        private bool waitForBoostRelease;
        private float tightSpeed, boostTurnMultiplier, tightTurnMultiplier, maneuverResponse;

        // Explicit opt-in to variable maneuverability in the flight session.
        public void ConfigureVariableManeuverability(float slowSpeed, float boostTurn,
            float tightTurn, float response)
        {
            variableManeuverability = true;
            tightSpeed = Mathf.Clamp(slowSpeed, minSpeed, cruiseSpeed);
            boostTurnMultiplier = Mathf.Max(.01f, boostTurn);
            tightTurnMultiplier = Mathf.Max(.01f, tightTurn);
            maneuverResponse = Mathf.Max(.1f, response);
            breakTurn?.CancelBreak();
        }

        // Optional maneuvers; null when a scene does not equip them.
        public void ConfigureBreakTurn(BreakTurn maneuver) { breakTurn = maneuver; }

        private void OnValidate()
        {
            cruiseSpeed = Mathf.Max(minSpeed, cruiseSpeed);
            maxSpeed = Mathf.Max(cruiseSpeed, maxSpeed);
        }

        private void Awake()
        {
            motor = new AircraftMotor(transform, visual, Mathf.Clamp(cruiseSpeed, minSpeed, maxSpeed));
        }

        // Called after a scripted launch, with the visual already back at rest.
        // Re-seed the motor even on a repeated sortie; keep all flight tuning.
        public void PrepareCruiseHandover()
        {
            motor = new AircraftMotor(transform, visual, CruiseSpeed);
            waitForBoostRelease = Keyboard.current != null && Keyboard.current.wKey.isPressed;
        }

        private void Update()
        {
            if (MissionPause.IsTacticalMapPausedFor(gameObject.scene)) return;
            var keyboard = Keyboard.current;
            if (variableManeuverability)
            {
                TickVariableFlight(keyboard, Time.deltaTime);
                return;
            }
            float throttle = 0f;
            float steer = 0f;
            if (keyboard != null)
            {
                if (!useFixedCruiseWBoost)
                    throttle = (keyboard.wKey.isPressed ? 1f : 0f)
                        - (keyboard.sKey.isPressed ? 1f : 0f);
                steer = (keyboard.dKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed ? 1f : 0f);
            }

            if (breakTurn != null && keyboard != null && Application.isFocused)
            {
                int side = keyboard.qKey.wasPressedThisFrame ? -1
                    : keyboard.eKey.wasPressedThisFrame ? 1 : 0;
                if (side != 0) breakTurn.TryBegin(side);
            }

            var targetSpeed = useFixedCruiseWBoost ? cruiseSpeed
                : throttle > 0f ? maxSpeed : throttle < 0f ? minSpeed : cruiseSpeed;
            float actualTurnRate = turnRate;
            float breakSteer = 0f, breakTurnRate = 0f;
            bool breaking = breakTurn != null && breakTurn.TryGetFlightCommand(
                Time.deltaTime, out breakSteer, out breakTurnRate);
            if (breaking) { steer = breakSteer; actualTurnRate = breakTurnRate; }

            // Break turn already paid its configured fuel cost. No extra charge drain or
            // held boost during the maneuver; grant the motor a short powered burst.
            bool boostHeld = keyboard != null && Application.isFocused &&
                (useFixedCruiseWBoost ? keyboard.wKey.isPressed : keyboard.spaceKey.isPressed);
            bool boost = afterburner != null && afterburner.Tick(
                !breaking && boostHeld, Time.deltaTime);
            if (boost || breaking) targetSpeed = Mathf.Max(targetSpeed, afterburner.BoostSpeed);
            float accel = boost || breaking
                ? Mathf.Max(acceleration, afterburner.BoostAcceleration) : acceleration;
            motor.Tick(targetSpeed, steer, accel, deceleration,
                actualTurnRate, maxBank, bankRate, Time.deltaTime);
        }

        private void TickVariableFlight(Keyboard keyboard, float dt)
        {
            bool focused = Application.isFocused && keyboard != null;
            // S takes priority if both are held; fuel is consumed exactly once.
            bool tightHeld = focused && keyboard.sKey.isPressed;
            if (waitForBoostRelease && keyboard != null && !keyboard.wKey.isPressed)
                waitForBoostRelease = false;
            bool boostHeld = focused && !tightHeld && !waitForBoostRelease && keyboard.wKey.isPressed;
            float steer = focused ? (keyboard.dKey.isPressed ? 1f : 0f)
                - (keyboard.aKey.isPressed ? 1f : 0f) : 0f;

            bool powered = afterburner != null && afterburner.TickResource(
                tightHeld || boostHeld, boostHeld, dt);
            float target = cruiseSpeed;
            float yaw = turnRate;
            float accel = maneuverResponse;
            if (powered && tightHeld)
            {
                target = tightSpeed;
                yaw *= tightTurnMultiplier;
            }
            else if (powered && boostHeld)
            {
                target = afterburner.BoostSpeed;
                yaw *= boostTurnMultiplier;
                accel = Mathf.Max(accel, afterburner.BoostAcceleration);
            }
            // The motor still owns smooth speed and bank. Steering is never
            // committed to an angle or locked for a maneuver duration.
            motor.Tick(target, steer, accel, maneuverResponse, yaw, maxBank, bankRate, dt);
#if UNITY_EDITOR
            // Add twice the nominal displacement; no extra steering, fuel or time ticks.
            if (DebugFastTravel && focused && keyboard.spaceKey.isPressed)
                transform.position += transform.forward * (2f * motor.CurrentSpeed * dt);
#endif
        }
    }
}
