# Gameplay Core A3d — common weapon, physical target contract and missile launcher

Source: green `0eeeab5` (A3c). **Architectural extraction only**; do not rebalance or regenerate scenes.

- `WeaponController`: the inherited, reusable BURST/GAUSS mode/aim/config/stat controller. The old `WeaponLabCannon` class is now a **minimal serialized scene compatibility subclass** with its original script GUID and settings. Do not remove the subclass until the prefab-migration atom rebinds the scene. Editor builders that refer to `WeaponLabCannon` continue to compile.
- `DamageableTarget`: an abstract hit receiver with `CombatTargetDomain` (`Air`, `Ground`) and a boolean `ApplyDamage` result. The existing `WeaponLabEnemy` implements it by calling its original `Hit` method, preserving 5 HP, flash, death, respawn and hit-counter logic.
- `BallisticProjectile` moves/renames the old `WeaponLabProjectile` with its existing GUID. Raycast sweeps, nearest target, tracer logic, speed, range, callbacks and 1/5 damage remain unchanged. It accepts only **Air** damageable targets, not ground objects.
- `GuidedMissileLauncher` renames the old Lab-specific launcher with its existing GUID. It uses shared `TargetLock` / `MissileProjectile`, physical hit callbacks, configurable damage and a generic `MissileLaunched` event. `WeaponLabSession` supplies `fighter.MaxHp` (preserving one-hit-kill), and forwards launches to `WeaponLabEvasion`. One active missile, lock reset, aim check and offsets remain the same. Sandbox's separate `MissileLauncher` is **not touched**.
- No change to game scene YAML, camera, input, sound, prefab, movement, AI decision randomness, or gameplay tuning. All changes remain in runtime scripts and documentation.

**Unity gate:** open `WeaponLab.unity`, verify no Missing Script/Inspector changes; BURST/Gauss damage, HP/kill/hit counters, projectile visuals, 1/2/3 reselect/cooldowns, missile lock/launch/impact/AI reaction, R reset, Q/E break. Check `Sandbox.unity` missiles and camera. Do not run generators.

**Next:** reusable aircraft/camera/HUD prefabs (first proof should assemble a minimal empty scene), then reuse Sandbox FlightHud. Domain-specific ground weapon damage is for Ground Attack v0, *not* this patch.
