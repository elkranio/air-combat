# REF-01B3 — mission flight safety

Base `9ccad730ef43e9c3e4fc1d2ab0de39ab8a43cfb9` (REF-01B2 green).

`MissionFlightSafety` is a plain C# helper that owns the previous flight position and combines the existing `MissionBoundaryGuard.TryClipExit` and `TerrainFlightCollision.TryHit` queries. It returns Clear, TerrainImpact or BoundaryExit plus the resolved position. It updates the existing boundary warning only for clear flight. The mission still owns the boundary guard lifetime and exposes the same boundary to radar and tactical map. No change to either geometry algorithm, warning presentation, collision radius, operation rectangle, or projectile collision.

The launch sequence seeds the helper on every flight handover (including debug start and retry), checks the boundary at handover as before, and calls Check in the same LateUpdate before camera, weapons and landing capture. The rock sweep uses the clipped segment and retains the existing priority. Editor Debug No Clip still skips terrain checks, not the operation boundary. The controller retains crash damage/FX, failure pause, weapon/SAM shutdown, landing gating and retry. No new scene component or authoring menu. Existing serialized fields, GUIDs, scenes and prefabs remain unchanged.

## Local regression gate

- Compile and fly Mission01. Check ordinary flight, radar/Tab boundary, caution/critical distance and chimes.
- Hit a rock at normal and boost speed; check explosion, loss and R. Verify lethal SAM hit still uses the crash path.
- Cross the boundary without terrain: stop just inside, boundary failure (no explosion), pause; R restores flight and audio. Test crossing near a rock where practical.
- Debug No Clip on: pass rocks, but still fail at the boundary. Restore flag after testing.
- Tab near rocks and boundary, resume; retry from failure and check for no sweep from the old position. Check normal takeoff and landing capture/rollout.
- Unity compile and Play Mode remain the acceptance gate. No scene save is required.
