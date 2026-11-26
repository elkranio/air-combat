# CA02 — Carryall flight visual trial

Accepted CA01 shape V8 and Heavy Duty V9A+ livery are frozen. This atom is a removable, scene-owned preview only. No target, damage, pickup, extraction, objective, radar contact, weapon or convoy rule changes.

Open/save Mission02 and run **Air Combat > Missions > M02 - Install CA01 Flight Preview**. It places one loaded CA01 in a small circle near the existing airborne start. The root is selected; F frames it in Scene view. Play to observe. For quick Game view checks enable the existing **Start Airborne** switch on Mission02RouteTrial manually; the installer does not alter it. R resets the preview to its initial pose with the sortie. You can observe continuously in Scene view while playing, or repeat Game view passes. Remove via **M02 - Remove CA01 Flight Preview** when done; restore any intentional Start Airborne setting separately.

Preview defaults: cruise Y78, radius 32, speed 18 units/s, mild 5-degree bank, hover ±1.2 units, 720 degrees/s counter-rotating turbines. Inspector allows tuning and toggling Show Payload in Play. CG01 is rigidly attached to the existing CargoMount for comparison, not picked up. The four exhaust and capture sockets remain unchanged. No exhaust particles yet.

CarryallRotorVisual identifies the 84 existing blade triangles per turbine from the accepted source, uses transient instance-only meshes, and checks triangle conservation. The shroud, hub, other surfaces, prefab asset, original meshes and Blender source are unchanged. This deliberately temporary approach avoids touching accepted asset GUIDs; author explicit reusable rotor meshes before production integration if needed. Preview follows scaled time and map pause. No collision or terrain-aware flight logic.

Local gate: compile, install, Play, check all four rotors spin without duplicating blades or obscuring bores, confirm loaded cargo stays attached during banking and unloaded toggle works, test R and Tab pause, verify no unrelated scene or prefab changes. The exact blade selection was verified against committed CA01 geometry JSON (84 triangles in each of four engines); Unity execution remains local acceptance.
