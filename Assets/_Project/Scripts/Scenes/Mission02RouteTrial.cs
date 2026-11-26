using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // M02 route and convoy timing trial. No deployment or mission objective state yet.
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
        [Header("M02D / Convoy march")]
        [SerializeField] private PicketDeploymentVisual convoyPrefab;
        [SerializeField] private Terrain convoyTerrain;
        [SerializeField] private LineRenderer convoyRoute;
        [SerializeField, Min(16f)] private float convoySpacing = 30f;
        [SerializeField, Min(.1f)] private float convoySpeed = 24f;
        private ConvoyMarchPreview convoy;
        private AircraftRadar radar;
        private ArcadeNursLauncher nurs;
        private float sortieTime, contactTime = -1f, arrivalTime = -1f, clearedTime = -1f;
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

        public Rect OperationBounds => operationBounds;
        public Vector3 PointA => a;
        public Vector3 PointB => b;
        public Vector3 PointC => c;
        public GameObject RouteGuide => guide;
        internal MissionBoundaryGuard Boundary => boundary;

        public string DebugStatus => $"M02 | {phase} | {sortieTime:F1}s";
        internal string ConvoyStatus => convoy != null ? convoy.DebugStatus : "CONVOY UNAVAILABLE";
        internal bool TryGetPicketPosition(int index, out Vector3 position)
        {
            position = default;
            return convoy != null && convoy.TryGetPosition(index, out position);
        }

        private bool BuildConvoy()
        {
            if (convoyPrefab == null || convoyTerrain == null || convoyRoute == null ||
                convoyRoute.positionCount < 2 || radar == null)
            { Debug.LogError("M02D: assign convoy prefab, terrain and A to B line.", this); return false; }
            var points = new Vector3[convoyRoute.positionCount];
            convoyRoute.GetPositions(points);
            for (int i = 0; i < points.Length; i++)
            {
                if (!convoyRoute.useWorldSpace) points[i] = convoyRoute.transform.TransformPoint(points[i]);
                points[i].y = 0f;
                if (i > 0 && (points[i] - points[i - 1]).sqrMagnitude < 1f)
                { Debug.LogError("M02D: duplicate route nodes.", this); return false; }
            }
            var inset = new Rect(operationBounds.xMin + 260f, operationBounds.yMin + 260f,
                operationBounds.width - 520f, operationBounds.height - 520f);
            bool Inside(Vector3 p) => inset.Contains(new Vector2(p.x, p.z));
            Vector3 tail = points[0] - (points[1] - points[0]).normalized *
                (Mathf.Max(16f, convoySpacing) * (ConvoyMarchPreview.Count - 1));
            if (!Inside(tail)) { Debug.LogError("M02D: initial tail outside safe area.", this); return false; }
            foreach (var point in points)
                if (!Inside(point)) { Debug.LogError("M02D: route outside safe area.", this); return false; }
            convoy = new ConvoyMarchPreview(convoyPrefab, convoyTerrain, radar, transform,
                points, convoySpacing, convoySpeed);
            return true;
        }

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
            this.radar = radar;
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
            nurs = player.GetComponentInChildren<ArcadeNursLauncher>(true);
            if (nurs == null || !BuildConvoy())
            { Debug.LogError("M02D: incomplete convoy or NURS binding.", this); enabled = false; return; }
            nurs.gameObject.SetActive(true);
            nurs.Bind(flight, boost, trigger, view);
            trigger.ConfigureDualSlots(nurs);
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
            nurs.ResetMagazine();
            foreach (var gun in aircraft.GetComponentsInChildren<AutocannonWeapon>()) gun.ResetContinuous(Time.time);
            ClearRounds();
            // Rebuild from current Inspector tuning, even on an in-Play R restart.
            convoy?.Dispose(); convoy = null;
            if (!BuildConvoy() || !convoy.Restart())
            { phase = Phase.Failed; failure = "CONVOY UNAVAILABLE"; return; }
            boost.enabled = false; boost.enabled = true;
            flames?.SetManeuverFlames(false);
            aircraft.SetPositionAndRotation(runwayPosition, runwayRotation);
            visual.localRotation = visualRest;
            boundary.Clear(); takeoff.ResetTimer();
            elapsed = sortieTime = 0f; timeA = timeB = timeC = timeAfterC = -1f;
            contactTime = arrivalTime = clearedTime = -1f;
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
            if (phase == Phase.Takeoff || phase == Phase.Flight)
            {
                sortieTime += Time.deltaTime;
                if (convoy != null && convoy.AliveCount > 0)
                {
                    convoy.Tick(Time.deltaTime);
                    if (convoy.ReachedEnd && arrivalTime < 0f) arrivalTime = sortieTime;
                    if (contactTime < 0f)
                        for (int i = 0; i < ConvoyMarchPreview.Count; i++)
                            if (convoy.TryGetPosition(i, out Vector3 p) && Distance(p) <= 250f)
                            { contactTime = sortieTime; break; }
                }
                if (convoy != null && convoy.AliveCount == 0 && clearedTime < 0f) clearedTime = sortieTime;
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
            trigger.enabled = true;
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
            flight.enabled = false; boost.enabled = false; trigger.enabled = false;
            nurs?.ResetMagazine(); ClearRounds();
            flames?.SetManeuverFlames(false);
            boundary.Clear(); failure = reason; phase = Phase.Failed;
        }

        private float Distance(Vector3 p) => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(aircraft.position.x, aircraft.position.z));
        private static string Split(float value) => value < 0f ? "--" : $"{value:F1}s";

        private void OnGUI()
        {
            if (!bound || Event.current.type != EventType.Repaint || MissionPause.IsTacticalMapPausedFor(gameObject.scene)) return;
            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = Color.white } };
                pointStyle = new GUIStyle(textStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 19 };
            }
            GUI.Box(new Rect(12f, 12f, 470f, 192f), GUIContent.none);
            GUI.Label(new Rect(22f, 18f, 315f, 22f), $"M02 / {phase}   {elapsed:F1}s airborne", textStyle);
            GUI.Label(new Rect(22f, 41f, 315f, 22f), $"A {Split(timeA)}   C {Split(timeC)}   B {Split(timeB)}", textStyle);
            GUI.Label(new Rect(22f, 64f, 315f, 22f), $"C -> B {Split(timeAfterC)} | Speed {flight.CurrentSpeed:F0}", textStyle);
            GUI.Label(new Rect(22f, 88f, 315f, 22f), phase == Phase.Failed ? failure + " — R" :
                phase == Phase.Ready ? "W takeoff | TAB map | G guides | R" : "TAB map | G guides | R restart", textStyle);
            GUI.Label(new Rect(22f, 112f, 450f, 22f), ConvoyStatus, textStyle);
            GUI.Label(new Rect(22f, 135f, 450f, 22f), $"Sortie {sortieTime:F1}s | Contact {Split(contactTime)} | Arrival {Split(arrivalTime)}", textStyle);
            GUI.Label(new Rect(22f, 158f, 450f, 22f), $"All Pickets down {Split(clearedTime)} | ETA {(convoy != null && convoy.AliveCount > 0 && convoySpeed > 0f ? convoy.RemainingDistance / convoySpeed : 0f):F1}s", textStyle);
            DrawPoint(a, "A"); DrawPoint(b, "B"); DrawPoint(c, "C");
        }

        private void DrawPoint(Vector3 point, string label)
        {
            Vector3 screen = view.WorldToScreenPoint(point + Vector3.up * 4f);
            if (screen.z <= 0f || screen.x < 24f || screen.x > Screen.width - 24f ||
                screen.y < 24f || screen.y > Screen.height - 24f) return;
            GUI.Label(new Rect(screen.x - 25f, Screen.height - screen.y - 15f, 50f, 30f), label, pointStyle);
        }

        private void ClearRounds()
        {
            if (aircraft == null) return;
            foreach (var round in FindObjectsByType<BallisticProjectile>())
                if (round.IsOwnedBy(aircraft))
                { round.gameObject.SetActive(false); Destroy(round.gameObject); }
        }

        private void OnDestroy() { convoy?.Dispose(); boundary?.Dispose(); }
    }
}
