# AA-01b — scene-authored ground patrol route

Base `9c3d29c`. Mantis model/prefab and installed visual trial retained. First test of reusable mission authoring, not combat AI: independently authored waypoint route and terrain-following motor. No turret tracking, shooting, radar or damage in this atom. NURS and the optional air encounter are unchanged.

## First test

1. Open `ArcadeDesertE1` in Edit Mode. Use **Air Combat > Mission > AA-01b - Install Mantis patrol trial**. This creates a separate four-point loop near the existing AA-01a model, adds `GroundPatrolMotor` to its scene wrapper and selects the route. It does not regenerate or automatically save the scene. Repeating the command selects the already assigned route instead of overwriting it.
2. In Scene view (Gizmos on), the route is visible as a cyan line with directional arrows and waypoint rings. Select the route to see numbered draggable XZ handles. Move the points to clear terrain, avoiding buildings and separately modeled plateaus. `Frame route` focuses the view; `Add point at Scene view pivot` appends one; the Inspector list permits removal/reordering. Undo supported.
3. Save the scene. Play: the Mantis begins at the selected start waypoint (default first) and moves at 12 units/s, turns at 95 degrees/s, follows the Terrain height and normal. Corners use an in-place turn rather than side-sliding. Stop Play Mode to restore the authored transform. R resets the flight/air encounter only; the independent patrol has an explicit `RestartPatrol()` for future mission orchestration.
4. Edit `Traversal` to compare Loop, PingPong or Once; verify end-of-route behavior and slope alignment. Enable/disable `Show Route` for a clean Scene view. Gizmos are editor-only (may also show in the Game view if its Gizmos toggle is enabled).

## Reuse in missions

`Air Combat > Mission > Create Patrol Route` creates a generic route near the Scene view pivot. Any ground unit can carry a `GroundPatrolMotor` and reference the route with its own speed, turn rate and start waypoint. Mission scripts can call `SetRoute(route, startIndex)` or `RestartPatrol()`; route data has no knowledge of the unit or weapon. Patrol routes are explicit paths, not random roaming zones, trigger volumes or navigation. Future mission trigger/area tools should be separate. V0 follows the assigned Unity Terrain only; no obstacle avoidance, road finding, surface raycasts, convoy spacing or independent turret tracking. Keep routes off buildings and mesh plateaus for now.

Play Mode gate: edit/undo/save/reopen, route arrows and points match movement, loop/pingpong/once, slopes, no duplicated install or scene regeneration, NURS still fires. Commit saved scene with green. Next: independent turret tracking and twin-stream AA burst, then NURS damage/juice and convoy.
