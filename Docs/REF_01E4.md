# REF-01E4 — cockpit warning audio

Base `34d11c1f5b3381296372642e11906c26bf103c42` (E3 green).

Extract boundary warning audio creation, tone generation, cadence and disposal into `BoundaryWarningAudio`. The boundary guard still owns warning level, distance, focus and clipping. It invokes the audio helper synchronously at the original Tick/Clear sites; no new Update or event timing.

Extract SAM source/clip creation, tone generation, playback and disposal into `SamWarningAudio`. The launcher still owns acquisition, warning start, pulse count and clock, and the three-pulse launch gate. Picket and legacy SAM1 timing remain unchanged. Preserve the original samples, volume, pitch, cadence, and source placement. SAM helper explicitly releases its owned AudioSource on component removal as well as the generated clip. Existing engine audio and terrain warning graphic/audio are already presentation components and are not modified. No scene, prefab, serialized field or tuning changes. The landing corridor world-space lines remain in ScriptedLanding; not part of this audio atom. Existing compile warnings are deferred to CLEAN-04.

## Local regression gate

- Compile and enter Mission01. Boundary caution/critical chimes retain their volume, pitch and cadence; move away and verify silence.
- Picket acquisition is silent until its three pre-launch beeps; cancel by leaving range and verify the cue stops. Re-enter, launch and reload. Test multiple SAMs and missiles.
- Tab, R, crash, boundary failure and landing: no stale warnings. Repeat a sortie and verify signals work again.
- If using legacy GroundAttackLab, verify SAM1 lock beeps and launch signal unchanged.
- Leave/reenter Play Mode. Unity compile and runtime checks are the acceptance gate.
