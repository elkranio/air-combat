# G3 — gravity bombs (current)

In `GroundAttackLab.unity` only; scene composition adds the bomb components to `PlayerJet` at runtime. `WeaponLab` and Sandbox do not equip slot 4.

## Controls and trajectory

- Select **4**. Holding LMB releases **four** separate gravity bombs every **0.25s**; a **3s** cooldown starts after the last released bomb. A/D turns and W boost remain available during the burst; changing slots is locked only while releases remain queued.
- A successful Q/E Break Turn sacrifices queued bombs and starts the full cooldown; already detached bombs continue. A refused break leaves the burst in progress. R cancels a burst.
- Each release inherits the player's **current** heading and horizontal speed. W increases range and introduces independent release error (maximum radial 9 units/s; longitudinal factor .4). Normal cruise has no artificial random error.
- `BombTrajectory` integrates gravity **80 units/s²**, horizontal drag **.045/s**, step **.04s**. `BombProjectile` uses swept raycasts so roofs intercept falling bombs; no Rigidbody or impact predictor.
- Blast radius **13**, damage **1** per ground zone in radius. `GroundBlastDamage` is the shared ground-domain blast policy (also used by NURS); the projectile owns its own trajectory, radius and temporary VFX.

## Current aiming presentation

- **No CCIP, forecast corridor or predicted area.** Those earlier prototypes were removed to make bombing a learnable timing/flight skill.
- Slot 4 alone displays `BombNadirMarker`: dashed straight-down line plus small ground/slope/roof ring at the true point below the aircraft. It communicates altitude/orientation, not where a forward-moving bomb will land.
- All aircraft and ordnance shadows in GroundAttackLab are **real URP mesh shadows** controlled by the shared `CombatLightingRig` prefab. The legacy flat `AircraftGroundShadow` is not active in this scene.

## Depot damage

- Four G2 MilitaryDepot prefab instances; runtime `GroundDepotTarget` attaches 4 independent zones each: bunker **3 HP**, fuel tanks **1 HP** each, supply crates **2 HP**.
- Destroyed zones lose colliders and approximately disappear by clipping triangles from runtime **copies** of the imported material-grouped FBX. Replacements dispose of previous clones. This is not true geometry fracture or finished debris/VFX.
- The diagnostic HUD reports destroyed zones out of **16** (not total HP). A blast can affect multiple live ground zones.

## Regression

Check straight vs turning drops, afterburner dispersion, Q/E interruption, R reset, slot switching, roofs/slopes vs ground, the shadow cast by jet and bombs, all 16 depot zones, and WeaponLab/Sandbox unaffected. See [HANDOFF.md](HANDOFF.md) for the full cross-scene checklist.

Historical G3/G3a/G3b/G3c marker and fake-shadow experiments: [Docs/Archive/GAMEPLAY_CORE_G3_HISTORY.md](Docs/Archive/GAMEPLAY_CORE_G3_HISTORY.md).
