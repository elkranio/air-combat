# REF-01D — objective progress and target outcomes

Base `8b3c98e6596493256f6e6d002bbfb2d1447102c8` (C3 green).

`IObjectiveTarget` exposes pending, patrolling, charging, escaping, destroyed and escaped. Watcher and Overlord report their existing state without changing damage, movement, reset or radar behavior. `DestroyTargetsObjective` reads a fixed target list; only Destroyed counts, not Escaped or Pending. Missing, duplicate or uninitialized targets cannot unlock completion. No event subscription or cumulative counter is needed, so retry naturally restores progress from the units.

`Mission01ReconEncounter` retains the existing three-Watcher authoring validation, initialization and effects, but delegates counting to the shared objective. `Mission01LaunchSequence` consumes `IPrimaryObjectiveSource`, not the concrete encounter or hard-coded kill count. The same progress drives RTB, landing availability, recon HUD and tactical map snapshot. The map retains the authored transforms for markers and planned-data fallback; it reads generic target state instead of specific aircraft components. Optional status is independent of primary completion. Overlord's existing public ObjectiveStatus delegates to the shared display mapping. Objective-specific text, map composition and OnGUI drawing remain until REF-01E; no visual redesign.

Normal gameplay is unchanged. Invalid/uninitialized or duplicate target references now fail closed, and the map shows planned/pending rather than falsely reporting destroyed targets. No scene/prefab, serialized field, existing GUID, execution order, balance or flight changes.

## Local regression gate

- Compile; start Mission01; verify recon HUD 0/3 and Tab map 0/3. Check marker positions and optional label.
- Destroy each Watcher: HUD and map agree at 1/3, 2/3, 3/3; marker disappears immediately on kill. RTB arrow/landing only unlock at 3/3 with Debug Landing Near Airfield off.
- Let Overlord escape, then retry and destroy it: status differs, neither changes primary progress or landing eligibility.
- R during a falling wreck and after all kills: progress returns to 0/3 and markers restore. Tab/R ordering unchanged.
- With debug landing enabled, early landing remains a test-only result. With debug landing disabled, finish all three and land normally. Restore debug flags before commit.
- Unity compile and Play Mode are acceptance gates. No scene save or editor installer required.
