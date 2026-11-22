# Air Combat

Arcade air combat prototype in Unity 6.6 / URP.

## Start here

- **`Assets/_Project/Scenes/WeaponLab.unity`** — current air-to-air combat reference. It uses prefab instances of `PlayerJet`, `EnemyJet` and `CombatCameraRig`, with nested `Kestrel` chassis. This is the former tested `WeaponLabPrefabs.unity` (renamed in cleanup C1).
- **`Assets/_Project/Scenes/GroundAttackLab.unity`** — G3 gravity-bomb flight range with four G2 Military Depot prefab targets, independent damage zones and projected impact area. G3 setup occurs at Play Mode, not through an editor generator.
- **`Assets/_Project/Scenes/Sandbox.unity`** — legacy flight/missile comparison and the **only scene enabled in Build Settings**. Do not delete until the build entry point is deliberately migrated.
- **`Assets/_Project/Scenes/FrontierStrata.unity`** — environment baseline (V17); not yet a combat scene.
- `FrontierBakeTruth`, `FrontierPracticalBake`, `FrontierMesaPolish`, and `ScorchedFrontier` — visual experiments/reference scenes, deliberately retained pending separate art cleanup.

In **WeaponLab** and **GroundAttackLab** (F1 feel test): constant 70-unit/s cruise; A/D steer, hold **W** for afterburner (to 110), Q/E Break Turn. S and Space do nothing. WeaponLab: left-click fire, 1 BURST / 2 GAUSS / 3 MISSILE, B enemy evasion toggle, R reset, H diagnostic overlay. Camera pitch: 65 degrees; look-ahead active. **Sandbox retains its original W/S throttle + Space afterburner** for comparison. GroundAttackLab additionally equips **4 BOMBS**: four 0.25s-spaced releases, 3s post-burst cooldown, forecast landing corridor, afterburner scatter; Q/E cancels remaining releases. Slot switching is blocked only while bombs are still releasing.

**Never run the old `ConfigureWeaponLab*` or scene-export generators.** They were retired in C1. The approved prefabs/scenes are committed Unity assets; edit them directly. New aircraft chassis replace the nested visual prefab; gameplay lives on the role root.

## Active layout

```text
Assets/_Project/
  Scenes/WeaponLab.unity          # Prefab-composed combat reference
  Scenes/GroundAttackLab.unity    # G3: bombs + four depot targets (G2)
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
- [GAMEPLAY_CORE_G3.md](GAMEPLAY_CORE_G3.md) — bomb burst, ballistics, indicator and per-part damage verification.
- [Docs/Archive](Docs/Archive) — previous handoff, per-atom migration records, old Weapon Lab guide. Historical content may mention deleted scripts and scene names.

## Next

G3 pending local Unity smoke test: bombs on 4, physics-based impact cue and per-part depot destruction. After green, refine feel/markers before G4 NURS. Keep WeaponLab and Sandbox regressions; radar and AA defence remain undesigned.
