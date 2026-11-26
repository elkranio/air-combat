# Art pipeline — active assets

## Folders

- `Resources/Blender/`, `Resources/Audio/`, `Resources/References/`: editable/raw sources outside Unity Assets. Not runtime `Resources.Load` paths. Keep source and provenance.
- `Tools/Blender/`: reproducible geometry/export scripts for retained assets and libraries. One-time obsolete Unity installers are retired.
- `Assets/_Project/Art/`: imported meshes, materials, textures and live audio. `Environment/ArcadeDesertE1` remains a historical folder name used by CombatSandbox; do not rename it casually.
- `Assets/_Project/Prefabs/`: scene-neutral combat, camera, HUD and environment assets. Scene references stay on scene instances.
- `Assets/_Project/Generated/`: live generated assets such as mission map data and referenced legacy-named materials. Folder names alone are not evidence of disuse.
- `Assets/ThirdParty/`: imports plus licenses and model sidecars. See [provenance](THIRD_PARTY_ASSETS.md).
- Two authored scenes in `Assets/_Project/Scenes/`; do not regenerate them.

## Review and authoring

Build one asset at a time, test in the real flight view, then reuse it. Review silhouette, proportion, dynamic lighting, terrain/aircraft readability, model import and `.meta` stability. Reuse base/airfield, CL06 cliffs, ENV01–04 environment libraries and combat prefabs. Keep the generated rock-layout JSON and planning SVG as references, not live placement instructions. Bake the tactical map when geography changes using the persistent MissionMapBaker editor tool.

## Read-only asset audit

- **Air Combat > Tools > Audit unreferenced art assets**: conservative report (`Temp/AirCombat-asset-audit.txt`) protects all prefabs.
- **Air Combat > Tools > Audit scene reachability (CLEAN-03C)**: `Temp/AirCombat-scene-reachability.txt` separates unreachable prefabs, their exclusive dependencies and orphan candidates. Never deletes assets.

After CLEAN-03C2 the report counted 516 imported paths (down from 553), 712 protected dependency paths (unchanged) and 42 retained candidates:

| Retained group | Count | Reason |
| --- | --- | --- |
| MilitaryDepot (including ready file) | 14 | Future ground mission target/source |
| Original AA02 Mantis | 14 | Authoring source/visual variant, not combat carrier |
| ENV04 pads/tracks | 8 | Library variants and reproducible generator |
| CL06 variants and layout JSON | 5 | Reusable rock library and authoring recipe |
| Missile.mtl | 1 | Imported model sidecar |

These are explicit retention decisions, not proof of runtime use. Review actual Unity dependency closure, GUID and path references, source generators, dynamic loads, import sidecars and licenses before any deletion. Raw sources are separate from imported assets. Never delete indiscriminately based on an unused report.
