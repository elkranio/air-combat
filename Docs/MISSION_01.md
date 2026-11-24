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

## MISSION-01C2B — Compact airfield B (WIP)

Base `e7d1b7c`. With layout A in the existing mission scene, select **Air Combat > Missions > Apply Compact Airfield B** once. Undo is supported; A remains available in the prior green. This pass modifies only the current scene instance.

- Runway 240 x 24, center local X125, with proportionally resized markings. Apron 82 x 66 meets the runway directly; hide the old diagonal taxiway. Open the right edge marking at the shared access lane.
- Three 18 x 22 bays in one row on the south side of the apron, facing the open central lane. Two Kestrels parked; the third bay remains empty. Keep the through lane and hangar approach clear of aircraft.
- Hangar and tower transforms and models unchanged. The temporary free-flight player is unchanged; move only the static takeoff placeholder. No runtime changes. Verify composition, scale and takeoff space in Unity before choosing A or B.

## MISSION-01C3 — Unified pavement B (WIP)

Base `ea0ec92`. Layout B is the preferred composition for further visual iteration. With its saved scene open, select **Air Combat > Missions > Unify Mission 01 Airfield Pavement**. The command validates B, then replaces the visible runway, apron and hangar approach with one dark asphalt mesh and a continuous 3-unit ground shoulder. Rounded junctions use four segments; outside corner trims remain small to preserve parking space. The old surface objects stay disabled. No shared material or prefab is edited.

- Keep the 240 x 24 runway, 82 x 66 apron, hangar, tower, parked aircraft and static takeoff start in their B positions. Open the runway edge paint across the whole apron connection and thin parking lines to 0.42 units.
- The explicit editor command saves `Mission01_AirfieldPavement.asset` (two meshes) in the mission art folder. Save the scene and commit the mesh asset and its .meta. Undo restores scene objects; the mesh asset remains available for reapplication. No runtime generation, collision or gameplay changes. Unity import/Play Mode and aircraft contrast remain to be checked locally.

## MISSION-01C4 — Simple parking markings (WIP)

Base `b967377`. Direct scene-only art pass: replace the three U-shaped parking outlines with two 15-unit separators (0.42 wide). Keep the unused marking objects disabled for easy reversal. Center the row within the apron along local X (158, 178, 198; midpoint 178), with 20-unit spacing, and move it one unit inward to Z-95. The third bay remains empty; its disabled Lynx follows the new position. Retain the clear lane to the hangar, unified pavement, buildings, runway and player as-is. Open the scene after applying; no editor command or asset regeneration needed. Verify in Unity.

## MISSION-01C5 — Outer parking separators (WIP)

Base `4087039`. Keep the C4 parking layout and add the two outer separators at local X148 and X208, matching the existing lines at X168 and X188 (Z-95, 15 units long, 0.42 wide). This defines three equal bays, including the empty player bay, without restoring U-shaped outlines. Reactivate the existing unused outer line objects; no new assets or editor commands. Aircraft, pavement, runway and buildings are unchanged. Next visual pass: control tower.

## MISSION-01C6 — Hangar flip and roof marking (WIP)

Base `c0d3a0c`. Open the saved mission scene and select **Air Combat > Missions > Apply Mission 01 Hangar Pass**. Orient the actual imported front portal toward the apron and bring it to the apron edge. Preserve the hangar model and annex. Replace the long approach neck with a continuous rounded apron edge and three short guide dashes. Add a heavy, solid white `05` conforming to the roof surface. The number reuses the existing paint material and is attached to the scene hangar instance, with no font or texture dependency.

Save the scene and commit the new `Mission01_HangarPass.asset` and `.meta`. The old pavement remains disabled for comparison; no shared prefabs or flight logic change. Verify portal alignment, roof marking placement and appearance in Unity.

## MISSION-01C7 — Offset hangar and pilot barracks (WIP)

Base `76ba27b`. With the saved C6 mission scene open, select **Air Combat > Missions > Apply Mission 01 Barracks Pass**. The hangar and guide move 12 units along local +Z; hide the annex and its collider on this instance. Reproject the heavy `05` to a clear part of the camera-facing roof, avoiding ribs and vents. Keep the existing apron, runway, tower and parked aircraft unchanged.

Add two 20 x 7.6 single-storey barracks with gabled roofs, doors, windows and a shared narrow footpath. They reuse existing hangar materials and are visual-only. Save the scene and commit `Mission01_BarracksPass.asset` and its `.meta`. No changes to flight or shared prefabs. Verify roof marking, spacing and building colors in Unity.

## MISSION-01C7a — Barracks roof repair

Base `6fee760`. Fix the face builder so correcting winding never reverses the reused input rings in place. For already generated barracks, open the mission and select **Air Combat > Missions > Repair Mission 01 Barracks Roofs**. The command rebuilds the shared roof subasset in place, preserving references and all layout/material settings. Save and commit the repaired `Mission01_BarracksPass.asset` and the mission scene if it was not saved previously. Do not rerun the C7 layout command on an already built scene. Fresh C7 generation uses the corrected geometry.

## MISSION-01C8 — CQ01 single crew quarters (WIP)

Base `1459a2f`. Replace the two C7 barracks (including their footpath) with one independent imported visual prefab. With the local C7 scene open, select **Air Combat > Missions > Install CQ01 Crew Quarters**. The old C7 group is disabled, not destroyed. The hangar, number, guide, tower, apron and aircraft are untouched. The building is 20 x 16 with a closed shallow gabled roof, 4-unit walls, 6-unit ridge, windows and an entry landing facing the apron. Reuse hangar materials; no new runtime or collision behavior. Save and commit the mission scene plus `CQ01_CrewQuarters.prefab` and its `.meta`.

The OBJ files are generated from `Tools/Blender/build_cq01_crew_quarters.py`. Run `blender --background --factory-startup --python Tools/Blender/build_cq01_crew_quarters.py` to generate the editable source under `Resources/Blender/`. Each solid is checked for nondegenerate faces, consistent outward winding and closed edge topology. Unity import and appearance still require local verification.

## MISSION-01C8a — Orphan hangar accent cleanup

Base `dd63baf`. The orange mark visible on the CQ01 roof comes from the old B1f service-annex door stripe in `Arch_Accent.obj`, not CQ01. With the C7/C8 mission scene open, select **Air Combat > Missions > Clean Up Mission 01 Hangar Accent**. Replace only this mission instance's accent mesh with the original portal stripe geometry (12 triangles instead of 24); the annex stripe is omitted. Keep the original shared hangar prefab, accent material, CQ01 and all transforms unchanged. Save and commit the mission scene. The command is safe to repeat; verify appearance in Unity.

## MISSION-01C8b — Simplified hangar guide

Base `a8c38fe`. In the mission scene select **Air Combat > Missions > Simplify Mission 01 Hangar Guide**. Replace the three guide dashes with two of the same dimensions, farther from the portal (local X195 and X201). Keep the existing C7 Z offset and paint material. Save the scene and new `Mission01_HangarGuideTwoDashes.asset` with its .meta. No other layout changes.

## MISSION-01C8c — Remove hangar guide

Base `19856c5`. The guide was rejected after visual review. Open the mission scene and select **Air Combat > Missions > Remove Mission 01 Hangar Guide**. Disable the guide object (either the original three or revised two dashes), leaving its mesh available for reversal. Save and commit the scene. No other layout or asset changes.

## MISSION-01D — Player Lynx free-flight preview (WIP)

Base `7071ba6`. Open Mission01_FirstSortie and select **Air Combat > Missions > Install Player Lynx Preview**. Create a separate `PlayerLynx_Combat.prefab` from the Kestrel combat composition, replace only its chassis with the approved PF02 visual and authored sockets, and preserve flight, boost, HP, radar and weapon tuning. Adapt cosmetic launcher origins and chassis collision geometry only if the source has colliders. Keep Kestrel and PF02 visual prefabs unchanged.

Replace the temporary airborne mission player at its current pose, rebind the flight session and camera, and leave firing disabled in this preview. The static runway Lynx remains until the ready-state atom. Save the scene, new player prefab and its .meta. Unity verification: Play, A/D steering, W boost and S tight turn with both exhausts aligned, R reset; check console and model orientation. No changes to the production combat scene or runtime scripts.

## Next work (order flexible, all WIP)

Current visual trial: [ART-CT-01 — Compact control tower](ART_CT_01.md).
Install the new tower in the mission only, then compare at gameplay zoom.

1. Review the airfield layout in the actual game camera; adjust roads, spacing and model missing pieces only if needed.
2. Integrate the approved Lynx visual into a player aircraft prefab without retuning the existing flight/weapon logic. Keep Kestrel available.
3. Mission intro/ready state with room for a later cutscene or dialogue; W initiates automatic takeoff.
4. Automatic roll, liftoff, climb and handover to normal flight.

This is the first milestone only: scene launch through control handover. Objective, route, landing and debrief are later iterations. No universal mission framework or final campaign commitment yet.
