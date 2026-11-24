# AA-01b — scene-authored ground patrol route

## AA-01b2 — rounded moving turns (current)

Base `c3ffd79`. No scene migration or reinstallation. `Corner Radius` on the existing `GroundPatrolRoute` defaults to 9 world units. The authored points remain unchanged: straight legs are joined with tangent circular arcs, trimmed to at most 45% of either neighboring leg. The cyan gizmo and obstacle checker use the same rounded path as the motor. Radius zero restores hard corners. Inspect the arc clearance before driving around buildings; no automatic obstacle avoidance.

The motor advances by distance along each straight/arc and keeps its forward axis tangent, with terrain height and normal sampled at the actual position. It limits speed on a tight arc to the configured yaw rate. A normal 90-degree turn at the default 12 units/s and 9-unit radius does not stop. Exact reversals (including PingPong endpoints), unrounded sharp corners and degenerate point configurations may still use an in-place turn. Loop joins are rounded too. Once still stops at its last point. The editor start preview and runtime start use the selected corner's outgoing tangent point (or the exact waypoint when unrounded); a loop no longer starts outside the rounded path. Path geometry is rebuilt at `RestartPatrol()` / `SetRoute()`, not every runtime frame.

Play Mode gate: inspect the existing loop, verify continuous 90-degree turns, no lateral sliding, adjust radius, check route clearance, verify the closing corner, terrain alignment, group move/yaw, and Once/PingPong endpoints. No changes to turret, weapons or scene YAML.

## AA-01b1 — movable patrol group (previous)

Base `8b0057b`. In Edit Mode, rerun **Air Combat > Mission > AA-01b - Install Mantis patrol trial** to migrate the existing scene without recreating its points. The existing route becomes `Mantis / Patrol Group`; its vehicle wrapper (with motor and original visual prefab) becomes a child. World waypoints are preserved, including the existing route scale, which is baked into the point data. No scene YAML rewrite, duplicate model or route. Save the migrated scene with the green.

- Select `Mantis / Patrol Group` and use the normal Move or Rotate tool (yaw). The route and vehicle travel together. Drag waypoint handles to edit the shape. The vehicle previews its actual start point and terrain orientation, not the group pivot; the pivot can be in the middle of a loop. Start-point labels reflect the assigned child motor. Play uses the same start-pose computation and moves only the child; the route remains fixed.
- Change `Start Point` (zero-based) on the motor; `Snap to Start Point` and `Select patrol group` are available. Do not manually reset or move the visual child. Do not scale the group to resize a route: use waypoint handles or `Bake group scale into waypoints`, which preserves world points and restores unit scale.
- `Check route for obstacles` samples the path against non-Terrain colliders and reports the first potential blockage or off-terrain segment in Console. It is an approximate authoring check, not avoidance or pathfinding. Buildings without colliders are not detected; avoid mesh plateaus and inspect the whole route visually.
- Dedicated root routes and root units can also be grouped via `Group with assigned route` on any motor. Independent shared routes still work as before and are not automatically reparented. The visual installer recognizes the nested Mantis to avoid duplicates.

Play Mode gate: migrate, verify original route shape and size, move/yaw group, move start waypoint, change start index, check obstacles, save/reopen, Play and verify the route does not follow the driving vehicle. Undo migration and transforms. No changes to weapon logic.

Base `9c3d29c`. Mantis model/prefab and installed visual trial retained. First test of reusable mission authoring, not combat AI: independently authored waypoint route and terrain-following motor. No turret tracking, shooting, radar or damage in this atom. NURS and the optional air encounter are unchanged.

## Original AA-01b test (historical; installation now creates a group)

1. Open `ArcadeDesertE1` in Edit Mode. Use **Air Combat > Mission > AA-01b - Install Mantis patrol trial**. This creates a separate four-point loop near the existing AA-01a model, adds `GroundPatrolMotor` to its scene wrapper and selects the route. It does not regenerate or automatically save the scene. Repeating the command selects the already assigned route instead of overwriting it.
2. In Scene view (Gizmos on), the route is visible as a cyan line with directional arrows and waypoint rings. Select the route to see numbered draggable XZ handles. Move the points to clear terrain, avoiding buildings and separately modeled plateaus. `Frame route` focuses the view; `Add point at Scene view pivot` appends one; the Inspector list permits removal/reordering. Undo supported.
3. Save the scene. Play: the Mantis begins at the selected start waypoint (default first) and moves at 12 units/s, turns at 95 degrees/s, follows the Terrain height and normal. Corners use an in-place turn rather than side-sliding. Stop Play Mode to restore the authored transform. R resets the flight/air encounter only; the independent patrol has an explicit `RestartPatrol()` for future mission orchestration.
4. Edit `Traversal` to compare Loop, PingPong or Once; verify end-of-route behavior and slope alignment. Enable/disable `Show Route` for a clean Scene view. Gizmos are editor-only (may also show in the Game view if its Gizmos toggle is enabled).

## Reuse in missions

`Air Combat > Mission > Create Patrol Route` creates a generic route near the Scene view pivot. Any ground unit can carry a `GroundPatrolMotor` and reference the route with its own speed, turn rate and start waypoint. Mission scripts can call `SetRoute(route, startIndex)` or `RestartPatrol()`; route data has no knowledge of the unit or weapon. Patrol routes are explicit paths, not random roaming zones, trigger volumes or navigation. Future mission trigger/area tools should be separate. V0 follows the assigned Unity Terrain only; no obstacle avoidance, road finding, surface raycasts, convoy spacing or independent turret tracking. Keep routes off buildings and mesh plateaus for now.

Play Mode gate: edit/undo/save/reopen, route arrows and points match movement, loop/pingpong/once, slopes, no duplicated install or scene regeneration, NURS still fires. Commit saved scene with green. Next: independent turret tracking and twin-stream AA burst, then NURS damage/juice and convoy.
