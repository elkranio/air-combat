# REF-01B2 — reusable scripted landing

Base `42750757e98167a60482bd33fa24937382fb68c1` (REF-01B1 green).

Move the existing plain C# `Mission01Landing` helper to `Flight/ScriptedLanding.cs`, preserving its .meta GUID. It receives a resolved `LandingRunway` (world origin/stop, forward axis, physical width, threshold distance, cue elevation) and retains its corridor, capture, trajectory, rollout, navigation point, warning and disposal behavior. No mission-specific mesh name or scene search remains in the helper.

`Scenes/Mission01RunwayGeometry.cs` owns the original Mission01 pavement lookup, local anchors, alignment validation and fallback. Resolution still occurs at the same point in Awake. Missing, duplicate or incompatible pavement uses the same 24-unit fallback and warning. Hard-coded mesh dimensions remain deliberately in this adapter only; later missions can supply their own runway data. No new scene component or authoring menu. Only the runtime cue hierarchy name is generic; its visual presentation is unchanged. UI separation is deferred to REF-01E.

All Inspector fields, scenes/prefabs, existing GUIDs, update ordering, objective gating, pause and retry remain unchanged. Capture, approach, rollout and cue methods are unchanged.

## Local regression gate

- Compile and open Mission01. Check Console for the old pavement fallback warning; the accepted airfield should resolve as before.
- Debug Landing Near Airfield on: approach from the correct side. Verify runway width/center, ground diamond, one-second aligned capture, and capture reset on exiting the corridor.
- Check automatic descent, early nose-up, touchdown, rollout, final stop and heading. Tab during capture and landing; R during approach, landing and after stopping.
- Debug Landing Near Airfield off: landing and RTB arrow still require all three Watchers destroyed; Overlord is optional. Verify return arrow and SAM/projectile cleanup at capture.
- Restore test-only Inspector flags. No scene save required. Unity compile and Play Mode are the acceptance gate.
