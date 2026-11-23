# Art pipeline — active map and assets

The current asset direction is [Stylized Arcade Military](Docs/Production/ART_DIRECTION.md). We author **one reusable asset at a time**, compare in the actual `ArcadeDesertE1` flight view and only then reuse or expand it. No more blind whole-map generators.

## Folders

- `Resources/Blender/`: editable `.blend` sources for **currently used** aircraft, targets and accepted new props; not imported into Unity.
- `Resources/References/`: raw reference images and notes, not game assets. Check licenses before any direct reuse.
- `Tools/Blender/`: reproducible authoring/generator scripts **only for live assets**. Rejected V17/V19/forest study generators are retired.
- `Assets/_Project/Art/`: imported meshes, materials and textures. `Environment/ArcadeDesertE1` holds the current terrain, plateau and local aircraft paint.
- `Assets/_Project/Prefabs/`: scene-neutral gameplay and camera prefabs. Prefer prefab reuse over copying materials or composing dependencies inside an obsolete scene.
- `Assets/_Project/Scenes/ArcadeDesertE1.unity`: persistent, hand-editable map; **never regenerate**. Combat labs remain strictly for regression.
- `Assets/_Project/Settings/ArcadeCombat_URP.asset`: main-map candidate for 600-unit, four-cascade shadows. Compare its cost/quality to the current combat-lab profile before global adoption.
- `Assets/ThirdParty/`: only protected dependencies after cleanup; attribution/license rules in [THIRD_PARTY_ASSETS.md](THIRD_PARTY_ASSETS.md).

## Base/airfield surface authoring — as built

B1d roads, shoulder meshes and sparse wear are **separate authored objects**. Its initial B1d/B1d2 compacted-ground TerrainLayer/alphamap experiment was superseded by **B1d3**, a separately toggleable ~183×226 m **sharp-edge prepared-lot mesh**. The B1d3 install cleared the old local prepared paint; the original editable Terrain and TerrainCollider remain. B1e runway/apron and B1f hangar trial occupy a different sector. **Do not rerun any one-time B1 installers on the accepted scene**. See [base](Docs/Production/BASE_DESIGN.md) and [airfield](Docs/Production/AIRFIELD_B1E.md).

## Model handoff

1. Find a few relevant geometry reference images (proportion/massing, not styling to copy), set object footprint in **world units** next to the actual aircraft at Y78.
2. Block out an intentionally designed silhouette. Use a small number of large surfaces, clear side/roof separation, no procedural spikes or crowded bevels. Inspect from gameplay yaw -45°, pitch 50°, ortho 80.
3. Assign 2–4 intentional flat/matte material colors. Give neutral buildings enough contrast from muted sand `#B8A993`; reserve saturated accents for aircraft, doors/markings, weapons and VFX.
4. If the model reads with dynamic URP sunlight, **do not bake just because the tool exists**. If local concavity/structural seams need more read, author restrained AO/normal independently; never bake a directional sun/shadow into albedo.
5. Keep source `.blend` or documented model generator, source mesh, unity mesh import settings, prefab/materials and `.meta` GUIDs. Check collider/shadow flags and asset sizes before green.
6. Evaluate at cruise and boost under real day lighting; compare readability, size, color, shadows and performance. Approve or reject the individual asset before adding another.

No historical V14–V20 steps are production instructions. Git preserves those experiments.
