# C1b/C2/C3 — combat camera, fixed 50° and forward look-ahead

Status: **C1b rotating 50° rejected for combat comfort; C2 fixed-yaw 50° is green. C3 adds smoothed aircraft-heading look-ahead and modest boost lead on the same scene; Play Mode verdict pending.** Reference: [ENVIRONMENT_MAP_PLAN.md](ENVIRONMENT_MAP_PLAN.md).

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

- **Camera (C3):** default **fixed-world yaw -45°, 50° pitch**, aircraft turns onscreen. `C` toggles older rotating-world C1b (the old 28m lead), useful only as a reference; it is no longer a pure yaw-only comparison. `J/K/L` select 50/58/65°; default 50°.
- **Flight:** `A/D` steer, `W` boost, `Q/E` 90° Break Turn, `R` reset, `H` overlays.
- **Important:** `1/2/3` in **C1b** select weapons, NOT camera pitch. The original art-only C1 still uses `1/2/3` for pitch.
- **Weapons:** `1` BURST, `2` GAUSS, `3` locked A2A MISSILE, `4` bombs, `5` cabrage NURS, `6` direct NURS, `LMB` fire/commit. `B` toggles fighter's existing evasion AI.
- **AA1/SAM1:** physical shots and incoming missiles; watch HP/fuel, SAM alert, radar detection/zone rings. No flares or impact-prediction marker.
- **Radar:** other labs remain north-up. In C2 it follows the camera's *actual* screen orientation: at fixed yaw -45°, contact positions stay aligned with the visible world and the aircraft arrow rotates; in heading-locked mode the arrow faces upper-right and contacts rotate with the scene. C never requires manual radar-mode selection.
- Test fighter lock and guided missile immediately, then BURST/GAUSS, then depots with the three ground-attack modes. Try sharp left/right turns while monitoring radar, target on screen, AA lead and SAM incoming.

## Limits / verdict

This intentionally does **not** transplant the accepted CombatLightingRig to V19, fix V19 blur/texel mismatch, add 3D obstacle crash logic, rebuild gun balance or introduce missions. Those belong in the separate map phases. Terrain collider is real; old decorative props may still lack colliders. Keep C1 and all combat labs untouched if the result is rejected.

**C1b observed verdict:** At 50° the environment reads more volumetrically and feels lively. But full combat is less readable, there is less time/space to anticipate threats, and rotating terrain causes noticeable mild motion sickness. This is **not approved as the final camera**.

**C2 observed verdict (2026-11-23):** fixed 50° is more comfortable and better for gameplay, while keeping the desired depth. Approved as the starting point for the map. The remaining issue: show substantially more world **ahead of the aircraft**, at every heading, with a small additional shift during boost.

**C3 — camera follow / forward visibility (awaiting green):** Same `FrontierCameraCombatC1.unity`, no new scene or editor menu. In fixed mode, separate follow smoothing (0.12s) from heading-lead smoothing (0.42s); **48 world units** forward at cruise, **+14** while `Afterburner.IsActive`, with smooth release. Project the actual jet through the orthographic camera and clamp lead to keep it at least **20% inside the screen** on both axes. Camera yaw remains fixed as the jet banks/turns. The existing NURS cabrage cue (24m selected / 70m committed, independently clamped) runs *after* this follow. In rotating comparison mode keep the previous 28m lead; art-only C1 remains unchanged.

**C3 validation:** open committed combat scene, Play, test full 360° A/D turns and Q/E breaks while tracking space *ahead* (including left/right/down-screen headings), W boost enter/release (the extra should be subtle), enemy targeting/lock, slots 4–6, and cabrage selected/committed at map edges. The aircraft must never be cropped or whip across the viewport. If sustained boost/repeated turns feel uncomfortable, tune lead/transition in the C1b camera Inspector before changing gameplay. On green: record result in HANDOFF and proceed to E1.
