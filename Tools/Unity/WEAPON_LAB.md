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
- **Hold LMB**: autocannon, 14 shots/s, infinite ammunition. With default free aim, two faint sector edges and the actual firing-direction ring appear while held. Projectiles originate from alternating existing muzzle points; no actual turret mesh has been rigged yet.
- **T**: toggle overheat gating (default **OFF**), for a direct feel A/B. With limiter on, sustained fire eventually blocks shots while the weapon cools. Heat and timings are serialized and intentionally tentative.
- **V**: toggle aiming `FREE +/-30` (default) / `FIXED` (previous forward-only firing). Never redirects or moves the mouse cursor. In FREE, the visible circle follows the screen pointer on the flight plane but stays within the wedge. If the pointer is behind the aircraft, the circle is centred ahead at its previous radial distance (clamped to the guide reach). Release LMB to hide the guide.
- **G**: cycle cannon sound in Play Mode: `SYNTH` (original reference) / `MG 01` / `MG 02` / `MG 03` / `MG 04`. Default after installing audio: `MG 01`. HUD shows the chosen mode. This does **not** change fire rate, projectile logic, aim or damage.
- **C**: switch camera **LOOK-AHEAD / CENTER** while flying (defaults to look-ahead). Lead follows the aircraft's forward heading, smoothly moves toward ~22% of the visible height ahead at ortho size 80, and grows slightly during afterburner. Turning and switching back never snap the camera. `1/2/3` pitch still works. This is WeaponLab-only; no mouse-aim or gun change.
- **R**: reset aircraft to start and place the passive targets ahead. **H**: hide/show lab HUD. The map only resets automatically when approaching the distant outer edge.

## Files and boundaries

- `Assets/_Project/Scripts/Weapons/WeaponLabFreeAim.cs`: WeaponLab-only bounded mouse aim, world-space guide lines and circle; creates one LineRenderer per child object, no changes to the actual mouse position. Set `halfAngle=30`, `guideReach=165`, and `aimSmoothing=.065` for current test.
- `Assets/_Project/Scripts/Weapons/WeaponLabCannon.cs`: cadence, alternating barrel offsets, heat A/B, muzzle flash, four processed CC0 audio variants and one synthesized fallback, bounded five-voice pool (prevents piling up long audio tails). Tracer core is now 0.50 world units instead of 0.27, with a separate faint 1.10-unit halo using the existing `AirCombat/SandboxEffect` shader. This is a cheap alpha-blended glow **approximation**, not URP postprocessing bloom; test at size 80.
- `Assets/_Project/Scripts/Weapons/WeaponLabProjectile.cs`: moving tracer rounds and swept physics raycast per frame; not a fixed-pitch hitscan laser. Tracer topology: **one LineRenderer on `Autocannon Round`**, with the optional halo **LineRenderer on child `Tracer Halo`**. Unity rejects two `LineRenderer` components on one GameObject. Both lines follow the same swept projectile endpoints; child is destroyed with its round. No separate prefab or generator step.
- `Assets/_Project/Scripts/Weapons/WeaponLabTarget.cs`: noncombat hit flash on test targets.
- `Assets/_Project/Scripts/Visual/WeaponLabSession.cs`: local URP override, HUD, bounds reset, target recycling and WeaponLab-only camera-toggle wiring. Does not change the standard ArtLab controller.
- `Assets/_Project/Scripts/Visual/WeaponLabCameraLead.cs`: Play-Mode-only camera offset component with `[DefaultExecutionOrder(100)]`. Runs *after* shared `TopDownCameraFollow` without changing it or the serialized scene. Settings: `leadFractionOfHalfView=0.45`, `boostLeadMultiplier=1.3`, `transitionSeconds=0.42`; deliberately test values, not accepted tuning. No scene generator required after this patch.
- `Assets/_Project/Art/Weapons/Audio/AutocannonShot.wav`: original synthesized reference (kept for A/B). Four `Autocannon_MG_01..04.wav` files are generated by the audio installer. Do not edit/commit the staging WAV.
- `Tools/Unity/ConfigureWeaponLab.cs`: reproducible scene/mesh/material/texture assembly. Generated output: `Assets/_Project/Generated/WeaponLab/` (small shared mesh, material and repeat texture) plus `Assets/_Project/Scenes/WeaponLab.unity`.

## First pass questions

1. Does holding LMB feel satisfying at ordinary and boosted speeds, or do tracers become visually disconnected from the aircraft?
2. Are 14 shots/s and 420 world units/s projectile velocity readable at ortho size 80? Check the actual model-relative muzzle offsets; they're placeholders.
3. Are hit flashes and passive targets enough to understand where bullets go? Does the sound convey a cannon or a weak click?
4. Compare unlimited fire versus `T` overheat. Do not assume overheat is better just because it introduces a resource.
5. Compare `C` while flying straight, boosting, turning hard and cycling `1/2/3` pitch. Does the lead make forward targets easier to anticipate? Is the camera too floaty or disorienting on rapid reversals? Does center feel preferable for shooting?
6. Look for framerate/audio clipping, oversized map issues and shadow quality. Test before creating missiles/extra weapons in this scene.

**Not implemented / not committed as design:** final gun hardpoints, enemy HP or damage, combat impacts/explosions, gun aiming modes, loadout UI, defensive modules, infinite versus finite missile reserves, final sound asset and final overheating rules.

## Installing the CC0 machine-gun candidates (audio + tracer pass)

The raw pack has already been committed under `Resources/Audio/military_sfx_pack_v0.2/` (no `curated` directory), including `SOURCES_CC0.txt`. The four originals are `machine_guns/mg/mg_single_near_01..04.wav`. They vary greatly in length; **do not import all raw files into Unity or play the whole long recordings once per shot**.

After applying the patch and letting Unity compile, stop Play Mode and save open scenes, then run from the repo root:

```sh
unity command script run --file Tools/Unity/ConfigureWeaponLabAudio.cs --entry ConfigureWeaponLabAudio.Configure
```

This importer temporarily stages one WAV in `Generated/WeaponLab`, uses Unity's audio importer to decode it, detects its first significant attack, exports an approximately 0.15-second mono PCM excerpt with a short fade, and deletes the staging asset. Four small, reproducible WAVs are created under `Assets/_Project/Art/Weapons/Audio/`, and only `WeaponLab.unity` is wired to them. Retain the original CC0 pack and license unchanged in Resources. Rerun the audio installer **after** using `ConfigureWeaponLab` to rebuild the scene. No Blender step.

Test `G` at the same speed and rate of fire. Compare initial attack/body, intelligibility of a long 14 rounds/s burst, fatigue/harshness, and how tracers read when the aircraft turns or boosts. All four candidates and the current .50 core/1.10 halo are **unapproved values** until tested in Unity. An actual bloom pass is not added; no art studies, Sandbox, shared camera or missile systems are modified.

### V2 audio tail diagnostic (unverified in Unity)

A single click was reported to produce one projectile but two successive audible hits (the second softer and shorter). `WeaponLabCannon.Fire()` plays one clip per projectile, so the raw clip tail/echo is a likely culprit, not a confirmed diagnosis. The audio installer now trims each CC0 MG excerpt from ~340 ms to ~150 ms with a ~45 ms end fade. **Rerun `ConfigureWeaponLabAudio.Configure`** to rewrite the four generated `Autocannon_MG_01..04.wav` files; recompiling alone does not change previously generated WAV data. Compare the result on one-shot LMB clicks, then sustained fire. This is a provisional sound test, not an approved sound design.

## v3 mouse free-aim experiment (no generator needed)

After applying v3, open the existing `WeaponLab.unity` and Play. Nothing to reconfigure in Unity or reimport in Blender. `WeaponLabSession` attaches `WeaponLabFreeAim` at runtime and binds the actual smart-camera position. Firing now happens in `WeaponLabCannon.LateUpdate` after camera follow + camera lead to avoid a discrepancy between the visible ring and shot direction. The reticle is a **direction indicator** on the horizontal flight plane, not a prediction marker or a guaranteed hit; moving targets still require lead. Physical gun barrels are **not** visually rotated yet (two original hardpoints remain).

Try `V` while holding LMB and while released; try pointer straight ahead, off-axis past the ±30° boundary, directly behind the aircraft, crossing the side/rear line, and far from the aircraft. Test `C` look-ahead and `1/2/3` pitch presets. Look for sudden jumps, guide clutter or tracer/marker disagreement. The FREE mode, limit angle and smoothing are unapproved until flight-tested.

## v4 — one unarmed fighter (5 HP), replaces passive targets

The player accepted the bounded mouse-aim experiment and locally narrowed `WeaponLabFreeAim.halfAngle` to **12 degrees per side** (±12°, 24° total). Keep that tuning; no other aim/flight/camera/audio changes in this atom.

After applying the patch, **do not rerun** the original `ConfigureWeaponLab.Configure`: it is the older V0 arena builder that recreates seven dummies and can discard audio setup. Instead, stop Play Mode, save open scenes, let Unity import/compile, then upgrade the **existing** scene in place:

```sh
unity command script run --file Tools/Unity/ConfigureWeaponLabDogfight.cs --entry ConfigureWeaponLabDogfight.Configure
```

The idempotent updater deletes `Floating static shooting dummies`, retains the 12 km field, player, look-ahead, gun and installed audio clips, and clones the player's `JetVisual` into one `Dogfight Opponent`. It strips the cloned player-bound `AfterburnerVfx`, uses the existing amber enemy aircraft materials, adds a single sphere hitbox and uses the existing Sandbox `EnemyFlightController` **Cruise / CatchUp** behavior (no player-aim-sector awareness or special dodges). Enemy does **not** fire. `WeaponLabEnemy`: 5 hits to down it, immediate pale hit flash, disappears after final hit, respawns in front after 2 seconds. R resets the fighter as well as the player. HUD displays enemy HP and kills. Moving tracers still use a swept raycast; only this enemy registers cannon damage.

This is an intentionally asymmetric shooting/chasing test, **not yet a real dogfight**. Check that the enemy stays visible often enough, target hitbox is fair, and 5 hits feel right. Next, if this is fun, trial AI reacting to the aiming sector as a separate atom. The original V0 generator and `WeaponLabTarget.cs` are retained as archival sources only; they do not run in the updated scene. No changes to Sandbox gameplay or global settings.
