using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AirCombat
{
    // One test sortie. No respawn, pursuit after the salvo, or airfield logistics yet.
    internal sealed class InterceptorPassTrial
    {
        [Serializable]
        internal sealed class Settings
        {
            [Min(1f)] public float approachSpeed = 96f;
            [Min(1f)] public float breakawaySpeed = 55f;
            [Min(1f)] public float returnSpeed = 125f;
            [Min(1f)] public float approachTurnRate = 30f;
            [Min(1f)] public float breakawayTurnRate = 32f;
            [Min(1f)] public float speedResponse = 45f;
            [Min(.1f)] public float salvoGap = .38f;
            [Min(1f)] public float approachTimeout = 9f;
            [Min(0f)] public float minimumBreakawaySeconds = 2.2f;
            [Min(1)] public int hp = 5;
        }

        private enum Phase { Approach, Salvo, Breakaway, Return, Escaped, Destroyed }
        private readonly Transform owner, player;
        private readonly GameObject prefab;
        private readonly AircraftRadar radar;
        private readonly ArcadeMissileLauncher playerMissiles;
        private readonly EnemyMissileStressTest enemyMissiles;
        private readonly CombatHitFeedback feedback;
        private readonly Settings tuning;
        private readonly float boundary;
        private GameObject actor;
        private PlayerHitPoints health;
        private RadarAirContact contact;
        private readonly GameObject[] missileProps = new GameObject[2];
        private readonly Vector3[] mounts = new Vector3[2];
        private Phase phase;
        private float speed, yaw, age;
        private int shots;
        private bool passedWithoutShot;

        internal InterceptorPassTrial(Transform owner, GameObject prefab, Transform player,
            AircraftRadar radar, ArcadeMissileLauncher playerMissiles, EnemyMissileStressTest enemyMissiles,
            CombatHitFeedback feedback, Settings tuning, float boundary)
        {
            this.owner = owner; this.prefab = prefab; this.player = player; this.radar = radar;
            this.playerMissiles = playerMissiles; this.enemyMissiles = enemyMissiles;
            this.feedback = feedback; this.tuning = tuning ?? new Settings(); this.boundary = boundary;
        }

        internal string Status => $"IN02 | {phase}{(passedWithoutShot ? " / NO LOCK" : "")} | " +
            $"HP {(health != null ? health.Hp : phase == Phase.Destroyed ? 0 : tuning.hp)} | " +
            $"{speed:F0} u/s | AAM {shots}/2 | Hits {enemyMissiles.Hits} | R retry";

        internal bool Restart()
        {
            Clear();
            if (prefab == null || playerMissiles == null || enemyMissiles == null || boundary < 500f)
            { Debug.LogError("IN02: missing model, launcher or test space.", owner); return false; }
            // Deterministic head-on test: player (0,-300), interceptor (0,+300).
            actor = Object.Instantiate(prefab, new Vector3(0f, player.position.y, 300f),
                Quaternion.Euler(0f, 180f, 0f), owner);
            actor.name = "IN02 / Interceptor combat";
            Transform visual = actor.transform.Find("Visual");
            for (int i = 0; i < missileProps.Length; i++)
            {
                var body = visual != null ? visual.Find(i == 0 ? "L missile body" : "R missile body") : null;
                var filter = body != null ? body.GetComponent<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null)
                { Debug.LogError("IN02: expected IN01 missile mesh missing.", owner); Clear(); return false; }
                missileProps[i] = body.gameObject;
                mounts[i] = actor.transform.InverseTransformPoint(body.TransformPoint(filter.sharedMesh.bounds.center));
            }
            health = actor.AddComponent<PlayerHitPoints>();
            health.InstallHitbox();
            health.ConfigureAircraftHealth(tuning.hp);
            var box = actor.GetComponent<BoxCollider>();
            // IN01 V2 visual is 10.1 x 3.0 x 17.64. Generous central combat volume.
            box.center = new Vector3(0f, .5f, .2f);
            box.size = new Vector3(9.5f, 3f, 16f);
            actor.AddComponent<AircraftTeam>().SetSide(AircraftSide.Hostile);
            contact = actor.AddComponent<RadarAirContact>();
            feedback.Track(health, false, visual);
            radar.Register(contact);
            playerMissiles.Register(health);
            speed = Mathf.Max(1f, tuning.approachSpeed);
            yaw = 180f; age = 0f; shots = 0; passedWithoutShot = false;
            phase = Phase.Approach;
            Physics.SyncTransforms();
            return true;
        }

        internal void Tick(float dt, float now)
        {
            if (actor == null || dt <= 0f) return;
            if (!health.IsAlive)
            {
                feedback.SpawnWreck(health, speed);
                RemoveActor(); phase = Phase.Destroyed; speed = 0f;
                return;
            }
            if (MissionPause.IsTacticalMapPausedFor(owner.gameObject.scene)) return;
            Vector3 position = actor.transform.position;
            Vector3 delta = player.position - position; delta.y = 0f;
            age += dt;
            // Acquire in a real forward cone. A sharp player turn can make the
            // approach miss; do not grant a teleport, snap yaw or auto-fire.
            if (phase == Phase.Approach)
            {
                if (enemyMissiles.TryBeginSalvo(health, now, tuning.salvoGap, mounts, OnShot))
                { phase = Phase.Salvo; age = 0f; }
                else if (age >= tuning.approachTimeout || delta.magnitude < 65f || NearEdge(position, 110f))
                { passedWithoutShot = true; BeginBreakaway(); }
            }
            else if (phase == Phase.Salvo && !enemyMissiles.SalvoPending(health)) BeginBreakaway();

            float targetSpeed = tuning.approachSpeed;
            if (phase == Phase.Approach || phase == Phase.Salvo)
            {
                if (delta.sqrMagnitude > .01f)
                    TurnToward(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, tuning.approachTurnRate, dt);
            }
            else if (phase == Phase.Breakaway)
            {
                targetSpeed = tuning.breakawaySpeed;
                TurnToward(0f, tuning.breakawayTurnRate, dt);
                if (Mathf.Abs(Mathf.DeltaAngle(yaw, 0f)) < .1f && age >= tuning.minimumBreakawaySeconds)
                { phase = Phase.Return; age = 0f; }
            }
            else if (phase == Phase.Return)
            {
                targetSpeed = tuning.returnSpeed;
            }
            speed = Mathf.MoveTowards(speed, Mathf.Max(1f, targetSpeed), Mathf.Max(1f, tuning.speedResponse) * dt);
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            actor.transform.SetPositionAndRotation(position + rotation * Vector3.forward * (speed * dt), rotation);
            // Test-zone exit, not an airfield landing. Keep already launched AAMs alive.
            if (NearEdge(actor.transform.position, 10f))
            { RemoveActor(); phase = Phase.Escaped; speed = 0f; }
        }

        private void TurnToward(float targetYaw, float rate, float dt) =>
            yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, Mathf.Max(1f, rate) * dt);

        private void BeginBreakaway() { phase = Phase.Breakaway; age = 0f; }

        private bool NearEdge(Vector3 p, float margin) =>
            Mathf.Abs(p.x) >= boundary - margin || Mathf.Abs(p.z) >= boundary - margin;

        private void OnShot(int index)
        {
            shots++;
            if (index >= 0 && index < missileProps.Length && missileProps[index] != null)
                missileProps[index].SetActive(false);
        }

        internal void SetAltitude(float y)
        {
            if (actor == null) return;
            Vector3 p = actor.transform.position; p.y = y; actor.transform.position = p;
        }

        private void RemoveActor()
        {
            if (health != null)
            {
                enemyMissiles?.Unregister(health);
                playerMissiles?.Unregister(health);
                feedback?.Untrack(health);
            }
            if (contact != null) radar?.Unregister(contact);
            if (actor != null) { actor.SetActive(false); Object.Destroy(actor); }
            actor = null; health = null; contact = null;
            Array.Clear(missileProps, 0, missileProps.Length);
        }

        internal void Clear() => RemoveActor();
    }
}
