using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // M02 timing trial. Visual deployment only; no SAM arming or objectives yet.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)]
    public sealed class Mission02RouteTrial : MonoBehaviour, IFlightSessionEncounter, IPlayerEngineAudioContext
    {
        [SerializeField] private CombatHud hud;
        [SerializeField] private GameObject guide;
        [SerializeField] private Rect operationBounds = new Rect(-2430f, -1845f, 4860f, 3690f);
        [SerializeField] private Vector3 a, b, c; // c is C1 in the two-target layout.
        [SerializeField] private bool twoPartRoute;
        [SerializeField] private Vector3 c2;
        [SerializeField, Min(1f)] private float arrivalRadius = 75f;
        [SerializeField] private bool startAirborne;
        [SerializeField] private CarryallFlightPreview carryallFlightPreview;
        [SerializeField] private Mission02CargoEvacuation cargoEvacuation;
        [Header("CA06 / stationary firing range (R applies)")]
        [SerializeField] private bool carryallFiringRange;
        private bool rangeAimAtCargo;
        [Header("BD05 / Picket SAM (R applies)")]
        [SerializeField] private bool picketSamEnabled;
        [SerializeField] private SurfaceToAirLauncher picketSamTemplate;
        private bool picketSamActive;
        private EnemyMissileThreatHud samThreatHud;
        [Header("BD04 / Base deployment trial (R applies)")]
        [SerializeField] private bool baseDeploymentTrial;
        [SerializeField] private bool baseTrialHoldAircraft = true;
        [Header("BD06 / Base assault trial (R applies)")]
        [SerializeField] private bool baseAssaultTrial;
        [SerializeField] private Vector3 baseAssaultSpawn = new Vector3(1640f, 78f, 790f);
        private bool assaultTrialActive;
        private bool baseTrialActive;
        private Vector3 depotForward;
        [Header("M02D / Convoy march")]
        [SerializeField] private PicketDeploymentVisual convoyPrefab;
        [SerializeField] private Terrain convoyTerrain;
        [SerializeField] private LineRenderer convoyRoute;
        [SerializeField, Min(16f)] private float convoySpacing = 30f;
        [SerializeField, Min(.1f)] private float convoySpeed = 24f;
        [Header("M02D2 / Warden escort")]
        [SerializeField] private bool escortEnabled = true;
        [SerializeField] private WardenCombat escortPrefab;
        [SerializeField, Min(10f)] private float escortLateralRadius = 55f;
        [SerializeField, Min(10f)] private float escortEndClearance = 45f;
        [SerializeField, Min(1f)] private float escortPatrolSpeed = 36f;
        [SerializeField, Min(0f)] private float escortAlphaAcquireDelay = .15f;
        [SerializeField, Min(0f)] private float escortBravoAcquireDelay = 1.25f;
        private ConvoyMarchPreview convoy;
        private Mission02ConvoyEscort escort;
        private PlayerHitPoints playerHealth;
        private CombatHitFeedback hitFeedback;
        private float escortClearedTime = -1f, allClearedTime = -1f;
        private float firstParkedTime = -1f, deploymentCompleteTime = -1f;
        private bool escortActive; // Snapshot on restart, not a mid-sortie change.
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
        private enum Phase { Ready, Takeoff, Flight, Failed, Range }
        private Phase phase;
        private bool bound, handover;
        private float elapsed, timeA = -1f, timeB = -1f, timeC = -1f, timeAfterC = -1f;
        private float timeC2 = -1f, timeBetweenC = -1f;
        private string failure;
        private GUIStyle textStyle, pointStyle;

        public Rect OperationBounds => operationBounds;
        public Vector3 PointA => a;
        public Vector3 PointB => b;
        public Vector3 PointC => c;
        public Vector3 PointC2 => c2;
        public bool HasSecondPoint => twoPartRoute;
        public void ConfigureTwoPartRoute(Vector3 convoyStart, Vector3 depot, Vector3 first, Vector3 second)
        { a = convoyStart; b = depot; c = first; c2 = second; twoPartRoute = true; }
        public GameObject RouteGuide => guide;
        internal MissionBoundaryGuard Boundary => boundary;

        FlightController IPlayerEngineAudioContext.EngineAudioFlight => flight;
        PlayerEnginePhase IPlayerEngineAudioContext.EngineAudioPhase => !bound || !isActiveAndEnabled ? PlayerEnginePhase.Silent : phase switch
        {
            Phase.Ready => PlayerEnginePhase.Ready,
            Phase.Takeoff => PlayerEnginePhase.Takeoff,
            Phase.Flight => PlayerEnginePhase.Flight,
            _ => PlayerEnginePhase.Silent
        };

        public string DebugStatus => $"M02 | {phase} | {sortieTime:F1}s";
        internal string ConvoyStatus => convoy != null ? convoy.DebugStatus : "CONVOY UNAVAILABLE";
        internal bool TryGetPicketPosition(int index, out Vector3 position)
        {
            position = default;
            return convoy != null && convoy.TryGetPosition(index, out position);
        }

        internal string EscortStatus => escort != null ? $"Wardens {escort.AliveCount}/2" : !escortActive ? "ESCORT OFF" : "ESCORT UNAVAILABLE";
        internal string DeploymentStatus => convoy != null ? $"Deployed {convoy.DeployedCount}/{convoy.AliveCount} | First {Split(firstParkedTime)} | All {Split(deploymentCompleteTime)}" : "DEPLOYMENT UNAVAILABLE";
        internal bool TryGetWardenPosition(int index, out Vector3 position)
        {
            position = default;
            return escort != null && escort.TryGetPosition(index, out position);
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
            depotForward = (points[points.Length-1]-points[points.Length-2]).normalized;
            convoy = new ConvoyMarchPreview(convoyPrefab, convoyTerrain, radar, transform,
                points, convoySpacing, convoySpeed);
            if (!convoy.EnableDepotDeployment(inset))
            {
                Debug.LogError("M02D3: depot needs a clear final segment and parking area inside safe bounds.", this);
                convoy.Dispose(); convoy = null;
                return false;
            }
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
            playerHealth = health;
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
            if (nurs == null || playerHealth == null || (escortEnabled && escortPrefab == null) || !BuildConvoy())
            { Debug.LogError("M02D: incomplete convoy or NURS binding.", this); enabled = false; return; }
            nurs.gameObject.SetActive(true);
            nurs.Bind(flight, boost, trigger, view);
            trigger.ConfigureDualSlots(nurs);
            var team = player.GetComponent<AircraftTeam>();
            if (team != null) team.SetSide(AircraftSide.Player);
            playerHealth.InstallHitbox();
            hitFeedback = gameObject.AddComponent<CombatHitFeedback>();
            hitFeedback.Bind(view);
            hitFeedback.Track(playerHealth, true);
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
            hitFeedback.ResetEffects();
            cargoEvacuation?.Clear();
            // Reset only targets owned by this scene, including the BD02 preview instances.
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var building in root.GetComponentsInChildren<DepotBuildingTarget>(true))
                    building.ResetTarget();
            escort?.Dispose(); escort = null;
            playerHealth.ResetHealth();
            rangeAimAtCargo = false;
            nurs.ClearRangeAim();
            // Rebuild from current Inspector tuning, even on an in-Play R restart.
            if (samThreatHud != null) samThreatHud.BindSam(view, aircraft, null);
            convoy?.Dispose(); convoy = null;
            picketSamActive = picketSamEnabled && !carryallFiringRange;
            assaultTrialActive = baseAssaultTrial && !carryallFiringRange;
            baseTrialActive = baseDeploymentTrial && !carryallFiringRange && !assaultTrialActive;
            bool cargoInstalled = cargoEvacuation != null && cargoEvacuation.isActiveAndEnabled;
            if (carryallFlightPreview != null)
                carryallFlightPreview.gameObject.SetActive(carryallFiringRange || !cargoInstalled);
            if (carryallFiringRange)
            {
                EnterCarryallRange();
                return;
            }
            if (!BuildConvoy() || !convoy.Restart(baseTrialActive ? Mathf.Max(0f,convoy.RouteLength-230f) : 0f, assaultTrialActive))
            { phase = Phase.Failed; failure = "CONVOY UNAVAILABLE"; return; }
            if (picketSamActive)
            {
                if (!convoy.EnableSam(picketSamTemplate, aircraft, playerHealth))
                {
                    convoy.Dispose(); convoy = null;
                    phase = Phase.Failed; failure = "BD05 SAM UNAVAILABLE";
                    Debug.LogError("BD05: assign the stationary Picket SAM template and check mobile rail hierarchy.", this);
                    return;
                }
                if (hud != null)
                {
                    samThreatHud = hud.GetComponent<EnemyMissileThreatHud>();
                    if (samThreatHud == null) samThreatHud = hud.gameObject.AddComponent<EnemyMissileThreatHud>();
                    samThreatHud.BindSam(view, aircraft, convoy.SamSites);
                }
            }
            escortActive = escortEnabled && !baseTrialActive;
            if (escortActive)
            {
                if (escortPrefab == null)
                { phase = Phase.Failed; failure = "ESCORT UNAVAILABLE"; return; }
                escort = new Mission02ConvoyEscort(convoy, escortPrefab, radar, transform,
                    playerHealth, flight, hitFeedback, escortLateralRadius, escortEndClearance,
                    escortPatrolSpeed, escortAlphaAcquireDelay, escortBravoAcquireDelay);
                if (!escort.Spawn())
                { phase = Phase.Failed; failure = "ESCORT UNAVAILABLE"; return; }
            }
            boost.enabled = false; boost.enabled = true;
            flames?.SetManeuverFlames(false);
            aircraft.SetPositionAndRotation(runwayPosition, runwayRotation);
            visual.localRotation = visualRest;
            boundary.Clear(); takeoff.ResetTimer();
            elapsed = sortieTime = 0f; timeA = timeB = timeC = timeAfterC = -1f;
            timeC2 = timeBetweenC = -1f;
            contactTime = arrivalTime = clearedTime = -1f;
            escortClearedTime = allClearedTime = -1f;
            firstParkedTime = deploymentCompleteTime = -1f;
            failure = null; handover = false; phase = Phase.Ready;
            if (!baseTrialActive && !assaultTrialActive && cargoInstalled && !cargoEvacuation.Restart(radar))
            { phase = Phase.Failed; failure = "CA07 CARGO UNAVAILABLE"; return; }
            if (assaultTrialActive)
            {
                Vector3 p = baseAssaultSpawn; p.y = 78f;
                Vector3 approach = Vector3.ProjectOnPlane(b - p, Vector3.up);
                bool safe = boundary.Distance(p) > 250f && approach.sqrMagnitude > 1f;
                if (picketSamActive)
                    foreach (var site in convoy.SamSites)
                        if (site != null && Vector3.ProjectOnPlane(site.RadarPosition - p, Vector3.up).magnitude < site.RadarThreatRadius + 40f)
                            safe = false;
                if (!safe)
                {
                    phase = Phase.Failed; failure = "BD06 UNSAFE ASSAULT SPAWN";
                    Debug.LogError("BD06: move Base Assault Spawn outside SAM ranges plus 40 units and boundary warning.", this);
                    return;
                }
                aircraft.SetPositionAndRotation(p, Quaternion.LookRotation(approach, Vector3.up));
                Physics.SyncTransforms();
                EnterFlight();
                if (phase == Phase.Flight && picketSamActive) convoy.TickSamArming();
                arrivalTime = firstParkedTime = deploymentCompleteTime = 0f;
            }
            else if (cargoInstalled && cargoEvacuation.TryTestPose(out Vector3 testGround, out Quaternion testHeading))
            {
                flight.PrepareCruiseHandover();
                nurs.PositionForRange(testGround, testHeading, 78f);
                Physics.SyncTransforms();
                EnterFlight();
                if (phase == Phase.Flight && cargoEvacuation.HoldTestAircraft) flight.enabled = false;
            }
            else if (baseTrialActive)
            {
                Vector3 p = b - depotForward*55f;
                p.y = 78f;
                aircraft.SetPositionAndRotation(p,Quaternion.LookRotation(depotForward,Vector3.up));
                EnterFlight();
                if (baseTrialHoldAircraft) flight.enabled = false; // weapons and convoy still tick
            }
            else if (startAirborne)
            {
                Vector3 p = runwayPosition + runwayRotation * Vector3.forward * (flight.CruiseSpeed * 3.6f);
                p.y = 78f; aircraft.position = p;
                EnterFlight();
            }
            if (carryallFlightPreview != null && carryallFlightPreview.gameObject.activeInHierarchy)
                carryallFlightPreview.RestartPreview();
            view.GetComponent<ArcadeCameraFollow>()?.SnapToAircraft();
        }

        private void EnterCarryallRange()
        {
            escortActive = false;
            boost.enabled = false;
            flames?.SetManeuverFlames(false);
            boundary.Clear();
            takeoff.ResetTimer();
            visual.localRotation = visualRest;
            failure = null; handover = false;
            if (carryallFlightPreview == null || !carryallFlightPreview.RangeReady)
            {
                phase = Phase.Failed;
                failure = "INSTALL CA01 FLIGHT PREVIEW FIRST";
                Debug.LogError("CA06: install CA01 Flight Preview before enabling the range.", this);
                return;
            }
            carryallFlightPreview.RestartRange();
            flight.PrepareCruiseHandover(); // Retain nominal cruise-speed launch inheritance.
            nurs.PositionForRange(carryallFlightPreview.RangeGround, carryallFlightPreview.RangeHeading,
                carryallFlightPreview.RangeAltitude);
            Physics.SyncTransforms();
            phase = Phase.Range;
            trigger.enabled = true;
            view.GetComponent<ArcadeCameraFollow>()?.SnapToAircraft();
        }

        public void SetAltitude(float altitude) { } // Fixed Y78 for comparable flight tests.

        private void Update()
        {
            if (!bound || MissionPause.IsTacticalMapPausedFor(gameObject.scene)) return;
            var keyboard = Keyboard.current;
            if (phase == Phase.Range)
            {
                if (Application.isFocused && keyboard != null && keyboard.nKey.wasPressedThisFrame)
                    rangeAimAtCargo = !rangeAimAtCargo;
                if (rangeAimAtCargo && carryallFlightPreview.RangeCargoAlive)
                    nurs.AimRangeAt(carryallFlightPreview.RangeCargoAim);
                else nurs.ClearRangeAim();
                return;
            }
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
                cargoEvacuation?.Tick(Time.deltaTime, sortieTime);
                if (convoy != null && convoy.AliveCount > 0)
                {
                    convoy.Tick(Time.deltaTime);
                    if (convoy.ReachedEnd && arrivalTime < 0f) arrivalTime = sortieTime;
                    if (convoy.ParkedCount > 0 && firstParkedTime < 0f) firstParkedTime = sortieTime;
                    if (convoy.AllSurvivorsDeployed && deploymentCompleteTime < 0f) deploymentCompleteTime = sortieTime;
                    if (contactTime < 0f)
                        for (int i = 0; i < ConvoyMarchPreview.Count; i++)
                            if (convoy.TryGetPosition(i, out Vector3 p) && Distance(p) <= 250f)
                            { contactTime = sortieTime; break; }
                }
                escort?.TickMovement(Time.deltaTime);
                if (convoy != null && convoy.AliveCount == 0 && clearedTime < 0f) clearedTime = sortieTime;
                if (escort != null && escort.AliveCount == 0 && escortClearedTime < 0f) escortClearedTime = sortieTime;
                if (clearedTime >= 0f && (!escortActive || escortClearedTime >= 0f) && allClearedTime < 0f) allClearedTime = sortieTime;
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
            if (!playerHealth.IsAlive)
            {
                hitFeedback.SpawnWreck(playerHealth, flight.CurrentSpeed);
                Fail(aircraft.position, "AIRCRAFT LOST"); return;
            }
            var result = safety.Check(gameObject.scene, aircraft.position, true, 3.5f, out Vector3 stop);
            if (result != MissionFlightSafety.Result.Clear)
            { Fail(stop, result == MissionFlightSafety.Result.TerrainImpact ? "TERRAIN IMPACT" : "BOUNDARY EXIT"); return; }
            if (picketSamActive) convoy?.TickSamArming();
            escort?.TickFire(Time.time, Time.deltaTime);
            elapsed += Time.deltaTime;
            if (timeA < 0f && Distance(a) <= arrivalRadius) timeA = sortieTime;
            if (timeC < 0f && Distance(c) <= arrivalRadius) timeC = sortieTime;
            if (twoPartRoute && timeC2 < 0f && Distance(c2) <= arrivalRadius)
            {
                timeC2 = sortieTime;
                if (timeC >= 0f) timeBetweenC = timeC2 - timeC;
            }
            if (Distance(b) <= arrivalRadius)
            {
                if (timeB < 0f) timeB = sortieTime;
                float lastOptional = twoPartRoute ? timeC2 : timeC;
                if (lastOptional >= 0f && timeAfterC < 0f) timeAfterC = sortieTime - lastOptional;
            }
        }

        private void Fail(Vector3 stop, string reason)
        {
            aircraft.position = stop;
            flight.enabled = false; boost.enabled = false; trigger.enabled = false;
            nurs?.ResetMagazine(); ClearRounds();
            escort?.Stop();
            convoy?.StopSam();
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
            if (phase == Phase.Range)
            {
                GUI.Box(new Rect(12f, 12f, 690f, 87f), GUIContent.none);
                GUI.Label(new Rect(22f, 18f, 670f, 22f), "CA06 / " + carryallFlightPreview.RangeStatus, textStyle);
                GUI.Label(new Rect(22f, 41f, 670f, 22f), "LMB AC01 | RMB NURS | J descend | P hold/resume | N aim mode | R reset", textStyle);
                GUI.Label(new Rect(22f, 64f, 670f, 22f), $"NURS pitch {nurs.RangePitch:F1} deg | {(rangeAimAtCargo ? "LAB AIM" : "NORMAL")}", textStyle);
                return;
            }
            GUI.Box(new Rect(12f, 12f, 490f, 239f), GUIContent.none);
            GUI.Label(new Rect(22f, 18f, 470f, 22f), assaultTrialActive ? $"BD06 BASE ASSAULT / {elapsed:F1}s | R retry" : baseTrialActive ? $"BD04 BASE TRIAL / {elapsed:F1}s | R retry" : $"M02 / {phase}   {elapsed:F1}s airborne", textStyle);
            GUI.Label(new Rect(22f, 41f, 470f, 22f), twoPartRoute ?
                $"From W: A {Split(timeA)} | C1 {Split(timeC)} | C2 {Split(timeC2)} | B {Split(timeB)}" :
                $"From W: A {Split(timeA)} | C {Split(timeC)} | B {Split(timeB)}", textStyle);
            GUI.Label(new Rect(22f, 64f, 470f, 22f), twoPartRoute ?
                $"C1 -> C2 {Split(timeBetweenC)} | C2 -> B {Split(timeAfterC)} | Speed {flight.CurrentSpeed:F0}" :
                $"C -> B {Split(timeAfterC)} | Speed {flight.CurrentSpeed:F0}", textStyle);
            GUI.Label(new Rect(22f, 88f, 315f, 22f), phase == Phase.Failed ? failure + " — R" :
                phase == Phase.Ready ? "W takeoff | TAB map | G guides | R" : "TAB map | G guides | R restart", textStyle);
            GUI.Label(new Rect(22f, 112f, 450f, 22f), ConvoyStatus, textStyle);
            GUI.Label(new Rect(22f, 135f, 450f, 22f), $"Sortie {sortieTime:F1}s | Contact {Split(contactTime)} | B reached {Split(arrivalTime)}", textStyle);
            GUI.Label(new Rect(22f, 158f, 450f, 22f), $"All Pickets down {Split(clearedTime)} | B ETA {(convoy != null && convoy.AliveCount > 0 && convoySpeed > 0f ? convoy.RemainingDistance / convoySpeed : 0f):F1}s", textStyle);
            GUI.Label(new Rect(22f, 181f, 470f, 22f), $"{EscortStatus} | Down {Split(escortClearedTime)} | All down {Split(allClearedTime)}", textStyle);
            GUI.Label(new Rect(22f, 204f, 470f, 22f), DeploymentStatus, textStyle);
            if (picketSamActive && convoy != null)
            {
                GUI.Box(new Rect(12f, 255f, 490f, 29f), GUIContent.none);
                GUI.Label(new Rect(22f, 258f, 470f, 22f), convoy.SamStatus, textStyle);
            }
            if (cargoEvacuation != null && cargoEvacuation.Active)
            {
                float y = picketSamActive && convoy != null ? 289f : 255f;
                GUI.Box(new Rect(12f, y, 620f, 76f), GUIContent.none);
                GUI.Label(new Rect(22f, y + 4f, 600f, 22f), cargoEvacuation.FirstStatus(sortieTime), textStyle);
                GUI.Label(new Rect(22f, y + 27f, 600f, 22f), cargoEvacuation.SecondStatus(sortieTime), textStyle);
                GUI.Label(new Rect(22f, y + 50f, 600f, 22f), cargoEvacuation.Status, textStyle);
            }
            DrawPoint(a, "A"); DrawPoint(b, "B"); DrawPoint(c, twoPartRoute ? "C1" : "C");
            if (twoPartRoute) DrawPoint(c2, "C2");
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

        private void OnDestroy()
        {
            escort?.Dispose();
            convoy?.Dispose();
            boundary?.Dispose();
        }
    }
}
