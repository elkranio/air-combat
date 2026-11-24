# MISSION-01 — First Sortie (WIP)

The first campaign mission is a development slice, not a finished design. Aim: a full sortie from intro and takeoff to intercepting a slow reconnaissance airship, returning to base and landing. A few Mantis units may provide light pressure along the route; the initial loadout is cannon only. Mission geography, timings, difficulty and assets remain open for playtesting.

## MISSION-01A — Empty map

Base `d6f6d21`. Apply the patch, let Unity compile, then select **Air Combat > Missions > Create or Open Mission 01 - First Sortie**. The command creates `Assets/_Project/Scenes/Mission01_FirstSortie.unity` and independent assets under `Assets/_Project/Art/Environment/Mission01_FirstSortie/`. It retains the source scene's player, camera, lighting and flight settings in the copy only; removes the encounter, firing, HUD and other authored objects. It never overwrites the existing mission or changes the source scene, terrain trial, or build settings.

- Flat editable TerrainData: 2000 x 2000, Y0, 257 height samples, 180 units of future relief capacity.
- Separate matte sand mesh: 70 x 70 grid, ~28.6-unit cells, 9800 flat triangles. Same baker as TERRAIN-01; independent material, data and mesh. No terrain collision yet. No mission-specific rebake menu in this atom; add it when relief authoring is needed.
- Empty Airfield root at (-700, 0, -700) is a provisional southwest anchor; Objectives root is empty. No runway, structures or enemies.
- Temporary Kestrel spawns airborne above the anchor at Y78, yaw 45. A/D/W/S and R work; no automatic boundary reset. This is a layout preview, not the mission start. Lynx visual and wingtip palette are approved, but player integration is a separate atom.

Save/commit the created scene and its new asset folder including all .meta files after verifying Play Mode. The menu reopens rather than regenerates an existing scene. Unity compilation and Play Mode need local verification.

## MISSION-01B/C — Airfield layout v0 (WIP)

Base `86f7331`. The player approved the schematic: one runway, a connecting taxiway, three parked aircraft, one hangar and one control tower. With the existing mission scene open, select **Air Combat > Missions > Build Mission 01 Airfield v0**. This adds an editable child layout beneath the existing empty Airfield anchor. It refuses to overwrite an existing layout. Review in Scene/Game view, then save and commit the scene. Do not rerun an old B1 installer.

- Reuse the existing B1e runway/shoulders/markings (460 x 36), B1f arch hangar and B1e tower. Only the new scene instance is modified. The entire layout is rotated 30 degrees towards the map interior.
- Resize the apron to 150 x 130, move the 22-wide taxiway towards the near end of the runway, add a separate hangar approach and three open parking stands. Old apron markings and wear are hidden on this instance. Roads and spacing are provisional and expected to change after visual review.
- Two visual-only Kestrels reuse the combat prefab's painted chassis meshes (no flight, weapons, scripts or colliders); one parked Lynx uses the approved visual prefab. Another static Lynx marks the future runway start, to be removed when the real player is integrated.
- No change to the temporary airborne player, camera, terrain, flight logic, mission flow, existing prefabs or other scenes. No new runtime code. Unity compile/Play Mode verification remains local.

## MISSION-01C2A — Compact airfield A (WIP)

Base `7f6911d`. In the existing mission scene, select **Air Combat > Missions > Apply Compact Airfield A** once. This adjusts the existing editable v0 layout in place (Undo supported), without changing shared prefabs, terrain, flight or the buildings. Save the scene after review. The v0 layout is preserved in the prior green commit.

- Runway 300 x 26, with proportional shoulders and markings; shift its center 85 units towards the apron. Keep the static takeoff Lynx aligned with the new runway.
- Apron 90 x 70, three 18 x 22 stands facing the taxi lane; two Kestrels parked and the Lynx bay empty (disabled parked visual). A short 18-wide diagonal taxi connection leads towards the runway's takeoff direction.
- Hangar and tower stay at their exact v0 transforms. Adapt the hangar approach to meet the smaller apron. Temporary airborne player unchanged.
- Compare A from the game camera, then try a separate, even more compact B. No final art or gameplay decision yet. Unity verification is local.

## Next work (order flexible, all WIP)

1. Review the airfield layout in the actual game camera; adjust roads, spacing and model missing pieces only if needed.
2. Integrate the approved Lynx visual into a player aircraft prefab without retuning the existing flight/weapon logic. Keep Kestrel available.
3. Mission intro/ready state with room for a later cutscene or dialogue; W initiates automatic takeoff.
4. Automatic roll, liftoff, climb and handover to normal flight.

This is the first milestone only: scene launch through control handover. Objective, route, landing and debrief are later iterations. No universal mission framework or final campaign commitment yet.
