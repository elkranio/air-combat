# B1 — first military base (working production plan)

**Status:** three B1b barracks and B1c headquarters look broadly acceptable in Game view; **B1d roads and prepared soil** now being evaluated. Continue on the same `ArcadeDesertE1.unity`, default Y78 flight and accepted C3 camera. This is an incremental art/scale test, not a combat mission. [HQ asset and install instructions](HQ_B1C.md).

## Site concept

A small generic military airfield outpost with a believable approach: one asphalt access road feeds a service/warehouse area, barracks and utility buildings; an open vehicle/motor pool sits beside it; an adjacent runway/apron belongs to the same compound. A flag and sparse perimeter hints can provide identity, but the game camera should read the **big massing and functional zoning** first. Place it in clear terrain, not on top of the accepted plateau. Leave broad flight corridors around it. Do not build a full 2 km compound or an airport-city.

## Production sequence

1. **B1a — three barracks v0 (placed):** one 32×12×6 m master in three instances, separate base sector, Y150 approach, accepted plateau unchanged. Scale and mass appear promising; shadows and character need improvement. [B1a source](BARRACKS_B1A.md).
2. **B1b — stylized barracks and directional shadows (current):** preserve B1a prefabs and GUIDs, enlarge roof eaves, add one entrance canopy + two roof vents, stronger cool/warm material grouping; rotate the sun on the **main scene instance only**. No bake or installer. [Changes and visual gate](BARRACKS_B1B.md).
3. **B1c — modular headquarters and courtyard (current):** one two-height 29×19 m HQ, independent entry, concrete plaza, two planters and flagpole beside barracks. Spawn at Y78; no road or bake yet. [Current gate](HQ_B1C.md).
4. **B1d — road + ground preparation (current):** separate north-south approach, barracks service lane and HQ courtyard spur; subtle compacted soil painted into native Terrain with soft irregular boundary; sparse repair/skid meshes. No bake, no changes to approved architecture. [B1d visual gate](ROAD_B1D.md). Then a *separate* small variation pass on one existing building.
5. **B1e — utility and identity:** small generator/service building, flagpole, minimal fence/gate and selected markings. Use functionally clear, sparsely distributed pieces.
6. **B1f — adjacent airstrip:** a simple appropriately scaled runway/apron connection and perhaps one hangar, matched to base proportions. No detailed airport before earlier pieces are accepted.
7. **B1g — first combat-context test:** add existing AA1/SAM1 props and perhaps depot to the base via an isolated scene-composition atom; check gameplay readability. Do not modify the already-green lab prefabs just to decorate this scene.

## Material / shape checkpoints

**Environment:** quiet `#B8A993` ground, neutral mid-light concrete/asphalt, low gloss. **Structures:** slightly more contrast and functional color zones than terrain, without saturated toy roofs. **Vehicles / aircraft / AA:** stronger color or silhouette contrast, modest exaggerated scale permitted for readable threats. Shadow and materials must look coherent with the enormous plateau and flying jet. Evaluate in motion at yaw -45°, pitch 50°, ortho 80 and **Y78** before any bake.

## Reference workflow

For each new asset, gather a *small* set of real-world massing photos and optionally one strong stylized-game reference. Extract proportions, large shapes and distinct functional parts; do **not** copy another game's geometry. Block out first, then compare in Unity, then detail only if it helps. Source images belong in `Resources/References/` with source/licensing notes. Editable model in `Resources/Blender/`, export in `Assets/_Project/Art/Buildings/Base/`, prefab in scene-neutral reusable folder.

## Gate for B1d green

The HQ and row of barracks must now read as one compact *used* base with a believable service road and prepared soil, not scattered assets on uniformly colored sand or an overly clean diorama. Check dynamic URP shadows, moderate road width, subdued surface wear and softened soil border at Y78. Reject over-dark asphalt, repetitive noise and any buried/skimming surfaces before further buildings.
