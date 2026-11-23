# Airfield — B1e installed sector and B1f hangar trial

**Status at `645081ba`:** B1e **exists in the persistent main scene**, including generated prefabs, materials and imported meshes. B1f **arched hangar** is a second, independently toggleable visual trial placed next to the original B1e hangar; it does **not** replace it. Neither trial implies final Game-view art approval. Keep camera, Y78, base HQ/barracks, prepared lot and legacy combat scenes unchanged. **Do not rerun B1e/B1f installers on the authored scene**.

## B1e as built

- **Runway:** ~460×36 m, matte asphalt near X−770/Z−290 with grade/shoulder and sparse threshold/center/edge lines. **Apron:** ~120×102 m light concrete near X−678/Z−280, short runway taxi connection and two marked parking positions. No landing/takeoff mechanics.
- **Original hangar:** `Assets/_Project/Prefabs/Environment/Base/B1e_Hangar.prefab`, approximately **48 m wide × 36 m deep × 14 m high**, pitched roof and large open portal near X−619/Z−280.
- **Tower:** `Assets/_Project/Prefabs/Environment/Base/B1e_ControlTower.prefab`, ~22 m tall near X−603/Z−200; elevated dark-window cabin and open support legs.
- **Two parked Kestrels:** static **chassis-only** copies; not enemies, not independently flyable. TerrainCollider is still authoritative. Hangar/tower solids have simple colliders but there is no flight crash system.
- **Source:** `Tools/Blender/generate_airfield_b1e.py` → seven source OBJ parts, generated URP materials and `B1e_RunwayAndApron.prefab` plus hangar/tower prefabs. These are committed assets; do not reinstall to update them.

## B1f arched hangar — additional visual candidate

- `Assets/_Project/Prefabs/Environment/Base/B1f_ArchHangar.prefab`; source `Resources/Blender/ArchHangar_B1f.blend`, generated/imported sections at `Assets/_Project/Art/Buildings/Base/ArchHangar_B1f/`, reproducible geometry script `Tools/Blender/build_arch_hangar_b1f.py`.
- Eight separately authored mesh groups (roof, walls, front portal, ribs, side annex, dark detail, roof vents, accent). Flat/matte colored materials. Installer `InstallArchHangarB1F.cs` places it in `B1 / B1f Arch hangar - STYLE TRIAL (toggle)` near the original B1e hangar **without** replacing or moving B1e objects.
- **Visual verdict still separate:** compare shape, readability, dimensions and shadow at production Y78 camera, then retain/select/rework only the favored version. B1f is **hangar art**, not a name for final service roads.

## Deferred

Only after the combat-focused work and explicit visual decisions: add **hand-authored curved service roads** linking base and airfield, with intentional junctions rather than perpendicular tiles. No additional airfield installer, runway redesign or map-wide terrain pass is currently scheduled.
