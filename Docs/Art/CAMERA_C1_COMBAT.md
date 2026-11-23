# C1b/C2 — full combat study, rotating vs fixed 50° camera

Status: **C1b played: visually promising but combat harder, reduced anticipation and mild motion sickness while the world rotates. C2 fixed-yaw 50° now ready for Play Mode evaluation.** Reference: [ENVIRONMENT_MAP_PLAN.md](ENVIRONMENT_MAP_PLAN.md).

## Rationale

C1 (the copied V19 scene) was a positive visual experiment; the user explicitly prefers **50°**. Now test whether that camera remains pleasant when real combat, cursor-based missile acquisition, ground weapons, incoming projectiles, altitude reference, and radar are visible. This is still a **camera/combat experiment**, not the E1 production environment or new gameplay.

## One-time authoring (from a clean master)

1. Apply the C1b patch and allow Unity to compile. Do **not** rerun the C1 builder or the old AA/SAM/HUD installers.
2. In Edit Mode with scenes saved, use **Air Combat > Art > C1b - Create 3-4 Camera Combat Study**.
3. Open **Assets/_Project/Scenes/FrontierCameraCombatC1.unity** and Play.
4. The generated scene is persistent. After a good test, save and commit it with its `.meta`. The new authoring menu deliberately refuses to overwrite it.

## What is reused (not rewritten)

- V19 environment via already committed `FrontierCameraC1` scene. Existing art-study camera and renderer profile, **not** the new shared CombatLightingRig yet. C1b disables V19's B-to-switch-materials preview (the saved full AO+normal materials remain) so `B` exclusively toggles fighter evasion.
- `PlayerJet`, `EnemyJet`, `AA1Autocannon`, `SAM1Launcher`, `MilitaryDepot` x4 and `CombatHUD` prefab assets.
- Existing `GroundAttackLabSession` for six slots, their ballistics, health, AA/SAM, sensor registrations and HUD. The `C1AirTargetSession` adapter attaches a fighter target for slots 1–3 and the **actual existing player A2A missile prefab**, so the cursor-lock test is real.
- `Scorched terrain` has no physical collider in V19. C1b adds **one MeshCollider** only in the copied study so bombs/rockets can hit its geometry. Other decorative V19 meshes are not yet fully physically collidable; canyon collision is deferred to E3.

## Controls and checks

- **Camera (C2):** `C` compares **fixed-world 50°** (default, yaw -45°, jet turns onscreen) vs **rotating-world 50°** (old C1b). Both use the same pitch and forward lead, so this A/B changes only world rotation. `J/K/L` select **50/58/65°** for either C2 mode; default **50°**.
- **Flight:** `A/D` steer, `W` boost, `Q/E` 90° Break Turn, `R` reset, `H` overlays.
- **Important:** `1/2/3` in **C1b** select weapons, NOT camera pitch. The original art-only C1 still uses `1/2/3` for pitch.
- **Weapons:** `1` BURST, `2` GAUSS, `3` locked A2A MISSILE, `4` bombs, `5` cabrage NURS, `6` direct NURS, `LMB` fire/commit. `B` toggles fighter's existing evasion AI.
- **AA1/SAM1:** physical shots and incoming missiles; watch HP/fuel, SAM alert, radar detection/zone rings. No flares or impact-prediction marker.
- **Radar:** other labs remain north-up. In C2 it follows the camera's *actual* screen orientation: at fixed yaw -45°, contact positions stay aligned with the visible world and the aircraft arrow rotates; in heading-locked mode the arrow faces upper-right and contacts rotate with the scene. C never requires manual radar-mode selection.
- Test fighter lock and guided missile immediately, then BURST/GAUSS, then depots with the three ground-attack modes. Try sharp left/right turns while monitoring radar, target on screen, AA lead and SAM incoming.

## Limits / verdict

This intentionally does **not** transplant the accepted CombatLightingRig to V19, fix V19 blur/texel mismatch, add 3D obstacle crash logic, rebuild gun balance or introduce missions. Those belong in the separate map phases. Terrain collider is real; old decorative props may still lack colliders. Keep C1 and all combat labs untouched if the result is rejected.

**C1b observed verdict:** At 50° the environment reads more volumetrically and feels lively. But full combat is less readable, there is less time/space to anticipate threats, and rotating terrain causes noticeable mild motion sickness. This is **not approved as the final camera**.

**C2 question:** Can a **fixed-yaw 50°** camera retain 3D depth and remove nausea while restoring aim, navigation and AA/SAM avoidance? On the already committed `FrontierCameraCombatC1.unity` hit Play; no editor menu/rebuild/asset migration. Test `1` burst, `3` lock, `4–6` ground attacks, Q/E and SAM dodges, and compare `C` briefly at the *same* pitch. Prioritize comfort over visuals. If fixed 50° loses too much forward visibility, document that before considering any alternate movement; **no threshold-based / hybrid auto rotation is planned**. Record verdict in HANDOFF and proceed to E1.
