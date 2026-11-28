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
            [Min(1f)] public float approachSpeed = 175f;
            [Min(1f)] public float breakawaySpeed = 55f;
            [Min(1f)] public float returnSpeed = 165f;
            [Min(1f)] public float approachTurnRate = 30f;
            [Min(1f)] public float breakawayTurnRate = 32f;
            [Min(1f)] public float speedResponse = 45f;
            [Min(1f)] public float breakawayDeceleration = 100f;
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
        private readonly Rect? campaignBounds;
        private readonly Vector2? campaignEntry;
        private Vector3 returnPoint;
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
            CombatHitFeedback feedback, Settings tuning, float boundary,
            Rect? campaignBounds = null, Vector2? campaignEntry = null)
        {
            this.owner = owner; this.prefab = prefab; this.player = player; this.radar = radar;
            this.playerMissiles = playerMissiles; this.enemyMissiles = enemyMissiles;
            this.feedback = feedback; this.tuning = tuning ?? new Settings(); this.boundary = boundary;
            this.campaignBounds = campaignBounds; this.campaignEntry = campaignEntry;
        }

        internal bool IsResolved => phase == Phase.Escaped || phase == Phase.Destroyed;
        internal string Status => $"{phase}{(passedWithoutShot ? " / NO LOCK" : "")} | " +
            $"HP {(health != null ? health.Hp.ToString() : phase == Phase.Destroyed ? "0" : "-")} | " +
            $"{speed:F0} u/s | AAM {shots}/2";

        internal bool Restart(float startX, string callsign)
        {
            Clear();
            Vector2 entry = campaignEntry ?? new Vector2(0f, 650f);
            Vector3 start = new Vector3(entry.x + startX, player.position.y, entry.y);
            if (prefab == null || playerMissiles == null || enemyMissiles == null ||
                (!campaignBounds.HasValue && boundary <= 760f) || NearEdge(start, 110f))
            { Debug.LogError("IN02: missing model, launcher or test space.", owner); return false; }
            // Campaign entry is an authored arcade staging point. Sandbox stays southbound.
            returnPoint = start;
            float initialYaw = campaignEntry.HasValue ? 225f : 180f;
            actor = Object.Instantiate(prefab, start, Quaternion.Euler(0f, initialYaw, 0f), owner);
            actor.name = "IN02 / Interceptor " + callsign;
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
            yaw = initialYaw; age = 0f; shots = 0; passedWithoutShot = false;
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
                float exitYaw = ReturnYaw(position);
                TurnToward(exitYaw, tuning.breakawayTurnRate, dt);
                if (Mathf.Abs(Mathf.DeltaAngle(yaw, exitYaw)) < (campaignEntry.HasValue ? 1f : .1f) && age >= tuning.minimumBreakawaySeconds)
                { phase = Phase.Return; age = 0f; }
            }
            else if (phase == Phase.Return)
            {
                targetSpeed = tuning.returnSpeed;
                if (campaignEntry.HasValue) TurnToward(ReturnYaw(position), tuning.breakawayTurnRate, dt);
            }
            float response = phase == Phase.Breakaway && speed > targetSpeed
                ? tuning.breakawayDeceleration : tuning.speedResponse;
            speed = Mathf.MoveTowards(speed, Mathf.Max(1f, targetSpeed), Mathf.Max(1f, response) * dt);
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            actor.transform.SetPositionAndRotation(position + rotation * Vector3.forward * (speed * dt), rotation);
            // Exit the preview at its authored staging point (or the scene edge).
            // Already launched AAMs remain owned by the shared projectile service.
            bool returned = campaignEntry.HasValue && phase == Phase.Return &&
                Vector2.Distance(new Vector2(actor.transform.position.x, actor.transform.position.z),
                    new Vector2(returnPoint.x, returnPoint.z)) <= 90f;
            if (returned || NearEdge(actor.transform.position, 10f))
            { RemoveActor(); phase = Phase.Escaped; speed = 0f; }
        }

        private void TurnToward(float targetYaw, float rate, float dt) =>
            yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, Mathf.Max(1f, rate) * dt);

        private void BeginBreakaway() { phase = Phase.Breakaway; age = 0f; }

        private float ReturnYaw(Vector3 p)
        {
            if (!campaignEntry.HasValue) return 0f;
            Vector3 delta = returnPoint - p;
            return delta.x * delta.x + delta.z * delta.z > .01f
                ? Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg : yaw;
        }

        private bool NearEdge(Vector3 p, float margin)
        {
            if (!campaignBounds.HasValue)
                return Mathf.Abs(p.x) >= boundary - margin || Mathf.Abs(p.z) >= boundary - margin;
            Rect b = campaignBounds.Value;
            return p.x <= b.xMin + margin || p.x >= b.xMax - margin ||
                p.z <= b.yMin + margin || p.z >= b.yMax - margin;
        }

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

    // Shared arcade preview pair. Each interceptor keeps its own flight phase,
    // health, mounts and salvo; the shared missile service owns live projectiles.
    internal sealed class InterceptorPairTrial
    {
        private readonly Transform owner;
        private readonly EnemyMissileStressTest enemyMissiles;
        private readonly InterceptorPassTrial alpha, bravo;
        private readonly bool includeBravo;
        private readonly float delay, lateralOffset;
        private float remaining;
        private bool running, bravoPending, bravoStarted;

        internal InterceptorPairTrial(Transform owner, GameObject prefab, Transform player,
            AircraftRadar radar, ArcadeMissileLauncher playerMissiles, EnemyMissileStressTest enemyMissiles,
            CombatHitFeedback feedback, InterceptorPassTrial.Settings tuning, float boundary,
            bool includeBravo, float delay, float lateralOffset,
            Rect? campaignBounds = null, Vector2? campaignEntry = null)
        {
            this.owner = owner;
            this.enemyMissiles = enemyMissiles;
            this.includeBravo = includeBravo;
            this.delay = Mathf.Max(0f, delay);
            this.lateralOffset = Mathf.Max(0f, lateralOffset);
            alpha = new InterceptorPassTrial(owner, prefab, player, radar, playerMissiles,
                enemyMissiles, feedback, tuning, boundary, campaignBounds, campaignEntry);
            bravo = new InterceptorPassTrial(owner, prefab, player, radar, playerMissiles,
                enemyMissiles, feedback, tuning, boundary, campaignBounds, campaignEntry);
        }

        internal bool IsResolved => running && alpha.IsResolved && !bravoPending &&
            (!bravoStarted || bravo.IsResolved);
        internal string Status
        {
            get
            {
                if (!running) return "IN02 | INACTIVE";
                string wingman = !includeBravo ? "" : bravoPending ? $" | B: INBOUND {remaining:F1}s" :
                    bravoStarted ? $" | B: {bravo.Status}" : " | B: UNAVAILABLE";
                return $"IN02 | A: {alpha.Status}{wingman} | Hits {enemyMissiles.Hits} | R retry";
            }
        }

        internal bool Restart()
        {
            Clear();
            running = alpha.Restart(0f, "Alpha");
            if (!running) return false;
            remaining = delay;
            bravoPending = includeBravo;
            if (bravoPending && remaining <= 0f) SpawnBravo();
            return true;
        }

        internal void Tick(float dt, float now)
        {
            if (!running || dt <= 0f || MissionPause.IsTacticalMapPausedFor(owner.gameObject.scene)) return;
            alpha.Tick(dt, now);
            if (bravoPending)
            {
                remaining = Mathf.Max(0f, remaining - dt);
                if (remaining <= 0f) SpawnBravo();
            }
            else if (bravoStarted) bravo.Tick(dt, now);
        }

        private void SpawnBravo()
        {
            bravoPending = false;
            // Local right of a southbound approach is world -X. Delayed entry
            // gives longitudinal separation without spawning outside the arena.
            bravoStarted = bravo.Restart(-lateralOffset, "Bravo");
        }

        internal void SetAltitude(float y)
        {
            alpha.SetAltitude(y);
            bravo.SetAltitude(y); // A pending Bravo will spawn at the player's altitude.
        }

        internal void Clear()
        {
            running = bravoPending = bravoStarted = false;
            remaining = 0f;
            alpha.Clear();
            bravo.Clear();
        }
    }
}
