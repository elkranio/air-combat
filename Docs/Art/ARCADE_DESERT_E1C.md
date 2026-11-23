# E1c — scale reset: one real plateau, contrast jet, flight heights

**Status:** implementation prepared; visual verdict **pending Unity Play Mode**. Continue editing `Assets/_Project/Scenes/ArcadeDesertE1.unity` — **not a new map**. E1b's flat, muted `#B8A993` ground was approved; its 18 tiny-looking rock chunks, pale aircraft paint and the aircraft/ground-shadow scale relationship were rejected. Keep terrain color and the approved C3 50° fixed-world camera.

## Install once on the E1b green commit

1. Apply E1c patch. Wait for Unity to import `Models/MainPlateau_E1C.obj` and compile the new Editor script.
2. Stop Play Mode; save/discard unsaved changes. Run **Air Combat > Art > E1c - Install Big Plateau + Altitude Test**.
3. Play the **existing** `Assets/_Project/Scenes/ArcadeDesertE1.unity`. Commit its revised `.unity`, new `E1C/` material assets, their Unity `.meta` files and the E1c source model after a successful review.

**The installer refuses to overwrite its output.** Never rerun the original E1, E1b or E1c installers on this authored map. Old E1/E1b rock roots are disabled rather than destroyed for historical reference; only the new plateau should render. Shared PlayerJet, Kestrel chassis, combat scenes, weapons and `CombatLightingRig` prefab are untouched.

## Exact first pass

- **Terrain:** same 2×2 km editable Unity Terrain, completely quiet and matte, same E1b `#B8A993` ground layer; no new map, vegetation, noise, fog or fake shadow.
- **One plateau:** `Models/MainPlateau_E1C.obj`, editable in Blender. Roughly **430×275 m footprint and 30 m above ground**, low relative to its huge width. Irregular authored perimeter, broad approximately level upper surface, large single-piece faceted walls, no toy pebble scatter, no tiny rock chips, no procedural noise or repeating cliff ring bands. One matte grey-sandstone `#968A7A` material, real MeshCollider and URP shadow casting. Initial placement approximately **X110/Z135**, ahead and slightly right of spawn; can be repositioned directly in Hierarchy. The OBJ remains the editable model source; **do not scale up a 30m rock prefab** to make a plateau.
- **Jet paint:** scene-local darker blue-grey hull `#31536B`, deep blue `#19364B` upper, controlled orange `#E07940` panel accents. Replaces only the E1b instance's material references. Original fighter geometry **unchanged**; possible ~15–20% nose/fuselage elongation is a separate later visual test.
- **Flight:** initial **Y150** versus E1b Y78. In Play Mode **7 = Y78**, **8 = Y110**, **9 = Y150**. The current flight position is retained in X/Z, only height changes; the camera snaps vertically so a comparison does not involve a long camera glide. `R` returns to spawn at whichever trial altitude is currently selected. `A/D`, `W`, `Q/E` and fixed-world C3 behavior unchanged. No altitude switching in regular combat labs.
- **Shadows:** exactly the existing `CombatLightingRig`, no new shadow blobs or material hacks. At Y150 the jet's ground shadow may be distant, outside the 50° viewport, or limited by the accepted **230-unit** URP shadow distance. That is information for the scale evaluation, not a reason to fake the shadow in this atom. Focus on whether the plateau's own silhouette/shadow reads as large terrain.

## Visual gate

Fly along the approach toward the plateau, then turn along and over its edge. Compare 7/8/9 in the **same location**. Does the jet read as an actual aircraft above a landmass instead of a miniature among similarly sized props? Is the aircraft still legible against the muted earth? Is the terrain shadow visually plausible at each height? Does the large object read as part of the landscape under the accepted 50° camera? Judge using Game view at normal zoom while moving, not distant Scene view.

Do not tune gameplay ballistics, attach enemies, revise shared aircraft prefabs, relight, or build roads/canyon during this scale gate. **After approval:** consider a separate elongated-aircraft silhouette pass and add authored geological detail/road/base pieces only where they help the larger map's composition.
