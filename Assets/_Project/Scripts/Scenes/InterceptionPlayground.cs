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
        private readonly List<CruiseMissileTarget> live = new List<CruiseMissileTarget>();
        private Vector3[] route;
        private float nextSpawn;
        private int spawned, destroyed, escaped;
        private bool running;

        public InterceptionPlayground(CruiseMissileTarget prefab, AircraftRadar radar,
            CombatHitFeedback feedback, int count, float interval)
        {
            this.prefab = prefab;
            this.radar = radar;
            this.feedback = feedback;
            this.count = Mathf.Max(1, count);
            this.interval = Mathf.Max(.1f, interval);
        }

        public string DebugStatus => $"Sparrow {destroyed}/{count} DOWN   {escaped} ESCAPED   {live.Count} LIVE";

        public void Restart(Vector3 origin, float boundary, float now)
        {
            Clear();
            spawned = destroyed = escaped = 0;
            float safe = Mathf.Max(50f, boundary - 65f);
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
                var target = Object.Instantiate(prefab, route[0], Quaternion.identity);
                target.name = $"CM-01 Sparrow {++spawned:00}";
                target.Begin(route);
                live.Add(target);
                radar.Register(target);
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
