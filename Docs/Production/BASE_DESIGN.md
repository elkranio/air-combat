# B1 — first military base (working production plan)

**Status:** B1a three-barracks test is prepared; **Unity Play Mode verdict pending**. Build on the existing `ArcadeDesertE1.unity`, use muted ground, current dark-contrast Kestrel, Y150 flight and the accepted C3 camera/light rig. **One reviewable atom at a time**. The base is for finding our art/scale language, not yet a fully functional combat mission. [First asset and install steps](BARRACKS_B1A.md).

## Site concept

A small generic military airfield outpost with a believable approach: one asphalt access road feeds a service/warehouse area, barracks and utility buildings; an open vehicle/motor pool sits beside it; an adjacent runway/apron belongs to the same compound. A flag and sparse perimeter hints can provide identity, but the game camera should read the **big massing and functional zoning** first. Place it in clear terrain, not on top of the accepted plateau. Leave broad flight corridors around it. Do not build a full 2 km compound or an airport-city.

## Production sequence

1. **B1a — three barracks first:** model one simple 32×12×6 m barracks, install three prefab instances in a row on a new sector of the *same* map, and move the Y150 aircraft spawn to a readable approach. Leave the accepted plateau untouched. First compare roof/wall palette, shadows, dimensions and three-building rhythm; no bake. [Asset gate](BARRACKS_B1A.md).
2. **B1b — site footprint:** once the barracks read well, add a minimal asphalt access road/apron and only the functional footprints needed for the future site. Align to the actual C3 camera and Y150 flight approach; no full runway yet.
3. **B1c — warehouse/maintenance hall:** use 2–3 references for proportions (low broad industrial mass, wide doors, roof slope/overhang, a loading/service side), then model one reusable building. Match accepted barracks proportions/material language. Simple faceted form, few matte materials, proper shadow/collider; no detailed bake initially.
4. **B1d — motor pool:** parking slab, one boxy jeep/truck silhouette, then an APC/tank *only if size and shape read*. A second vehicle is a variation pass, not a forced asset dump.
5. **B1e — utility and identity:** small generator/service building, flagpole, minimal fence/gate and selected markings. Use functionally clear, sparsely distributed pieces.
6. **B1f — adjacent airstrip:** a simple appropriately scaled runway/apron connection and perhaps one hangar, matched to base proportions. No detailed airport before earlier pieces are accepted.
7. **B1g — first combat-context test:** add existing AA1/SAM1 props and perhaps depot to the base via an isolated scene-composition atom; check gameplay readability. Do not modify the already-green lab prefabs just to decorate this scene.

## Material / shape checkpoints

**Environment:** quiet `#B8A993` ground, neutral mid-light concrete/asphalt, low gloss. **Structures:** slightly more contrast and functional color zones than terrain, without saturated toy roofs. **Vehicles / aircraft / AA:** stronger color or silhouette contrast, modest exaggerated scale permitted for readable threats. Shadow and materials must look coherent with the enormous plateau and flying jet. Evaluate in motion at yaw -45°, pitch 50°, ortho 80 and Y150 before any bake.

## Reference workflow

For each new asset, gather a *small* set of real-world massing photos and optionally one strong stylized-game reference. Extract proportions, large shapes and distinct functional parts; do **not** copy another game's geometry. Block out first, then compare in Unity, then detail only if it helps. Source images belong in `Resources/References/` with source/licensing notes. Editable model in `Resources/Blender/`, export in `Assets/_Project/Art/Buildings/Base/`, prefab in scene-neutral reusable folder.

## Gate for B1a green

Three copies of a single barracks prefab read as **barracks** from the normal gameplay camera, contrast with the ground, and sit at convincing scale against the flying jet and distant plateau. Check dynamic shadows and roof silhouette, no baked direct lighting, clean imported model / prefab references. If it fails, revise *this building*, not the map or six weapons at once.
