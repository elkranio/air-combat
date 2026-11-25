using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Mission-specific launch state. Normal flight remains owned by FlightController.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)] // Before normal flight; E1 session initializes first.
    public sealed class Mission01LaunchSequence : MonoBehaviour
    {
        public enum Phase { Intro, Ready, Takeoff, Flight }

        [SerializeField] private FlightController flight;
        [SerializeField] private PlayerWeaponTrigger weaponTrigger;
        [SerializeField] private bool skipIntro = true;
        [SerializeField] private CombatHud missionHud;

        [Header("Automatic takeoff (WIP)")]
        [SerializeField, Min(.1f)] private float rollSeconds = 2.2f;
        [SerializeField, Min(.1f)] private float climbSeconds = 2.5f;
        [SerializeField, Min(1f)] private float cruiseAltitude = 78f;
        [SerializeField, Range(0f, 35f)] private float maxPitch = 18f;

        private Transform aircraft;
        private Transform visual;
        private Afterburner boost;
        private AfterburnerVfx flames;
        private Vector3 runwayPosition;
        private Quaternion runwayRotation, visualRest;
        private double elapsed;
        private bool handoverPending;
        private bool initialized;

        public Phase State { get; private set; } = Phase.Intro;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(FlightController controller, PlayerWeaponTrigger trigger)
        {
            flight = controller;
            weaponTrigger = trigger;
        }

        public void ConfigureHudForAuthoring(CombatHud hud) { missionHud = hud; }
#endif

        private void Awake()
        {
            if (flight == null || weaponTrigger == null)
            {
                Debug.LogError("Mission01 launch: missing player references.", this);
                enabled = false;
                return;
            }
            aircraft = flight.transform;
            visual = flight.Visual;
            boost = aircraft.GetComponent<Afterburner>();
            flames = visual != null ? visual.GetComponent<AfterburnerVfx>() : null;
            if (visual == null || boost == null || flames == null)
            {
                Debug.LogError("Mission01 launch: missing Lynx visual or afterburner.", this);
                enabled = false;
                return;
            }
            runwayPosition = aircraft.position;
            runwayRotation = aircraft.rotation;
            visualRest = visual.localRotation;
            flight.enabled = false;
            weaponTrigger.enabled = false;
            initialized = true;
            State = Phase.Intro;
        }

        private void Start()
        {
            if (missionHud != null)
            {
                // Explicit cannon-only loadout. The empty secondary uses the existing HUD label.
                weaponTrigger.ConfigureDualSlots(null);
                missionHud.gameObject.SetActive(false);
            }
            if (skipIntro) CompleteIntro();
        }

        // A future intro/cutscene can call this when it finishes.
        public void CompleteIntro()
        {
            if (State == Phase.Intro) State = Phase.Ready;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (Application.isFocused && keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                ResetToReady();
                return;
            }
            if (State == Phase.Ready && Application.isFocused && keyboard != null && keyboard.wKey.wasPressedThisFrame)
            {
                elapsed = 0d;
                handoverPending = false;
                State = Phase.Takeoff;
                // Existing straight twin plumes, independent of the gameplay tank.
                flames.SetManeuverFlames(true);
                return;
            }
            if (State == Phase.Takeoff) TickTakeoff(Time.deltaTime);
        }

        private void TickTakeoff(float dt)
        {
            float roll = Mathf.Max(.1f, rollSeconds);
            float climb = Mathf.Max(.1f, climbSeconds);
            // Do not saturate a float timer and then compare it to a separately
            // evaluated sum: 2.2f + 2.5f can round differently across runtimes.
            // Advance in double precision; clamp only the trajectory sample.
            elapsed += Mathf.Max(0f, dt);
            double duration = (double)roll + climb;
            bool finished = elapsed >= duration;
            float speed = flight.CruiseSpeed;
            float t = finished ? roll : Mathf.Min((float)elapsed, roll);
            float airTime = finished ? climb : Mathf.Clamp((float)(elapsed - roll), 0f, climb);
            // Analytic integral of linear acceleration; deterministic at any FPS.
            float distance = .5f * speed * t * t / roll + speed * airTime;
            float u = Mathf.Clamp01(airTime / climb);
            float rise = Mathf.Max(0f, cruiseAltitude - runwayPosition.y) * Mathf.SmoothStep(0f, 1f, u);
            Vector3 forward = runwayRotation * Vector3.forward;
            aircraft.SetPositionAndRotation(runwayPosition + forward * distance + Vector3.up * rise, runwayRotation);
            // Pitch the visual only. Limit pitch near the runway so the tail does not dip through it.
            float pitch = Mathf.Min(maxPitch * Mathf.Sin(Mathf.PI * u), Mathf.Atan2(rise, 7.5f) * Mathf.Rad2Deg);
            visual.localRotation = visualRest * Quaternion.Euler(-Mathf.Max(0f, pitch), 0f, 0f);
            if (finished) handoverPending = true;
        }

        private void LateUpdate()
        {
            if (State != Phase.Takeoff || !handoverPending) return;
            // Enable after Update so the motor cannot translate twice on this frame.
            handoverPending = false;
            visual.localRotation = visualRest;
            flames.SetManeuverFlames(false);
            flight.PrepareCruiseHandover();
            State = Phase.Flight;
            flight.enabled = true;
            Debug.Log($"Mission01: flight handover at Y{aircraft.position.y:F1}, speed {flight.CurrentSpeed:F1}.", this);
            // The mission HUD opt-in also equips the cannon-only loadout.
            if (missionHud != null)
            {
                weaponTrigger.enabled = true;
                missionHud.gameObject.SetActive(true);
            }
        }

        private void ResetToReady()
        {
            handoverPending = false;
            elapsed = 0d;
            flight.enabled = false;
            weaponTrigger.enabled = false;
            flames.SetManeuverFlames(false);
            if (missionHud != null)
            {
                missionHud.gameObject.SetActive(false);
                aircraft.GetComponent<PlayerHitPoints>()?.ResetHealth();
            }
            // A repeat sortie starts with a full tank, as a fresh mission does.
            boost.enabled = false;
            boost.enabled = true;
            aircraft.SetPositionAndRotation(runwayPosition, runwayRotation);
            visual.localRotation = visualRest;
            State = Phase.Ready;
        }

        private void OnDisable()
        {
            if (initialized && flames != null) flames.SetManeuverFlames(false);
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || State == Phase.Flight) return;
            string message = State == Phase.Intro ? "MISSION INTRO" :
                State == Phase.Ready ? "PRESS W TO TAKE OFF" : "TAKEOFF";
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * .025f, 16f, 32f)),
                fontStyle = FontStyle.Bold
            };
            var rect = new Rect(0f, Screen.height * .84f, Screen.width, 48f);
            Color previous = GUI.color;
            GUI.color = Color.white;
            style.normal.textColor = new Color(.05f, .07f, .09f, .9f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), message, style);
            style.normal.textColor = Color.white;
            GUI.Label(rect, message, style);
            GUI.color = previous;
        }
    }
}
