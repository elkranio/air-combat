# ART-CT-01 — Compact control tower (WIP)

Base `67c635b`. Mission 01 visual pass after the unified pavement and four parking separators. Original fictional compact two-storey concrete tower, flared panoramic cabin, chamfered roof, two antennas and red beacon. Matte colors, five mesh groups, no baked light or textures. Approx. 15.2 x 15.8 footprint (roof), 17.73 units high; 624 triangles. Local -X faces the runway. Existing B1e tower remains available in the mission, disabled for comparison; shared assets, runway, apron, hangar, aircraft and gameplay unchanged.

After importing the patch, open `Mission01_FirstSortie` and select **Air Combat > Missions > Install CT01 Control Tower**. The command creates the CT01 materials and reusable prefab, installs it at the original tower position and disables the original. It refuses to overwrite an installed scene instance. Save the scene and commit the generated `.mat`, `.prefab` and `.meta` assets. Undo restores scene objects (generated assets remain). Visual-only trial; no new colliders or runtime logic.

Source: `Tools/Blender/build_ct01_control_tower.py`. Python generates the committed OBJs; to additionally produce `Resources/Blender/CT01_ControlTower.blend`, run from the project root:

```bash
blender --background --python Tools/Blender/build_ct01_control_tower.py
```

Python geometry generation checked. Blender and Unity not executed in the patch environment. Evaluate silhouette, size, colors, OBJ orientation and placement at the existing camera zoom before approval.
