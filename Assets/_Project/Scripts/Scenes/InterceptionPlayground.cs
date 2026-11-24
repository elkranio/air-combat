using System.Collections.Generic;
using UnityEngine;

namespace AirCombat
{
    // Temporary interception playtest, not a general mission or spawn system.
    public sealed class InterceptionPlayground
    {
        private readonly CruiseMissileTarget prefab;
        private readonly AircraftRadar radar;
        private readonly CombatHitFeedback feedback;
        private readonly int count;
        private readonly float interval;
        private readonly float lateralSpread;
        private readonly ArcadeMissileLauncher aam;
        private float safeExtent;
        private readonly List<CruiseMissileTarget> live = new List<CruiseMissileTarget>();
        private Vector3[] route;
        private float nextSpawn;
        private int spawned, destroyed, escaped;
        private bool running;

        public InterceptionPlayground(CruiseMissileTarget prefab, AircraftRadar radar,
            CombatHitFeedback feedback, ArcadeMissileLauncher aam, int count, float interval, float lateralSpread)
        {
            this.prefab = prefab;
            this.radar = radar;
            this.feedback = feedback;
            this.count = Mathf.Max(1, count);
            this.interval = Mathf.Max(.1f, interval);
            this.lateralSpread = Mathf.Max(0f, lateralSpread);
            this.aam = aam;
        }

        public string DebugStatus => $"Sparrow {destroyed}/{count} DOWN   {escaped} ESCAPED   {live.Count} LIVE";

        public void Restart(Vector3 origin, float boundary, float now)
        {
            Clear();
            spawned = destroyed = escaped = 0;
            float safe = safeExtent = Mathf.Max(50f, boundary - 65f);
            // Simple straight authoring-test lane across the map, at combat altitude.
            float z = Mathf.Clamp(origin.z + 100f, -safe, safe);
            float startX = Mathf.Clamp(origin.x - 200f, -safe, safe - 50f);
            route = new[] { new Vector3(startX, origin.y, z), new Vector3(safe, origin.y, z) };
            nextSpawn = now;
            running = true;
            Tick(now, 0f);
        }

        public void Tick(float now, float dt)
        {
            if (!running) return;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var target = live[i];
                if (target == null) { live.RemoveAt(i); continue; }
                if (!target.Alive)
                {
                    destroyed++;
                    if (feedback != null) feedback.EmitInterceptionBurst(target.transform.position, target.transform.forward);
                    Remove(i);
                    continue;
                }
                target.Tick(dt);
                if (target.Finished) { escaped++; Remove(i); }
            }
            // At most one spawn per frame: no overlapping catch-up batch after a stall.
            if (spawned < count && now >= nextSpawn)
            {
                // Each target gets its own parallel lane, still at combat altitude.
                // Sample inside the map rather than clamping multiple lanes onto its edge.
                float z = Random.Range(Mathf.Max(-safeExtent, route[0].z - lateralSpread),
                    Mathf.Min(safeExtent, route[0].z + lateralSpread));
                var lane = new[] {
                    new Vector3(route[0].x, route[0].y, z),
                    new Vector3(route[1].x, route[1].y, z)
                };
                var target = Object.Instantiate(prefab, lane[0], Quaternion.identity);
                target.name = $"CM-01 Sparrow {++spawned:00}";
                target.Begin(lane);
                live.Add(target);
                radar.Register(target);
                if (aam != null) aam.Register(target);
                nextSpawn = now + interval;
            }
            if (live.Count > 0) Physics.SyncTransforms();
            if (spawned == count && live.Count == 0) running = false;
        }

        public void SetAltitude(float y)
        {
            if (route != null)
                for (int i = 0; i < route.Length; i++) route[i].y = y;
            foreach (var target in live) if (target != null) target.SetAltitude(y);
            Physics.SyncTransforms();
        }

        private void Remove(int i)
        {
            var target = live[i];
            if (radar != null) radar.Unregister(target);
            if (target != null)
            {
                if (aam != null) aam.Unregister(target);
                target.gameObject.SetActive(false);
                Object.Destroy(target.gameObject);
            }
            live.RemoveAt(i);
        }

        public void Clear()
        {
            running = false;
            for (int i = live.Count - 1; i >= 0; i--) Remove(i);
        }
    }
}
