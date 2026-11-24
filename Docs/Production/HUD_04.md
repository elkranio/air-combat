# HUD-04 — forward framing and automatic air tracking

Base: `a7b0fa5`. FLIGHT-01 and GUN-03 accepted in Play Mode. HUD-04 is pending Play Mode verification.

- Production camera cruise lead 48 -> 57.6; boost extra 14 -> 16.8. Orthographic size 80, fixed yaw/pitch, smoothing and 20% aircraft safe margin unchanged. Vertical framing may clamp the requested lead.
- Production TargetMarkerHud samples the existing AircraftRadar detections, using actual world positions and the sensor's InRange flag. Only Aircraft contacts generate arrows, only while outside the camera viewport. No manual RMB selection, no on-screen brackets, no ground-defense or missile arrows. Out-of-range aircraft remain radar-only. Nearby coincident arrows get an inward offset for readability.
- Legacy lab camera/marker behavior is preserved. No changes to AI, weapons, flight, radar range or scene authoring.

## Play Mode gate

1. ArcadeDesertE1: turn in all directions, boost and use Q/E. Verify increased forward space and enough rear view, no clipping of player.
2. Both Tigers spawn outside radar range: no screen arrows yet. When each enters radar range, its arrow appears if offscreen. When onscreen, arrow disappears without a bracket. RMB does nothing.
3. Verify arrows for two similar bearings, kill either Tiger, range exit and R reset. Radar remains unchanged; no arrows for non-aircraft contacts.
4. Check WeaponLab and GroundAttackLab regressions. Next atom: independent enemy timing/separation.
