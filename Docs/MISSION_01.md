# MISSION-01 — First Sortie (WIP)

The first campaign mission is a development slice, not a finished design. Aim: a full sortie from intro and takeoff to intercepting a slow reconnaissance airship, returning to base and landing. A few Mantis units may provide light pressure along the route; the initial loadout is cannon only. Mission geography, timings, difficulty and assets remain open for playtesting.

## MISSION-01A — Empty map

Base `d6f6d21`. Apply the patch, let Unity compile, then select **Air Combat > Missions > Create or Open Mission 01 - First Sortie**. The command creates `Assets/_Project/Scenes/Mission01_FirstSortie.unity` and independent assets under `Assets/_Project/Art/Environment/Mission01_FirstSortie/`. It retains the source scene's player, camera, lighting and flight settings in the copy only; removes the encounter, firing, HUD and other authored objects. It never overwrites the existing mission or changes the source scene, terrain trial, or build settings.

- Flat editable TerrainData: 2000 x 2000, Y0, 257 height samples, 180 units of future relief capacity.
- Separate matte sand mesh: 70 x 70 grid, ~28.6-unit cells, 9800 flat triangles. Same baker as TERRAIN-01; independent material, data and mesh. No terrain collision yet. No mission-specific rebake menu in this atom; add it when relief authoring is needed.
- Empty Airfield root at (-700, 0, -700) is a provisional southwest anchor; Objectives root is empty. No runway, structures or enemies.
- Temporary Kestrel spawns airborne above the anchor at Y78, yaw 45. A/D/W/S and R work; no automatic boundary reset. This is a layout preview, not the mission start. Lynx visual and wingtip palette are approved, but player integration is a separate atom.

Save/commit the created scene and its new asset folder including all .meta files after verifying Play Mode. The menu reopens rather than regenerates an existing scene. Unity compilation and Play Mode need local verification.

## Next work (order flexible, all WIP)

1. Agree on the airfield plan (runway, taxiway, three parked aircraft, hangar, tower); reuse accepted assets where suitable, model missing pieces and evaluate from the actual game camera.
2. Integrate the approved Lynx visual into a player aircraft prefab without retuning the existing flight/weapon logic. Keep Kestrel available.
3. Mission intro/ready state with room for a later cutscene or dialogue; W initiates automatic takeoff.
4. Automatic roll, liftoff, climb and handover to normal flight.

This is the first milestone only: scene launch through control handover. Objective, route, landing and debrief are later iterations. No universal mission framework or final campaign commitment yet.
