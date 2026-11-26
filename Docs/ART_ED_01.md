# ART-ED-01 — Warden escort drone (visual trial)

Working name, not accepted canon. Original fictional Dominion heavy escort drone, visually smaller than Watcher (17.7 vs 25 units wingspan), 13.68 units long, Y-up/+Z forward, 1152 rendered triangles. Matte olive/graphite with red accent trial. The two turrets each have a yaw pivot, pitch pivot and +Z muzzle socket; two exhaust sockets point rearward. Visual only: no collider, flight, combat, mission or sandbox changes.

Prefab: `Assets/_Project/Prefabs/Aircraft/ED01_Warden.prefab`. Drag into CombatSandbox or Mission01 temporarily at cruise Y78, scale 1, near the airborne player path. Review in actual game camera; remove temporary scene instance before saving. Compare with Watcher and Tiger. Inspect all mesh/material references and rotate each yaw/pitch pivot in prefab mode (undo changes after inspection). Do not create a new mission scene in this atom.

Reproducible source: `Tools/Blender/build_ed01_warden.py`. OBJ files are included. To save an editable `.blend` from the repo root use Blender's bundled Python via `blender --background --factory-startup --python Tools/Blender/build_ed01_warden.py`. Geometry export validates source solids' winding, closure and nonzero triangles. Unity import and Blender execution still need local validation.

Next after silhouette approval: reusable non-evasive air target and destruction feedback, then turret firing and encounter test.
