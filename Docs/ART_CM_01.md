# ART-CM-01 — Sparrow (visual trial)

Based on green `58528f5`. Original fictional cruise-missile-shaped arcade target prop. Readable cylinder, broad straight wings, graphite nose and muted amber marking; deliberately enlarged relative to a realistic object. No internal weapon geometry or gameplay logic. Four matte URP material sections, 608 triangles. +Z forward, +Y up. `ExhaustVisualSocket` is a cosmetic attachment point only; no smoke or effect implemented.

Prefab: `Assets/_Project/Prefabs/Aircraft/CM01_Sparrow.prefab`. Drag it into the main map and compare to the player at flight altitude. For a quick static trial, position `(-383, 78, -372)`, yaw `-45`, scale 1. Pause early to inspect from gameplay camera; remove the trial after evaluation. No scene changes are included. The prop is not yet a damageable target and will not move.

Rebuild geometry: `python Tools/Blender/build_cm01_sparrow.py`. Run the same script in Blender to produce an editable blend in `Resources/Blender/CM01_Sparrow.blend` (not included as Blender has not been run). Import assets use the established Tiger single-object OBJ prefab pattern. Inspect materials and orientation after Unity import.

Next after visual approval: a fictional route-following target test and damage/feedback, separate from this art-only atom. Cruise flight visuals should avoid the thick AAM smoke trail; consider a short subtle exhaust effect later.
