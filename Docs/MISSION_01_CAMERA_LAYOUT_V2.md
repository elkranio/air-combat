# MISSION-01H2C — Camera layout v2 (WIP)

Base `d69b4e3`. Supersedes the north-running H2B/v4 barrier after gameplay-camera review: long wall planes and huge faces read poorly at the actual flight zoom. The accepted v2 scheme is drawn in ground-plane axes of the fixed yaw -45 camera. Keep the map 3200 x 3200, the flattened sand, the airfield, launch sequence, HUD and all combat tuning. Reuse the four authored cliff prefabs; do not change the model sources or shared materials.

With the saved `Mission01_FirstSortie` open, run **Air Combat > Missions > Apply Mission 01 Camera Layout v2** once. It creates eight editable cliff instances and replaces the obsolete barrier and route plan. The two existing SAM01 visuals are moved and four additional instances are created. All six are still visual-only; radii in names are planning values, not runtime detection settings. Three recon markers and one optional objective marker are empty transforms, not actors. The route markers are not checkpoints. Save the scene after review; the command does not change any assets or other scenes. Undo supported; repeat with the new groups present is a no-op.

## Cliff placement

| Module | World X | World Z | Yaw | Scale X/Y/Z |
| --- | ---: | ---: | ---: | --- |
| CL03 Ridge | -968 | -395 | 49 | .63/.76/.84 |
| CL01 Fan | -680 | -106 | -40 | .66/.83/.52 |
| CL03 Ridge | -418 | 165 | 43 | .62/.72/.82 |
| CL03 Ridge | -143 | 449 | 51 | .61/.79/.86 |
| CL01 Fan | 137 | 729 | -48 | .65/.78/.48 |
| CL03 Ridge | 404 | 1004 | 40 | .60/.74/.76 |
| CL04 Citadel | 638 | 1203 | -31 | .87/.66/.70 |
| CL02 Spur | 1270 | -820 | -48 | 1.16/1.02/.93 |

All origins Y0. Main ridge has seven smaller modules, top heights approximately 131–145, and runs along the screen horizontal with varied silhouette and yaw. The separate lower spur extends to the map edge. Some geometry deliberately extends outside the square to close edge bypasses once boundaries are enforced. Keep one RadarTerrainObstacle per closed module. Overlapping sections may still show internal radar outlines. No radar rendering change or physical collision in this atom.

## Mission layout

| Site | X | Z | Planning radius |
| --- | ---: | ---: | ---: |
| SAM A | -359 | -337 | 320 |
| SAM B | 805 | -436 | 465 |
| SAM C | 688 | 711 | 350 |
| SAM D | 593 | 1573 | 420 |
| SAM E | 1194 | 1938 | 465 |
| SAM F | 1726 | 2083 | 450 |
| Recon 01 | 1383 | 1081 | — |
| Recon 02 | 1415 | 1266 | — |
| Recon 03 | 1613 | 1284 | — |
| Optional target | -698 | 814 | — |

The base remains (-700,-700). Main route bends below the ridge around A/B/C, then reaches the open recon area. Return by the same path, or voluntarily pass through the D/E threat overlap, around the ridge end, to the optional objective and back. Optional target type remains open; its marker is at ground level only for planning. No new mission gates, actors or timers. Calculated source mesh slices at Y78 form one connected main ridge and intersect the intended map edge; the separate spur touches its edge. Planned main route clears A/B/C by about 78/141/131 units and source cliff sections by about 250 units. These are approximate geometric checks, not actual aircraft turning or in-game shadow/visibility verification. Safe-route geometry starts near the base marker, not a scripted takeoff path; local adjustment may be needed.

## Review

Inspect the main ridge at gameplay zoom from both sides, the smaller modules, lower spur, joins, shadow quality, radar, caution arc, and space around the airfield and targets. Fly the route in both directions. Terrain and old trial remain retired; do not run earlier layout commands. Unity compilation, actual imported mesh orientation, flight and visual verification remain local. Radar union, actual crashes, map boundaries, SAM behavior and mission goals are separate later work.
