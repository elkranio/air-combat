using UnityEngine;

namespace AirCombat
{
    // Temporary one-fighter encounter. AIR-01 validates assembly, movement,
    // spawn, radar and damage. No combat decisions or firing requests yet.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-70)]
    public sealed class Air01Encounter : MonoBehaviour
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField, Min(1f)] private float spawnRadius = 550f;
        [SerializeField, Min(0f)] private float initialBoostSeconds = .8f;
        private JetAssembly player;
        private Camera view;
        private AircraftRadar radar;
        private PlayerHitPoints playerHealth;
        private GameObject enemy;
        private CombatAircraftFlight enemyFlight;
        private PlayerHitPoints enemyHealth;
        private RadarAirContact contact;
        private float boundary;
        private float spawnedAt;
        private bool returning;
        private bool bound;

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
            bound = true;
        }

        private void Start() { Restart(); }

        public void Restart()
        {
            if (!bound) return;
            // Clear only this encounter's modular gun rounds; no previous sortie
            // may hit the new fighter. This search runs once per reset, not per frame.
            foreach (var round in FindObjectsByType<BallisticProjectile>(FindObjectsSortMode.None))
            {
                if (!round.IsOwnedBy(player.transform) && (enemy == null || !round.IsOwnedBy(enemy.transform))) continue;
                round.gameObject.SetActive(false);
                Destroy(round.gameObject);
            }
            RemoveEnemy();
            playerHealth.ResetHealth();
            if (!TrySpawnPosition(out Vector3 position, out Quaternion rotation))
            {
                Debug.LogError("AIR-01: no off-screen spawn on the requested ring within map bounds.", this);
                return;
            }
            enemy = Instantiate(enemyPrefab, position, rotation);
            enemy.name = "EF-01 Tiger / AIR-01";
            enemyFlight = enemy.GetComponent<CombatAircraftFlight>();
            enemyHealth = enemy.GetComponent<PlayerHitPoints>();
            contact = enemy.GetComponent<RadarAirContact>();
            if (enemyFlight == null || enemyHealth == null || contact == null)
            {
                Debug.LogError("AIR-01: incomplete fighter prefab.", enemy);
                RemoveEnemy();
                return;
            }
            enemyHealth.InstallHitbox();
            radar.Register(contact);
            spawnedAt = Time.time;
            returning = false;
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
            if (enemy == null || enemyHealth == null) return;
            if (!enemyHealth.IsAlive) { RemoveEnemy(); return; }
            // Temporary fly-through target, not combat AI. Fly straight until
            // near the edge, then return toward map centre with normal steering.
            Vector3 p = enemy.transform.position;
            float extent = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z));
            if (extent > boundary - 150f) returning = true;
            else if (extent < boundary - 300f) returning = false;
            float steer = 0f;
            if (returning)
            {
                Vector3 inward = new Vector3(-p.x, 0f, -p.z);
                steer = Mathf.Clamp(Vector3.SignedAngle(enemy.transform.forward, inward, Vector3.up) / 30f, -1f, 1f);
            }
            enemyFlight.SetPilotCommand(steer, !returning && Time.time - spawnedAt < initialBoostSeconds);
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
                enemy.SetActive(false);
                Destroy(enemy);
            }
            enemy = null;
            enemyHealth = null;
            enemyFlight = null;
            contact = null;
        }

        private void OnDestroy() { RemoveEnemy(); }
    }
}
