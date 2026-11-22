# Gameplay Core A4c — prefab-composed WeaponLab scene

Baseline: green `master` `fa5db43` (A4b). **No old WeaponLab scene generators.**

## Scope

The migration command **Air Combat > Prefabs > Create WeaponLab Prefab Scene** creates a separate `Assets/_Project/Scenes/WeaponLabPrefabs.unity`. It **never overwrites** `WeaponLab.unity`; the original remains a green comparison reference. The generated scene is ordinary Unity YAML authored by the Editor, not handcrafted in a Git patch. Stage the `.unity` and its Unity-generated `.meta` after testing.

It copies the current WeaponLab scene, replaces its `Player`, `Dogfight Opponent` and `Main Camera` with scene **instances** of `PlayerJet`, `EnemyJet` and `CombatCameraRig`, retaining each source object's world pose/parent. The former enemy remains a child of the range root. The `WeaponLabSession` scene component is kept and its `player`, `flight`, `boost`, `cannon` and `fighter` references are redirected. `EnemyFlightController.target`, `WeaponLabEnemy.player`, `WeaponLabEvasion.player` are redirected to the new PlayerJet. These are **instance overrides**; the reusable prefab assets retain no scene-level target references. Material/audio and flight tuning remain in the A4b prefabs, including the nested Kestrel chassis and authored mount sockets. The migration copies the original enemy renderer material assignments as **scene-instance overrides** to preserve its approved colors; it does not modify the shared Kestrel or EnemyJet prefab assets.

At Play Mode startup, `WeaponLabSession` detects a `CombatCameraRig`, calls its player/boost binding and configures its camera-owned `CameraLookAhead`; it **does not add a second** look-ahead to the session object. The old `WeaponLab.unity` without a camera rig follows exactly the previous path. `WeaponLabSession` continues to configure missile lock, missile asset, enemy evade notification and Break Turn as before. Full mission-independent composition is a later atom, after prefab-scene validation.

## One-time Unity Editor steps

1. Apply A4c patch. Wait for successful Unity compilation. Open **`Assets/_Project/Scenes/WeaponLab.unity`** in **Edit Mode**. Save/discard unrelated scene edits first; migration refuses a dirty source scene.
2. Select **Air Combat > Prefabs > Create WeaponLab Prefab Scene**. This is *not* an older scene generator. The editor creates and opens `WeaponLabPrefabs.unity` once; refuses to overwrite an existing result.
3. In Hierarchy, confirm `Player`, `Dogfight Opponent` and `Main Camera` show **prefab instance** indicators. Inspect both `JetAssembly` references and nested Kestrel, `WeaponLabSession`'s five scene references, enemy target references and camera's `CombatCameraRig`. There should be **one** active camera and no old scene aircraft copies.
4. Play the **new** `WeaponLabPrefabs` scene: BURST cadence and both muzzle flashes, GAUSS single fixed-forward shot and sight, guided missile 1.0s lock / 0.5s grace, B evasion, 5 HP, death/respawn, Q/E player and enemy Break Turn, fuel recharge, R reset, look-ahead, H toggle, no double camera shift. Test `Sandbox` camera pitch 1/2/3 and its own missiles unchanged.
5. Reopen original `WeaponLab.unity` and verify it still works. Add the new `.unity`, `.meta` and code/docs in Git only after both scenes pass.

If the migration throws, it reopens the untouched source and deletes its incomplete copy. If the converted scene fails Play testing, delete `WeaponLabPrefabs.unity` and its `.meta` and fix the binder before retrying; the original stays intact. Leave the existing Sandbox build-scene selection unchanged.

## Explicitly deferred

- Replacing/renaming the canonical `WeaponLab.unity` and deleting old test-scene artifacts.
- Generalizing `WeaponLabSession` and `WeaponLabEnemy` into a reusable mission/HP/respawn stack.
- Additional chassis, bomber loadouts, NURS, bombs, ground targets, or tuning changes.
