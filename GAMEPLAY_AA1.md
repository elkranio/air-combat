# AA1 — stationary tracer autocannon (AA1c burst tuning)

Baseline: AA1b tested green and pushed. AA1c changes only the burst count, from 18 to 12 rounds. One stationary hostile ground turret. **No HUD threat indicator, no radar, no hitscan or predictive area marker.** The rounds themselves must be enough warning to dodge.

## One-time installation

1. Apply the AA1 patch to the clean C2 master, with Unity closed.
2. Open **only** `Assets/_Project/Scenes/GroundAttackLab.unity` in Unity Edit Mode, save any edits.
3. Run **Air Combat > AA1 > Install Autocannon Trial**, exactly once. This creates:
   - `Assets/_Project/Prefabs/Enemies/AA1Autocannon.prefab` — independent, movable turret prefab with static base, yaw pivot, pitch pivot and two muzzle transforms.
   - `Assets/_Project/Art/AA1/*.mat` — URP-lit armor/accents and an unlit tracer material.
   - One prefab instance at `(85, 0, 300)` and a serialized reference from `GroundAttackLabSession` to that instance.
4. Commit **the generated prefab, materials, scene and `.meta` files** with the patch after smoke-test green. The menu command refuses to overwrite existing art or scene instances. Never rerun after it succeeds.

The prefab is built from Unity primitive meshes for an initial **editable low-poly model** with no external Blender/FBX dependency. Its mesh children use normal URP shadow casting and receiving, from the existing `CombatLightingRig`. It is intentionally a v0 model, not a finished production asset; its hierarchy allows replacing visual meshes later without changing the turret logic.

## Mechanics and tuning (Inspector on prefab)

- Detection **230** world units (350 / 1.5 approximately); target position is sampled every frame, but the velocity used by the intercept is refreshed only every **0.45 seconds**. Straight-flight prediction still accounts for bullet travel time and gravity; sharp maneuvers can outpace the stale intercept velocity.
- Variant **B**: turret continuously rotates while firing: **yaw 85°/s**, **pitch 55°/s**, max elevation 70°. Fires only once within 9° of its current target lead. The barrel still tracks continuously, but does not instantaneously correct its velocity estimate on sharp turns.
- **12 physical rounds** per burst at **18 rounds/s**, followed by **2.5s** pause. Alternate twin barrels, slight ±1.75° shot scatter.
- Round speed **240 units/s**, gravity **12 units/s²**, max range **470**. Continuous swept segment raycast (no frame tunneling); the actual round produces the orange line + short impact spark. Hits on roof, terrain and other solid geometry stop it; only **Air** damage targets lose HP.
- Lab-only `PlayerHitPoints`: **20 HP**, a basic aircraft-sized BoxCollider, no auto-death animation. At 0 HP the turret stops acquiring; press **R** to reset both. HP and shot/hit counts are in the existing H diagnostic panel.
- Turret itself: **6 HP**, `CombatTargetDomain.Ground`, accepts existing bombs/NURS blasts; destroyed turret stops firing, its already launched rounds continue. R restores the turret for repeatable tests.
- This is a single isolated defense; no radar, AA coordination, railgun warning beams, reusable threat map or mission framework yet.

## AA1c playtest

- With the existing turret prefab, **do not rerun the installer**. This patch updates its serialized burst size directly.
- Count twelve tracers in each burst. Check that ordinary A/D or Q/E maneuvers can escape the shorter stream while a straight approach remains dangerous.
- Detection, velocity refresh, spread, projectile speed, fire rate, inter-burst pause, HP and camera are unchanged.

## AA1b playtest

- With the existing scene and prefab, **do not rerun the one-time installer**. The prefab and script values are updated directly by this patch.
- Check the first burst starts noticeably closer. Fly straight into the gun: it should still be dangerous. Zigzag or use Q/E after seeing tracers: the lead should lag a new heading enough to create a dodge window.
- If it still fires completely outside the camera, reduce `detectionRange` further in the prefab (the next iteration can calibrate this against camera framing); no HUD warnings are added.
- Only three difficulty dials changed: detection 350→230, lead velocity refresh continuous→0.45s, scatter ±1°→±1.75°. At AA1b, burst count/cadence, turret servo speeds, projectile speed and player HP were unchanged; AA1c now reduces burst count only.

## Verification

1. Start GroundAttackLab. Fly directly toward the gun without steering: it should rotate and fire visible long streams. Check shots traverse the world at finite speed; HP falls only when rounds intersect the aircraft.
2. On separate runs, avoid bursts with **A/D**, **W**, and **Q/E**. Check each moves the jet physically out of the stream. The gun should track *during* the burst but lag sudden turns.
3. Approach at different headings, speeds and altitude (including **5** cabrage). Confirm servo yaw/elevation remain finite and no infinite-range shots.
4. Bomb or rocket the turret: HP falls and the turret ceases fire at zero. Try hitting adjacent depot pieces as before. R restores the encounter and aircraft HP.
5. Regression: slots **1–5**, G4c camera, bomb nadir guide, shared URP shadows (day/sunset), four depots, WeaponLab and Sandbox. Especially verify the new aircraft hitbox does not intercept its own bombs/NURS.
6. Save/commit generated `.prefab`, `.mat`, `.unity` and **Unity `.meta` files** only after all scenes are green. Report Unity compile errors or first burst/hit behavior before balancing further.

## Reuse contract

`AntiAircraftTurret` handles sensing, leading, yaw/pitch and burst schedule. `AntiAircraftProjectile` handles ballistic movement, real swept collision and visual tracer. `PlayerHitPoints` implements the existing `DamageableTarget` contract. The prefab never stores the player scene reference: `GroundAttackLabSession` binds target and creates its **lab-only** damage receiver. Multiple instances later require only scene placement and scene-level target binding, not copied behavior.
