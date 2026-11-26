# Mission 02 — Convoy Interdiction / route graybox

Working geometry only. Source topology: [V03](MISSION_02_TOPOLOGY_V03.svg). The river, bridge, combat encounters, supply depot buildings, objectives, and convoy deployment are intentionally absent in this pass. Do not regenerate the scene after authoring starts.

## Create once

1. Apply the patch, open Unity, and allow imports/compilation. Save any unrelated changes first.
2. Run **Air Combat > Missions > M02 - Create Route Graybox**. It copies the saved M01 scene as a new scene, keeps the actual airfield visual hierarchy (including runway/apron overrides), reuses shared player/camera/HUD prefabs and M01 tuning, and discards the other M01 gameplay and geography from the copy. The original M01 scene is not edited.
3. Open `Assets/_Project/Scenes/Mission02_ConvoyInterdiction.unity` (the authoring menu opens it automatically). Generated meshes/materials/terrain are in `Assets/_Project/Art/Environment/Mission02_ConvoyInterdiction/`. Commit the generated scene, assets and metadata alongside this patch. The installer refuses to overwrite an existing scene or generated art folder.

## Route test

- W starts the same scripted roll/climb as M01. Normal flight handover is at Y78; A/D, W/S, Shift retain the saved M01 tuning. Weapons are disabled for this trial. G toggles guide lines; R restarts from the runway with full fuel and clears split times. A collision or leaving the operation bounds stops flight until R.
- The timer counts only airborne time after the handover, excluding the scripted takeoff. A, B, C register once on entering a 75-unit XZ radius. C -> B is measured independently, including time spent at C, even if B was previously visited.
- Blue = direct intercept / return to B; teal = optional canyon; red = future convoy road. Guides are diagnostic only, never enforce flight rails. The underlying rock meshes use the existing radar/flight collision path.
- Test S -> A, reset, then S -> C -> B. Try sustained boost bursts and ordinary flight. Note route time, blind turns, and collision spots. The optional target is just a marker; wait at C if simulating objective completion. No enemies, river, bridge, mission success or tactical map in this pass. This scene is not registered in campaign build settings.

## Working scale

Operation rectangle X -2430..2430, Z -1845..1845 (4860 x 3690). Flat terrain extends 600 units beyond each side. Geometry follows the actual V03 SVG map-local coordinates, using X=(u-540)*4.5 and Z=(410-v)*4.5. The two massif polygons are simplified by 2.3 SVG units before closed mesh construction. The flight-plane section is vertical up to Y94; sloped crowns are above it. This is steep graybox geometry, not finished rock art.

| POI | X | Z |
|---|---:|---:|
| S / runway spawn | -1912.5 | 580.5 |
| A / convoy start | -1363.5 | 1314 |
| B / depot | 1732.5 | 1156.5 |
| C / optional | 1998 | -1156.5 |

Approximate centerline distances: S-A 942, S-C 7280, C-B 3348 (including backtracking), A-B 3157. At nominal cruise 70, the first three are ~13, ~104, ~48 seconds respectively, before takeoff endpoint, turns, boost, and arrival radius corrections. At sandbox convoy speed 12, A-B is ~263 seconds: **mission convoy timing is not balanced yet**. After flight measurement, adjust convoy movement/timing or route as necessary; no timing assumptions are baked into objectives.

Offline topology check: 2 closed, valid polygonal meshes, 832 triangles total; every undirected geometric edge occurs exactly twice; centerlines avoid the rock footprints. The minimum S-C and exit centerline clearance at Y78 is about 197 and 194 units respectively, so narrowing some sections may be appropriate after testing the actual camera. Unity compilation, rendering, and flight are local validation steps.
