# ART-PF-02 — Lynx visual trial (WIP)

Base `c7c78c3`. Original fictional arcade fighter exterior inspired by the long-nosed twin-tail Tomcat silhouette. Fixed swept wings, exaggerated elongated canopy, separate rear pods, muted cool-grey matte paint with restrained amber tips. Approx. 13.45 units long, 9.56 wide; five material groups and 888 triangles. +Z forward, +Y up. No real aircraft dimensions or functional geometry intended.

Drag `Assets/_Project/Prefabs/Aircraft/PF02_Lynx.prefab` into the current playground or main map at combat height, scale 1. Compare with Kestrel at the same camera zoom. It is visual-only, with no flight components, weapons or collider. Do not replace Kestrel until the visual is approved. No existing scene, asset, gameplay or build settings changed.

Rebuild OBJs: `python Tools/Blender/build_pf02_lynx.py`. Run the same script in Blender to generate `Resources/Blender/PF02_Lynx.blend` if desired. Blender and Unity import/Play Mode not run in the patch environment; check model orientation, material overrides, and appearance locally. If accepted, a separate atom will integrate the chassis, sockets, hitbox and effects into the player prefab, retaining flight tuning.
