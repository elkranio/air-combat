# Art pipeline — active assets

- `Resources/Blender/`, `Resources/Audio/`, `Resources/References/`: editable/raw outside Unity Assets, not Resources.Load paths. Keep provenance.
- `Tools/Blender/`: reproducible geometry/export scripts. Use Blender bundled Python in factory-empty background mode for generators that clear the scene. Do not assume system Python installed.
- `Assets/_Project/Art/`: imported meshes, materials, textures and live audio. Historical folder names may still be live (e.g. Environment/ArcadeDesertE1).
- `Assets/_Project/Prefabs/`: scene-neutral reusable prefabs. Scene bindings stay on instances.
- `Assets/_Project/Generated/`: live generated data such as mission map assets. `Assets/ThirdParty/`: imports and license sidecars; [provenance](THIRD_PARTY_ASSETS.md).
- Three authored scenes under `Assets/_Project/Scenes/`; do not regenerate.

Build one asset at a time; verify silhouette, scale, lighting, camera readability, sockets, normals and .meta stability. Reuse base/airfield, CL06 cliffs, ENV01–04 libraries. M02 V07B geography/paths accepted; use MissionMapBaker after actual terrain edits. Keep planning SVG/JSON as source/reference; do not rerun superseded installers.

Current art: IN01 V2 shape, selected graphite C not yet applied to main prefab/source; temporary livery comparison in Sandbox. See [IN02](Docs/IN_02_INTERCEPTOR_PASS.md) for paths and next pass. Carryall V8/V9A+ accepted, polish deferred. No unrelated geometry overhaul now.

## Read-only asset audit

- Air Combat > Tools > Audit unreferenced art assets: `Temp/AirCombat-asset-audit.txt`, protects all prefabs.
- Air Combat > Tools > Audit scene reachability (CLEAN-03C): `Temp/AirCombat-scene-reachability.txt`, reports unreachable prefabs, exclusive dependencies and orphan candidates. Neither deletes assets.

Retain source/library variants with purpose (MilitaryDepot, original Mantis, ENV04 pads/tracks, CL06 layout/geometry, imported Missile.mtl sidecar) unless actual review proves unnecessary. Verify Unity dependency closure, GUID/path references, dynamic loads, authoring tools, source and licenses before deletion. Old asset counts are historical, not current. [History](Docs/Archive/README.md).
