# BOUND-03 — Operation boundary on instruments

The launch sequence binds its existing runtime `MissionBoundaryGuard` to the combat HUD. The radar draws the absolute operation rectangle in warm orange, clipped to the local radar disc using its existing camera axes. Other combat labs do not bind this overlay.

The tactical map resolves the same guard in its scene on open. Orange = actual failure boundary; pale amber = beginning of the distance warning. Contours are clipped to the baked geography and rotate with it. Neither overlay uses the decorative surround bounds. No changes to failure, distances, audio, flight, scene, or map bake.

## Test

- In Mission01, approach an edge and a corner. Radar lines enter the disc from the correct direction, stay clipped, and align with the warning. Check that SAM circles and terrain remain readable.
- Open Tab: verify the orange operation perimeter and amber inset, with matching legend. Change the guard caution distance in Inspector and check the inset moves after restarting Play Mode.
- Close Tab and cross the line: existing failure/retry works unchanged. R restores the sortie. Confirm ArcadeDesertE1 and the legacy labs have no operation overlay.

No installer or rebake required. Visual and Play Mode verification pending.
