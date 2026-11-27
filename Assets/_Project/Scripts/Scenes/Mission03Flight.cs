using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // M03 foundation. Only flight lifecycle and shared aircraft systems live here;
    // Sparrow waves, interceptor sorties and objectives will be separate owners.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)]
    public sealed class Mission03Flight : MonoBehaviour, IFlightSessionEncounter, IPlayerEngineAudioContext
    {
        [SerializeField] private CombatHud hud;
        [SerializeField] private Renderer ground;
        [SerializeField] private Transform runway;
        [SerializeField] private MissileProjectile missilePrefab;
        [SerializeField] private Rect operationBounds = new Rect(-1500f, -1200f, 3000f, 2400f);
        [SerializeField, Min(1f)] private float cruiseAltitude = 78f;

        public Renderer MapGround => ground;
        public Transform MapRunway => runway;
        public Rect OperationBounds => operationBounds;
        public float CruiseAltitude => cruiseAltitude;
        internal MissionBoundaryGuard Boundary => boundary;
        public bool FlightActive => phase == Phase.Flight;

        private enum Phase { Ready, Takeoff, Flight, Landing, Landed, Failed }
        private Phase phase;
        private bool bound, handover;
        private string failure;
        private Transform aircraft, visual;
        private Vector3 spawn;
        private Quaternion spawnRotation, visualRest;
        private FlightController flight;
        private Afterburner boost;
        private AfterburnerVfx flames;
        private MissionCheats cheats;
        private PlayerHitPoints health;
        private PlayerWeaponTrigger trigger;
        private ArcadeMissileLauncher missiles;
        private TargetingCursor cursor;
        private CombatHitFeedback feedback;
        private Camera view;
        private MissionBoundaryGuard boundary;
        private MissionFlightSafety safety;
        private ScriptedTakeoff takeoff;
        private ScriptedLanding landing;
        private GUIStyle messageStyle;
        private const float Roll = 2.2f, Climb = 2.5f, MaxPitch = 18f;

        public string DebugStatus => $"M03 | {phase} | flight foundation";
        FlightController IPlayerEngineAudioContext.EngineAudioFlight => flight;
        PlayerEnginePhase IPlayerEngineAudioContext.EngineAudioPhase => !bound || !isActiveAndEnabled
            ? PlayerEnginePhase.Silent : phase switch
            {
                Phase.Ready => PlayerEnginePhase.Ready,
                Phase.Takeoff => PlayerEnginePhase.Takeoff,
                Phase.Flight => PlayerEnginePhase.Flight,
                Phase.Landing => PlayerEnginePhase.Landing,
                _ => PlayerEnginePhase.Silent
            };

#if UNITY_EDITOR
        public void ConfigureForAuthoring(CombatHud display, Renderer surface, Transform runwayCenter,
            MissileProjectile projectile, Rect bounds)
        {
            hud = display; ground = surface; runway = runwayCenter;
            missilePrefab = projectile; operationBounds = bounds;
        }
#endif

        public void Bind(JetAssembly player, Camera camera, AircraftRadar radar,
            PlayerHitPoints playerHealth, float halfExtent)
        {
            aircraft = player.transform; health = playerHealth; view = camera;
            flight = player.GetComponent<FlightController>();
            boost = player.GetComponent<Afterburner>();
            cheats = player.GetComponent<MissionCheats>();
            trigger = player.GetComponent<PlayerWeaponTrigger>();
            visual = flight != null ? flight.Visual : null;
            flames = visual != null ? visual.GetComponent<AfterburnerVfx>() : null;
            missiles = player.GetComponentInChildren<ArcadeMissileLauncher>(true);
            var nurs = player.GetComponentInChildren<ArcadeNursLauncher>(true);
            var team = player.GetComponent<AircraftTeam>();
            if (flight == null || boost == null || trigger == null || health == null ||
                radar == null || visual == null || flames == null || missiles == null ||
                nurs == null || team == null || camera == null || hud == null ||
                runway == null || ground == null || missilePrefab == null)
            {
                Debug.LogError("M03: incomplete shared flight references.", this);
                if (flight != null) flight.enabled = false;
                if (trigger != null) trigger.enabled = false;
                enabled = false; return;
            }
            spawn = aircraft.position; spawnRotation = aircraft.rotation;
            visualRest = visual.localRotation;
            flight.enabled = false; trigger.enabled = false;
            team.SetSide(AircraftSide.Player);
            health.InstallHitbox();
            boundary = new MissionBoundaryGuard(transform, operationBounds, 250f, 100f);
            safety = new MissionFlightSafety(boundary);
            takeoff = new ScriptedTakeoff(aircraft, visual, spawn, spawnRotation, visualRest);
            // The builder supplies an explicit runway center, so adding an enemy
            // airfield later cannot make our approach resolve to the wrong mesh.
            Vector3 axis = Vector3.ProjectOnPlane(runway.forward, Vector3.up).normalized;
            float threshold = Vector3.Dot(runway.position + axis * 120f - spawn, axis);
            var landingRunway = new LandingRunway(spawn, axis, 24f, threshold, runway.position.y + .2f);
            landing = new ScriptedLanding(transform, landingRunway, visualRest, operationBounds,
                150f, 350f, 1f, 550f, 15f, 1f, 65f, 65f);
            feedback = gameObject.AddComponent<CombatHitFeedback>();
            feedback.Bind(camera); feedback.Track(health, true);
            nurs.gameObject.SetActive(false);
            cursor = camera.gameObject.AddComponent<TargetingCursor>();
            cursor.Bind(camera); cursor.enabled = false;
            missiles.gameObject.SetActive(true);
            missiles.Bind(camera, cursor, player, missilePrefab, feedback);
            trigger.ConfigureDualSlots(missiles);
            bound = true;
        }

        private void OnEnable()
        {
            if (bound && !MissionRetry.Register(this)) enabled = false;
        }

        private void Start()
        {
            if (!bound || !MissionRetry.Register(this)) { enabled = false; return; }
            hud.BindOperationBoundary(boundary);
            TerrainCollisionWarning.Create(hud.transform, flight, view);
            if (hud.Marker != null) hud.Marker.BindNavigationTarget(() =>
                phase == Phase.Flight && EarlyLanding ? (Vector3?)landing.NavigationPoint : null);
            Restart();
        }

        private bool EarlyLanding => cheats != null && cheats.AllowEarlyLanding;

        public void Restart()
        {
            if (!bound) return;
            MissionPause.Release(this);
            boundary.Clear(); landing.Clear();
            flight.enabled = false; trigger.enabled = false; cursor.enabled = false;
            missiles.ResetMagazine(); ClearRounds();
            foreach (var gun in aircraft.GetComponentsInChildren<AutocannonWeapon>())
                gun.ResetContinuous(Time.time);
            feedback.ResetEffects(); health.ResetHealth();
            boost.enabled = false; boost.enabled = true;
            visual.gameObject.SetActive(true);
            flames.SetManeuverFlames(false);
            aircraft.SetPositionAndRotation(spawn, spawnRotation); visual.localRotation = visualRest;
            takeoff.ResetTimer(); handover = false; failure = null; phase = Phase.Ready;
            hud.gameObject.SetActive(false);
#if UNITY_EDITOR
            if (cheats != null && cheats.SkipTakeoff)
            {
                takeoff.PlaceAtEnd(flight.CruiseSpeed, Roll, Climb, cruiseAltitude);
                EnterFlight();
            }
#endif
            view.GetComponent<ArcadeCameraFollow>()?.SnapToAircraft();
        }

        private void Update()
        {
            if (!bound || MissionPause.IsTacticalMapPausedFor(gameObject.scene)) return;
            var keys = Keyboard.current;
            if (Application.isFocused && keys != null && keys.rKey.wasPressedThisFrame)
            {
                Restart(); MissionRetry.MarkRequested(this); return;
            }
            if (phase == Phase.Ready && Application.isFocused && keys != null && keys.wKey.wasPressedThisFrame)
            {
                phase = Phase.Takeoff; flames.SetManeuverFlames(true); return;
            }
            if (phase == Phase.Takeoff)
                handover = takeoff.Tick(Time.deltaTime, flight.CruiseSpeed, Roll, Climb, cruiseAltitude, MaxPitch);
            else if (phase == Phase.Landing && landing.TickLanding(aircraft, visual, Time.deltaTime))
                phase = Phase.Landed;
        }

        private void EnterFlight()
        {
            handover = false; visual.localRotation = visualRest;
            flames.SetManeuverFlames(false); flight.PrepareCruiseHandover();
            phase = Phase.Flight;
            if (safety.Begin(aircraft.position, out Vector3 stop))
            { Fail(stop, "OPERATION AREA ABANDONED", false, true); return; }
            flight.enabled = true; trigger.enabled = true; cursor.enabled = true;
            hud.gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (!bound || MissionPause.IsTacticalMapPausedFor(gameObject.scene)) return;
            if (phase == Phase.Takeoff && handover) { EnterFlight(); return; }
            if (phase != Phase.Flight) return;
            if (!health.IsAlive) { Fail(aircraft.position, "AIRCRAFT LOST", false, false); return; }
            var result = safety.Check(gameObject.scene, aircraft.position,
                cheats == null || !cheats.NoClip, 3.5f, out Vector3 stop);
            if (result == MissionFlightSafety.Result.TerrainImpact)
            { Fail(stop, "TERRAIN IMPACT", true, false); return; }
            if (result == MissionFlightSafety.Result.BoundaryExit)
            { Fail(stop, "OPERATION AREA ABANDONED", false, true); return; }
            if (!Application.isFocused) { landing.Clear(); return; }
            // No normal victory/RTB before actual objectives exist. DEV01 can
            // already exercise the same assisted landing path as M01.
            if (landing.TickApproach(aircraft, visual, flight.CurrentSpeed, EarlyLanding, Time.deltaTime))
            {
                phase = Phase.Landing; boundary.Clear(); StopCombat();
            }
        }

        private void StopCombat()
        {
            flight.enabled = false; trigger.enabled = false; cursor.enabled = false;
            boost.enabled = false; flames.SetManeuverFlames(false);
            missiles.CancelMissiles(); ClearRounds();
            hud.gameObject.SetActive(false);
        }

        private void Fail(Vector3 stop, string reason, bool terrain, bool edge)
        {
            aircraft.position = stop; phase = Phase.Failed; failure = reason;
            boundary.Clear(); landing.Clear(); StopCombat();
            if (terrain)
            {
                if (health.IsAlive) health.ApplyDamage(stop, health.Hp);
                visual.gameObject.SetActive(false); feedback.EmitTerrainCrash(stop);
            }
            else if (!health.IsAlive) feedback.SpawnWreck(health, flight.CurrentSpeed);
            if (edge) MissionPause.TryAcquire(this, MissionPause.Reason.BoundaryFailure);
        }

        private void ClearRounds()
        {
            foreach (var round in FindObjectsByType<BallisticProjectile>())
                if (round.gameObject.scene == gameObject.scene && round.IsOwnedBy(aircraft))
                { round.gameObject.SetActive(false); Destroy(round.gameObject); }
        }

        public void SetAltitude(float altitude) { } // Campaign altitude is authored.

        private void OnGUI()
        {
            if (!bound || Event.current.type != EventType.Repaint || MissionPause.IsTacticalMapPausedFor(gameObject.scene)) return;
            string message = phase switch
            {
                Phase.Ready => "PRESS W TO TAKE OFF",
                Phase.Takeoff => "TAKEOFF",
                Phase.Landing => "AUTOPILOT / LANDING",
                Phase.Landed => "LANDING TEST COMPLETE / R TO RETRY",
                Phase.Failed => failure + " / R TO RETRY",
                _ => boundary.WarningLevel == 2 ? "RETURN TO COMBAT AREA" :
                    boundary.WarningLevel == 1 ? "APPROACHING OPERATION BOUNDARY" :
                    landing.ApproachVisible ? "ALIGN WITH LANDING CORRIDOR" : null
            };
            if (message == null) return;
            if (messageStyle == null) messageStyle = new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            messageStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * .025f, 16f, 32f));
            var rect = new Rect(0f, Screen.height * (phase == Phase.Flight ? .085f : .84f), Screen.width, 42f);
            messageStyle.normal.textColor = new Color(.05f, .07f, .09f, .9f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), message, messageStyle);
            messageStyle.normal.textColor = phase == Phase.Failed || boundary.WarningLevel == 2
                ? new Color(1f, .40f, .32f) : Color.white;
            GUI.Label(rect, message, messageStyle);
        }

        private void OnDisable()
        {
            MissionRetry.Unregister(this); MissionPause.Release(this);
            boundary?.Clear(); landing?.Clear();
        }
        private void OnDestroy()
        {
            if (hud != null && hud.Marker != null) hud.Marker.BindNavigationTarget(null);
            boundary?.Dispose(); landing?.Dispose();
            if (cursor != null) Destroy(cursor);
        }
    }
}
