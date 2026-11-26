# Model authoring and bake rules

Readable stylized arcade military art: bold silhouettes, muted ground, controlled saturated accents and dynamic light. Gameplay readability before microdetail. [Historical art direction](Docs/Archive/ART_DIRECTION.md).

## Geometry and materials

- Use Unity units for scale comparison, exaggerate important shapes deliberately. Inspect actual moving Game view at the scene camera and cruise Y78; camera yaw differs between sandbox and campaign. Keep silhouettes coherent with Lynx/Kestrel and authored terrain.
- Prefer deliberate Blender mesh shapes, clean normals/winding and known pivots. Avoid noisy microtriangles, excessive bevels and miniature-looking dioramas. Test collision separately: aircraft use explicit flight collision and marked obstacle geometry, not just any collider.
- URP Lit, matte/nonmetallic default, restrained palette. Ground is quiet; aircraft, targets and VFX stand out. Use real shadows, not painted directional lighting. Bake modest local AO/normal only when useful.
- Use the shared lighting rig and verify shadow range/cascade artifacts and frame time. See [lighting](LIGHTING_L1.md).

## Delivery

One reusable asset at a time: reference -> footprint and silhouette -> `.blend` or reproducible generator -> imported mesh/materials -> prefab and `.meta`. Verify scale, sockets, normals, collision, materials, shadows, in-game readability and actual dependencies. Do not rebuild whole scenes or overwrite accepted art to add a new variant. Retain source/attribution separately from runtime assets.
