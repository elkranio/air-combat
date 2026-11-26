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
        public enum SandboxMode { Dogfight = 0, GroundAttack = 1, Interception = 2 }
        [Header("Combat sandbox / select before Play")]
        [SerializeField] private SandboxMode mode = SandboxMode.GroundAttack;
        private bool groundMode;
        [Header("Cruise missile interception")]
        [SerializeField] private CruiseMissileTarget sparrowPrefab;
        [SerializeField, Min(1)] private int interceptionCount = 10;
        [SerializeField, Min(.1f)] private float interceptionInterval = .7f;
        [SerializeField, Min(0f)] private float interceptionLateralSpread = 60f;
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
            if (mode != SandboxMode.Dogfight && mode != SandboxMode.GroundAttack && mode != SandboxMode.Interception)
            {
                Debug.LogError("Combat sandbox: invalid mode.", this);
                return;
            }
            interceptionMode = mode == SandboxMode.Interception;
            groundMode = mode == SandboxMode.GroundAttack;
            player = aircraft;
            view = camera;
            radar = sensor;
            playerHealth = health;
            boundary = halfExtent;
            if ((!groundMode && !interceptionMode && enemyPrefab == null) ||
                (interceptionMode && (sparrowPrefab == null || missilePrefab == null)) || player == null || radar == null || playerHealth == null)
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
            if (!groundMode && !interceptionMode && missilePrefab != null)
            {
                missileStress = gameObject.AddComponent<EnemyMissileStressTest>();
                missileStress.Bind(playerHealth, missilePrefab, hitFeedback, enemyMissiles, radar);
                gameObject.AddComponent<EnemyMissileThreatHud>().Bind(view, missileStress);
            }
            // AC-02 remains a prefab option, not an equipped weapon in this test.
            foreach (var gun in player.GetComponentsInChildren<AutocannonWeapon>(true))
                if (gun.Preset != null && gun.Preset.Continuous) gun.enabled = false;
            if (groundMode)
            {
                nursLauncher = groundLauncher;
                nursLauncher.gameObject.SetActive(true);
                nursLauncher.Bind(playerFlight, playerBoost, playerTrigger, view);
                playerTrigger.ConfigureDualSlots(nursLauncher);
            }
            else
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
            if (interceptionMode)
                interception = new InterceptionPlayground(sparrowPrefab, radar, hitFeedback,
                    missileLauncher, interceptionCount, interceptionInterval, interceptionLateralSpread);
            bound = true;
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
            if (interceptionMode)
            {
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
                for (int i = fighters.Count - 1; i >= 0; i--)
                    RemoveFighterAt(i, !fighters[i].Health.IsAlive);
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
            if (missileStress != null) missileStress.Tick(Time.time);
            // Combined combat: gun requests tick independently of AAM readiness,
            // pending launch warnings and the number of missiles in flight.
            foreach (var fighter in fighters)
                if (fighter.Health.IsAlive) fighter.Unit.TickFire(Time.time);
        }

        private void OnGUI()
        {
            if (interceptionMode && !defeated && Event.current.type == EventType.Repaint && interception != null)
            {
                var counter = new Rect((Screen.width - 340f) * .5f, 12f, 340f, 30f);
                GUI.Box(counter, interception.DebugStatus);
            }
            if ((!victory && !defeated) || Event.current.type != EventType.Repaint) return;
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
                "TIGERS DOWN — R TO RESTART", resultStyle);
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

        private void OnDestroy()
        {
            interception?.Clear();
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
