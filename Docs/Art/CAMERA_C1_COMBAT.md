# C1b — heading-locked 50° camera with all combat systems

Status: **implementation prepared, Unity Play Mode verdict pending**. Reference: [ENVIRONMENT_MAP_PLAN.md](ENVIRONMENT_MAP_PLAN.md).

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

- **Camera:** `C` compares oblique heading-locked vs original north-up; `J/K/L` select **50/58/65°** in the oblique mode; default = **50°**.
- **Flight:** `A/D` steer, `W` boost, `Q/E` 90° Break Turn, `R` reset, `H` overlays.
- **Important:** `1/2/3` in **C1b** select weapons, NOT camera pitch. The original art-only C1 still uses `1/2/3` for pitch.
- **Weapons:** `1` BURST, `2` GAUSS, `3` locked A2A MISSILE, `4` bombs, `5` cabrage NURS, `6` direct NURS, `LMB` fire/commit. `B` toggles fighter's existing evasion AI.
- **AA1/SAM1:** physical shots and incoming missiles; watch HP/fuel, SAM alert, radar detection/zone rings. No flares or impact-prediction marker.
- **Radar:** normal labs remain **north-up** (jet arrow rotates). C1b **heading-up** in 3/4 mode (jet arrow fixed up, contacts/AA/SAM rings rotate); `C` switches it back to north-up along with camera.
- Test fighter lock and guided missile immediately, then BURST/GAUSS, then depots with the three ground-attack modes. Try sharp left/right turns while monitoring radar, target on screen, AA lead and SAM incoming.

## Limits / verdict

This intentionally does **not** transplant the accepted CombatLightingRig to V19, fix V19 blur/texel mismatch, add 3D obstacle crash logic, rebuild gun balance or introduce missions. Those belong in the separate map phases. Terrain collider is real; old decorative props may still lack colliders. Keep C1 and all combat labs untouched if the result is rejected.

After real Play Mode: record **accepted / rejected / unresolved**, with 50° vs 58/65, cursor lock comfort, radar readability and whether projectiles/targets remain legible. Then continue E1 regardless.
