# MISSION-01 — First Sortie

**Source of truth:** `Assets/_Project/Scenes/Mission01_FirstSortie.unity`. Authored mission, never regenerate. Campaign slice through assisted landing; final debrief, scoring and progression are not implemented as a finished system.

## Normal sortie

1. Disable Debug Air Start, Debug No Clip and Debug Landing Near Airfield in `Mission01LaunchSequence` before Play. W starts scripted takeoff from airfield B; handover to WASD at cruise Y78. LMB AC-01 only.
2. Navigate the desert rock formations and gaps in six Picket SAM coverage zones. Radar gives bearing/range context; Tab pauses for the tactical map, Esc or Tab closes it. Terrain proximity warning, collision, projectiles and operation bounds are live.
3. Destroy all three Watcher recon aircraft (primary objective). Optional Overlord may be destroyed or escape; neither optional outcome blocks completion of the primary objective.
4. After primary completion, use the cyan return cue and fly into the inbound runway corridor. Stay inside, aligned within 15 degrees of the inbound runway axis, for one continuous second. No S hold or fuel requirement. Departure, misalignment, lost focus or map opening clears capture progress.
5. The scripted landing takes control, disarms weapons/SAMs and clears owned threat projectiles. Descend, touch down and stop. `Landed` is the terminal success phase; R starts another sortie. A debug-only early landing is a test, not primary completion.

Terrain crash or boundary exit fails the sortie. R is handled by the mission owner, resets shared player pose/camera, health, targets and SAM state. Tab uses the shared pause owner, not an independent time-scale override.

## Authored settings (baseline 8134ba9)

| Item | Saved value |
| --- | --- |
| Operation X | -1100 to 2200 |
| Operation Z | -1400 to 2550 |
| Cruise altitude | 78 |
| Primary targets | 3 Watchers |
| SAM sites | 6 Pickets |
| Landing capture | 1 s, +/-15 degrees |
| Corridor | Actual runway width (scale 1), 150–350 approach offsets |
| Debug Air Start / No Clip / Landing Near Airfield | ON / ON / OFF (editor only) |

Use scene overrides for mission tuning rather than editing reusable units. The landing geometry adapter resolves the active `Mission01_HangarPass` pavement; the old disabled pavement is not the active runway. If fallback geometry is used, inspect the warning and verify the actual corridor before committing. The saved scene, not the older [rock planning sketch](MISSION_01_ROCK_LAYOUT_V3.md), defines the current bounds and placements.

## Authoring and validation

- Airfield, terrain, SAMs and objective actors already exist; no install menus required. Use prefab instances for new units. Inspect scene overrides and preserve GUIDs/valid FileIDs.
- **Air Combat > Missions > Bake Mission Map** updates the scene-derived map asset after terrain/geography changes; verify it against the actual scene. Dynamic SAM/target state and primary progress are supplied at runtime.
- Test a normal no-debug sortie, optional destruction and escape, warning distances, collision, pause/retry in each phase, runway capture, and touchdown. Debug Near Player on Overlord can accelerate isolated testing. Restore normal settings for the full test.
- Next design decision: tune the first sortie and define its result/debrief experience before adding more campaign content.

[Architecture](../GAMEPLAY_ARCHITECTURE.md) | [Combat](../GAMEPLAY_COMBAT.md) | [Handoff](../HANDOFF.md)
