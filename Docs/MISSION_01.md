# Mission01 — First Sortie

Source of truth: `Assets/_Project/Scenes/Mission01_FirstSortie.unity`. Authored campaign slice through assisted landing. Final debrief/scoring/progression not implemented.

1. Disable Debug Air Start, Debug No Clip and Debug Landing Near Airfield on Mission01LaunchSequence before normal play (previous saved baseline had first two ON). W scripted takeoff; WASD handover at cruise Y78. LMB AC01 only.
2. Navigate desert rock formations and gaps in six Picket SAM coverage zones. Radar, terrain proximity cue, actual collision, projectile blocking and operation bounds are live. Tab tactical map; Esc/Tab close.
3. Destroy all three Watcher recon aircraft. Optional Overlord may be destroyed or escape; neither optional outcome blocks primary completion.
4. Follow cyan return cue into runway corridor after primary completion. Remain aligned within 15 degrees of inbound axis for one continuous second. No S hold/fuel requirement; departure, misalignment, lost focus or map opening clears capture.
5. Scripted landing disarms weapons/SAMs and clears owned threats; touch down and stop. Landed is success phase. R retries; debug early landing is not primary completion.

Terrain crash/boundary exit fails sortie. Mission owner handles R, resets shared player pose/camera, health, targets and SAM state; shared pause owner handles Tab. Engine audio mixer shared with M02 (historical Mission01EngineAudio class).

## Authoring and regression

Prior authored operation bounds X -1100..2200, Z -1400..2550, landing corridor width scale 1, approach offsets 150–350; verify scene Inspector before tuning. Active Mission01_HangarPass pavement defines runway, not old disabled pavement. If fallback geometry used, inspect warning/corridor. Scene defines actual geography, not earlier planning sketch.

All scene actors already authored; no install menus needed. Air Combat > Missions > Bake Mission Map after terrain/geography edits. Use prefab instances; preserve GUIDs/valid FileIDs. Check normal no-debug sortie, optional destruction/escape, collision/boundary, pause/retry, runway capture/touchdown. Debug Near Player on Overlord accelerates isolated checks; restore normal flags for full test. Final polish/results deferred while M03 behavior is prototyped.

[Architecture](../GAMEPLAY_ARCHITECTURE.md) | [Combat](../GAMEPLAY_COMBAT.md) | [Handoff](../HANDOFF.md)
