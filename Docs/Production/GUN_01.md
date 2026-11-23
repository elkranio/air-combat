# GUN-01 — modular fixed-forward autocannon

**Goal:** test whether a nose-fixed cannon feels good under the production oblique camera, while establishing a weapon that can be mounted on either aircraft without its own player/AI input code. This is **not** the full loadout/slot system or a Tiger dogfight yet.

## Design boundary

- `AutocannonPreset` ScriptableObject holds the weapon variant: identity, rate, rounds per burst, cooldown, projectile speed/range/damage, tracer length/material and shot audio/volume. Duplicate the `.asset` to create another cannon variant. No damage balance change in GUN-01 (1 damage per hit, current baseline).
- `AutocannonWeapon` is the reusable prefab module under any `JetAssembly` (player or enemy). It binds to its parent `JetChassis` gun sockets and **always fires along the aircraft's root forward**, alternating left/right muzzles. It wraps the existing `Autocannon` cadence and `AutocannonEmitter` projectile/VFX/SFX, rather than recreating ballistics or duplicating damage logic.
- `ITriggeredWeapon.TickTrigger(held, now)` is an input-agnostic contract. `PlayerWeaponTrigger` reads LMB only for the player; future AI can issue exactly the same trigger requests after choosing when to fire. No generic weapon slot/equipment system or enemy AI changes yet.
- Shots remain procedural `BallisticProjectile` objects from the existing `AutocannonEmitter`; separate projectile/fx prefabs are a **future presentation pass**, not fake fields in the current variant data. Existing `WeaponController` remains for legacy air/ground labs.

## One-time install in Unity

1. Apply patch on `master` after green HUD-01. Exit Play Mode, open `Assets/_Project/Scenes/ArcadeDesertE1.unity`; save/discard dirty scene edits.
2. Run **Air Combat > Weapons > GUN-01 - Install Forward Autocannon (Main Map)** exactly once. The menu reads existing `PlayerJet.WeaponController` serialized values, creates `Assets/_Project/Prefabs/Weapons/AC01_Burst.asset` and `AC01_Autocannon.prefab`, mounts one prefab instance under main-scene player and adds its LMB adapter. It **disables the legacy WeaponController on that E1 scene instance only** to prevent duplicate shots; shared PlayerJet and both regression labs are unmodified.
3. Commit generated `.asset`, `.prefab`, scene and **all `.meta` files** with the source patch after green. Never rerun the one-time installer or regenerate existing prefabs. Editing a preset asset changes its linked mounts.

## Play Mode gate

- Main E1: hold LMB while steering A/D and Q/E, test cruise/boost. Shots follow the **aircraft nose**, not cursor position, screen center or banked visual wings. No aim wedge/reticle in E1. Compare camera tracking and actual hit controllability before further mechanics.
- Verify L/R muzzle origin, existing four-shot cadence, cooldown, tracer/SFX and projectile lifetime. HUD-01 radar/HP/boost remain visible; static Tiger remains a visual/radar contact, **not yet a damageable dogfight target**.
- Regression: `WeaponLab` old bounded BURST/GAUSS/A2A, `GroundAttackLab` all six slots, AA/SAM and radar, E1 Y78/7/8/9/R/boost. No other scene should accidentally receive an LMB adapter.

**Follow-up:** [GUN-02](GUN_02.md) adds preset-controlled glow and muzzle juice after fixed-forward fire was green. After HUD-02, integrate the visually accepted EF-01 Tiger with existing enemy flight/evasion/health and add one airborne combat encounter; do not implement a MechWarrior-style slots menu now.
