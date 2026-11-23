# Terrain T1 — native Unity Terrain flight study

Status: **first playable test, waiting for Unity screenshot and fly-through**.

This is NOT another V2 noise-generated mesh. It is genuine `TerrainData` with standard Unity Sculpt/Paint tools, an automatic TerrainCollider, and three editable Terrain Layers. The initial heightmap contains only deliberate macro shapes; artists can change every contour directly with Unity Terrain brushes. The generated textures vary at small scale, not as terrain displacement.

## Build once

From a clean Unity Edit Mode with scenes saved:

**Air Combat > Art > Terrain T1 - Create Native Terrain Study**

Open `Assets/_Project/Scenes/FrontierTerrainT1.unity`, press Play. The builder uses the established V17 player/camera/sun but does not modify V17 or any combat scene. Refuses to overwrite an existing T1 scene or generated folder because brush changes must never be silently erased.

Generated Unity assets live at `Assets/_Project/Generated/TerrainT1/` and the scene at `Assets/_Project/Scenes/FrontierTerrainT1.unity`. Commit both and their `.meta` files only if the study is approved.

## Layout / scale

- Unity Terrain **900 x 900** with an intentionally flat outside margin; the interesting central zone is roughly 600 x 600. A 600 x 600 tile proved too small for a 45–70 unit/s flight rig plus camera edge margin.
- Heightmap **1025²**, splatmap **512²**, vertical world range **-15..150**.
- West: asymmetric plateau rising to about **105**; east: long turning ridge rising to about **97**; south-east: low hill ~39; middle: broad safe corridor with shallow dry wash.
- Player starts **(0,48,-205)** safely within the art flight reset boundary. A study-only height probe checks the hull and wing positions against the actual native Terrain, and resets after contact. The native TerrainCollider is also installed.
- **1/2/3** changes gameplay camera pitch 90/75/65°, **WASD** flight, **Space** boost, **R** reset, **H** hides overlay (the V17 art controller manages controls).

## How to sculpt / paint

Select **`T1 / Native Unity Terrain (select to sculpt)`** in the Hierarchy. In Inspector, use Paint Terrain (mountain/brush icon) → Raise or Lower Terrain, Set Height, Smooth Height, Paint Texture. Do this directly in the saved T1 scene. Use an 80–150 unit broad brush for mountain mass, 12–30 for the edges. One terrain asset is the authoring source; DO NOT rerun the one-shot builder on it.

The three native layers are `Dust`, `Sandstone`, `Rock`. They are muted placeholders; screenshot approval is about whether macro shapes and flight-height obstacles read at 80 ortho size. No Blender, third-party assets, normal bakes, new runtime terrain systems or gameplay prefab modifications.

## Pass/fail

- Are plateau/ridge silhouettes readable in play at pitch 65/75/90 and at y=48?
- Is 60–80% of the flight area safely open instead of endless walls?
- Does collision/reset work near the cliff without pinning the player at spawn?
- Do slopes look like usable terrain rather than an artificial trench or an egg-shaped mountain? If not, hand-sculpt with Unity brushes before adding assets.
- Do not progress to roads/buildings/rock props until the terrain alone feels plausible in motion.

If this is rejected, delete T1 scene, generated assets and editor/runtime helpers (and `.meta`), keep gameplay/V17. No need to preserve another rejected study in the live project.
