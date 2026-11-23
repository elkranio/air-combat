# Environment art — reset after V2A–V2A5

**Status (2026-11-23):** the five V2 studies were evaluated in Unity and rejected as visual art. Their test scenes, exported FBX/AO, Blender sources, generated terrain meshes and materials, and one-shot `Air Combat > Art V2` menu commands have been removed from the live repo. The results remain recoverable from Git history, **not** from shipping project assets.

## What we learned

- **Camera and scale:** the game is a low-altitude arcade attack game. Terrain must read at the same distance as AA guns, roads and depots. Obstacles may rise above flight height; collisions should be deliberate and correctly instrumented. The old V2A4 study exposed a spawn outside the art-map safety limit: always test reset/spawn against bounds before evaluating art.
- **V2A / A2 / A3:** color-noise/slope painting produced blurred ground, terraced bands or synthetic trenches. Higher grid resolution alone did not fix silhouettes.
- **V2A4:** a physically deep canyon proved big forms can read, but equal cross-section bands and concentric mountain rings looked like a machine-cut excavation and pyramids.
- **V2A5:** attached spikes and loose triangles made a self-intersecting Frankenstein cliff; baking AO cannot fix topology, scale or art direction.
- **Keep:** `FrontierStrata.unity` (V17 reference), established Blender→FBX→URP and high-to-low bake recipes (`ART_AUTHORING.md`), all gameplay scenes and authored enemy/player prefabs. **Do not rerun** retired scene generators.

## Next atom — one rock, done correctly

1. Find **2–3 real geological or professionally stylized** cliff references. Identify silhouettes, stratification, fracture directions, bevel sizes, ledges, scree and the palette visible from aircraft height. No blind Perlin terrain.
2. In Blender, construct **one** intentionally shaped rock/cliff using a coherent closed low-poly shell. Use deliberate asymmetry and several clear nonconcentric planes. Where independent blocks meet, avoid floating/intersecting spikes and open seams. Check normals, manifold state, degenerate faces and object transforms before export.
3. Inspect in Blender Solid mode, first as **one neutral material** from orthographic gameplay-like angles. **No bake if the untextured model is unconvincing.** Test the silhouette against an aircraft-scale marker.
4. Only if geometry passes: make a restrained material test and optional local normal/AO bake with the existing `ART_AUTHORING.md` recipe. Dynamic sun/shadows stay separate; keep source `.blend` and a compact FBX.
5. Import into a **single new isolated Unity study**, preserve existing camera/aircraft, set a verified safe spawn and test collision without an unconditional reset loop. Judge at ortho size 80, pitches 90/75/65, in motion, with before/after screenshots. Approve the one rock before building mountains/canyon/road kits.

## Free-asset comparison fallback

If the next rock still fails visually, **stop generating shapes**. Search for free reference assets with verifiable licenses (prefer CC0; CC BY requires attribution), import one or two without modifying combat, and compare **the exact same camera, scale, materials and lighting**. Study why they work: mesh silhouette, proportions, UVs, normal detail, shading, LOD. Legitimately licensed assets can be used directly with their terms; use other work as a learning reference, **not** as a license to copy restricted meshes/textures.

## Repo hygiene

Keep one active art study at a time; delete rejected generated outputs including Unity `.meta` and one-time editor menus as soon as the user rejects them. The active study should never overwrite V17 or the two combat labs. Large raw Blender sources belong under `Resources/Blender`, accepted exports under `Assets/_Project/Art`, reproducible scripts under `Tools/Blender`. `Assets/_Project/Generated` is disposable study output. Beware: deleting tracked files makes HEAD smaller, **but does not shrink historical Git objects**; do not force-push/rewriter history as part of normal cleanup.
