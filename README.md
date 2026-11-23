# Air Combat

Unity 6.6 / URP. **Stylized arcade air combat**, fixed-world oblique camera and one growing 2×2 km map. This is a playable prototype, **not yet an integrated mission**.

## Start here

1. [HANDOFF.md](HANDOFF.md) — current green, remaining issues, next step.
2. [Docs/Production/BASE_DESIGN.md](Docs/Production/BASE_DESIGN.md) — **active production plan:** build the first military base one asset at a time.
3. [Docs/Production/MAP_DESIGN.md](Docs/Production/MAP_DESIGN.md) — map size, elevation, flight/collision language.
4. [Docs/Production/ART_DIRECTION.md](Docs/Production/ART_DIRECTION.md) — palette, geometry, scale, lighting and VFX direction.
5. [ART_PIPELINE.md](ART_PIPELINE.md) / [ART_AUTHORING.md](ART_AUTHORING.md) — model/asset pipeline and bake discipline.
6. [GAMEPLAY_COMBAT.md](GAMEPLAY_COMBAT.md) / [GAMEPLAY_ARCHITECTURE.md](GAMEPLAY_ARCHITECTURE.md) — gameplay rules and scene/prefab ownership.
7. [LIGHTING_L1.md](LIGHTING_L1.md) / [THIRD_PARTY_ASSETS.md](THIRD_PARTY_ASSETS.md) — lighting and dependency licenses.

## Scenes

| Scene | Role |
| --- | --- |
| `Assets/_Project/Scenes/ArcadeDesertE1.unity` | **Main working map**: muted editable terrain, huge plateau, scene-specific jet paint, flight at Y150, `ArcadeCameraRig`, real 600-unit shadow candidate. Art/flight only for now. |
| `Assets/_Project/Scenes/WeaponLab.unity` | **Regression only**: fighter and air weapons 1–3. |
| `Assets/_Project/Scenes/GroundAttackLab.unity` | **Regression only**: ground weapons 4–6, AA/SAM, radar/HUD. |

Build Settings starts from the main map; labs may be present as disabled entries. Combat labs stay until their behavior can be exercised on the main map. Do not resurrect retired one-time map builders or camera studies.

## Process

One small atom → `.patch` → `git apply --check --verbose` → `git apply` → Unity Play Mode → green push. For an authored asset, preserve the source model, Unity import GUID, prefab and its `.meta` files. The main map is **hand-edited and persistent**: do not regenerate it from obsolete builders. Git history preserves removed experiments; no duplicate historical plans are maintained in-tree.
