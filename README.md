# Air Combat

Arcade air combat prototype in Unity 6.6 / URP.

## Start here

- **`Assets/_Project/Scenes/WeaponLab.unity`** — current air-to-air combat reference. It uses prefab instances of `PlayerJet`, `EnemyJet` and `CombatCameraRig`, with nested `Kestrel` chassis. This is the former tested `WeaponLabPrefabs.unity` (renamed in cleanup C1).
- **`Assets/_Project/Scenes/GroundAttackLab.unity`** — G1 independent prefab flight/composition proof, after one-time Unity authoring. Flat training ground; no ground targets or ordnance yet.
- **`Assets/_Project/Scenes/Sandbox.unity`** — legacy flight/missile comparison and the **only scene enabled in Build Settings**. Do not delete until the build entry point is deliberately migrated.
- **`Assets/_Project/Scenes/FrontierStrata.unity`** — environment baseline (V17); not yet a combat scene.
- `FrontierBakeTruth`, `FrontierPracticalBake`, `FrontierMesaPolish`, and `ScorchedFrontier` — visual experiments/reference scenes, deliberately retained pending separate art cleanup.

In **WeaponLab**: WASD steer/throttle, Space afterburner, Q/E Break Turn, left-click fire, 1 BURST / 2 GAUSS / 3 MISSILE, B enemy evasion toggle, R reset, H diagnostic overlay. Camera pitch: 65 degrees; look-ahead active.

**Never run the old `ConfigureWeaponLab*` or scene-export generators.** They were retired in C1. The approved prefabs/scenes are committed Unity assets; edit them directly. New aircraft chassis replace the nested visual prefab; gameplay lives on the role root.

## Active layout

```text
Assets/_Project/
  Scenes/WeaponLab.unity          # Prefab-composed combat reference
  Scenes/GroundAttackLab.unity   # Independent prefab flight proof (G1)
  Scenes/Sandbox.unity            # Only build scene, legacy comparison
  Prefabs/Aircraft/PlayerJet.prefab
  Prefabs/Aircraft/EnemyJet.prefab
  Prefabs/Camera/CombatCameraRig.prefab
  Prefabs/Chassis/Kestrel.prefab
  Scripts/Aircraft/              # JetAssembly, JetChassis, camera binder
  Scripts/{Flight,Enemy,Weapons,Targeting,Combat,Camera,UI}/
```

## Documentation

- [HANDOFF.md](HANDOFF.md) — **current state**, approved feel, follow-up work and important limitations.
- [GAMEPLAY_ARCHITECTURE.md](GAMEPLAY_ARCHITECTURE.md) — live dependency/ownership map; not a migration diary.
- [ART_PIPELINE.md](ART_PIPELINE.md), [ART_AUTHORING.md](ART_AUTHORING.md) — active art pipeline.
- [Tools/Unity/ARTLAB.md](Tools/Unity/ARTLAB.md) — retained visual experiments and commands.
- [THIRD_PARTY_ASSETS.md](THIRD_PARTY_ASSETS.md) — asset licenses.
- [GAMEPLAY_CORE_G1.md](GAMEPLAY_CORE_G1.md) — independent GroundAttackLab flight scene.
- [GAMEPLAY_CORE_G2.md](GAMEPLAY_CORE_G2.md) — modeled depot, Cycles roof normal/AO bake, and four scene prefab placements.
- [Docs/Archive](Docs/Archive) — previous handoff, per-atom migration records, old Weapon Lab guide. Historical content may mention deleted scripts and scene names.

## Next

G2: generate the MilitaryDepot model and Cycles roof normal/AO bake, import it with the Unity menu, and test four targets in GroundAttackLab. After green, G3 adds bombs and ground damage. Radar remains undesigned. Before any change, check `HANDOFF.md` and run WeaponLab + Sandbox regressions.
