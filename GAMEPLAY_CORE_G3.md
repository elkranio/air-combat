# G3 — Gravity Bombs (GroundAttackLab prototype)

Base: green `master` `6ff85069` (G2 and F1 tested). G3 changes no bake source, imported FBX, depot materials, prefab, WeaponLab scene, Sandbox or enemy AI.

## Try in Unity

Open `Assets/_Project/Scenes/GroundAttackLab.unity` and enter Play Mode. **No editor-generation command is required.** The scene composition installs the bomb rack on its PlayerJet instance and attaches four independent damage zones to each of the four depot instances at runtime. Prefab assets remain unchanged.

- **4**: equip bombs (only in GroundAttackLab). **Hold LMB**: release four bombs spaced **0.25s** apart; another burst starts after cooldown while held, matching autocannon behavior. **3s** cooldown starts when the fourth bomb is released, not at the first shot.
- **A/D**: steer normally throughout release. Each bomb inherits aircraft world-heading and *current* speed at release; released bombs have no guidance and do not follow the aircraft.
- **W**: afterburner is allowed throughout the burst. It increases horizontal release velocity, elongating the pattern. While the afterburner is *active*, each bomb also gets independent random lateral/longitudinal velocity error (tuning: 9 m/s maximum radial error, longitudinal component at 40%). Cruise has no artificial random scatter.
- **Q/E**: if the Break Turn really starts (requires the normal full fuel tank), cancel the remaining unlaunched bombs and pay the full 3-second cooldown. Already detached bombs continue flying. If the Break Turn is refused, the burst continues.
- While any bombs remain scheduled, slot keys **1/2/3/4** cannot change weapon selection. Switching works during the cooldown. This does not change existing autocannon selection/cancellation behavior.
- **R** resets flight and cancels any unfinished bomb burst; **H** toggles diagnostics.

## Aiming/flight rules

`BombTrajectory` integrates an **80-unit/s²** downward acceleration and 0.045/s horizontal drag (**G3a**, changed from 24). From 46-ish units of release altitude a bomb lands in roughly **1.1 seconds** on open terrain. The faster fall reduces lead distance at cruise and on W boost; do not offset the visual cue independently of ballistics. Predict and live travel use the *same integration step* and swept Physics raycasts, so buildings intercept bombs before the terrain. The bomb visual has **no collider**; its physical collision is a ray sweep, not Rigidbody tunneling.

While bombs are selected, **one ground-aligned four-corner HUD bracket** bounds all four predicted impact points for straight, constant-speed flight. Each of its four corners has only two short legs (no filled area, connected rectangle or individual rings). It uses the same cool, pale HUD color and SandboxEffect shader as the existing gun guides. The rectangle is oriented to the aircraft heading; projections from impacts on roofs and terrain share one flat horizontal plane instead of skewing the shape. W/afterburner widens this footprint from release error and higher speed lengthens it. Prediction **does not assume future turns, future acceleration or hidden random errors**, intentionally; that is the skill element. The marker is an approximate area, not a guaranteed hitbox.

## Individual ground damage

Each G2 depot gets four runtime `GroundDepotPart` colliders and HP values: bunker **3**, south/north fuel tanks **1** each, supply crates **2**. A bomb blasts a **13m** sphere (1 damage per zone in radius). The broad original depot collider is disabled *only in this scene at runtime* and replaced by these colliders. An individual part loses collision when destroyed.

G2's FBX is combined by material, not destructible part. To avoid a re-bake, `GroundDepotTarget` reconstructs **runtime mesh clones** after each part is killed, deleting triangles whose centres fall inside that zone's local bounds. This produces independent visual destruction without changing source meshes/UVs/materials. Geometry trimming is approximate (centroid-based, not true fracture), and there is only temporary impact feedback; proper debris, smoke, fire, damage shader and high-quality breakup are later visual atoms. The bunker foundation and blast walls remain static scenery. Each depot has **4 independently destructible parts**; the HUD counts destroyed parts out of 16, not depot-wide HP.

## Tunables and verification

- `BombRack`: burst size 4, release interval 0.25, cooldown 3, release offset (0, -2, 1.5), afterburner random error 9.
- `BombTrajectory`: gravity **80** (G3a; originally 24), horizontal drag .045, integration step .04.
- `BombProjectile`: radius 13, zone damage 1.
- Test straight approach vs A/D sweep; cruise vs W footprint and observed spread; Q/E partial cancellation; switching blocked *only during pending shots*; R during burst; damage to each of 4 zones; multiple depots and 3s cooldown.
- Regression: run WeaponLab (old 1–3 selections, air weapons and enemy) and Sandbox (legacy flight/input), and verify no scene/prefab/FBX changes.

**Limits:** bomb visuals, marker, effects and zones are prototype geometry/materials created in Play Mode; not yet a reusable authored bomb mesh/VFX prefab or persistent target damage system. No NURS, anti-aircraft fire or radar in G3. Do not interpret the prediction as physics-based accuracy under future turns or acceleration. Aim afterburner speed still **110**, not the exploratory 140.

## G3a — bomb cue geometry and range (after initial G3 green)

G3a keeps the four-bomb burst, actual prediction, per-bomb independent gravity trajectories, 0.25s cadence, afterburner error, switch lock, damage and all other weapon tuning unchanged. The marker is **not screen-space clamped** and is never artificially shifted closer: increased gravity shortens the real flight/lead distance as well as the predicted one. The single bracket is the expected burst envelope, not four guaranteed hit circles. Roof impacts may raise the entire flat marker plane to the highest impact surface; its corners never follow different collider heights. Check cruise / W, heading changes and releases over bunker roofs. With a very narrow viewport or certain headings, the full high-speed burst envelope may still exceed the frame; revisit camera framing separately rather than lying about ballistics.
