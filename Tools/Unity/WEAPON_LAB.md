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

## v5 — intermittent sector evasion and afterburner

**Tested V4 result:** user can shoot down the 5-HP, non-evasive fighter easily. This is expected and supports the viability of ±12° free aim. Difficulty should come from enemy behavior, not unresponsive aim controls. The user accepted the 150 ms audio trim and current tracer feel. **V5 needs a new flight test.**

Apply V5 patch, let Unity compile, stop Play and save open scenes. Do **not** rebuild the V0 dummy range or rerun the V4 dogfight builder. Run this one-time, idempotent **upgrade to the existing** `WeaponLab.unity`:

```sh
unity command script run --file Tools/Unity/ConfigureWeaponLabEvasion.cs --entry ConfigureWeaponLabEvasion.Configure
```

This adds `WeaponLabEvasion` and an ordinary `Afterburner` to the V4 opponent, reuses the original player-authored afterburner flame VFX **bound to the enemy's own fuel**, and keeps the player's sound, camera, free-aim ±12°, tracer, five-hit target and scene layout unchanged. The enemy remains **unarmed**.

- **B** in Play Mode toggles AI reactions ON/OFF. HUD shows `WATCH`, `BREAK`, `BOOST`, `RECOVER` or `OFF`. `B OFF` = original V4 Cruise/CatchUp flight, a controlled A/B.
- AI notices its position inside the **aircraft-forward ±12° sector** with a small tolerance at distances about 12–135 units. It does **not** read cursor position, exact reticle or incoming projectile positions. A reaction takes ~0.32–0.52 s of continuous exposure.
- About 72% of eligible encounters produce a committed turn to the nearer edge; 28% go to cooldown without reacting. About 40% of turns request afterburner for part of the escape; both the speed and visible twin exhaust flames are limited by the existing Afterburner fuel resource. Ordinary EnemyFlightController `turnRate` and banking still govern the actual maneuver.
- Each break lasts roughly 0.85–1.35 s, followed by a 1.6–2.7 s cooldown, preventing instant zigzag reactions. `R`, death and respawn reset the current reaction. The `B` preference stays unchanged across respawns.
- Tweaks are serialized in `WeaponLabEvasion.cs`: notice range, probabilities, reaction window, escape duration and cooldown. All numbers are provisional. Shared `EnemyFlightController` checks an optional lab-only component; Sandbox opponents have none and retain their existing behavior.

Test whether an approaching enemy sometimes remains easy, sometimes breaks away and sometimes lights its engines, without reading as a perfect dodge-bot. Compare `B ON`/`B OFF` under otherwise similar conditions; do not tune enemy gunfire or player HP yet.

## v6 — guaranteed boost request on every evasive break (A/B test)

Test result: V5 remains too easy to shoot down. This is an intentionally tiny V6 code-only change: when the enemy elects to break out of the sector, it **always requests afterburner** during the original first 70% of the break instead of rolling the former 40% chance. Existing fuel limits, charge/recharge, turning/banking, 72% evasion decision, reaction/cooldown, five HP and player free aim ±12° are unchanged. `B` still compares reactive AI ON/OFF. The HUD's `BOOST` means a boost *request*, not guaranteed thrust when the fuel tank is empty.

**No scene generator, audio installer or asset rebuild.** Apply patch, let scripts compile, Play the existing `WeaponLab.unity`. Do not run the V0/V4/V5 upgraders for this code-only test. If even the boosted enemy is easy to kill, the next separate variable to test is turn rate/escape direction; do not mix that into this A/B.

## v7 — player autocannon continuous/burst A/B

V6 playtest: the unarmed enemy stays easy to shoot down despite guaranteed boost requests. Before adding enemy weapons or extra AI states, test whether **short player firing windows** change the feel of the existing pursuit.

- **M** toggles `CONTINUOUS` (default, original hold-to-fire at 14 rounds/s) and `BURST 4` in Play Mode. The Weapon Lab HUD displays the selected mode.
- `BURST 4`: a single LMB click commits four shots spaced at the existing 14 rounds/s cadence; holding LMB automatically starts another burst after a **0.45 s gap after the last shot**. Releasing/re-pressing LMB cannot skip that gap. A fresh burst always begins immediately after the cooldown; no accumulated catch-up shots.
- Settings in `WeaponLabCannon`: `burstSize` (2–8, default 4) and `burstCooldown` (seconds, default 0.45). `roundsPerSecond` is shared by both modes. If heat limiting is enabled, overheated shots wait rather than consuming burst ammo.
- Aim guide stays active long enough to complete a committed burst after a short click. Existing projectile movement/hit logic, twin muzzles, sounds, heat toggle, enemy, flight and aim angle remain unchanged. Only Weapon Lab scripts and docs change.

**No Unity scene configuration command.** Let C# compile, Play `WeaponLab.unity`, then compare M CONTINUOUS versus BURST while chasing the same enemy. Try short taps, holding LMB, releasing mid-burst, and clicking repeatedly during the cooldown. This is a feel test, not an accepted balance target.

## v8 — fixed-forward single-shot Gauss versus BURST

V7 playtest: user decisively preferred **BURST** over CONTINUOUS because limited firing windows made piloting and timing matter. V8 adds one distinct weapon without touching the opponent, its evasion, camera framing, player controls, terrain, or scene assets.

- **1** selects **BURST 4** (14 rounds/s, 0.45 s post-burst gap, existing ±12° free aim); this is the default in Play Mode. **2** selects **GAUSS**, fixed to the nose of the aircraft (no sector, reticle or mouse steering). Weapon switch cancels any pending autocannon burst; Gauss cooldown never resets on switching. **M** still toggles CONTINUOUS/BURST when autocannon is selected for optional A/B.
- Gauss fires **one slug per fresh LMB press** (no held-fire repeat, no queued shots during cooldown). **5 damage** kills the existing 5-HP enemy in one hit. Configurable in `WeaponLabCannon`: `gaussCooldown` = 1.5 s, `gaussVelocity` = 300 units/s (autocannon 420), `gaussRange` = 250 units (autocannon 430). No spread, no charge-up, no target assist.
- Distinct cyan-white physical tracer and halo, center muzzle flash, short synthesized magnetic sound (runtime clip, provisional). Reuses the existing swept projectile raycast with a `damage` field; normal autocannon rounds remain 1 damage. HUD shows GAUSS readiness/time remaining, and aim as FIXED.
- **Shift+1/2/3** changes camera pitch in Weapon Lab (90/75/65 degrees); `1/2/3` still changes pitch normally in Sandbox. `V` keeps the autocannon free-aim preference while Gauss ignores it.

**No scene upgrader and no audio importer.** Apply, compile, Play existing `WeaponLab.unity`. Compare precision, cooldown and satisfaction of landing one forward-aligned hit versus chasing with BURST. Note whether 300-unit projectile travel feels fair or merely sluggish. Unity playback and audio feel require local verification.

## v9 — short forward sight and kinetic Gauss presentation

After the V8 green push, the cyan glow projectile and sweeping synthetic "pew" were reported as too magical for a heavy electromagnetic gun. This is a presentation-only revision: damage (5), projectile velocity (300), max range (250), cooldown (1.5 s), fixed-forward aim and click-only trigger stay unchanged.

- Selecting **2 GAUSS** displays a short, thin, red forward-pointing guide attached to the nose. It is always visible with Gauss selected (including cooldown), fades out at the tip, has no target snapping, no raycast to the enemy and no effect on collision. `gaussSightLength` defaults to 36 world units; adjustable on `WeaponLabCannon`. Switching to **1 BURST** hides it. This is an aim aid, not a physical laser weapon.
- Replaced the bright cyan orb/2.6-wide halo with one **slim neutral-white kinetic tracer** (0.42 width, 22-unit tail instead of 16, no halo). The muzzle flash is short and warm-white instead of a long blue beam. Reworked the generated Gauss sound to a ~0.19 s metallic crack + low punch instead of a descending electronic chirp. Still temporary art/audio, for an immediate feel test.
- **No scene generator, no new assets.** Compile and test in the current Weapon Lab: switch 1/2, turn the plane with Gauss selected, compare guide length and visibility at camera pitches, check projectile legibility, shoot while turning. No opponent/flight/terrain/autocannon changes.
