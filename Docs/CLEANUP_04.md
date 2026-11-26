# CLEAN-04 — Unity API modernization

Base `3b244f16cb2a24c49463fb7a1b0e4d109f443ea7`.

Migrate nine calls in four runtime scripts to Unity 6000.6 APIs. Use FindAnyObjectByType for the single active arcade camera in the supported scenes; the selection of the lowest InstanceID is not required. Use the new unsorted FindObjectsByType overloads for rounds, hostile aircraft and Mantis guns. Preserve FindObjectsInactive.Include for Mantis, default exclusion elsewhere, owner/scene filters, and target distance selection. No changes to scenes, prefabs, gameplay tuning or execution order. As before, equal-distance targets have no guaranteed tie-break order. Multi-camera/additive scenes would need explicit camera binding, outside this migration.

References: https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/object/findobjectsbytype and https://docs.unity.com/en-us/engine/6000.6/manual/scripting/get-started/instanceid-to-entityid-migration

## Local regression gate

- Recompile in Unity; check Console for CS0618 and other warnings.
- Mission01: takeoff/debug air start, hit shake, boundary/terrain warnings, landing, R with active rounds.
- CombatSandbox: all three modes; Mantis binding, RMB target marker, restart with active rounds.
- Confirm there are no unexpected modified scene or prefab files.
