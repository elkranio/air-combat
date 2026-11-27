using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirCombat
{
    // PG08: explicit scene-local handles for the three fictional mission props.
    // No objective rules or hostile flight behavior in this visual atom.
    internal sealed class Mission03PowerComplex
    {
        private static readonly string[] Names = {
            "M03 / PG01 Transformer - VISUAL TRIAL",
            "M03 / PG02 Control Building - VISUAL TRIAL",
            "M03 / PG03 Transformer Long - VISUAL TRIAL"
        };
        private static readonly BuildingDestructionTrial.RuinProfile[] Profiles = {
            BuildingDestructionTrial.RuinProfile.CompactProp,
            BuildingDestructionTrial.RuinProfile.ControlBuilding,
            BuildingDestructionTrial.RuinProfile.LongProp
        };
        private readonly BuildingDestructionTrial[] targets = new BuildingDestructionTrial[Names.Length];
        internal IReadOnlyList<BuildingDestructionTrial> Targets => targets;

        internal static Mission03PowerComplex Bind(Scene scene)
        {
            // Resolve only the accepted authored roots, never instantiate or
            // reposition scene props. A future authoring pass can serialize these refs.
            var roots = scene.GetRootGameObjects();
            var props = new GameObject[Names.Length];
            for (int i=0; i<Names.Length; i++)
            {
                foreach (var root in roots)
                {
                    if (root.name != Names[i]) continue;
                    if (props[i] != null) { Debug.LogError("PG08: duplicate root " + Names[i]); return null; }
                    props[i] = root;
                }
                if (props[i] == null || props[i].transform.Find("Visual") == null ||
                    props[i].GetComponent<DamageableTarget>() != null)
                {
                    Debug.LogError("PG08: missing or already bound prop " + Names[i]);
                    return null;
                }
            }
            var site = new Mission03PowerComplex();
            for (int i=0; i<props.Length; i++)
            {
                var trial = props[i].AddComponent<BuildingDestructionTrial>();
                site.targets[i] = trial;
                if (trial.Initialize(i, Profiles[i], useSharedYard:true, installWeaponHitbox:false)) continue;
                foreach (var target in site.targets)
                    if (target != null) { target.enabled = false; Object.Destroy(target); }
                Debug.LogError("PG08: power complex visual binding failed.");
                return null;
            }
            return site;
        }

        internal void ResetTargets()
        {
            foreach (var target in targets)
                if (target != null) target.ResetTarget();
        }

#if UNITY_EDITOR
        internal void PreviewDestroy(int index)
        {
            if (index < 0 || index >= targets.Length || targets[index] == null) return;
            var target = targets[index];
            target.ApplyDamage(target.transform.position, 1);
        }
#endif
    }
}
