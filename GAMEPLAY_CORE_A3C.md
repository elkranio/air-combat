# Gameplay Core A3c — gun emitter extraction

Source green: `master` `9e1b09e` (A3b tested green). Experimental tuning is locked.

- `AutocannonEmitter` is a Unity component owning BURST muzzle flashes, voice pool, MG02 sound, shader/material, tracer/halo setup and 1-damage physical projectiles. `Autocannon` still owns the burst cadence.
- `GaussEmitter` owns its forward sight, muzzle flash, deterministic mechanical audio, tracer and 5-damage slug. `GaussCannon` still owns click-only cooldown.
- `WeaponLabCannon` is only a temporary *serialized tuning and mode/aim adapter*. It attaches/configures emitters in `Awake()` with the current scene settings, and preserves HUD counters. Original scene YAML and script GUID stay unchanged.
- `WeaponLabProjectile` now reports hits through a callback rather than referring directly to `WeaponLabCannon`. Its enemy-only collision selection remains untouched; future damage contract extraction will handle ground targets.
- No old scene generators; no changes to the Sandbox scene, enemy AI, camera, missiles, prefabs or balance.

## Regression

Compile both scenes. In WeaponLab, check LMB held BURST, MG02 sound/pitch and alternation of left/right muzzle flashes, 4-shot cadence, reselect interruption, fast 1/2/3 switching, cursor sector, Gauss sight and 5-damage slug, single-click cooldown, and shot/hit counters. Check missile lock, launch, enemy death/respawn and Break Turn. In Sandbox, confirm camera presets and missiles are untouched.

## Next

A3d: turn the temporary `WeaponLabCannon` adapter into reusable weapon controller/loadout wiring, generic projectile damage contract and launcher integration. Keep one migration gate per patch before prefab creation.
