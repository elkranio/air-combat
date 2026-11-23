# Cleanup A — migrate the accepted game camera before deleting old scenes

**State:** code patch prepared; requires one Unity Editor migration and Play Mode green. Source master `3a905fd2f8e097093d0650ac4f44f9dbacf4590c`. Cleanup B is deliberately separate: do not delete old scenes/assets/one-time installers yet.

## Why

The approved camera is a 50° / yaw -45° fixed-world orthographic view with heading-based look-ahead (cruise 48, boost +14, lead smoothing .42s, 20% screen margin). Previously it lived only as `HeadingLockedObliqueCameraStudy` on `ArcadeDesertE1.unity`, while `CombatCameraRig.prefab` still contained legacy `TopDownCameraFollow` + `CameraLookAhead`. That was a dependency trap for building a new level.

## Migrate once

1. Apply Cleanup A, let Unity compile. **Stop Play Mode; save or discard dirty scenes**.
2. Run **Air Combat > Maintenance > Cleanup A - Extract Arcade Camera Prefab**.
3. It creates `Assets/_Project/Prefabs/Camera/ArcadeCameraRig.prefab` and swaps only the camera instance in `Assets/_Project/Scenes/ArcadeDesertE1.unity`. Check both assets into Git **with their .meta files** after testing. It refuses to overwrite an existing prefab.
4. Play **ArcadeDesertE1**. Test normal A/D turns, W boost look-ahead, Q/E turns, R reset, height 7/8/9, Y150 and both ground/plateau shadows. Confirm fixed world yaw and no competing camera motion.
5. Run **WeaponLab** and **GroundAttackLab** as regressions for their original camera, target lock and all their weapon slots. Also inspect Console for missing scripts/prefabs. Do not report green solely from `git apply --check`.

## What changes

- `ArcadeCameraFollow`: stable production component, serialized pitch/yaw/ortho/lead and zero C/J/K/L comparison hotkeys. It has no direct scene references on its prefab asset; the E1 scene instance owns the aircraft link.
- `ArcadeCameraRig.prefab`: created from the existing combat camera prefab with `Camera`, `AudioListener`, `TargetLock`, `TargetLockHud`, `CombatCameraRig` and URP camera data retained; legacy TopDown/LookAhead scripts excluded. E1 art-only scene keeps its lock UI disabled.
- `CombatCameraRig.BindPlayer` chooses the production follow if present; otherwise continues to use the exact same legacy follow used by `WeaponLab`, `GroundAttackLab` and C1b. No fighter/weapon/sensor/prefab changes.
- `CombatHud` and `RadarHud` now use the camera **transform** for oblique orientation instead of requiring the obsolete study component. The old combat labs still default to north-up radar.
- `E1MapFlightSession` handles camera reset through the production component, but temporarily accepts the older study component so the map is still playable **before** the one-time migration. The fallback goes away after the migrated map has passed and Cleanup B removes old scenes.
- Original `CombatCameraRig.prefab` is not modified. `ArcadeDesertE1` Terrain, plateau, muted ground and aircraft paint remain unchanged. The scene-specific 600m/4-cascade profile (E1d) is not altered.

## Cleanup B — after A is green

1. Audit actual serialized scene GUID/material/mesh references. Delete old art-only scenes and generated assets **only after reference checks**, keeping functional combat regression scenes until equivalent functionality plays on the main map.
2. Remove superseded E1/E1b/E1c/E1d and C1/T1/rock-kit menu installers together with their `.meta`; retire the one-time Cleanup A command too. Keep only useful authoring tools.
3. Clean temporary disabled rock roots and only their demonstrably unused models/materials, preserving the approved plateau and working player paint/terrain/shadow resources.
4. Reconcile README/HANDOFF/architecture into current truth; remove outdated setup advice and failed experiments from active docs. Fix Build Settings (currently still Sandbox-only) **when** the main map is ready to build.
5. Write the incremental base-production plan: site blockout and road/apron, then warehouse/maintenance hall first, then barracks, generator, vehicle park, perimeter/flag, and adjacent airstrip; one asset approved at a time under real C3 lighting, with silhouette/color/shadows/scale reviewed at Y150. Do not generate a full base in one pass.

**Note:** Main `ArcadeDesertE1` is still an **art/flight test**, not the full gameplay scene. Migrating six weapon slots and air/ground enemies into it is a separate scene-composition task; removing the working combat labs before that would destroy our regression baseline.
