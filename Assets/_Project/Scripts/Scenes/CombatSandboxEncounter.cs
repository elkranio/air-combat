using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // Two-fighter arcade encounter. Each Tiger owns an independent attack pilot;
    // the encounter owns pair spawning, radar lifetime, health and reset.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-70)]
    public sealed class CombatSandboxEncounter : MonoBehaviour, IFlightSessionEncounter
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private MissileProjectile missilePrefab;
        [SerializeField, Min(1f)] private float spawnRadius = 550f;
        [SerializeField, Min(20f)] private float pairSpacing = 45f;

        [Header("Enemy AAM / independent attack cooldown")]
        [SerializeField] private EnemyMissileStressTest.Settings enemyMissiles = new EnemyMissileStressTest.Settings();
        private EnemyMissileStressTest missileStress;
        // Explicit serialized values: do not reorder or reuse these IDs.
        public enum SandboxMode { Dogfight = 0, GroundAttack = 1, Interception = 2, Warden = 3, Convoy = 4, Interceptor = 5, Bastion = 6, BuildingDestruction = 7, MothAttack = 8 }
        [Header("Combat sandbox / select before Play")]
        [SerializeField] private SandboxMode mode = SandboxMode.GroundAttack;
        [Header("IN02 / Interceptor pass")]
        [SerializeField] private GameObject interceptorPrefab;
        [SerializeField] private bool interceptorWingmanEnabled = true;
        [SerializeField, Range(0f, 4f)] private float interceptorWingmanDelay = 1.5f;
        [SerializeField, Range(0f, 300f)] private float interceptorWingmanLateralOffset = 120f;
        [SerializeField] private InterceptorPassTrial.Settings interceptorPass = new InterceptorPassTrial.Settings();
        [SerializeField] private EnemyMissileStressTest.Settings interceptorMissiles = new EnemyMissileStressTest.Settings
        {
            warningSeconds = .65f, minLaunchRange = 110f, maxLaunchRange = 570f,
            acquisitionHalfAngle = 45f, speed = 225f, turnRate = 77f, turnBudget = 110f,
            lifetime = 6f, hitRadius = 5f, damage = 5
        };
        private bool interceptorMode;
        private InterceptorPairTrial interceptor;
        [Header("SAM02 / Bastion sandbox")]
        [SerializeField] private GameObject bastionPrefab;
        [SerializeField] private SurfaceToAirLauncher bastionPicketTemplate;
        [SerializeField, Min(.1f)] private float bastionSalvoGap = .8f;
        [SerializeField] private bool bastionSixSiteTest = true;
        private bool bastionMode;
        private readonly List<SurfaceToAirLauncher> bastions = new List<SurfaceToAirLauncher>(6);
        [Header("PG07 / Building destruction sandbox")]
        [SerializeField] private GameObject destructionBuildingPrefab;
        private bool destructionMode;
        private readonly List<BuildingDestructionTrial> destructionTrials = new List<BuildingDestructionTrial>(3);
        private bool groundMode;
        private bool wardenMode;
        private readonly List<WardenCombat> wardens = new List<WardenCombat>();
        private bool convoyMode;
        [Header("CV02 / Straight convoy preview")]
        [SerializeField] private PicketDeploymentVisual convoyPicketPrefab;
        [SerializeField] private Terrain convoyTerrain;
        [SerializeField] private Vector3 convoyStart = new Vector3(-290f, 0f, -100f);
        [SerializeField] private float convoyYaw;
        [SerializeField, Min(16f)] private float convoySpacing = 30f;
        [SerializeField, Min(.1f)] private float convoySpeed = 12f;
        [SerializeField, Min(1f)] private float convoyDistance = 650f;
        [Header("CV06 / Warden elliptical patrol")]
        [SerializeField] private bool convoyEscortEnabled = false;
        [SerializeField] private WardenCombat convoyWardenPrefab;
        [SerializeField, Min(10f)] private float escortLateralRadius = 55f;
        [SerializeField, Min(10f)] private float escortEndClearance = 45f;
        [SerializeField, Min(1f)] private float escortPatrolSpeed = 36f;
        [SerializeField, Min(0f)] private float escortAlphaAcquireDelay = .15f;
        [SerializeField, Min(0f)] private float escortBravoAcquireDelay = 1.25f;
        private ConvoyMarchPreview convoy;
        private float escortAltitude;
        private const int EscortArcSamples = 256;
        private readonly float[] escortArcLengths = new float[EscortArcSamples + 1];
        private readonly float[] escortWorldSpeeds = new float[2];
        private float escortPhaseDistance, escortLateral, escortLongitudinal;
        private bool EscortCleared => convoyMode && encounterReady && wardens.Count == 2 &&
            wardens.TrueForAll(warden => warden != null && !warden.Alive);
        [Header("Cruise missile interception")]
        [SerializeField] private CruiseMissileTarget sparrowPrefab;
        [SerializeField, Min(1)] private int interceptionCount = 10;
        [SerializeField, Min(.1f)] private float interceptionInterval = .7f;
        [SerializeField, Min(0f)] private float interceptionLateralSpread = 60f;
        [Header("Moth Attack / local arcade trial")]
        [SerializeField] private GameObject mothVisualPrefab;
        [SerializeField, Min(1)] private int mothAttackCount = 3;
        [SerializeField, Min(.1f)] private float mothAttackInterval = .65f;
        private bool mothAttackMode;
        private BuildingDestructionTrial mothDemoSite;
        private InterceptionPlayground interception;
        private bool interceptionMode;
        private ArcadeNursLauncher nursLauncher;
        private readonly List<MantisGun> groundGuns = new List<MantisGun>();

        private sealed class Fighter
        {
            public int Index;
            public ArcadeFighterUnit Unit;
            public GameObject Root => Unit != null ? Unit.gameObject : null;
            public PlayerHitPoints Health => Unit.Health;
            public RadarAirContact Contact => Unit.Contact;
        }

        private const int FighterCount = 2;
        private readonly List<Fighter> fighters = new List<Fighter>(FighterCount);
        private JetAssembly player;
        private Camera view;
        private CombatHitFeedback hitFeedback;
        private AircraftRadar radar;
        private PlayerHitPoints playerHealth;
        private float boundary;
        private FlightController playerFlight;
        private PlayerWeaponTrigger playerTrigger;
        private TargetingCursor targetingCursor;
        private ArcadeMissileLauncher missileLauncher;
        private Afterburner playerBoost;
        private bool bound;
        private bool encounterReady;
        private bool defeated;
        private bool victory;
        private GUIStyle resultStyle;

        public string DebugStatus
        {
            get
            {
                if (interceptorMode) return interceptor != null ? interceptor.Status : "IN02 UNAVAILABLE";
                if (bastionMode) return BastionStatus();
                if (destructionMode) return DestructionStatus();
                if (convoyMode)
                {
                    string convoyStatus = convoy != null ? convoy.DebugStatus : "CONVOY UNAVAILABLE";
                    if (defeated) convoyStatus += " | DEFEAT";
                    else if (EscortCleared) convoyStatus += " | ESCORT DOWN";
                    for (int i = 0; i < wardens.Count; i++)
                        if (wardens[i] != null) convoyStatus += $" | {(i == 0 ? "A" : "B")}:{wardens[i].DebugStatus}";
                    return convoyStatus;
                }
                if (wardenMode)
                {
                    string wardenStatus = defeated ? "DEFEAT" : victory ? "VICTORY" : "Warden";
                    foreach (var warden in wardens)
                        if (warden != null) wardenStatus += " | " + warden.DebugStatus;
                    return wardenStatus;
                }
                if (interceptionMode) return interception != null ? interception.DebugStatus : "INTERCEPT UNAVAILABLE";
                if (groundMode)
                {
                    string groundStatus = nursLauncher != null ? nursLauncher.DebugStatus : "NURS UNAVAILABLE";
                    int fired = 0, hits = 0;
                    foreach (var gun in groundGuns)
                        if (gun != null) { fired += gun.FiredCount; hits += gun.HitCount; }
                    return $"{groundStatus} | AA {hits}/{fired} hit/fired";
                }
                if (defeated || victory)
                {
                    string result = defeated ? "DEFEAT" : "VICTORY";
                    return missileStress != null ? $"{result} | AAM {missileStress.Hits}/{missileStress.Launched} hits/fired" : result;
                }
                if (fighters.Count == 0) return "NO ENEMY";
                // Show actual gun shots per live pilot alongside the AAM totals.
                string status = $"Tigers {fighters.Count}/{FighterCount}";
                if (missileStress != null)
                    status += $" | AAM {missileStress.InFlight} live {missileStress.Hits}/{missileStress.Launched} hits/fired";
                foreach (var fighter in fighters)
                    status += $" | {(fighter.Index == 0 ? "A" : "B")}:{fighter.Unit.DebugStatus}";
                return status;
            }
        }

        public void Bind(JetAssembly aircraft, Camera camera, AircraftRadar sensor,
            PlayerHitPoints health, float halfExtent)
        {
            if (mode != SandboxMode.Dogfight && mode != SandboxMode.GroundAttack && mode != SandboxMode.Interception && mode != SandboxMode.Warden && mode != SandboxMode.Convoy && mode != SandboxMode.Interceptor && mode != SandboxMode.Bastion && mode != SandboxMode.BuildingDestruction && mode != SandboxMode.MothAttack)
            {
                Debug.LogError("Combat sandbox: invalid mode.", this);
                return;
            }
            mothAttackMode = mode == SandboxMode.MothAttack;
            interceptionMode = mode == SandboxMode.Interception || mothAttackMode;
            groundMode = mode == SandboxMode.GroundAttack;
            wardenMode = mode == SandboxMode.Warden;
            convoyMode = mode == SandboxMode.Convoy;
            interceptorMode = mode == SandboxMode.Interceptor;
            bastionMode = mode == SandboxMode.Bastion;
            destructionMode = mode == SandboxMode.BuildingDestruction;
            player = aircraft;
            view = camera;
            radar = sensor;
            playerHealth = health;
            boundary = halfExtent;
            if ((mode == SandboxMode.Dogfight && enemyPrefab == null) ||
                (interceptionMode && (sparrowPrefab == null || missilePrefab == null)) ||
                (mothAttackMode && mothVisualPrefab == null) ||
                (interceptorMode && (interceptorPrefab == null || missilePrefab == null)) ||
                (bastionMode && (bastionPrefab == null || bastionPicketTemplate == null || convoyTerrain == null)) ||
                ((destructionMode || mothAttackMode) && (destructionBuildingPrefab == null || convoyTerrain == null)) ||
                player == null || radar == null || playerHealth == null)
            {
                Debug.LogError("AIR encounter is missing its prefab or player binding.", this);
                return;
            }
            var team = player.GetComponent<AircraftTeam>();
            playerFlight = player.GetComponent<FlightController>();
            playerTrigger = player.GetComponent<PlayerWeaponTrigger>();
            playerBoost = player.GetComponent<Afterburner>();
            var groundLauncher = player.GetComponentInChildren<ArcadeNursLauncher>(true);
            var airLauncher = player.GetComponentInChildren<ArcadeMissileLauncher>(true);
            if (team == null || playerFlight == null || playerTrigger == null || playerBoost == null ||
                groundLauncher == null || airLauncher == null || view == null)
            {
                Debug.LogError("AIR encounter: incomplete PlayerKestrel_Combat prefab.", player);
                return;
            }
            team.SetSide(AircraftSide.Player);
            groundLauncher.gameObject.SetActive(false);
            airLauncher.gameObject.SetActive(false);
            playerHealth.InstallHitbox();
            if (groundMode)
            {
                foreach (var gun in FindObjectsByType<MantisGun>(FindObjectsInactive.Include))
                {
                    if (gun.gameObject.scene != gameObject.scene) continue;
                    groundGuns.Add(gun);
                    gun.Bind(playerHealth, radar);
                }
            }
            // Production scene opt-in; legacy labs have no hit-feedback listener.
            hitFeedback = GetComponent<CombatHitFeedback>();
            if (hitFeedback == null) hitFeedback = gameObject.AddComponent<CombatHitFeedback>();
            hitFeedback.Bind(view);
            hitFeedback.Track(playerHealth, true);
            if ((mode == SandboxMode.Dogfight || interceptorMode) && missilePrefab != null)
            {
                missileStress = gameObject.AddComponent<EnemyMissileStressTest>();
                missileStress.Bind(playerHealth, missilePrefab, hitFeedback, interceptorMode ? interceptorMissiles : enemyMissiles, radar);
                gameObject.AddComponent<EnemyMissileThreatHud>().Bind(view, missileStress);
            }
            // AC-02 remains a prefab option, not an equipped weapon in this test.
            foreach (var gun in player.GetComponentsInChildren<AutocannonWeapon>(true))
                if (gun.Preset != null && gun.Preset.Continuous) gun.enabled = false;
            if (groundMode || convoyMode || destructionMode)
            {
                nursLauncher = groundLauncher;
                nursLauncher.gameObject.SetActive(true);
                nursLauncher.Bind(playerFlight, playerBoost, playerTrigger, view);
                playerTrigger.ConfigureDualSlots(nursLauncher);
            }
            if (convoyMode)
            {
                convoy = new ConvoyMarchPreview(convoyPicketPrefab, convoyTerrain, radar, transform,
                    convoyStart, convoyYaw, convoySpacing, convoySpeed, convoyDistance);
                // Isolate the previous Warden and Mantis trials in this mode.
                foreach (var warden in FindObjectsByType<WardenCombat>())
                    if (warden.gameObject.scene == gameObject.scene) warden.gameObject.SetActive(false);
                foreach (var gun in FindObjectsByType<MantisGun>())
                    if (gun.gameObject.scene == gameObject.scene) gun.gameObject.SetActive(false);
                if (convoyEscortEnabled && !SpawnConvoyEscorts()) return;
            }
            else if (wardenMode)
            {
                playerTrigger.ConfigureDualSlots(null);
                foreach (var warden in FindObjectsByType<WardenCombat>())
                {
                    if (warden.gameObject.scene != gameObject.scene ||
                        !warden.Bind(playerHealth, playerFlight, hitFeedback)) continue;
                    wardens.Add(warden);
                    radar.Register(warden.Contact);
                }
                if (wardens.Count == 0)
                    Debug.LogError("Warden sandbox: place an ED01_Warden prefab in the scene.", this);
            }
            else if (!groundMode && !destructionMode)
            {
                targetingCursor = view.gameObject.AddComponent<TargetingCursor>();
                targetingCursor.Bind(view);
                if (missilePrefab != null)
                {
                    missileLauncher = airLauncher;
                    missileLauncher.gameObject.SetActive(true);
                    missileLauncher.Bind(view, targetingCursor, player, missilePrefab, hitFeedback);
                }
                else Debug.LogError("AIR encounter: missing AAM missile prefab.", this);
                playerTrigger.ConfigureDualSlots(missileLauncher);
            }
            if (mothAttackMode && !BindMothDemoSite()) return;
            if (interceptionMode)
                interception = new InterceptionPlayground(sparrowPrefab, radar, hitFeedback,
                    missileLauncher, mothAttackMode ? mothAttackCount : interceptionCount,
                    mothAttackMode ? mothAttackInterval : interceptionInterval,
                    interceptionLateralSpread, mothAttackMode, mothDemoSite, mothVisualPrefab);
            if (interceptorMode)
            {
                foreach (var root in gameObject.scene.GetRootGameObjects())
                {
                    if (root.name.StartsWith("IN01 / ")) root.SetActive(false);
                    foreach (var gun in root.GetComponentsInChildren<MantisGun>(true)) gun.gameObject.SetActive(false);
                    foreach (var warden in root.GetComponentsInChildren<WardenCombat>(true)) warden.gameObject.SetActive(false);
                    foreach (var sam in root.GetComponentsInChildren<SurfaceToAirLauncher>(true)) sam.gameObject.SetActive(false);
                }
                interceptor = new InterceptorPairTrial(transform, interceptorPrefab, player.transform,
                    radar, missileLauncher, missileStress, hitFeedback, interceptorPass, boundary,
                    interceptorWingmanEnabled, interceptorWingmanDelay, interceptorWingmanLateralOffset);
            }
            if (bastionMode && !BindBastionTrial()) return;
            if (destructionMode && !BindDestructionTrial()) return;
            bound = true;
        }

        private bool BindMothDemoSite()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root.name.StartsWith("IN01 / ") || root.name.StartsWith("SAM02 / ") ||
                    root.name.StartsWith("D1 / EF-01 Tiger")) root.SetActive(false);
                foreach (var gun in root.GetComponentsInChildren<MantisGun>(true)) gun.gameObject.SetActive(false);
                foreach (var warden in root.GetComponentsInChildren<WardenCombat>(true)) warden.gameObject.SetActive(false);
                foreach (var sam in root.GetComponentsInChildren<SurfaceToAirLauncher>(true)) sam.gameObject.SetActive(false);
            }
            // Reuse the proven flat PG07 test area and a single protected prop.
            Vector3 position = new Vector3(-290f, 0f, -100f);
            position.y = convoyTerrain.SampleHeight(position) + convoyTerrain.transform.position.y;
            var instance = Instantiate(destructionBuildingPrefab, position, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
            instance.name = "Moth Attack / protected test prop";
            mothDemoSite = instance.AddComponent<BuildingDestructionTrial>();
            if (mothDemoSite.Initialize(0, BuildingDestructionTrial.RuinProfile.ControlBuilding, false, false))
                return true;
            instance.SetActive(false);
            Destroy(instance);
            mothDemoSite = null;
            return false;
        }

        private bool BindDestructionTrial()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root.name.StartsWith("IN01 / ") || root.name.StartsWith("SAM02 / ") ||
                    root.name.StartsWith("D1 / EF-01 Tiger")) root.SetActive(false);
                foreach (var gun in root.GetComponentsInChildren<MantisGun>(true)) gun.gameObject.SetActive(false);
                foreach (var warden in root.GetComponentsInChildren<WardenCombat>(true)) warden.gameObject.SetActive(false);
                foreach (var sam in root.GetComponentsInChildren<SurfaceToAirLauncher>(true)) sam.gameObject.SetActive(false);
            }
            // PG07C: keep the original approach target and stagger two more behind it.
            // Identical footprints let us compare the fires without changing the art.
            Vector3 origin = new Vector3(-290f, 0f, -100f);
            var offsets = new[] { Vector3.zero, new Vector3(-36f, 0f, 30f), new Vector3(36f, 0f, 30f) };
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 position = origin + offsets[i];
                position.y = convoyTerrain.SampleHeight(position) + convoyTerrain.transform.position.y;
                var instance = Instantiate(destructionBuildingPrefab, position, Quaternion.identity);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
                instance.name = $"PG07C / Control building {i + 1}";
                var trial = instance.AddComponent<BuildingDestructionTrial>();
                destructionTrials.Add(trial);
                if (!trial.Initialize(i))
                {
                    ReleaseDestructionTrials();
                    return false;
                }
            }
            return true;
        }

        private string DestructionStatus()
        {
            if (destructionTrials.Count == 0) return "PG07D UNAVAILABLE";
            int destroyedCount = 0;
            string foci = "";
            foreach (var trial in destructionTrials)
            {
                if (foci.Length > 0) foci += "/";
                bool down = trial != null && trial.IsDestroyed;
                if (down) destroyedCount++;
                foci += down ? trial.FireFocusCount.ToString() : "-";
            }
            string hint = destroyedCount == destructionTrials.Count ? "FIRES ACTIVE" : "RMB NURS";
            return $"PG07D / {destroyedCount}/{destructionTrials.Count} DOWN / FOCI {foci} / {hint} / R TO RETRY";
        }

        private void ReleaseDestructionTrials()
        {
            foreach (var trial in destructionTrials)
            {
                if (trial == null) continue;
                trial.gameObject.SetActive(false);
                Destroy(trial.gameObject);
            }
            destructionTrials.Clear();
        }

        private bool BindBastionTrial()
        {
            // Isolate the live SAM trial without changing authored scene assets.
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root.name.StartsWith("SAM02 / ") || root.name.StartsWith("IN01 / "))
                    root.SetActive(false);
                foreach (var gun in root.GetComponentsInChildren<MantisGun>(true)) gun.gameObject.SetActive(false);
                foreach (var warden in root.GetComponentsInChildren<WardenCombat>(true)) warden.gameObject.SetActive(false);
                foreach (var sam in root.GetComponentsInChildren<SurfaceToAirLauncher>(true)) sam.gameObject.SetActive(false);
            }
            Vector3 center = new Vector3(-220f, 0f, 0f);
            // Six independent sites flank a notional north-south airfield.
            // Keep the one-site baseline available in the Inspector.
            Vector3[] offsets = bastionSixSiteTest ? new[]
            {
                new Vector3(-160f, 0f, -180f), new Vector3(160f, 0f, -180f),
                new Vector3(-160f, 0f,    0f), new Vector3(160f, 0f,    0f),
                new Vector3(-160f, 0f,  180f), new Vector3(160f, 0f,  180f)
            } : new[] { Vector3.zero };
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 position = center + offsets[i];
                position.y = convoyTerrain.SampleHeight(position) + convoyTerrain.transform.position.y;
                var instance = Instantiate(bastionPrefab, position, Quaternion.identity, transform);
                instance.name = $"SAM02 / Bastion {i + 1:00} - COMBAT TRIAL";
                var site = instance.AddComponent<SurfaceToAirLauncher>();
                if (!site.BindBastion(bastionPicketTemplate, player.transform, playerHealth, radar, bastionSalvoGap))
                {
                    Debug.LogError("SAM02: incomplete Bastion visual or Picket template.", instance);
                    instance.SetActive(false);
                    Destroy(instance);
                    ReleaseBastions();
                    return false;
                }
                bastions.Add(site);
                radar.Register(site);
            }
            gameObject.AddComponent<EnemyMissileThreatHud>().BindSam(view, player.transform, bastions.ToArray());
            return true;
        }

        private string BastionStatus()
        {
            if (bastions.Count == 0) return "SAM02 UNAVAILABLE";
            int launched = 0, threatening = 0;
            foreach (var site in bastions)
            {
                if (site == null) continue;
                launched += site.LaunchedCount;
                if (site.HasIncomingMissile) threatening++;
            }
            if (bastions.Count == 1 && bastions[0] != null)
            {
                var site = bastions[0];
                return $"SAM02 | {site.StatusLabel} | SAM {launched} | Reload {site.ReloadLeft:F1}s | R retry";
            }
            return $"SAM02 | Sites {bastions.Count} | Sites with airborne SAM {threatening} | Total launches {launched} | R retry";
        }

        private void StopBastion()
        {
            foreach (var site in bastions)
            {
                if (site == null) continue;
                site.SetArmed(false);
                site.CancelProjectiles();
            }
        }

        private void ReleaseBastions()
        {
            StopBastion();
            foreach (var site in bastions)
            {
                if (site == null) continue;
                radar?.Unregister(site);
                site.gameObject.SetActive(false);
                Destroy(site.gameObject);
            }
            bastions.Clear();
        }

        private void Start() { Restart(); }

        public void Restart()
        {
            if (!bound) return;
            // ED02A: reset opt-in visual patrols along with the sandbox session.
            foreach (var preview in FindObjectsByType<WardenPatrolPreview>())
                if (preview.gameObject.scene == gameObject.scene && preview.isActiveAndEnabled)
                    preview.RestartPreview();
            if (nursLauncher != null) nursLauncher.ResetMagazine();
            interceptor?.Clear();
            if (missileStress != null) missileStress.ResetTest();
            if (missileLauncher != null) missileLauncher.ResetMagazine();
            if (hitFeedback != null) hitFeedback.ResetEffects();
            if (playerTrigger != null) playerTrigger.enabled = false;
            foreach (var gun in player.GetComponentsInChildren<AutocannonWeapon>())
                gun.ResetContinuous(Time.time);
            ClearRounds(player.transform);
            RemoveAllFighters();
            encounterReady = false;
            playerHealth.ResetHealth();
            if (playerFlight != null) playerFlight.enabled = true;
            if (playerTrigger != null) playerTrigger.enabled = true;
            defeated = victory = false;
            if (destructionMode)
            {
                if (destructionTrials.Count == 0) return;
                foreach (var trial in destructionTrials)
                    if (trial != null) trial.ResetTarget();
                Vector3 start = destructionTrials[0].transform.position + Vector3.back * 300f;
                start.y = player.transform.position.y;
                player.transform.SetPositionAndRotation(start, Quaternion.identity);
                playerFlight.PrepareCruiseHandover();
                playerBoost.enabled = false; playerBoost.enabled = true;
                encounterReady = true;
                view.GetComponent<ArcadeCameraFollow>()?.SnapToAircraft();
                return;
            }
            if (bastionMode)
            {
                player.transform.SetPositionAndRotation(new Vector3(-220f, player.transform.position.y, -650f), Quaternion.identity);
                playerFlight.PrepareCruiseHandover();
                playerBoost.enabled = false; playerBoost.enabled = true;
                foreach (var site in bastions)
                {
                    if (site == null) continue;
                    site.ResetEncounter();
                    site.SetArmed(true);
                }
                encounterReady = bastions.Count > 0;
                view.GetComponent<ArcadeCameraFollow>()?.SnapToAircraft();
                return;
            }
            if (interceptorMode)
            {
                player.transform.SetPositionAndRotation(new Vector3(0f, player.transform.position.y, -400f), Quaternion.Euler(0f, 90f, 0f));
                playerFlight.PrepareCruiseHandover();
                playerBoost.enabled = false; playerBoost.enabled = true;
                encounterReady = interceptor != null && interceptor.Restart();
                view.GetComponent<ArcadeCameraFollow>()?.SnapToAircraft();
                return;
            }
            if (convoyMode)
            {
                encounterReady = convoy != null && convoy.Restart();
                if (!encounterReady) return;
                escortAltitude = player.transform.position.y;
                ResetEscortOrbit();
                for (int i = 0; i < wardens.Count; i++)
                {
                    var warden = wardens[i];
                    if (warden == null) continue;
                    ClearRounds(warden.transform);
                    warden.ResetCombat(Time.time, false, i == 0 ? escortAlphaAcquireDelay : escortBravoAcquireDelay);
                    PlaceEscort(i);
                }
                return;
            }
            if (wardenMode)
            {
                foreach (var warden in wardens)
                {
                    if (warden == null) continue;
                    ClearRounds(warden.transform);
                    warden.ResetCombat(Time.time);
                }
                encounterReady = wardens.Count > 0;
                return;
            }
            if (interceptionMode)
            {
                if (mothAttackMode)
                {
                    if (mothDemoSite == null) return;
                    mothDemoSite.ResetTarget();
                    Vector3 start = mothDemoSite.transform.position + new Vector3(-185f, 0f, -40f);
                    start.y = player.transform.position.y;
                    player.transform.SetPositionAndRotation(start, Quaternion.Euler(0f, 90f, 0f));
                    playerFlight.PrepareCruiseHandover();
                    playerBoost.enabled = false; playerBoost.enabled = true;
                    view.GetComponent<ArcadeCameraFollow>()?.SnapToAircraft();
                }
                interception?.Restart(player.transform.position, boundary, Time.time);
                encounterReady = true;
                return;
            }
            if (groundMode)
            {
                foreach (var gun in groundGuns)
                {
                    if (gun == null) continue;
                    var motor = gun.GetComponent<GroundPatrolMotor>();
                    if (motor != null) motor.RestartPatrol();
                    gun.ResetCombat();
                }
                return; // No Tigers or ground victory yet.
            }

            if (!TrySpawnPair(out Vector3 first, out Vector3 second, out Quaternion heading))
            {
                Debug.LogError("AIR encounter: no offscreen spawn pair within map bounds.", this);
                return;
            }
            // Small opposite yaw offsets preserve a readable two-plane arrival,
            // without forcing the pair to fly as a coordinated formation.
            bool firstLater = Random.value < .5f;
            if (!SpawnFighter(0, first, heading * Quaternion.Euler(0f, -3f, 0f), firstLater) ||
                !SpawnFighter(1, second, heading * Quaternion.Euler(0f, 3f, 0f), !firstLater))
            {
                RemoveAllFighters();
                return;
            }
            encounterReady = true;
        }

        private bool SpawnFighter(int index, Vector3 position, Quaternion rotation, bool later)
        {
            GameObject root = Instantiate(enemyPrefab, position, rotation);
            root.name = index == 0 ? "EF-01 Tiger Alpha / AIR-03" : "EF-01 Tiger Bravo / AIR-03";
            var unit = root.GetComponent<ArcadeFighterUnit>();
            if (unit == null || !unit.Prepare())
            {
                Debug.LogError("AIR encounter: incomplete Tiger combat prefab.", root);
                root.SetActive(false);
                Destroy(root);
                return false;
            }

            if (hitFeedback != null) hitFeedback.Track(unit.Health, false);
            if (missileLauncher != null) missileLauncher.Register(unit.Health);
            if (missileStress != null) missileStress.Register(unit.Health);
            unit.BindPilot(player.transform, playerFlight, boundary, later, index == 0 ? -1f : 1f);
            fighters.Add(new Fighter { Index = index, Unit = unit });
            radar.Register(unit.Contact);
            return true;
        }

        private bool TrySpawnPair(out Vector3 first, out Vector3 second, out Quaternion rotation)
        {
            Vector3 origin = player.transform.position;
            float safe = boundary - 65f;
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < 72; i++)
            {
                Vector3 radial = Quaternion.AngleAxis(start + i * 5f, Vector3.up) * Vector3.forward;
                Vector3 centre = origin + radial * spawnRadius;
                Vector3 heading = Quaternion.AngleAxis(Random.Range(-8f, 8f), Vector3.up) * -radial;
                Vector3 right = Vector3.Cross(Vector3.up, heading).normalized;
                Vector3 a = centre - right * (pairSpacing * .5f);
                Vector3 b = centre + right * (pairSpacing * .5f);
                if (!ValidSpawn(a, safe) || !ValidSpawn(b, safe)) continue;
                first = a;
                second = b;
                rotation = Quaternion.LookRotation(heading, Vector3.up);
                return true;
            }
            first = second = default;
            rotation = Quaternion.identity;
            return false;
        }

        private bool ValidSpawn(Vector3 candidate, float safe)
        {
            if (Mathf.Abs(candidate.x) > safe || Mathf.Abs(candidate.z) > safe) return false;
            if (view == null) return true;
            Vector3 screen = view.WorldToViewportPoint(candidate);
            return screen.z <= 0f || screen.x <= -.1f || screen.x >= 1.1f ||
                screen.y <= -.1f || screen.y >= 1.1f;
        }

        private void Update()
        {
            if (!bound || (!encounterReady && !groundMode) || defeated || victory) return;
            if (!playerHealth.IsAlive)
            {
                defeated = true;
                StopBastion();
                interceptor?.Clear();
                if (missileStress != null) missileStress.StopTest();
                if (nursLauncher != null) nursLauncher.CeaseFire(Time.time);
                if (missileLauncher != null) missileLauncher.CancelMissiles();
                if (playerTrigger != null) playerTrigger.enabled = false;
                if (hitFeedback != null)
                    hitFeedback.SpawnWreck(playerHealth, playerFlight != null ? playerFlight.CurrentSpeed : 70f);
                if (playerFlight != null) playerFlight.enabled = false;
                playerBoost?.Tick(false, 0f);
                ClearRounds(player.transform);
                interception?.Clear();
                foreach (var warden in wardens)
                {
                    if (warden == null) continue;
                    ClearRounds(warden.transform);
                    warden.StopCombat();
                }
                for (int i = fighters.Count - 1; i >= 0; i--)
                    RemoveFighterAt(i, !fighters[i].Health.IsAlive);
                return;
            }

            if (destructionMode) return; // Observe the persistent wreck; no automatic victory.
            if (bastionMode) return; // SurfaceToAirLauncher owns its LateUpdate.
            if (interceptorMode)
            {
                interceptor.Tick(Time.deltaTime, Time.time);
                return;
            }
            if (convoyMode)
            {
                convoy.Tick(Time.deltaTime);
                escortPhaseDistance = Mathf.Repeat(escortPhaseDistance +
                    Mathf.Max(1f, escortPatrolSpeed) * Time.deltaTime, escortArcLengths[EscortArcSamples]);
                for (int i = 0; i < wardens.Count; i++)
                {
                    var warden = wardens[i];
                    if (warden == null) continue;
                    if (warden.Alive) PlaceEscort(i);
                    else if (warden.gameObject.activeSelf)
                    {
                        ClearRounds(warden.transform);
                        warden.HideDestroyed(escortWorldSpeeds[i]);
                    }
                }
                // Keep the sandbox running after either group is cleared.
                // The player can attack the remaining targets in any order.
                return;
            }
            if (wardenMode)
            {
                bool anyAlive = false;
                foreach (var warden in wardens)
                {
                    if (warden == null) continue;
                    if (warden.Alive) anyAlive = true;
                    else if (warden.gameObject.activeSelf)
                    {
                        ClearRounds(warden.transform);
                        warden.HideDestroyed();
                    }
                }
                victory = !anyAlive;
                return;
            }
            if (interceptionMode)
            {
                interception?.Tick(Time.time, Time.deltaTime);
                return;
            }
            if (groundMode) return;
            for (int i = fighters.Count - 1; i >= 0; i--)
            {
                if (!fighters[i].Health.IsAlive) RemoveFighterAt(i, true);
            }
            if (fighters.Count == 0)
            {
                victory = true;
                if (missileStress != null) missileStress.StopTest();
            }
            for (int i = 0; i < fighters.Count; i++)
            {
                // Both pilots sample the same pre-movement positions. A killed
                // wingman has already been removed, so the survivor flies normally.
                Transform wingman = fighters.Count == 2 ? fighters[1 - i].Root.transform : null;
                fighters[i].Unit.TickFlight(Time.time, wingman);
            }
        }

        private void LateUpdate()
        {
            if (!bound || !encounterReady || defeated || victory || !playerHealth.IsAlive) return;
            if (wardenMode || convoyMode)
            {
                foreach (var warden in wardens)
                    if (warden != null) warden.TickFire(Time.time, Time.deltaTime);
                return;
            }
            if (missileStress != null) missileStress.Tick(Time.time);
            // Combined combat: gun requests tick independently of AAM readiness,
            // pending launch warnings and the number of missiles in flight.
            foreach (var fighter in fighters)
                if (fighter.Health.IsAlive) fighter.Unit.TickFire(Time.time);
        }

        private void OnGUI()
        {
            if (destructionMode && !defeated && Event.current.type == EventType.Repaint && destructionTrials.Count > 0)
            {
                float width = Mathf.Min(620f, Screen.width - 24f);
                GUI.Box(new Rect((Screen.width - width) * .5f, 12f, width, 30f), DebugStatus);
            }
            if (bastionMode && !defeated && Event.current.type == EventType.Repaint && bastions.Count > 0)
            {
                float width = Mathf.Min(620f, Screen.width - 24f);
                GUI.Box(new Rect((Screen.width - width) * .5f, 12f, width, 30f), DebugStatus);
            }
            if (interceptorMode && !defeated && Event.current.type == EventType.Repaint && interceptor != null)
            {
                float width = Mathf.Min(880f, Screen.width - 24f);
                GUI.Box(new Rect((Screen.width - width) * .5f, 12f, width, 30f), interceptor.Status);
            }
            if (interceptionMode && !defeated && Event.current.type == EventType.Repaint && interception != null)
            {
                float width = Mathf.Min(mothAttackMode ? 760f : 340f, Screen.width - 24f);
                var counter = new Rect((Screen.width - width) * .5f, 12f, width, 30f);
                GUI.Box(counter, interception.DebugStatus);
            }
            if ((!victory && !defeated && !EscortCleared) || Event.current.type != EventType.Repaint) return;
            if (resultStyle == null)
                resultStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    normal = { textColor = Color.white }
                };
            var rect = new Rect((Screen.width - 340f) * .5f, 12f, 340f, 30f);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(rect, defeated ? "AIRCRAFT LOST — R TO RESTART" :
                convoyMode ? (convoy != null && convoy.AliveCount == 0
                    ? "ALL TARGETS DOWN — R TO RESTART"
                    : "ESCORT DOWN — CONVOY REMAINS") :
                wardenMode ? "WARDENS DOWN — R TO RESTART" : "TIGERS DOWN — R TO RESTART", resultStyle);
        }

        private static void ClearRounds(Transform owner)
        {
            if (owner == null) return;
            foreach (var round in FindObjectsByType<BallisticProjectile>())
            {
                if (!round.IsOwnedBy(owner)) continue;
                round.gameObject.SetActive(false);
                Destroy(round.gameObject);
            }
        }

        public void SetAltitude(float y)
        {
            interception?.SetAltitude(y);
            interceptor?.SetAltitude(y);
            if (convoyMode)
            {
                escortAltitude = y;
                if (encounterReady)
                    for (int i = 0; i < wardens.Count; i++)
                        if (wardens[i] != null && wardens[i].Alive) PlaceEscort(i);
            }
            else foreach (var warden in wardens) if (warden != null) warden.SetAltitude(y);
            foreach (var fighter in fighters)
            {
                if (fighter.Root == null) continue;
                Vector3 position = fighter.Root.transform.position;
                position.y = y;
                fighter.Root.transform.position = position;
            }
        }

        private void RemoveFighterAt(int index, bool destroyed = false)
        {
            Fighter fighter = fighters[index];
            if (missileLauncher != null) missileLauncher.Unregister(fighter.Health);
            if (missileStress != null) missileStress.Unregister(fighter.Health);
            if (hitFeedback != null)
            {
                if (destroyed) hitFeedback.SpawnWreck(fighter.Health, fighter.Unit.CurrentSpeed);
                hitFeedback.Untrack(fighter.Health);
            }
            if (radar != null && fighter.Contact != null) radar.Unregister(fighter.Contact);
            if (fighter.Root != null)
            {
                fighter.Unit.Stop(Time.time);
                ClearRounds(fighter.Root.transform);
                fighter.Root.SetActive(false);
                Destroy(fighter.Root);
            }
            fighters.RemoveAt(index);
        }

        private void RemoveAllFighters()
        {
            for (int i = fighters.Count - 1; i >= 0; i--) RemoveFighterAt(i);
        }

        private bool SpawnConvoyEscorts()
        {
            if (convoyWardenPrefab == null)
            {
                Debug.LogError("CV03: assign ED01_Warden to Convoy Warden Prefab.", this);
                return false;
            }
            for (int i = 0; i < 2; i++)
            {
                var warden = Instantiate(convoyWardenPrefab, transform);
                warden.name = i == 0 ? "CV03 / Warden Alpha" : "CV03 / Warden Bravo";
                if (!warden.Bind(playerHealth, playerFlight, hitFeedback))
                {
                    warden.gameObject.SetActive(false);
                    Destroy(warden.gameObject);
                    ReleaseWardens();
                    return false;
                }
                // ResetCombat disables the circular preview before its first Update.
                warden.GetComponent<WardenPatrolPreview>().enabled = false;
                wardens.Add(warden);
                radar.Register(warden.Contact);
            }
            return true;
        }

        private void ResetEscortOrbit()
        {
            escortLateral = Mathf.Max(10f, escortLateralRadius);
            escortLongitudinal = convoy.HalfLength + Mathf.Max(10f, escortEndClearance);
            escortPhaseDistance = 0f;
            escortArcLengths[0] = 0f;
            Vector2 previous = EscortOffset(0f);
            for (int i = 1; i <= EscortArcSamples; i++)
            {
                Vector2 next = EscortOffset(2f * Mathf.PI * i / EscortArcSamples);
                escortArcLengths[i] = escortArcLengths[i - 1] + Vector2.Distance(previous, next);
                previous = next;
            }
        }

        private Vector2 EscortOffset(float angle) => new Vector2(
            -escortLateral * Mathf.Cos(angle), escortLongitudinal * Mathf.Sin(angle));

        // Arc-length lookup, not constant angular speed: the two drones keep
        // opposite positions and a steady patrol speed relative to the convoy.
        private float EscortAngle(int index)
        {
            float length = escortArcLengths[EscortArcSamples];
            float distance = Mathf.Repeat(escortPhaseDistance + index * length * .5f, length);
            int low = 0, high = EscortArcSamples;
            while (high - low > 1)
            {
                int mid = (low + high) / 2;
                if (escortArcLengths[mid] <= distance) low = mid;
                else high = mid;
            }
            float fraction = Mathf.InverseLerp(escortArcLengths[low], escortArcLengths[high], distance);
            return 2f * Mathf.PI * (low + fraction) / EscortArcSamples;
        }

        private void PlaceEscort(int index)
        {
            float angle = EscortAngle(index);
            Vector2 offset = EscortOffset(angle);
            Vector3 forward = convoy.Forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 p = convoy.HeadPosition - forward * convoy.HalfLength +
                right * offset.x + forward * offset.y;
            p.y = escortAltitude;
            Vector3 tangent = (right * (escortLateral * Mathf.Sin(angle)) +
                forward * (escortLongitudinal * Mathf.Cos(angle))).normalized;
            // Actual world movement includes the moving centre of the orbit.
            Vector3 velocity = tangent * Mathf.Max(1f, escortPatrolSpeed) + forward * convoy.Speed;
            escortWorldSpeeds[index] = velocity.magnitude;
            Vector3 heading = velocity.sqrMagnitude > .01f ? velocity : tangent;
            wardens[index].transform.SetPositionAndRotation(p, Quaternion.LookRotation(heading, Vector3.up));
        }

        private void ReleaseWardens()
        {
            foreach (var warden in wardens)
            {
                if (warden == null) continue;
                warden.StopCombat();
                ClearRounds(warden.transform);
                if (radar != null) radar.Unregister(warden.Contact);
                if (hitFeedback != null) hitFeedback.Untrack(warden.Health);
                if (convoyMode)
                {
                    warden.gameObject.SetActive(false);
                    Destroy(warden.gameObject);
                }
            }
            wardens.Clear();
        }

        private void OnDestroy()
        {
            ReleaseDestructionTrials();
            ReleaseBastions();
            interceptor?.Clear();
            convoy?.Dispose();
            interception?.Clear();
            if (mothDemoSite != null) Destroy(mothDemoSite.gameObject);
            ReleaseWardens();
            foreach (var gun in groundGuns) if (gun != null) gun.Unbind();
            groundGuns.Clear();
            if (missileStress != null) { missileStress.ResetTest(); Destroy(missileStress); }
            RemoveAllFighters();
            if (nursLauncher != null)
            {
                if (playerTrigger != null) playerTrigger.ConfigureDualSlots(null);
                nursLauncher.ResetMagazine();
                nursLauncher.gameObject.SetActive(false);
            }
            if (missileLauncher != null)
            {
                missileLauncher.CeaseFire(Time.time);
                missileLauncher.CancelMissiles();
                missileLauncher.gameObject.SetActive(false);
                if (playerTrigger != null) playerTrigger.ConfigureDualSlots(null);
            }
            if (targetingCursor != null)
            {
                targetingCursor.enabled = false;
                Destroy(targetingCursor);
            }
        }
    }
}
