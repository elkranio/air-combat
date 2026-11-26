using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Temporary route-only encounter. Uses the existing flight session, takeoff,
    // terrain sweep and operation boundary. No mission objectives or combat AI.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)]
    public sealed class Mission02RouteTrial : MonoBehaviour, IFlightSessionEncounter
    {
        [SerializeField] private CombatHud hud;
        [SerializeField] private GameObject guide;
        [SerializeField] private Rect operationBounds = new Rect(-2430f, -1845f, 4860f, 3690f);
        [SerializeField] private Vector3 a, b, c;
        [SerializeField, Min(1f)] private float arrivalRadius = 75f;
        [SerializeField] private bool startAirborne;
        private FlightController flight;
        private PlayerWeaponTrigger trigger;
        private Afterburner boost;
        private AfterburnerVfx flames;
        private Transform aircraft, visual;
        private Camera view;
        private Vector3 runwayPosition;
        private Quaternion runwayRotation, visualRest;
        private ScriptedTakeoff takeoff;
        private MissionBoundaryGuard boundary;
        private MissionFlightSafety safety;
        private enum Phase { Ready, Takeoff, Flight, Failed }
        private Phase phase;
        private bool bound, handover;
        private float elapsed, timeA = -1f, timeB = -1f, timeC = -1f, timeAfterC = -1f;
        private string failure;
        private GUIStyle textStyle, pointStyle;

        public string DebugStatus => $"M02 ROUTE | {phase} | {elapsed:F1}s";

        public void Configure(CombatHud display, GameObject routeGuide, Rect bounds,
            Vector3 convoy, Vector3 depot, Vector3 extra)
        {
            hud = display; guide = routeGuide; operationBounds = bounds;
            a = convoy; b = depot; c = extra;
        }

        public void Bind(JetAssembly player, Camera camera, AircraftRadar radar,
            PlayerHitPoints health, float halfExtent)
        {
            aircraft = player.transform;
            flight = player.GetComponent<FlightController>();
            trigger = player.GetComponent<PlayerWeaponTrigger>();
            boost = player.GetComponent<Afterburner>();
            visual = flight != null ? flight.Visual : null;
            if (flight == null || trigger == null || boost == null || visual == null || camera == null)
            { Debug.LogError("M02 route trial: incomplete player/camera.", this); enabled = false; return; }
            view = camera;
            flames = visual.GetComponent<AfterburnerVfx>();
            runwayPosition = aircraft.position; runwayRotation = aircraft.rotation;
            visualRest = visual.localRotation;
            takeoff = new ScriptedTakeoff(aircraft, visual, runwayPosition, runwayRotation, visualRest);
            boundary = new MissionBoundaryGuard(transform, operationBounds, 250f, 100f);
            safety = new MissionFlightSafety(boundary);
            flight.enabled = false; trigger.enabled = false;
            bound = true;
        }

        private void Start()
        {
            if (!bound) return;
            if (hud != null)
            {
                hud.BindOperationBoundary(boundary);
                TerrainCollisionWarning.Create(hud.transform, flight, view);
            }
            Restart();
        }

        public void Restart()
        {
            if (!bound) return;
            flight.enabled = false; trigger.enabled = false;
            boost.enabled = false; boost.enabled = true;
            flames?.SetManeuverFlames(false);
            aircraft.SetPositionAndRotation(runwayPosition, runwayRotation);
            visual.localRotation = visualRest;
            boundary.Clear(); takeoff.ResetTimer();
            elapsed = 0f; timeA = timeB = timeC = timeAfterC = -1f;
            failure = null; handover = false; phase = Phase.Ready;
            if (startAirborne)
            {
                Vector3 p = runwayPosition + runwayRotation * Vector3.forward * (flight.CruiseSpeed * 3.6f);
                p.y = 78f; aircraft.position = p;
                EnterFlight();
            }
            view.GetComponent<ArcadeCameraFollow>()?.SnapToAircraft();
        }

        public void SetAltitude(float altitude) { } // Fixed Y78 for comparable flight tests.

        private void Update()
        {
            if (!bound || MissionPause.IsTacticalMapPausedFor(gameObject.scene)) return;
            var keyboard = Keyboard.current;
            if (Application.isFocused && keyboard != null)
            {
                if (keyboard.gKey.wasPressedThisFrame && guide != null) guide.SetActive(!guide.activeSelf);
                // FlightSession handles R first; do not immediately begin moving again on this frame.
                if (keyboard.rKey.wasPressedThisFrame) return;
                if (phase == Phase.Ready && keyboard.wKey.wasPressedThisFrame)
                {
                    phase = Phase.Takeoff;
                    flames?.SetManeuverFlames(true);
                    return;
                }
            }
            if (phase == Phase.Takeoff)
                handover = takeoff.Tick(Time.deltaTime, flight.CruiseSpeed, 2.2f, 2.5f, 78f, 18f);
        }

        private void EnterFlight()
        {
            flames?.SetManeuverFlames(false);
            visual.localRotation = visualRest;
            flight.PrepareCruiseHandover();
            if (safety.Begin(aircraft.position, out Vector3 stop)) { Fail(stop, "BOUNDARY EXIT"); return; }
            phase = Phase.Flight;
            flight.enabled = true;
            // No weapons/targets in this route-only scene.
            trigger.enabled = false;
        }

        private void LateUpdate()
        {
            if (!bound || MissionPause.IsTacticalMapPausedFor(gameObject.scene)) return;
            if (phase == Phase.Takeoff && handover) { handover = false; EnterFlight(); return; }
            if (phase != Phase.Flight) return;
            var result = safety.Check(gameObject.scene, aircraft.position, true, 3.5f, out Vector3 stop);
            if (result != MissionFlightSafety.Result.Clear)
            { Fail(stop, result == MissionFlightSafety.Result.TerrainImpact ? "TERRAIN IMPACT" : "BOUNDARY EXIT"); return; }
            elapsed += Time.deltaTime;
            if (timeA < 0f && Distance(a) <= arrivalRadius) timeA = elapsed;
            if (timeC < 0f && Distance(c) <= arrivalRadius) timeC = elapsed;
            if (Distance(b) <= arrivalRadius)
            {
                if (timeB < 0f) timeB = elapsed;
                if (timeC >= 0f && timeAfterC < 0f) timeAfterC = elapsed - timeC;
            }
        }

        private void Fail(Vector3 stop, string reason)
        {
            aircraft.position = stop;
            flight.enabled = false; boost.enabled = false;
            flames?.SetManeuverFlames(false);
            boundary.Clear(); failure = reason; phase = Phase.Failed;
        }

        private float Distance(Vector3 p) => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(aircraft.position.x, aircraft.position.z));
        private static string Split(float value) => value < 0f ? "--" : $"{value:F1}s";

        private void OnGUI()
        {
            if (!bound || Event.current.type != EventType.Repaint) return;
            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = Color.white } };
                pointStyle = new GUIStyle(textStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 19 };
            }
            GUI.Box(new Rect(12f, 12f, 330f, 120f), GUIContent.none);
            GUI.Label(new Rect(22f, 18f, 315f, 22f), $"M02 / {phase}   {elapsed:F1}s airborne", textStyle);
            GUI.Label(new Rect(22f, 41f, 315f, 22f), $"A {Split(timeA)}   C {Split(timeC)}   B {Split(timeB)}", textStyle);
            GUI.Label(new Rect(22f, 64f, 315f, 22f), $"C -> B {Split(timeAfterC)} | Speed {flight.CurrentSpeed:F0}", textStyle);
            GUI.Label(new Rect(22f, 88f, 315f, 22f), phase == Phase.Failed ? failure + " — R" :
                phase == Phase.Ready ? "W takeoff | G guides | R restart" : "G guides | R restart", textStyle);
            DrawPoint(a, "A"); DrawPoint(b, "B"); DrawPoint(c, "C");
        }

        private void DrawPoint(Vector3 point, string label)
        {
            Vector3 screen = view.WorldToScreenPoint(point + Vector3.up * 4f);
            if (screen.z <= 0f || screen.x < 24f || screen.x > Screen.width - 24f ||
                screen.y < 24f || screen.y > Screen.height - 24f) return;
            GUI.Label(new Rect(screen.x - 25f, Screen.height - screen.y - 15f, 50f, 30f), label, pointStyle);
        }

        private void OnDestroy() { boundary?.Dispose(); }
    }
}
