# ART-UD-01 — Moth visual trial

Based on green `248d5fd`. Original fictional arcade target visual: broad clipped triangular silhouette, faceted center mass, near-black charcoal hull and wings, long paired red wing stripes and static rear rotor motif. Four matte URP materials, 284 triangles, +Z forward / +Y up. Deliberately exaggerated game proportions, no functional or internal weapon geometry. No movement, collision, targeting, effects or scene changes.

Prefab: `Assets/_Project/Prefabs/Aircraft/UD01_Moth.prefab`. Drag into `ArcadeDesertE1` at `(-383, 78, -372)`, yaw -45, scale 1, as in the Sparrow art trial. Pause early to compare at the gameplay camera; remove the trial instance after evaluation. Check the silhouette, palette, scale, mesh orientation and material imports. No gameplay regression expected; existing interception remains unchanged.

Rebuild geometry: `python Tools/Blender/build_ud01_moth.py`. Run the script in Blender to create `Resources/Blender/UD01_Moth.blend`; Blender was not run for this patch. Source geometry and Unity assets are included. Visual approval before flight behavior or rotor animation.
