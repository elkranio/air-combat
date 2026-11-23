# BrokenVector Low Poly Cliff Pack — reference study

Status: **technical assembly ready, visual verdict pending Unity Play Mode**.

The imported package is at `Assets/BrokenVector/LowPolyCliffPack/`. Keep it unmodified.
Its contents, verified on `master`: 18 `.dae` models, 17 prefab assets, 3 palette
materials and 3 small palette textures. Demo scene was intentionally omitted.
Original prefabs already contain nonconvex `MeshCollider`s.

The imported `.dae` model raw bounds for typical straight modules are roughly
20 units along the section length and 15 units vertically (the COLLADA file has
axis transforms). Scale is computed from the actual Unity MeshRenderer bounds,
not hard-coded from raw COLLADA coordinates.

## One-time assembly

With Unity in Edit Mode and no unsaved scene changes:

**Air Combat > Art > Build Cliff Pack Reference Study**

Creates only `Assets/_Project/Scenes/FrontierCliffReference.unity`, copying the
flight/camera/light composition from V17, then removing old V17 art **in the copy
only**. The source `FrontierStrata.unity` and combat scenes are untouched. It
refuses to overwrite an existing reference study scene.

The new scene has a neutral flat plane, two short cliff walls with four source
prefabs each, and a roughly 120–180m free flight corridor between the walls.
Each cliff module is independently height-normalized to approximately 73–82m
from actual imported bounds. The left row uses Yellow palette, right uses Grey.
Their source mesh, UVs and MeshColliders stay unchanged. The Unity-generated
reference materials are URP Lit and isolated from the original package.

**Test:** Play `FrontierCliffReference.unity`, fly along +Z between the banks
with WASD / Space, change camera pitch via 1/2/3. Select a cliff in Hierarchy
and press F for close-up. Do not evaluate the art from a whole-map screenshot.

This is deliberately a *visual comparison*, not finished canyon topology: there
may be overlaps/joins between prefab segments. A collider is present, but no
custom aircraft collision/reset is installed in this study because the current
flight controller moves its Transform directly.

**Decision gate:** Does a real handcrafted third-party modular cliff read at
ortho size 80? If yes, analyze its silhouette and UV palette workflow. If no,
try another reference with a different art language; don't create another blind
noise-based terrain generator.
