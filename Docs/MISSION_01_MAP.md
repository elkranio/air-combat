# MISSION-01H — Map layout trial

Base `f669118`. In the saved Mission01_FirstSortie scene, run **Air Combat > Missions > Apply Mission 01 Map Layout** once. Save the scene and changed `Mission01_Terrain.asset` and `Mission01_Facets.asset`. Existing terrain/material/mesh GUIDs are preserved. No runtime or prefab changes. Do not regenerate the original map. The command refuses a non-flat source or an already populated Objectives root.

## Dimensions and route

Expand the flat editable terrain from 2000 x 2000 to 3200 x 3200, keeping the southwest origin (-1000, 0, -1000). World X/Z range is now -1000 to +2200; terrain height range remains 180 and the heightmap resolution remains 257. Rebuild the existing faceted surface at 112 x 112 cells (25,088 triangles; ~28.6-unit facets). No movement of the base, player, camera or HUD. For future edits of the mission TerrainData, **Air Combat > Missions > Rebake Mission 01 Terrain** rebuilds the render mesh from the edited data, without flattening the terrain or moving objects.

World coordinates (X east, Z north), provisional:

| Object | X | Z | Notes |
| --- | ---: | ---: | --- |
| Base | -700 | -700 | Existing airfield; unchanged |
| SAM A | -650 | 350 | Move existing visual truck; planning radius 280 |
| Central ridge | 525 | 640 | Planned footprint X -50..1100, Z 300..980 |
| SAM B | 1250 | 1050 | Second visual truck; planning radius 330 |
| Recon zone | 1550 | 1760 | Three empty markers at Y78 |

Suggested outbound route from the end of takeoff: (-512,-624) -> (-330,100) -> (-290,930) -> (40,1250) -> (800,1400) -> (1550,1760). Approx. 3644 units, 52 seconds at existing cruise speed 70, excluding takeoff, fighting and steering deviations. Direct flight would take about 45 seconds but crosses the planned ridge. Return route is a future playtest decision; do not force a one-way corridor or claim the same path is impossible. Optional north/south ridge anchors are placeholders only.

## Scope and next atom

The two SAMs are non-combat visual instances; their planning circles are not radar contacts. Recon and ridge markers are empty transforms, not target or terrain geometry. No new collision, map-edge behavior, terrain camera changes or forced navigation. Verify the map stays flat and the existing takeoff, HUD, cannon and R reset still work. Review flight distance before sculpting.

Next: test a central ridge sculpted in TerrainData; rebake the existing low-poly surface. For collision, use the visible baked mesh or a carefully verified equivalent collider so the crash threshold matches the silhouette, not an unrelated heightfield approximation. Introduce swept aircraft-vs-terrain checks and mission crash/reset in their own atom. Check camera occlusion and near/far clipping at gameplay height. If the terrain visual is not convincing, a low-poly Blender ridge with matching collision is a valid alternative. All of this is deferred until after the flat layout is reviewed.

## MISSION-01H1 — Central ridge visual trial (WIP)

Base `2f3d64b`. Open the expanded mission and choose **Air Combat > Missions > Apply Mission 01 Central Ridge** once. The command uses the existing central ridge marker, refuses a non-flat source, sculpts the editable TerrainData and rebakes the same facet mesh at the H resolution. Single broad irregular massif, nominal 1150 x 680 envelope, height up to 170 units (cruise altitude 78); ground outside the footprint remains flat. No moving actors, changing materials, extra scene objects or modifying shared terrain/lab assets. Undo the trial before rerunning, or sculpt the existing heightmap and use **Rebake Mission 01 Terrain**. Save/commit both terrain and facet assets and the scene if dirty.

Review from the existing game camera: the direct base-to-recon route should visually intersect the massif, while the western and northern route markers remain clear. Visual-only: neither TerrainCollider nor MeshCollider is enabled by this atom, so aircraft can still fly through it. Crash detection, camera occlusion handling, other ridges and boundaries are deferred. Unity visual and Play Mode verification are required locally.

## MISSION-01H1a — Rock palette / visibility (WIP)

Base `eefb060`. In the saved mission, choose **Air Combat > Missions > Apply Mission 01 Rock Palette**. It creates a small 64 x 32 point-filtered sRGB palette asset, binds it to the existing mission-only sand material and assigns one UV sample per existing triangle. Flat ground retains its original sand albedo (184,169,147); above 2 units transition toward stone, fully stone at 42 units; darker faces follow slope rather than light direction. Crest (137,121,108), steep rock (97,82,72). No heightmap, vertex position, triangle index, lighting, scene or collision changes. The existing Rebake command reapplies UV classification whenever this palette is bound, including after future sculpting. Undo restores mesh/material; the generated palette asset can remain on disk. Save/commit the palette and its meta, modified material and facet mesh. Verify both sides in the game camera; steeper geometry remains a separate trial.

## MISSION-01H1b — Cliff profile (WIP)

Base `74c14e0`. Open the saved mission and choose **Air Combat > Missions > Apply Mission 01 Cliff Profile**. Replaces only the generated central ridge height samples within its existing envelope; accepts the known H1 or H1b profile within a small height quantization tolerance, refuses custom sculpting inside the envelope, and preserves samples outside it. Low apron up to 18 units, steeper escarpment and broken crest up to 173 units. Rebuilds the same facet mesh at existing density and reapplies H1a rock UVs through the existing bake. No lighting, material, camera, collision, actor or other scene changes. Undo restores the previous terrain and mesh; save/commit both modified assets after review. Local Unity check required. Higher local facet density and terrain radar overlay are later atoms.

## MISSION-01H1c — Local relief facet density (WIP)

Base `a312b9d`. In Mission01_FirstSortie, run the existing **Air Combat > Missions > Rebake Mission 01 Terrain** command. Subdivide elevated coarse triangles into four, sample the existing TerrainData at their shared edge midpoints, and triangulate neighboring transition faces without T-junctions. Flat faces away from relief retain the original grid. Nominal local facet spacing is halved from 28.6 to 14.3 units; actual count is logged by the editor. The H1a palette is reapplied to all new faces. No change to the heightmap, cliff profile parameters, camera, material, collision or other scenes. The visible mesh samples the same heightfield more closely, so the exact rendered surface may differ slightly. Save/commit the modified facet asset. Undo restores the previous mesh; future rebakes use the refined rule. Unity visual/Play Mode verification is required locally.
