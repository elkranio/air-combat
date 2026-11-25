# BOUND-03A — operation breathing room

The warning contour is removed from the tactical map and its legend; the absolute boundary remains. Radar, warning distances (250/100), and failure logic are unchanged.

Mission01 operation: X -1100..2200, Z -1400..2550 (3300 × 3950). Original ground and all existing objects stay in place. The extra western space is deliberately small; the eastern edge stays unchanged. Ten accepted CL06 Bastion rocks extend the central massif south to keep the southern bypass closed. This is a first visual composition, not final rock art.

In Edit Mode, open Mission01_FirstSortie:
1. Air Combat > Art > Apply Mission01 Boundary Surround v0 (now 1000 units of visual padding around the original ground).
2. Air Combat > Missions > Apply Mission01 Boundary Expansion v0. The installer verifies visual reserve and an unbroken cruise-altitude terrain section before committing the new bounds.
3. Save scene. Air Combat > Missions > Bake Mission Map > Bake / refresh static map. The baker reads the authored operation rectangle for Mission01, retaining the map asset reference. Do not re-install Tactical Map.
4. Play: verify no warning at flight handover, no southern shortcut, Watcher approach space, warning and boundary failure/retry, and orange boundary alignment on radar/Tab. Review the new southern rocks at gameplay zoom.
5. Save/commit the scene and updated generated assets. No global gameplay or prefab tuning changes.
