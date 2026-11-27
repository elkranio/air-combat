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
        private readonly float altitude, startDistance, spacing;
        private readonly List<Entry> live = new List<Entry>(3);
        private int total, down, saves, impacts;
        private bool started;

        private sealed class Entry
        {
            public CruiseMissileTarget Actor;
            public BuildingDestructionTrial Prop;
        }

        internal Mission03WavePreview(Mission03PowerComplex site, CruiseMissileTarget prefab,
            GameObject visualPrefab, AircraftRadar radar, ArcadeMissileLauncher aam,
            CombatHitFeedback feedback, float altitude, float startDistance, float spacing)
        {
            this.site = site; this.prefab = prefab; this.visualPrefab = visualPrefab;
            this.radar = radar; this.aam = aam; this.feedback = feedback;
            this.altitude = altitude; this.startDistance = Mathf.Clamp(startDistance, 700f, 1500f);
            this.spacing = Mathf.Clamp(spacing, 20f, 100f);
        }

        internal string Status => !started ? "WAVE 1 / READY" :
            $"WAVE 1 / {down}/{total} DOWN / {saves} DIVE SAVES / {impacts} IMPACTS / {live.Count} LIVE" +
            (live.Count == 0 ? " / COMPLETE" : "");

        internal void Begin()
        {
            Clear();
            started = true;
            var props = site.Targets;
            Vector3 stage = Vector3.zero;
            foreach (var prop in props) stage += prop.transform.position;
            stage /= props.Count;
            stage.y = altitude;

            // Fixed, legible positions for three actors in the fictional game scene.
            // These are authored animation paths, not target selection or guidance.
            for (int i = 0; i < props.Count; i++)
            {
                var prop = props[i];
                float side = i - (props.Count - 1) * .5f;
                Vector3 start = stage + new Vector3(side * spacing, 0f,
                    startDistance + (i == 1 ? -spacing * .6f : 0f));
                Vector3 stageEnd = stage + new Vector3(side * spacing, 0f, 520f);
                Vector3 mark = prop.transform.position;
                mark.y = altitude;
                var clip = new[] { start, stageEnd, mark + Vector3.forward * 300f,
                    mark + Vector3.forward * 120f };
                var actor = Object.Instantiate(prefab, start, Quaternion.identity);
                SceneManager.MoveGameObjectToScene(actor.gameObject, prop.gameObject.scene);
                actor.name = $"M03 / UD01 Moth {i + 1:00}";
                actor.InstallMothVisual(visualPrefab);
                actor.Begin(clip);
                live.Add(new Entry { Actor = actor, Prop = prop });
                radar.Register(actor);
                aam.Register(actor);
                total++;
            }
            Physics.SyncTransforms();
        }

        internal void Tick(float dt)
        {
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
            total = down = saves = impacts = 0;
        }
    }
}
