# Model authoring and bake rules

Readable stylized arcade military art: bold silhouettes, muted ground, controlled saturated accents and dynamic light. Gameplay readability before microdetail. [Art direction reference](Docs/Archive/ART_DIRECTION.md).

- Use Unity units for comparison; inspect actual moving Game view at cruise Y78. Camera yaw may differ by scene. Preserve readability against terrain and existing Lynx/Kestrel. Prefer deliberate faceted Blender meshes, clean winding/normals and known pivots; avoid microtriangles, excessive bevels and miniature diorama look. Test collision separately, including explicit marked terrain obstacles.
- URP Lit, matte/nonmetallic default, restrained palette, ground quiet and aircraft/targets/VFX readable. Real shadows, not painted directional lighting. Bake modest AO/normal only if useful. Shared lighting rig, verify shadow range/cascade artifacts and frame time. [Lighting](LIGHTING_L1.md).
- Deliver one reusable asset: reference -> footprint/silhouette -> editable .blend or reproducible generator -> imported mesh/material -> prefab/.meta. Use Blender bundled Python on Windows, factory-empty background scene when generator clears data. Preserve raw source/attribution outside Assets.
- Do not overwrite accepted shape or regenerate scenes to add variants; test side by side, promote selected palette to original source and material assets preserving GUIDs, then remove temporary trials only after reference check. Current example: [IN01/IN02](Docs/IN_02_INTERCEPTOR_PASS.md).
