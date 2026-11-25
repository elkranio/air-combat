# BOUND-02 — Operation boundary v0

Base `364c708f`. No scene installer or rebake required. Open Mission01_FirstSortie and play. The existing visual surround (BOUND-01) remains independent of the operation rectangle.

The launch sequence owns a serialized XZ rectangle (default -1000..2200 on both axes, the accepted tactical map extent), caution 250 and critical 100 units. Changing operation bounds requires a corresponding map/geography review; changing the visual surround does not change the rectangle. All distances are mission-space units (displayed as metres). Warnings depend on nearest edge distance only, not heading, speed or time spent in the zone. A quiet low tone repeats faster in the critical zone. Return to the interior to clear the warning; no timer.

After flight movement, clip the swept segment at the first crossed operation edge, then test terrain on the reachable segment. If the boundary is reached, stop inside it, disable flight/weapon/boost and SAM launches, set BoundaryFailed and freeze scaled simulation/audio. No forced turn, HP penalty, crash or explosion. Retry R restores the previous time/audio state and uses the existing encounter reset, including debug air start. Tab remains available during warning, not during terminal failure. Debug no-clip only bypasses rock collision, never the operation boundary. Exit Play Mode releases owned pause. Tactical map/radar boundary graphics are a separate BOUND-03 atom.

## Local test
- At each edge, verify caution at 250 and critical at 100 even when moving parallel or turning inward. Check a corner and a high-speed crossing with debug Space.
- Turn back before the line: warning clears, no loss or steering change. Cross line: immediate separate mission failure, camera sees surround, no explosion, no motion on subsequent frames.
- R after failure: normal reset, movement/audio restored, Watchers and SAMs reset. Repeat twice. Try Tab while in warning and after failure. Stop Play Mode while failed and re-enter.
- Verify normal takeoff and terrain crash/retry are unchanged. Check warning UI at usual resolution, volume against terrain and SAM cues. Thresholds and audio are initial trial values.
