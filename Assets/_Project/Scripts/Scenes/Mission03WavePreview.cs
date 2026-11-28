using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // One authored arcade encounter. Uses fictional scene props and the existing
    // Sandbox flight and dive animation; no objective manager or enemy AI here.
    internal sealed class Mission03WavePreview
    {
        private readonly Mission03PowerComplex site;
        private readonly CruiseMissileTarget prefab;
        private readonly GameObject visualPrefab;
        private readonly AircraftRadar radar;
        private readonly ArcadeMissileLauncher aam;
        private readonly CombatHitFeedback feedback;
        private readonly float altitude, spacing;
        private readonly Rect bounds;
        // Game-only preview pacing: three repetitions of the existing animation clips.
        private const int PreviewCount = 9;
        private const float PreviewSpawnInterval = 1.15f;
        private readonly List<Entry> live = new List<Entry>(PreviewCount);
        private float spawnClock;
        private int total, spawned, down, saves, impacts;
        private bool started;

        private sealed class Entry
        {
            public CruiseMissileTarget Actor;
            public BuildingDestructionTrial Prop;
        }

        internal Mission03WavePreview(Mission03PowerComplex site, CruiseMissileTarget prefab,
            GameObject visualPrefab, AircraftRadar radar, ArcadeMissileLauncher aam,
            CombatHitFeedback feedback, float altitude, Rect bounds, float spacing)
        {
            this.site = site; this.prefab = prefab; this.visualPrefab = visualPrefab;
            this.radar = radar; this.aam = aam; this.feedback = feedback;
            this.altitude = altitude; this.bounds = bounds;
            this.spacing = Mathf.Clamp(spacing, 20f, 100f);
        }

        internal bool IsComplete => started && total > 0 && spawned == total && live.Count == 0;
        internal bool AllDown => IsComplete && down == total;

        internal string Status => !started ? "WAVE 1 / READY" :
            $"WAVE 1 / {down}/{total} DOWN / {saves} DIVE SAVES / {impacts} IMPACTS / {live.Count} LIVE" +
            (spawned < total ? $" / {total - spawned} QUEUED" :
                live.Count == 0 ? " / COMPLETE" : "");

        internal void Begin()
        {
            Clear();
            started = true;
            total = PreviewCount;
            SpawnNext();
            spawnClock = PreviewSpawnInterval;
            Physics.SyncTransforms();
        }

        private void SpawnNext()
        {
            var props = site.Targets;
            int i = spawned % props.Count;
            var prop = props[i];
            // Hand-authored arcade animation based on the M03 composition sketch.
            // Decorative sites have no effect on actor behavior.
            float side = i - (props.Count - 1) * .5f;
            Vector3 Scenic(float u, float v, float offsetX, float offsetZ) => new Vector3(
                Mathf.Lerp(bounds.xMin, bounds.xMax, u) + side * spacing * offsetX,
                altitude, Mathf.Lerp(bounds.yMin, bounds.yMax, v) + side * spacing * offsetZ);
            Vector3 start = Scenic(.016f, .92f, 0f, 1f);
            Vector3 scenicA = Scenic(.12f, .88f, 0f, 1f);
            Vector3 scenicB = Scenic(.17f, .80f, 1f, 0f);
            Vector3 scenicC = Scenic(.17f, .59f, 1f, 0f);
            Vector3 scenicD = Scenic(.23f, .45f, 1f, 0f);
            Vector3 scenicE = Scenic(.33f, .29f, 1f, 0f);
            Vector3 mark = prop.transform.position;
            mark.y = altitude;
            var clip = new[] { start, scenicA, scenicB, scenicC, scenicD, scenicE,
                mark + Vector3.forward * 300f, mark + Vector3.forward * 120f };
            var actor = Object.Instantiate(prefab, start, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(actor.gameObject, prop.gameObject.scene);
            actor.name = $"M03 / UD01 Moth {++spawned:00}";
            actor.InstallMothVisual(visualPrefab);
            actor.Begin(clip);
            live.Add(new Entry { Actor = actor, Prop = prop });
            radar.Register(actor);
            aam.Register(actor);
        }

        internal void Tick(float dt)
        {
            if (!started) return;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var entry = live[i];
                var actor = entry.Actor;
                if (actor == null) { Remove(i); continue; }
                if (!actor.Alive)
                {
                    down++;
                    if (actor.IsDiving) saves++;
                    feedback?.EmitInterceptionBurst(actor.transform.position, actor.transform.forward);
                    Remove(i);
                    continue;
                }
                actor.Tick(dt);
                if (!actor.Finished) continue;
                if (!actor.IsDiving)
                {
                    // Existing Sandbox rule: no new lock, already launched AAM may hit.
                    aam.Unregister(actor);
                    actor.BeginDive(entry.Prop.transform.position.y + .8f);
                    continue;
                }
                impacts++;
                if (!entry.Prop.IsDestroyed)
                    entry.Prop.ApplyDamage(actor.transform.position, 1);
                else feedback?.EmitInterceptionBurst(actor.transform.position, actor.transform.forward);
                Remove(i);
            }
            if (spawned < total)
            {
                spawnClock -= Mathf.Max(0f, dt);
                if (spawnClock <= 0f)
                {
                    SpawnNext();
                    spawnClock = PreviewSpawnInterval;
                }
            }
            if (live.Count > 0) Physics.SyncTransforms();
        }

        private void Remove(int index)
        {
            var actor = live[index].Actor;
            radar?.Unregister(actor);
            if (actor != null)
            {
                aam?.Unregister(actor);
                actor.gameObject.SetActive(false);
                Object.Destroy(actor.gameObject);
            }
            live.RemoveAt(index);
        }

        internal void Clear()
        {
            for (int i = live.Count - 1; i >= 0; i--) Remove(i);
            started = false;
            total = spawned = down = saves = impacts = 0;
            spawnClock = 0f;
        }
    }
}
