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

        [Header("Steering")]
        [SerializeField, Min(1f)] private float turnRate = 85f;
        [SerializeField, Range(.1f, 1f)] private float precisionTurnMultiplier = .4f;
        [SerializeField, Range(0f, 80f)] private float maxBank = 40f;
        [SerializeField, Min(1f)] private float bankRate = 160f;

        public float CurrentSpeed => motor != null ? motor.CurrentSpeed : cruiseSpeed;
        public float CruiseSpeed => Mathf.Clamp(cruiseSpeed, minSpeed, maxSpeed);
        public Transform Visual => visual; // Shared with optional flight maneuvers.
        private MissionCheats cheats;
        private AircraftMotor motor;
        private bool waitForBoostRelease;

        [Header("Powered maneuverability")]
        [SerializeField, Min(.1f)] private float tightSpeed = 35f;
        [SerializeField, Range(.1f, 1f)] private float boostTurnMultiplier = .6f;
        [SerializeField, Min(1f)] private float tightTurnMultiplier = 2f;
        [SerializeField, Min(.1f)] private float maneuverResponse = 140f;

        private void OnValidate()
        {
            cruiseSpeed = Mathf.Max(minSpeed, cruiseSpeed);
            maxSpeed = Mathf.Max(cruiseSpeed, maxSpeed);
            tightSpeed = Mathf.Clamp(tightSpeed, minSpeed, cruiseSpeed);
        }

        private void Awake()
        {
            cheats = GetComponent<MissionCheats>();
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
            TickVariableFlight(keyboard, Time.deltaTime);
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
                target = Mathf.Clamp(tightSpeed, minSpeed, cruiseSpeed);
                yaw *= tightTurnMultiplier;
            }
            else if (powered && boostHeld)
            {
                target = afterburner.BoostSpeed;
                yaw *= boostTurnMultiplier;
                accel = Mathf.Max(accel, afterburner.BoostAcceleration);
            }
            // Shift modifies only the resolved yaw rate, including W/S flight modes.
            if (focused && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
                yaw *= Mathf.Clamp(precisionTurnMultiplier, .1f, 1f);

            // The motor still owns smooth speed and bank. Steering is never
            // committed to an angle or locked for a maneuver duration.
            motor.Tick(target, steer, accel, maneuverResponse, yaw, maxBank, bankRate, dt);
#if UNITY_EDITOR
            // Two additional displacements = x3 travel, without accelerating simulation.
            if (cheats != null && cheats.TripleSpeed && focused && keyboard.spaceKey.isPressed)
                transform.position += transform.forward * (2f * motor.CurrentSpeed * dt);
#endif
        }
    }
}
