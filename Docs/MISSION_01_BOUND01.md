# BOUND-01 — visual boundary surround

Open `Mission01_FirstSortie` in Edit Mode and run **Air Combat > Art > Apply Mission01 Boundary Surround v0**. A separate flat ring extends 600 units past each edge of the original `Mission01 / Faceted surface` renderer. For the documented -1000..2200 X/Z rectangle, the resulting visual extent is -1600..2800 (4400 x 4400). The actual renderer bounds are used, not hard-coded coordinates. Save the scene and commit the generated `Mission01_BoundarySurround.asset` and all new `.meta` files. Re-apply updates the same generated mesh in place; **Remove Mission01 Boundary Surround** removes the scene object only.

The ring uses the existing ground material, matches its height, receives shadows and casts none. It contains 16 triangles and has no collider, radar obstacle, or gameplay component. The original renderer, mesh, geography, placements, and map asset are unchanged; MissionMapBaker continues to use the original renderer, not the surround. No rebake needed. With SAND-V0 the clamped edge colors continue outward (possible stretched color bands beyond the operation rectangle). Re-apply after changing the original ground material/UVs; unknown UV layouts are refused.

## Review

- Inspect all four edges and corners in Scene View and from the flight camera. No gaps, overlap/flicker, missing ground or shadow discontinuity at the original edge. Check boosted forward view and wide aspect ratios.
- Confirm tactical map still covers the original rectangle and markers line up. No changes to takeoff, terrain crashes, SAMs or Watchers.
- Re-apply once: one surround root only; remove/re-apply if needed. Save the scene and generated asset.

This atom does **not** stop flight outside the operation rectangle. BOUND-02 will enforce the original bounds with distance warnings and immediate mission failure at the limit; BOUND-03 will show the operation boundary on radar/map. The visual padding can be increased if the gameplay camera exposes the outer edge before failure. Unity import and Play Mode need local verification.
