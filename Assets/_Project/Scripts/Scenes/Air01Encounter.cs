using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // Two-fighter arcade encounter. Each Tiger owns an independent attack pilot;
    // the encounter owns pair spawning, radar lifetime, health and reset.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-70)]
    public sealed class Air01Encounter : MonoBehaviour
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private MissileProjectile missilePrefab;
        [SerializeField, Min(1f)] private float spawnRadius = 550f;
        [SerializeField, Min(20f)] private float pairSpacing = 45f;
        [SerializeField] private ArcadeFighterPilot.Settings attack = new ArcadeFighterPilot.Settings();

        [Header("MISSILE-03a / temporary evasion stress test")]
        [SerializeField] private EnemyMissileStressTest.Settings enemyMissiles = new EnemyMissileStressTest.Settings();
        private EnemyMissileStressTest missileStress;

        private sealed class Fighter
        {
            public int Index;
            public GameObject Root;
            public CombatAircraftFlight Flight;
            public PlayerHitPoints Health;
            public RadarAirContact Contact;
            public ArcadeFighterPilot Pilot;
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
        private BreakTurn playerBreak;
        private bool bound;
        private bool encounterReady;
        private bool defeated;
        private bool victory;
        private GUIStyle resultStyle;

        public string DebugStatus
        {
            get
            {
                if (defeated || victory)
                {
                    string result = defeated ? "DEFEAT" : "VICTORY";
                    return missileStress != null ? $"{result} | AAM {missileStress.Hits}/{missileStress.Launched} hits/fired" : result;
                }
                if (fighters.Count == 0) return "NO ENEMY";
                if (missileStress != null)
                    return $"Tigers {fighters.Count}/{FighterCount} | AAM {missileStress.InFlight} live | {missileStress.Hits}/{missileStress.Launched} hits/fired";
                // Both independent pilots remain visible on the H debug overlay.
                string status = $"Tigers {fighters.Count}/{FighterCount}";
                foreach (var fighter in fighters)
                    status += $" | {(fighter.Index == 0 ? "A" : "B")}:{fighter.Health.Hp}HP {fighter.Pilot.State}";
                return status;
            }
        }

        public void Bind(JetAssembly aircraft, Camera camera, AircraftRadar sensor,
            PlayerHitPoints health, float halfExtent)
        {
            player = aircraft;
            view = camera;
            radar = sensor;
            playerHealth = health;
            boundary = halfExtent;
            if (enemyPrefab == null || player == null || radar == null || playerHealth == null)
            {
                Debug.LogError("AIR encounter is missing its prefab or player binding.", this);
                return;
            }
            var team = player.GetComponent<AircraftTeam>();
            if (team == null) team = player.gameObject.AddComponent<AircraftTeam>();
            team.SetSide(AircraftSide.Player);
            playerHealth.InstallHitbox();
            // Production scene opt-in; legacy labs have no hit-feedback listener.
            hitFeedback = GetComponent<CombatHitFeedback>();
            if (hitFeedback == null) hitFeedback = gameObject.AddComponent<CombatHitFeedback>();
            hitFeedback.Bind(view);
            hitFeedback.Track(playerHealth, true);
            if (missilePrefab != null)
            {
                missileStress = gameObject.AddComponent<EnemyMissileStressTest>();
                missileStress.Bind(playerHealth, missilePrefab, hitFeedback, enemyMissiles);
            }
            playerFlight = player.GetComponent<FlightController>();
            playerTrigger = player.GetComponent<PlayerWeaponTrigger>();
            if (playerTrigger != null && view != null)
            {
                targetingCursor = view.gameObject.AddComponent<TargetingCursor>();
                targetingCursor.Bind(view);
                // AC-02 remains an available prefab/preset, but is not equipped here.
                foreach (var gun in player.GetComponentsInChildren<AutocannonWeapon>(true))
                    if (gun.Preset != null && gun.Preset.Continuous) gun.enabled = false;
                if (missilePrefab != null)
                {
                    var mount = new GameObject("AAM / launcher");
                    mount.transform.SetParent(player.transform, false);
                    missileLauncher = mount.AddComponent<ArcadeMissileLauncher>();
                    missileLauncher.Bind(view, targetingCursor, player, missilePrefab, hitFeedback);
                }
                else Debug.LogError("AIR encounter: missing AAM missile prefab.", this);
                playerTrigger.ConfigureDualSlots(missileLauncher);
            }
            playerBoost = player.GetComponent<Afterburner>();
            playerBreak = player.GetComponent<BreakTurn>();
            bound = true;
        }

        private void Start() { Restart(); }

        public void Restart()
        {
            if (!bound) return;
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
            var flight = root.GetComponent<CombatAircraftFlight>();
            var health = root.GetComponent<PlayerHitPoints>();
            var contact = root.GetComponent<RadarAirContact>();
            var boost = root.GetComponent<Afterburner>();
            var gun = root.GetComponentInChildren<AutocannonWeapon>();
            if (flight == null || health == null || contact == null || boost == null ||
                gun == null || !gun.isActiveAndEnabled || gun.Preset == null)
            {
                Debug.LogError("AIR encounter: incomplete Tiger prefab or AC-01 mount.", root);
                root.SetActive(false);
                Destroy(root);
                return false;
            }

            health.InstallHitbox();
            if (hitFeedback != null) hitFeedback.Track(health, false);
            if (missileLauncher != null) missileLauncher.Register(health);
            if (missileStress != null) missileStress.Register(health);
            var fighter = new Fighter
            {
                Index = index,
                Root = root,
                Flight = flight,
                Health = health,
                Contact = contact,
                Pilot = new ArcadeFighterPilot(flight, boost, gun, player.transform,
                    playerFlight, boundary, (attack ?? new ArcadeFighterPilot.Settings()).CreateVariant(later),
                    index == 0 ? -1f : 1f)
            };
            fighters.Add(fighter);
            radar.Register(contact);
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
            if (!bound || !encounterReady || defeated || victory) return;
            if (!playerHealth.IsAlive)
            {
                defeated = true;
                if (missileStress != null) missileStress.StopTest();
                if (missileLauncher != null) missileLauncher.CancelMissiles();
                if (playerTrigger != null) playerTrigger.enabled = false;
                if (hitFeedback != null)
                    hitFeedback.SpawnWreck(playerHealth, playerFlight != null ? playerFlight.CurrentSpeed : 70f);
                if (playerFlight != null) playerFlight.enabled = false;
                playerBreak?.CancelBreak();
                playerBoost?.Tick(false, 0f);
                ClearRounds(player.transform);
                for (int i = fighters.Count - 1; i >= 0; i--)
                    RemoveFighterAt(i, !fighters[i].Health.IsAlive);
                return;
            }

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
                fighters[i].Pilot.TickFlight(Time.time, wingman);
            }
        }

        private void LateUpdate()
        {
            if (!bound || !encounterReady || defeated || victory || !playerHealth.IsAlive) return;
            if (missileStress != null) missileStress.Tick(Time.time);
            // Isolate missile evasion during this temporary test; uncheck to
            // combine the original cannon attack passes with the missile pressure.
            if (missileStress == null || enemyMissiles == null || !enemyMissiles.missileOnly)
                foreach (var fighter in fighters)
                    if (fighter.Health.IsAlive) fighter.Pilot.TickFire(Time.time);
        }

        private void OnGUI()
        {
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
            foreach (var round in FindObjectsByType<BallisticProjectile>(FindObjectsSortMode.None))
            {
                if (!round.IsOwnedBy(owner)) continue;
                round.gameObject.SetActive(false);
                Destroy(round.gameObject);
            }
        }

        public void SetAltitude(float y)
        {
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
                if (destroyed) hitFeedback.SpawnWreck(fighter.Health, fighter.Flight.CurrentSpeed);
                hitFeedback.Untrack(fighter.Health);
            }
            if (radar != null && fighter.Contact != null) radar.Unregister(fighter.Contact);
            if (fighter.Root != null)
            {
                fighter.Pilot?.Stop(Time.time);
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
            if (missileStress != null) { missileStress.ResetTest(); Destroy(missileStress); }
            RemoveAllFighters();
            if (missileLauncher != null)
            {
                missileLauncher.CeaseFire(Time.time);
                missileLauncher.gameObject.SetActive(false);
                if (playerTrigger != null) playerTrigger.ConfigureDualSlots(null);
                Destroy(missileLauncher.gameObject);
            }
            if (targetingCursor != null)
            {
                targetingCursor.enabled = false;
                Destroy(targetingCursor);
            }
        }
    }
}
