using UnityEngine;

namespace AirCombat
{
    // One-fighter encounter: spawn, radar registration, health and reset.
    // AIR-02 delegates flight/fire intent to the independent arcade pilot.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-70)]
    public sealed class Air01Encounter : MonoBehaviour
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField, Min(1f)] private float spawnRadius = 550f;
        [SerializeField] private ArcadeFighterPilot.Settings attack = new ArcadeFighterPilot.Settings();
        private JetAssembly player;
        private Camera view;
        private AircraftRadar radar;
        private PlayerHitPoints playerHealth;
        private GameObject enemy;
        private CombatAircraftFlight enemyFlight;
        private PlayerHitPoints enemyHealth;
        private RadarAirContact contact;
        private float boundary;
        private ArcadeFighterPilot pilot;
        private FlightController playerFlight;
        private PlayerWeaponTrigger playerTrigger;
        private Afterburner playerBoost;
        private BreakTurn playerBreak;
        private bool bound;
        private bool defeated;
        private bool victory;
        private GUIStyle resultStyle;

        public string DebugStatus => defeated ? "DEFEAT" : victory ? "VICTORY" : pilot == null ? "NO ENEMY" :
            $"Tiger {pilot.State} | HP {enemyHealth.Hp}/{enemyHealth.MaxHp} | passes {pilot.AttackPasses} | hits {pilot.HitCount}/{pilot.FiredCount}";

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
                Debug.LogError("AIR-01 encounter is missing its prefab or player binding.", this);
                return;
            }
            var team = player.GetComponent<AircraftTeam>();
            if (team == null) team = player.gameObject.AddComponent<AircraftTeam>();
            team.SetSide(AircraftSide.Player);
            playerHealth.InstallHitbox();
            playerFlight = player.GetComponent<FlightController>();
            playerTrigger = player.GetComponent<PlayerWeaponTrigger>();
            playerBoost = player.GetComponent<Afterburner>();
            playerBreak = player.GetComponent<BreakTurn>();
            bound = true;
        }

        private void Start() { Restart(); }

        public void Restart()
        {
            if (!bound) return;
            // Cancel a committed player burst before clearing the previous fight.
            if (playerTrigger != null) playerTrigger.enabled = false;
            ClearRounds(player.transform);
            RemoveEnemy();
            playerHealth.ResetHealth();
            if (playerFlight != null) playerFlight.enabled = true;
            if (playerTrigger != null) playerTrigger.enabled = true;
            defeated = victory = false;
            if (!TrySpawnPosition(out Vector3 position, out Quaternion rotation))
            {
                Debug.LogError("AIR-01: no off-screen spawn on the requested ring within map bounds.", this);
                return;
            }
            enemy = Instantiate(enemyPrefab, position, rotation);
            enemy.name = "EF-01 Tiger / AIR-02";
            enemyFlight = enemy.GetComponent<CombatAircraftFlight>();
            enemyHealth = enemy.GetComponent<PlayerHitPoints>();
            contact = enemy.GetComponent<RadarAirContact>();
            var enemyBoost = enemy.GetComponent<Afterburner>();
            var gun = enemy.GetComponentInChildren<AutocannonWeapon>();
            if (enemyFlight == null || enemyHealth == null || contact == null || enemyBoost == null ||
                gun == null || !gun.isActiveAndEnabled || gun.Preset == null)
            {
                Debug.LogError("AIR-02: incomplete fighter prefab or AC-01 mount.", enemy);
                RemoveEnemy();
                return;
            }
            enemyHealth.InstallHitbox();
            radar.Register(contact);
            pilot = new ArcadeFighterPilot(enemyFlight, enemyBoost, gun, player.transform,
                playerFlight, boundary, attack);
        }

        private bool TrySpawnPosition(out Vector3 position, out Quaternion rotation)
        {
            Vector3 origin = player.transform.position;
            float safe = boundary - 65f;
            // Random start angle, then a full ring of candidates. Fixed radius,
            // no biased clamp/teleport to a corner when the player is near an edge.
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < 72; i++)
            {
                Vector3 radial = Quaternion.AngleAxis(start + i * 5f, Vector3.up) * Vector3.forward;
                Vector3 candidate = origin + radial * spawnRadius;
                if (Mathf.Abs(candidate.x) > safe || Mathf.Abs(candidate.z) > safe) continue;
                if (view != null)
                {
                    Vector3 screen = view.WorldToViewportPoint(candidate);
                    if (screen.z > 0f && screen.x > -.1f && screen.x < 1.1f &&
                        screen.y > -.1f && screen.y < 1.1f) continue;
                }
                position = candidate;
                Vector3 heading = Quaternion.AngleAxis(Random.Range(-8f, 8f), Vector3.up) * -radial;
                rotation = Quaternion.LookRotation(heading, Vector3.up);
                return true;
            }
            position = default;
            rotation = Quaternion.identity;
            return false;
        }

        private void Update()
        {
            if (!bound || defeated || victory || enemy == null || enemyHealth == null) return;
            if (!playerHealth.IsAlive)
            {
                defeated = true;
                pilot?.Stop(Time.time);
                if (playerTrigger != null) playerTrigger.enabled = false;
                if (playerFlight != null) playerFlight.enabled = false;
                playerBreak?.CancelBreak();
                playerBoost?.Tick(false, 0f);
                ClearRounds(player.transform);
                RemoveEnemy();
                return;
            }
            if (!enemyHealth.IsAlive)
            {
                victory = true;
                RemoveEnemy();
                return;
            }
            pilot?.TickFlight(Time.time);
        }

        private void LateUpdate()
        {
            if (bound && !defeated && !victory && playerHealth.IsAlive &&
                enemyHealth != null && enemyHealth.IsAlive)
                pilot?.TickFire(Time.time);
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
            GUI.Label(rect, defeated ? "AIRCRAFT LOST — R TO RESTART" : "TIGER DOWN — R TO RESTART", resultStyle);
        }

        private static void ClearRounds(Transform owner)
        {
            if (owner == null) return;
            // Once per reset or elimination, never a per-frame search. No stale
            // hostile rounds or callbacks to a destroyed fighter survive the encounter.
            foreach (var round in FindObjectsByType<BallisticProjectile>(FindObjectsSortMode.None))
            {
                if (!round.IsOwnedBy(owner)) continue;
                round.gameObject.SetActive(false);
                Destroy(round.gameObject);
            }
        }

        public void SetAltitude(float y)
        {
            if (enemy == null) return;
            Vector3 p = enemy.transform.position;
            p.y = y;
            enemy.transform.position = p;
        }

        private void RemoveEnemy()
        {
            if (radar != null && contact != null) radar.Unregister(contact);
            if (enemy != null)
            {
                pilot?.Stop(Time.time);
                ClearRounds(enemy.transform);
                enemy.SetActive(false);
                Destroy(enemy);
            }
            pilot = null;
            enemy = null;
            enemyHealth = null;
            enemyFlight = null;
            contact = null;
        }

        private void OnDestroy() { RemoveEnemy(); }
    }
}
