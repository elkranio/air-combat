using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Mission-specific launch state. Normal flight remains owned by FlightController.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)] // Before normal flight; E1 session initializes first.
    public sealed class Mission01LaunchSequence : MonoBehaviour, IAirTargetFlightContext
    {
        public enum Phase { Intro, Ready, Takeoff, Flight, Crashed, BoundaryFailed, Landing, Landed }

        [SerializeField] private FlightController flight;
        [SerializeField] private PlayerWeaponTrigger weaponTrigger;
        [SerializeField] private bool skipIntro = true;
        [SerializeField] private CombatHud missionHud;
        [SerializeField] private SurfaceToAirLauncher[] samSites;
        private AircraftRadar samRadar;
        private PlayerHitPoints playerHealth;

#if UNITY_EDITOR
        [Header("Temporary terrain test (Editor only)")]
        [SerializeField] private bool debugAirStart = true;
        [SerializeField] private bool debugNoClip;
        [SerializeField] private bool debugLandingNearAirfield = true;
#endif

        [Header("Terrain crash")]
        [SerializeField, Min(.1f)] private float terrainCrashRadius = 3.5f;

        [Header("Operation boundary (XZ)")]
        [SerializeField] private Rect operationBounds = new Rect(-1000f, -1000f, 3200f, 3200f);
        [SerializeField, Min(1f)] private float boundaryCautionDistance = 250f;
        [SerializeField, Min(1f)] private float boundaryCriticalDistance = 100f;

        [Header("Automatic takeoff (WIP)")]
        [SerializeField, Min(.1f)] private float rollSeconds = 2.2f;
        [SerializeField, Min(.1f)] private float climbSeconds = 2.5f;
        [SerializeField, Min(1f)] private float cruiseAltitude = 78f;
        [SerializeField, Range(0f, 35f)] private float maxPitch = 18f;

        [Header("Automatic landing (WIP, reverse runway heading)")]
        [SerializeField, Min(1f)] private float landingRevealDistance = 550f;
        [SerializeField, Min(1f)] private float landingCorridorNear = 150f;
        [SerializeField, Min(1f)] private float landingCorridorFar = 350f;
        [SerializeField, Range(.25f, 4f)] private float landingCorridorWidthScale = 1f;
        [SerializeField, Range(5f, 60f)] private float landingCaptureHeadingTolerance = 15f;
        [SerializeField, Min(.15f)] private float landingCaptureSeconds = 1f;
        [SerializeField, Min(10f)] private float landingTouchdownOffset = 65f;
        [SerializeField, Min(5f)] private float landingAutoTouchdownSpeed = 65f;

        private ScriptedTakeoff takeoff;
        private ScriptedLanding landing;
        private IPrimaryObjectiveSource primaryObjectives;
        private bool landingTestOnly;
        private Transform aircraft;
        private Transform visual;
        private Afterburner boost;
        private AfterburnerVfx flames;
        private Vector3 runwayPosition;
        private Quaternion runwayRotation, visualRest;
        private bool handoverPending;
        private bool initialized;
        private MissionFlightSafety flightSafety;
        private CombatHitFeedback crashFeedback;
        private MissionBoundaryGuard boundary;
        private E1MapFlightSession flightSession;

        public Phase State { get; private set; } = Phase.Intro;
        internal MissionBoundaryGuard Boundary => boundary;
        internal ScriptedLanding Landing => landing;
        internal bool LandingTestOnly => landingTestOnly;
        public Rect OperationBounds => operationBounds;
        bool IAirTargetFlightContext.FlightActive => State == Phase.Flight;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(FlightController controller, PlayerWeaponTrigger trigger)
        {
            flight = controller;
            weaponTrigger = trigger;
        }

        public void ConfigureHudForAuthoring(CombatHud hud) { missionHud = hud; }
        public void ConfigureSamSitesForAuthoring(SurfaceToAirLauncher[] sites) { samSites = sites; }
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
            takeoff = new ScriptedTakeoff(aircraft, visual, runwayPosition, runwayRotation, visualRest);
            flight.enabled = false;
            weaponTrigger.enabled = false;
            // Keep the authored operation rectangle independent of decorative ground.
            boundary = new MissionBoundaryGuard(transform, operationBounds,
                boundaryCautionDistance, boundaryCriticalDistance);
            flightSafety = new MissionFlightSafety(boundary);
            var landingRunway = Mission01RunwayGeometry.Resolve(transform, runwayPosition, runwayRotation, landingCorridorNear);
            landing = new ScriptedLanding(transform, landingRunway, visualRest,
                operationBounds, landingCorridorNear, landingCorridorFar, landingCorridorWidthScale,
                landingRevealDistance, landingCaptureHeadingTolerance, landingCaptureSeconds, landingTouchdownOffset,
                landingAutoTouchdownSpeed);
            initialized = true;
            State = Phase.Intro;
        }

        private void OnEnable()
        {
            if (initialized && !MissionRetry.Register(this)) enabled = false;
        }

        private void Start()
        {
            // The legacy flight session still configures the campaign player/camera.
            // Keep its reset side effects, but let the mission own the input.
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var session in root.GetComponentsInChildren<E1MapFlightSession>())
                {
                    if (!session.isActiveAndEnabled || !session.Controls(flight)) continue;
                    flightSession = session;
                    break;
                }
                if (flightSession != null) break;
            }
            primaryObjectives = GetComponent<IPrimaryObjectiveSource>();
            Mission01ObjectiveHud.Attach(this);
            Mission01StatusHud.Attach(this);
            samRadar = aircraft.GetComponent<AircraftRadar>();
            playerHealth = aircraft.GetComponent<PlayerHitPoints>();
            if (samSites != null && samSites.Length > 0)
            {
                if (samRadar == null || playerHealth == null)
                    Debug.LogError("Mission01: missing SAM radar receiver or player health.", this);
                else foreach (var site in samSites)
                {
                    if (site == null) continue;
                    samRadar.Register(site);
                    if (!site.IsConfigured) Debug.LogError("Mission01: SAM not configured; re-run Install Tactical Map.", site);
                    site.BindPicketTarget(aircraft, playerHealth, samRadar);
                }
            }
            // Dedicated scene-owned FX, not parented to the aircraft visual.
            var effects = new GameObject("Mission01 / terrain crash FX");
            effects.transform.SetParent(transform, false);
            crashFeedback = effects.AddComponent<CombatHitFeedback>();
            var crashCamera = FindFirstObjectByType<ArcadeCameraFollow>();
            crashFeedback.Bind(crashCamera != null ? crashCamera.GetComponent<Camera>() : null);
            if (missionHud != null)
            {
                // Explicit cannon-only loadout. The empty secondary uses the existing HUD label.
                weaponTrigger.ConfigureDualSlots(null);
                missionHud.BindOperationBoundary(boundary);
                if (missionHud.Marker != null) missionHud.Marker.BindNavigationTarget(ReturnNavigationPoint);
                // Mission-only terrain warning uses the already configured flight camera.
                var follow = FindFirstObjectByType<ArcadeCameraFollow>();
                if (follow != null)
                {
                    var camera = follow.GetComponent<Camera>();
                    TerrainCollisionWarning.Create(missionHud.transform, flight, camera);
                    var threatHud = missionHud.GetComponent<EnemyMissileThreatHud>();
                    if (threatHud == null) threatHud = missionHud.gameObject.AddComponent<EnemyMissileThreatHud>();
                    threatHud.BindSam(camera, aircraft, samSites);
                }
                missionHud.gameObject.SetActive(false);
            }
#if UNITY_EDITOR
            flight.DebugFastTravel = debugAirStart;
            if (debugAirStart)
            {
                StartAirborne();
                return;
            }
#endif
            if (skipIntro) CompleteIntro();
        }

        // A future intro/cutscene can call this when it finishes.
        public void CompleteIntro()
        {
            if (State == Phase.Intro) State = Phase.Ready;
        }

        private void Update()
        {
            if (MissionPause.IsTacticalMapPausedFor(gameObject.scene))
            {
                boundary?.Clear();
                landing?.Clear();
                return;
            }
            var keyboard = Keyboard.current;
            if (Application.isFocused && keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                flightSession?.ResetForMissionRetry();
                ResetToReady();
#if UNITY_EDITOR
                if (debugAirStart) StartAirborne();
#endif
                // Consumers observe this accepted request in their existing update order.
                MissionRetry.MarkRequested(this);
                return;
            }
            // A lethal missile hit uses the same mission-loss/retry path as terrain.
            if (State == Phase.Flight && playerHealth != null && !playerHealth.IsAlive)
            {
                Crash(aircraft.position);
                return;
            }
            if (State == Phase.Ready && Application.isFocused && keyboard != null && keyboard.wKey.wasPressedThisFrame)
            {
                takeoff.ResetTimer();
                handoverPending = false;
                State = Phase.Takeoff;
                // Existing straight twin plumes, independent of the gameplay tank.
                flames.SetManeuverFlames(true);
                return;
            }
            if (State == Phase.Takeoff)
            {
                if (takeoff.Tick(Time.deltaTime, flight.CruiseSpeed, rollSeconds, climbSeconds, cruiseAltitude, maxPitch))
                    handoverPending = true;
            }
            else if (State == Phase.Landing && landing.TickLanding(aircraft, visual, Time.deltaTime))
                State = Phase.Landed;
        }

        private void LateUpdate()
        {
            if (MissionPause.IsTacticalMapPausedFor(gameObject.scene)) return;
            if (State == Phase.Flight)
            {
                // Flight motor has already moved; camera and weapon LateUpdate
                // run later. Resolve safety before attempting landing capture.
                bool checkTerrain = true;
#if UNITY_EDITOR
                checkTerrain = !debugNoClip;
#endif
                var result = flightSafety.Check(aircraft.gameObject.scene, aircraft.position,
                    checkTerrain, terrainCrashRadius, out Vector3 stop);
                if (result == MissionFlightSafety.Result.TerrainImpact) Crash(stop);
                else if (result == MissionFlightSafety.Result.BoundaryExit) FailBoundary(stop);
                else TickLandingApproach();
                return;
            }
            if (State != Phase.Takeoff || !handoverPending) return;
            // Enable after Update so the motor cannot translate twice on this frame.
            EnterFlight();
        }

        internal ObjectiveProgress PrimaryProgress => primaryObjectives != null ? primaryObjectives.PrimaryProgress : default;
        private bool PrimaryComplete => PrimaryProgress.Complete;

        private Vector3? ReturnNavigationPoint()
        {
            if (!isActiveAndEnabled || State != Phase.Flight || !PrimaryComplete || landing == null ||
                (playerHealth != null && !playerHealth.IsAlive)) return null;
            return landing.NavigationPoint;
        }

        private void TickLandingApproach()
        {
            bool primaryComplete = PrimaryComplete;
            bool available = primaryComplete;
#if UNITY_EDITOR
            available |= debugLandingNearAirfield;
#endif
            if (!Application.isFocused || (playerHealth != null && !playerHealth.IsAlive))
            { landing.Clear(); return; }
            if (!landing.TickApproach(aircraft, visual, flight.CurrentSpeed, available, Time.deltaTime)) return;
            landingTestOnly = !primaryComplete;
            State = Phase.Landing;
            boundary.Clear();
            flight.enabled = false;
            weaponTrigger.enabled = false;
            boost.enabled = false;
            flames.SetManeuverFlames(false);
            SetSamArmed(false);
            if (samSites != null) foreach (var site in samSites)
                if (site != null) site.CancelProjectiles();
            // No more player shots after control is committed to landing.
            foreach (var round in FindObjectsByType<BallisticProjectile>(FindObjectsSortMode.None))
                if (round.gameObject.scene == gameObject.scene && round.IsOwnedBy(aircraft))
                { round.enabled = false; Destroy(round.gameObject); }
        }

        private void Crash(Vector3 impact)
        {
            if (State != Phase.Flight) return;
            State = Phase.Crashed;
            landing?.Clear();
            boundary?.Clear();
            SetSamArmed(false);
            aircraft.position = impact;
            flight.enabled = false;
            weaponTrigger.enabled = false;
            boost.enabled = false;
            flames.SetManeuverFlames(false);
            var health = aircraft.GetComponent<PlayerHitPoints>();
            if (health != null && health.IsAlive) health.ApplyDamage(impact, health.Hp);
            visual.gameObject.SetActive(false);
            crashFeedback?.EmitTerrainCrash(impact);
        }

        private void FailBoundary(Vector3 stop)
        {
            if (State != Phase.Flight) return;
            State = Phase.BoundaryFailed;
            landing?.Clear();
            aircraft.position = stop;
            flight.enabled = false;
            weaponTrigger.enabled = false;
            boost.enabled = false;
            flames.SetManeuverFlames(false);
            boundary.Clear();
            SetSamArmed(false);
            // No synthetic crash, damage or explosion. Freeze the failed sortie;
            // the existing R input runs even at timeScale zero.
            if (!MissionPause.TryAcquire(this, MissionPause.Reason.BoundaryFailure))
                Debug.LogError("Mission: could not acquire boundary-failure pause.", this);
        }

        private void ReleaseBoundaryPause()
        {
            MissionPause.Release(this);
        }

#if UNITY_EDITOR
        private void StartAirborne()
        {
            takeoff.PlaceAtEnd(flight.CruiseSpeed, rollSeconds, climbSeconds, cruiseAltitude);
            EnterFlight();
            var camera = FindFirstObjectByType<ArcadeCameraFollow>();
            if (camera != null) camera.SnapToAircraft();
        }
#endif

        private void EnterFlight()
        {
            handoverPending = false;
            visual.localRotation = visualRest;
            flames.SetManeuverFlames(false);
            flight.PrepareCruiseHandover();
            State = Phase.Flight;
            if (flightSafety.Begin(aircraft.position, out Vector3 stop))
            {
                FailBoundary(stop);
                return;
            }
            SetSamArmed(true);
            flight.enabled = true;
            Debug.Log($"Mission01: flight handover at Y{aircraft.position.y:F1}, speed {flight.CurrentSpeed:F1}.", this);
            // The mission HUD opt-in also equips the cannon-only loadout.
            if (missionHud != null)
            {
                weaponTrigger.enabled = true;
                missionHud.gameObject.SetActive(true);
            }
        }

        private void SetSamArmed(bool armed)
        {
            if (samSites != null) foreach (var site in samSites)
                if (site != null) site.SetArmed(armed);
        }

        private void ResetToReady()
        {
            ReleaseBoundaryPause();
            boundary?.Clear();
            landing?.Clear();
            landingTestOnly = false;
            SetSamArmed(false);
            if (samSites != null) foreach (var site in samSites)
                if (site != null) site.ResetEncounter();
            handoverPending = false;
            takeoff.ResetTimer();
            flight.enabled = false;
            weaponTrigger.enabled = false;
            flames.SetManeuverFlames(false);
            crashFeedback?.ResetEffects();
            visual.gameObject.SetActive(true);
            if (missionHud != null) missionHud.gameObject.SetActive(false);
            aircraft.GetComponent<PlayerHitPoints>()?.ResetHealth();
            // A repeat sortie starts with a full tank, as a fresh mission does.
            boost.enabled = false;
            boost.enabled = true;
            aircraft.SetPositionAndRotation(runwayPosition, runwayRotation);
            visual.localRotation = visualRest;
            State = Phase.Ready;
        }

        private void OnDestroy()
        {
            if (missionHud != null && missionHud.Marker != null) missionHud.Marker.BindNavigationTarget(null);
            ReleaseBoundaryPause();
            boundary?.Dispose();
            landing?.Dispose();
            if (samRadar != null && samSites != null) foreach (var site in samSites)
                if (site != null) samRadar.Unregister(site);
        }

        private void OnDisable()
        {
            MissionRetry.Unregister(this);
            ReleaseBoundaryPause();
            boundary?.Clear();
            landing?.Clear();
            SetSamArmed(false);
            if (initialized && flames != null) flames.SetManeuverFlames(false);
#if UNITY_EDITOR
            if (flight != null) flight.DebugFastTravel = false;
#endif
        }

    }
}
