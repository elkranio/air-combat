using UnityEngine;
using UnityEngine.InputSystem;

namespace AirCombat
{
    // Mission-specific launch state. Normal flight remains owned by FlightController.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)] // Before normal flight; E1 session initializes first.
    public sealed class Mission01LaunchSequence : MonoBehaviour
    {
        public enum Phase { Intro, Ready, Takeoff, Flight, Crashed, BoundaryFailed }

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

        private Transform aircraft;
        private Transform visual;
        private Afterburner boost;
        private AfterburnerVfx flames;
        private Vector3 runwayPosition;
        private Quaternion runwayRotation, visualRest;
        private double elapsed;
        private bool handoverPending;
        private bool initialized;
        private Vector3 previousFlightPosition;
        private CombatHitFeedback crashFeedback;
        private MissionBoundaryGuard boundary;
        private bool ownsBoundaryPause;
        private float previousTimeScale;
        private bool previousAudioPause;

        public Phase State { get; private set; } = Phase.Intro;
        internal MissionBoundaryGuard Boundary => boundary;

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
            flight.enabled = false;
            weaponTrigger.enabled = false;
            // Keep the authored operation rectangle independent of decorative ground.
            boundary = new MissionBoundaryGuard(transform, operationBounds,
                boundaryCautionDistance, boundaryCriticalDistance);
            initialized = true;
            State = Phase.Intro;
        }

        private void Start()
        {
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
                    site.BindMissionTarget(aircraft, playerHealth, samRadar);
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
            if (TacticalMapView.IsOpenFor(gameObject.scene))
            {
                boundary?.Clear();
                return;
            }
            var keyboard = Keyboard.current;
            if (Application.isFocused && keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                ResetToReady();
#if UNITY_EDITOR
                if (debugAirStart) StartAirborne();
#endif
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
            if (TacticalMapView.IsOpenFor(gameObject.scene)) return;
            if (State == Phase.Flight)
            {
                // Flight motor has already moved; camera and weapon LateUpdate
                // run later. Sweep before they see the next aircraft position.
                Vector3 next = aircraft.position;
                bool exited = boundary.TryClipExit(previousFlightPosition, next, out Vector3 stop);
                // Test the reachable segment only: a rock before the boundary
                // still wins, but a rock beyond the operation cannot cause a crash.
                bool checkTerrain = true;
#if UNITY_EDITOR
                checkTerrain = !debugNoClip;
#endif
                if (checkTerrain && TerrainFlightCollision.TryHit(aircraft.gameObject.scene,
                    previousFlightPosition, stop, Mathf.Max(.1f, terrainCrashRadius), out Vector3 impact))
                    Crash(impact);
                else if (exited) FailBoundary(stop);
                else
                {
                    previousFlightPosition = next;
                    boundary.Tick(next);
                }
                return;
            }
            if (State != Phase.Takeoff || !handoverPending) return;
            // Enable after Update so the motor cannot translate twice on this frame.
            EnterFlight();
        }

        private void Crash(Vector3 impact)
        {
            if (State != Phase.Flight) return;
            State = Phase.Crashed;
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
            aircraft.position = stop;
            previousFlightPosition = stop;
            flight.enabled = false;
            weaponTrigger.enabled = false;
            boost.enabled = false;
            flames.SetManeuverFlames(false);
            boundary.Clear();
            SetSamArmed(false);
            // No synthetic crash, damage or explosion. Freeze the failed sortie;
            // the existing R input runs even at timeScale zero.
            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
            ownsBoundaryPause = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        private void ReleaseBoundaryPause()
        {
            if (!ownsBoundaryPause) return;
            ownsBoundaryPause = false;
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
        }

#if UNITY_EDITOR
        private void StartAirborne()
        {
            // Exact endpoint of the scripted takeoff, without waiting for it.
            float distance = flight.CruiseSpeed * (.5f * Mathf.Max(.1f, rollSeconds) + Mathf.Max(.1f, climbSeconds));
            Vector3 position = runwayPosition + runwayRotation * Vector3.forward * distance;
            position.y = Mathf.Max(runwayPosition.y, cruiseAltitude);
            aircraft.SetPositionAndRotation(position, runwayRotation);
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
            previousFlightPosition = aircraft.position;
            State = Phase.Flight;
            if (boundary.TryClipExit(aircraft.position, aircraft.position, out Vector3 stop))
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
                if (site != null) site.SetMissionArmed(armed);
        }

        private void ResetToReady()
        {
            ReleaseBoundaryPause();
            boundary?.Clear();
            SetSamArmed(false);
            if (samSites != null) foreach (var site in samSites)
                if (site != null) site.ResetEncounter();
            handoverPending = false;
            elapsed = 0d;
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
            ReleaseBoundaryPause();
            boundary?.Dispose();
            if (samRadar != null && samSites != null) foreach (var site in samSites)
                if (site != null) samRadar.Unregister(site);
        }

        private void OnDisable()
        {
            ReleaseBoundaryPause();
            boundary?.Clear();
            SetSamArmed(false);
            if (initialized && flames != null) flames.SetManeuverFlames(false);
#if UNITY_EDITOR
            if (flight != null) flight.DebugFastTravel = false;
#endif
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || TacticalMapView.IsOpenFor(gameObject.scene)) return;
            if (State == Phase.Flight)
            {
                boundary?.DrawWarning();
                return;
            }
            string message = State == Phase.Intro ? "MISSION INTRO" :
                State == Phase.Ready ? "PRESS W TO TAKE OFF" :
                State == Phase.Crashed ? "AIRCRAFT LOST  /  PRESS R TO RETRY" :
                State == Phase.BoundaryFailed ? "MISSION FAILED — OPERATION AREA ABANDONED  /  PRESS R TO RETRY" : "TAKEOFF";
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
            style.normal.textColor = State == Phase.BoundaryFailed ? new Color(1f, .40f, .32f) : Color.white;
            GUI.Label(rect, message, style);
            GUI.color = previous;
        }
    }
}
