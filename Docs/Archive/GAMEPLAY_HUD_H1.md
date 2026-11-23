# H1 — shared Combat HUD (Unity playtest required)

Base: R1b north-up radar and boosted PlayerJet, both tested green. H1 changes HUD layout/composition only; no AA, weapons, controls or radar detection changes.

## Installation — once

1. Apply the H1 patch with Unity closed, then open `Assets/_Project/Scenes/GroundAttackLab.unity` in Edit Mode.
2. Run **Air Combat > HUD > Install Shared Combat HUD** once. This authors `Assets/_Project/Prefabs/UI/CombatHUD.prefab` and places one linked prefab instance in GroundAttackLab; it also saves the scene. No need to rerun when pressing Play.
3. Commit the generated `CombatHUD.prefab`, `.meta`, `UI.meta` (if created) and modified `GroundAttackLab.unity` with the code after the Unity test is green. The installer detects an existing HUD and avoids duplicates.

## Behaviour

- **North-up radar:** top right with 18px horizontal / 16px top inset, 460-unit detection, true 230-unit AA danger ring, player arrow rotates. Uses the existing `RadarHud` component and `AircraftRadar` sensor. The prefab has **no scene references**.
- **Bottom status strip:** full width, 4% screen height, using the existing reusable `FlightHud` camera viewport logic from Sandbox. Left half shows **FUEL xx%** with fill; right half **HP n/20** with fill. Fuel turns orange on boost; HP turns red below 30%. The status strip is outside the combat camera viewport, not a glass overlay across the action.
- `CombatHud.Bind(camera, afterburner, hitPoints, radar)` connects the prefab to each scene. If `hitPoints` is null, the optional HP section hides. Scene-specific references are set only by the session; nothing hardcodes the AA or GroundAttackLab in the prefab.
- No `GraphicRaycaster`: UI cannot eat mouse fire input. `FlightHud` remains backward compatible with the existing legacy Sandbox instance.
- Before the one-time installer is run, the existing runtime-created radar remains as a fallback, now at top right; there is no bottom strip until the prefab is installed.

## Test checklist

1. Enter GroundAttackLab with the new prefab instance. See **one** radar in the top-right and **one** bottom status strip. No duplicate radar. H shows/hides debug panel only; radar and bars stay visible.
2. Check 720p / 1080p. Radar stays at the top-right, circle/ring stay clipped. Bottom strip occupies 4% without blocking the jet; camera remains correctly aligned with weapon aiming.
3. W: fuel depletes, bar turns orange, label updates. Release W: it recharges. Q/E consumes full charge as before.
4. Fly into AA fire: HP numeric and fill fall on **real hits**, not on near misses. HP changes to red at low health. R restores 20/20 and turret while keeping radar/contact alignment.
5. Bomb/NURS/GAUSS and camera lead: no regressions. Check WeaponLab and Sandbox: no changes to their current HUD setups or controls. The old Sandbox fuel-only `FlightHud` still shows only its original percentage.
6. Reopen GroundAttackLab in Edit Mode: prefab instance survives and can be modified/moved in the scene. Other scenes can instantiate `CombatHUD.prefab` and call `CombatHud.Bind` with their own instruments.
