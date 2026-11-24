# HUD-03 — manual hostile target marking

- Right-click a **visible hostile aircraft** to mark it. Picking uses its projected visible mesh bounds, with configurable minimum size and padding. No physics-hitbox precision requirement.
- Right-click empty screen space to clear. A new target replaces the old selection.
- Selected aircraft on screen: four compact orange corner brackets. Offscreen: single orange edge-direction arrow, with no distance or name. The cue follows actual camera projection and remains available beyond radar range.
- The marker scans active `AircraftTeam` objects **only when RMB is pressed**, filtered by `AircraftSide.Hostile`, `PlayerHitPoints.IsAlive`, and a `JetAssembly` visual. There is **no per-frame scene scan**. Selected renderer bounds are cached until selection is cleared.
- Existing `CombatHUD.prefab` owns a non-raycast `TargetMarkerHud`; scene `CombatHud.Bind` supplies the active camera. Neither `Air01Encounter` nor `E1MapFlightSession` nor combat AI needs changes. On death, R/reset, or destruction, selection self-clears via the target's disabled/dead Unity object.
- No missile-lock coupling, no camera/flight/gun steering, no radar modifications. Legacy labs get no marker unless they have a hostile team aircraft and explicitly RMB-select it.

## Test in ArcadeDesertE1

1. RMB on Tiger: orange corner brackets, even if your click is slightly off its tiny hull. RMB elsewhere: brackets disappear.
2. Fly so Tiger exits screen: edge arrow replaces brackets; recapture Tiger on screen; try when beyond 460 radar units.
3. Kill Tiger, press R for a new fight, and suffer defeat: stale markers must clear. New Tiger is only targetable once on screen.
4. Verify no pink UI elements, no clicks intercepted, no HUD/radar/camera/weapon regression, and the legacy WeaponLab/GroundAttackLab remain functional.
