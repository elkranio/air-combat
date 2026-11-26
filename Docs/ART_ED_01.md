# ART-ED-01 — Warden escort drone (visual trial)

Working name, not accepted canon. ED01C is a fictional Dominion twin-rotor escort drone, smaller than Watcher (16.80 vs 25 units span), 10.27 units long including guns at rest, Y-up/+Z forward, 1916 rendered triangles. Two hollow ducted rotors with visible static blades, a compact tapered body, and forward compact turret mounts replace the rejected ED01A/B silhouettes. Matte olive/graphite with red accent trial. Existing prefab IDs and materials remain; the two yaw pivots and aft exhaust sockets are repositioned, pitch and muzzle local transforms unchanged. Visual only: rotor animation, flight, collision, weapons and mission integration are deferred.

Prefab: `Assets/_Project/Prefabs/Aircraft/ED01_Warden.prefab`. Drag into CombatSandbox or Mission01 temporarily at cruise Y78, scale 1, near the airborne player path. Review in actual game camera; remove temporary scene instance before saving. Compare with Watcher and Tiger. Inspect all mesh/material references and rotate each yaw/pitch pivot in prefab mode (undo changes after inspection). Do not create a new mission scene in this atom.

Reproducible source: `Tools/Blender/build_ed01_warden.py`. OBJ files are included. To save an editable `.blend` from the repo root use Blender's bundled Python via `blender --background --factory-startup --python Tools/Blender/build_ed01_warden.py`. Geometry export validates source solids' winding, closure and nonzero triangles. Unity import and Blender execution still need local validation.

Next after silhouette approval: reusable non-evasive air target and destruction feedback, then turret firing and encounter test.
