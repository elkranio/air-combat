# Weapon Lab — isolated weapon feel-test range

**Goal:** Test the feel of airborne weapons, VFX and audio before deciding on loadouts, survival rules, ammunition, missile targeting or enemy density. Sandbox remains the only build scene and still owns the existing lock-on missiles.

## Build

From the repository root, after applying the patch, wait for Unity to import/compile. **Stop Play Mode and save any dirty scene** before running:

```sh
unity command script run --file Tools/Unity/ConfigureWeaponLab.cs --entry ConfigureWeaponLab.Configure
```

Open `Assets/_Project/Scenes/WeaponLab.unity` and enter Play. The generator copies V20's aircraft/camera/sun/study-local URP presentation into a separate scene, removes that copy's terrain/settlements/preview, generates a light 12 km × 12 km ground and seven passive targets. No Blender step or new external package. Does not edit `Sandbox`, `FrontierMesaPolish`, `FrontierStrata`, shared aircraft materials, or any build settings. Regeneration overwrites only WeaponLab scene and assets under `Generated/WeaponLab/`; stop Play/save scenes first.

## Inputs

- **W/S** throttle, **A/D** turn, **Space** afterburner; **1/2/3** camera pitch 90/75/65 degrees (existing controllers).
- **Hold LMB**: forward-firing autocannon, 14 shots/s, infinite ammunition. Alternating test muzzle points, visible projectile tracers, hit flashes on static drones.
- **T**: toggle overheat gating (default **OFF**), for a direct feel A/B. With limiter on, sustained fire eventually blocks shots while the weapon cools. Heat and timings are serialized and intentionally tentative.
- **C**: switch camera **LOOK-AHEAD / CENTER** while flying (defaults to look-ahead). Lead follows the aircraft's forward heading, smoothly moves toward ~22% of the visible height ahead at ortho size 80, and grows slightly during afterburner. Turning and switching back never snap the camera. `1/2/3` pitch still works. This is WeaponLab-only; no mouse-aim or gun change.
- **R**: reset aircraft to start and place the passive targets ahead. **H**: hide/show lab HUD. The map only resets automatically when approaching the distant outer edge.

## Files and boundaries

- `Assets/_Project/Scripts/Weapons/WeaponLabCannon.cs`: cadence, alternating barrel offsets, heat A/B, muzzle flash, 2D audio playback.
- `Assets/_Project/Scripts/Weapons/WeaponLabProjectile.cs`: moving tracer rounds and swept physics raycast per frame; not a fixed-pitch hitscan laser.
- `Assets/_Project/Scripts/Weapons/WeaponLabTarget.cs`: noncombat hit flash on test targets.
- `Assets/_Project/Scripts/Visual/WeaponLabSession.cs`: local URP override, HUD, bounds reset, target recycling and WeaponLab-only camera-toggle wiring. Does not change the standard ArtLab controller.
- `Assets/_Project/Scripts/Visual/WeaponLabCameraLead.cs`: Play-Mode-only camera offset component with `[DefaultExecutionOrder(100)]`. Runs *after* shared `TopDownCameraFollow` without changing it or the serialized scene. Settings: `leadFractionOfHalfView=0.45`, `boostLeadMultiplier=1.3`, `transitionSeconds=0.42`; deliberately test values, not accepted tuning. No scene generator required after this patch.
- `Assets/_Project/Art/Weapons/Audio/AutocannonShot.wav`: original synthesized temporary WAV. No external licensing. Sound quality not verified in Unity; replace with a properly sourced sample if it does not work.
- `Tools/Unity/ConfigureWeaponLab.cs`: reproducible scene/mesh/material/texture assembly. Generated output: `Assets/_Project/Generated/WeaponLab/` (small shared mesh, material and repeat texture) plus `Assets/_Project/Scenes/WeaponLab.unity`.

## First pass questions

1. Does holding LMB feel satisfying at ordinary and boosted speeds, or do tracers become visually disconnected from the aircraft?
2. Are 14 shots/s and 420 world units/s projectile velocity readable at ortho size 80? Check the actual model-relative muzzle offsets; they're placeholders.
3. Are hit flashes and passive targets enough to understand where bullets go? Does the sound convey a cannon or a weak click?
4. Compare unlimited fire versus `T` overheat. Do not assume overheat is better just because it introduces a resource.
5. Compare `C` while flying straight, boosting, turning hard and cycling `1/2/3` pitch. Does the lead make forward targets easier to anticipate? Is the camera too floaty or disorienting on rapid reversals? Does center feel preferable for shooting?
6. Look for framerate/audio clipping, oversized map issues and shadow quality. Test before creating missiles/extra weapons in this scene.

**Not implemented / not committed as design:** final gun hardpoints, enemy HP or damage, combat impacts/explosions, gun aiming modes, loadout UI, defensive modules, infinite versus finite missile reserves, final sound asset and final overheating rules.
