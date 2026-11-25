# ART-SAM-01 — Dominion Picket (geometry trial)

Base `b03eb8f`. Fictional compact deployed 6x6 truck for Mission 01. Angular heavy cab, four closed decorative launcher boxes, compact sensor silhouette and four outboard stabilizers. Ground Y=0, forward +Z; neutral matte clay only, not the final Dominion palette. No gameplay, targeting, colliders, moving mechanisms or mission placement in this atom.

- Source: `Tools/Blender/build_sam01_picket.py`. Run with regular Python to regenerate the six material-group OBJs; run `blender --background --factory-startup --python Tools/Blender/build_sam01_picket.py` to additionally save the editable `Resources/Blender/SAM01_Picket.blend` (86 separate pieces).
- Unity: **Air Combat > Art > Create SAM01 Picket Preview**. Creates the prefab `Assets/_Project/Prefabs/Environment/SAM01_Picket.prefab` and six neutral URP materials; existing prefab and materials are preserved. Double-click the prefab or place it in a test scene for visual review at game zoom. No mission changes. Commit generated prefab/materials and their .meta files if accepted.
- Geometry validation: closed oriented triangle topology per solid, no zero-area faces. 1936 triangles. Unity/Blender application execution must be checked locally.
