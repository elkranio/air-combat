# ART-PF-02 — Lynx visual trial (WIP)

Base `c7c78c3`. Original fictional arcade fighter exterior inspired by the long-nosed twin-tail Tomcat silhouette. Fixed swept wings, exaggerated elongated canopy, separate rear pods, muted cool-grey matte paint with restrained amber tips. Approx. 13.45 units long, 9.56 wide; five material groups and 888 triangles. +Z forward, +Y up. No real aircraft dimensions or functional geometry intended.

Drag `Assets/_Project/Prefabs/Aircraft/PF02_Lynx.prefab` into the current playground or main map at combat height, scale 1. Compare with Kestrel at the same camera zoom. It is visual-only, with no flight components, weapons or collider. Do not replace Kestrel until the visual is approved. No existing scene, asset, gameplay or build settings changed.

Rebuild OBJs: `python Tools/Blender/build_pf02_lynx.py`. Run the same script in Blender to generate `Resources/Blender/PF02_Lynx.blend` if desired. Blender and Unity import/Play Mode not run in the patch environment; check model orientation, material overrides, and appearance locally. If accepted, a separate atom will integrate the chassis, sockets, hitbox and effects into the player prefab, retaining flight tuning.

## ART-PF-02B — Bright squadron palette (trial)

Base `ec6d2cf`. Material-only pass: saturated blue-teal hull, pale cool wings, turquoise canopy, vivid orange existing tips; dark nose and engine details unchanged. Keep matte lighting and existing material GUIDs. No logos, stripes, textures, mesh, prefab or gameplay edits. The Blender generator palette matches the Unity materials. Compare in the existing scene at gameplay zoom before acceptance.

## ART-PF-02C — Orange wings (trial)

Base `7ea99d1`. Replace the pale blue Wing material with saturated orange (RGB 0.94, 0.34, 0.07) on main wings and rear horizontal stabilizers. Keep the existing slightly brighter orange tips and all other materials unchanged. Blender generator palette synchronized. No geometry, logo, prefab or scene changes. Compare at gameplay zoom against the desert terrain and apron.

## ART-PF-02D — Kestrel orange (trial)

Base `5cd5176`. Match Wing and Mark base colors to the existing E1C_Kestrel_Panels material (RGB 224, 121, 64; #E07940). Both wings and tips use the same orange for a clean palette comparison. Hull, canopy, dark details, geometry and prefab unchanged. Blender generator palette synchronized.

## ART-PF-02E — Kestrel blue (trial)

Base `b74bd9a`. Match only the Hull base color to E1C_Kestrel_Hull (RGB 49, 83, 107; #31536B), not the darker Upper material. Keep the matte shader settings, Kestrel orange wings and tips, canopy, dark details, geometry and prefab unchanged. Blender generator palette synchronized. Compare at gameplay zoom.

## ART-PF-02F — Kestrel wingtip blocks (trial)

Base `093bbc9`. Match the Wing material to the Hull blue, including the rear horizontal stabilizers. Keep the exact Kestrel orange on broad main-wing tips only (outer ~0.93 units, about one third of the nominal main-wing panel span). Split the original wing surface triangles at the paint boundary rather than overlaying raised markings. No silhouette, prefab, material GUID, cockpit or gameplay changes. Regenerated Wing and Mark OBJs; the generator matches the Unity materials. Check the paint boundary and readability at gameplay zoom.

## Visual acceptance

PF-02F approved by the player. Lynx is the intended lead aircraft for First Sortie; the visual prefab remains separate until the player integration atom. Kestrel remains available. Preserve the accepted blue/orange paint and current silhouette.
