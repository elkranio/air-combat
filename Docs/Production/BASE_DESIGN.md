# B1 — first military base (working production plan)

**Status:** B1d3 hard-edged prepared lot is the chosen base-soil candidate after user green; **B1e airfield** (runway, hangar, tower, two parked aircraft) is next to evaluate. Keep `ArcadeDesertE1.unity`, Y78, C3 camera and existing HQ/barracks. [B1d3](PREPARED_LOT_B1D3.md) / [B1e airfield](AIRFIELD_B1E.md).

## Site concept

A small generic military airfield outpost with a believable approach: one asphalt access road feeds a service/warehouse area, barracks and utility buildings; an open vehicle/motor pool sits beside it; an adjacent runway/apron belongs to the same compound. A flag and sparse perimeter hints can provide identity, but the game camera should read the **big massing and functional zoning** first. Place it in clear terrain, not on top of the accepted plateau. Leave broad flight corridors around it. Do not build a full 2 km compound or an airport-city.

## Production sequence

1. **B1a — three barracks v0 (placed):** one 32×12×6 m master in three instances, separate base sector, Y150 approach, accepted plateau unchanged. Scale and mass appear promising; shadows and character need improvement. [B1a source](BARRACKS_B1A.md).
2. **B1b — stylized barracks and directional shadows (current):** preserve B1a prefabs and GUIDs, enlarge roof eaves, add one entrance canopy + two roof vents, stronger cool/warm material grouping; rotate the sun on the **main scene instance only**. No bake or installer. [Changes and visual gate](BARRACKS_B1B.md).
3. **B1c — modular headquarters and courtyard (current):** one two-height 29×19 m HQ, independent entry, concrete plaza, two planters and flagpole beside barracks. Spawn at Y78; no road or bake yet. [Current gate](HQ_B1C.md).
4. **B1d/B1d2/B1d3 — road + prepared-lot test (current):** approved north-south approach, barracks lane and HQ spur, plus sparse repair/skid meshes. After rejected grey/blurred B1d ground, B1d2 supplied warm brown. B1d3 tests **one truly continuous clearing with an unblurred uneven boundary** on a separately disableable object. The old B1d alpha paint is removed locally without touching roads. No bake or architecture changes. [B1d roads](ROAD_B1D.md) / [B1d3 A/B](PREPARED_LOT_B1D3.md). Then consider a small variation pass on one existing building.
5. **B1e — utility and identity:** small generator/service building, flagpole, minimal fence/gate and selected markings. Use functionally clear, sparsely distributed pieces.
6. **B1e — separate airfield slice (new visual test):** 460×36m runway with clear asphalt, wide apron, one open-front faceted hangar, control tower, two stationary Kestrels. Put it west of the main compound and leave B1d3 dirt footprint alone. Review in flight **before** drawing the final connections. [Exact assets/gate](AIRFIELD_B1E.md).
7. **B1f — final road connection:** hand-authored curved service-road links from the accepted compound to hangar/apron, integrated turns/junctions, restrained wear. No L-shaped tile snapping.
8. **B1g — first combat-context test:** add existing AA1/SAM1 props and perhaps depot to the base via an isolated scene-composition atom; check gameplay readability. Do not modify the already-green lab prefabs just to decorate this scene.

## Material / shape checkpoints

**Environment:** quiet `#B8A993` ground, neutral mid-light concrete/asphalt, low gloss. **Structures:** slightly more contrast and functional color zones than terrain, without saturated toy roofs. **Vehicles / aircraft / AA:** stronger color or silhouette contrast, modest exaggerated scale permitted for readable threats. Shadow and materials must look coherent with the enormous plateau and flying jet. Evaluate in motion at yaw -45°, pitch 50°, ortho 80 and **Y78** before any bake.

## Reference workflow

For each new asset, gather a *small* set of real-world massing photos and optionally one strong stylized-game reference. Extract proportions, large shapes and distinct functional parts; do **not** copy another game's geometry. Block out first, then compare in Unity, then detail only if it helps. Source images belong in `Resources/References/` with source/licensing notes. Editable model in `Resources/Blender/`, export in `Assets/_Project/Art/Buildings/Base/`, prefab in scene-neutral reusable folder.

## Gate for B1d green

The HQ and row of barracks must now read as one compact *used* base with a believable service road and prepared soil, not scattered assets on uniformly colored sand or an overly clean diorama. Check dynamic URP shadows, moderate road width, subdued surface wear and a clearly cut but uneven prepared-lot boundary at Y78. A/B the lot root against plain sand. Reject over-dark asphalt, repetitive noise and any buried/skimming surfaces before further buildings.
