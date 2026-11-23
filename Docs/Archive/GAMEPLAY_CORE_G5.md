# G5 — Direct NURS (first playable pass)

**Goal:** give the player a practical forward-firing ground-attack weapon without replacing the existing cabrage rockets (slot 5) or precision bomb run (slot 4). This is a deliberate, physical, unguided salvo: no cursor aiming, lock, impact predictor or aircraft-control lock.

## Setup

- **GroundAttackLab only:** select **6**, click once to begin a six-rocket salvo. Components are attached/configured by `GroundAttackLabSession` at runtime; **no editor installer, scene reauthoring or prefab changes**.
- `WeaponSelection` appends `DirectNurs` after `Nurs`, preserving serialized enum values for slots 1–5. `WeaponController` routes slot 6 to `DirectNursRack` instead of firing the air autocannon; other labs do not equip this slot.
- `DirectNursRack` owns salvo timing, launch offsets, down angle, spread and cooldown. `NursProjectile` reuses existing terrain/roof swept collisions, gravity, trail, ground-domain blast and impact effects, with an optional blast radius. G4 keeps the original default radius 6.

## Initial numbers

| Parameter | G5 default |
| --- | ---: |
| Rockets per click | 6 |
| Release spacing | 0.15 s |
| Cooldown after last rocket | 4 s |
| Initial downward angle | 10° |
| Muzzle speed | 170 units/s, plus inherited forward aircraft speed |
| Cruise spread | ±0.7° yaw / ±0.5° pitch |
| Boost spread multiplier | 3× |
| Blast radius | 8 units |
| Damage | 1 per ground target per rocket blast |
| Gravity / horizontal drag | existing G4 `NursBallistics`: 80 / 0.015 |

The actual trajectory is calculated from the aircraft's **current** position, heading and speed at each pylon release. Consecutive shots naturally fan out if the pilot steers while firing. At 70 cruise, the initial release height is about 48 world units and rockets travel a significant distance in front of the aircraft before impact. **There is no guaranteed impact point.**

## Interruptions and fairness

- Steering and W afterburner remain available throughout the salvo. Boost increases random angular dispersion, and higher forward speed also changes the impact distance.
- Q/E successfully starting a break cancels the **unreleased** rockets; already flying rockets continue. Switching or reselecting weapon slot 6 likewise cancels the remainder. An interrupted salvo still incurs the 4-second cooldown; an insufficient-charge Q/E request does not cancel.
- A new salvo requires another mouse-button press after cooldown. Holding LMB does not automatically repeat salvos. R cancels pending releases but does not pull already launched rockets out of the world, following other ordnance behavior.
- Bombs (slot 4), cabrage (slot 5), incoming SAM/AA fire, camera, radar and the player air-to-air missile are unchanged.

## Playtest checklist

1. In `GroundAttackLab`, press **6**, click once. Count **six** left/right alternating rocket launches ~0.15s apart; observe them fly forward/down and detonate on physical world geometry. H diagnostics display direct fired / pending / impacts / ground hits / cooldown.
2. Fly straight at **70** and repeat: salvo should be fairly tight. At **W / 140**, rockets should disperse more and land farther away. Make a wide A/D turn during the sequence and verify a real fan, not an automatic correction toward the target.
3. Strike an AA turret, SAM launcher and depot pieces; their HP should fall via existing ground-domain damage, with no damage to the player's aircraft. Compare the **radius 8** splash against G4 cabrage radius 6 and bombs radius 13.
4. Press Q/E mid-salvo with full fuel: stop remaining releases and continue normal break; a failed empty-fuel Q/E should not cancel it. Switching/reselecting slot during release cancels and preserves cooldown.
5. Verify slots 1–5, G4 cabrage, bombs, AA1 and SAM1 behavior, R reset, HUD/radar, and that WeaponLab/Sandbox have no slot 6. No Unity edit-mode installer is involved.

These are v0 numbers. Evaluate whether the 10° descent produces a useful attack window before making it easier with a range reticle. The objective is *skill-based area suppression*, not precision target locking.
