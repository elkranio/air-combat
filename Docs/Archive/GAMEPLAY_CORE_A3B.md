# Gameplay Core A3b — weapon firing cadence extraction

Base: `master` at `c354c28` (A3a tested green). No gun/lock tuning changes.

- `Autocannon` owns BURST queue, 4-shot burst, 14 rounds/s spacing, 0.45 s between bursts, 3-shot hitch catch-up cap, and cancellation semantics on *every* 1/2/3 reselect. No input or visuals inside the scheduler.
- `GaussCannon` owns one-shot-per-click and 1.5 s cooldown, independent of BURST and selection state.
- `WeaponLabCannon` remains the temporary **scene-serialized adapter**: it creates/uses the two modules and retains approved projectile creation, VFX, audio, hit counters, aim and lock wiring. Existing serialized tuning stays where it is until prefab migration, with no scene YAML or GUID changes.
- `WeaponSelection` and `TargetLock` remain unchanged. Missile launching is **not** moved in this atom.
- A3c: move projectile spawning, audio and effects out of the legacy adapter into independent weapon components and replace the Lab-specific projectile/damage coupling. Only after that rename/retire `WeaponLabCannon`, move prefab fields, and build player/enemy prefabs.

Regression: WeaponLab BURST held fire, reselect 1 mid-burst, switch 1/2/3 rapidly, hitch/slow frame, GAUSS click/hold/cooldown, MISSILE lock/launch, HUD counts, enemy HP/respawn. Sandbox 1/2/3 remains camera presets. Do not run old generators.
