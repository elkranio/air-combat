# ART-SAM-01 — Dominion Picket (geometry trial)

Base `b03eb8f`. Fictional compact deployed 6x6 truck for Mission 01. Angular heavy cab, four closed decorative launcher boxes, compact sensor silhouette and four outboard stabilizers. Ground Y=0, forward +Z; neutral matte clay only, not the final Dominion palette. No gameplay, targeting, colliders, moving mechanisms or mission placement in this atom.

- Source: `Tools/Blender/build_sam01_picket.py`. Run with regular Python to regenerate the six material-group OBJs; run `blender --background --factory-startup --python Tools/Blender/build_sam01_picket.py` to additionally save the editable `Resources/Blender/SAM01_Picket.blend` (86 separate pieces).
- Unity: **Air Combat > Art > Create SAM01 Picket Preview**. Creates the prefab `Assets/_Project/Prefabs/Environment/SAM01_Picket.prefab` and six neutral URP materials; existing prefab and materials are preserved. Double-click the prefab or place it in a test scene for visual review at game zoom. No mission changes. Commit generated prefab/materials and their .meta files if accepted.
- Geometry validation: closed oriented triangle topology per solid, no zero-area faces. 1936 triangles. Unity/Blender application execution must be checked locally.

## ART-SAM-01B — Readability pass (WIP)

Base `35b5c80`. Bake 1.5x uniform scale into the mesh geometry (keep prefab root and instance scale at one), enlarge the complete decorative equipment module by an additional 28% around the deck, and thicken the deployed supports and feet. Preserve the material groups, OBJ asset paths, mesh topology and existing prefab references. No new scene changes or installer; Unity reimports the meshes automatically. Compare at flight height before selecting the Dominion palette.

## ART-SAM-01C — Imperial Olive palette A (WIP)

Base `e8a30f2`. Geometry and scale unchanged. Separate the four launcher box bodies and front caps into `Launcher` and `Accent` mesh/material groups; headlights retain their own light neutral `Caps` material. Olive hull, dark olive equipment, graphite mechanical parts, light muted olive launcher bodies and saturated red launcher caps. No camouflage or texture noise. The Blender source exports eight OBJ groups and the palette trial. After Unity imports the OBJ changes, run **Air Combat > Art > Apply SAM01 Imperial Olive** to update the existing prefab in place, retain its GUID and old child/material references, and add the two missing groups. Save/commit the updated prefab and new/changed materials and metas. No placement or flight changes. Review in the actual game camera before palette B/C.

## ART-SAM-01D — Industrial Empire palette B (WIP)

Base `120783c`. Alternate cold industrial gray hull, charcoal equipment and chassis, light gray launcher bodies and saturated red front caps. Reuse all eight existing material assets, mesh assets and the same prefab. No geometry, scale, lighting or scene changes. In Unity choose **Air Combat > Art > Apply SAM01 Industrial Empire** for B, or **Apply SAM01 Imperial Olive** to restore A. Both menu commands update shared materials globally, so compare successive screenshots under identical lighting; they do not create independent per-instance variants. The Blender generator retains palette A during this Unity-only color trial. Commit the selected material state after review.

## ART-SAM-01E — Heavy Industry palette C (WIP)

Base `d3804e3`. Muted yellow-olive hull, dark olive equipment and chassis, pale gray-green launcher bodies and yellow industrial accents on the front caps. Choose **Air Combat > Art > Apply SAM01 Heavy Industry**. A and B remain selectable via their existing menu commands. Same eight shared materials, prefab, geometry, scene and lighting; Blender retains palette A during the color trial. Compare under identical game-camera conditions and commit the selected material state after review.
