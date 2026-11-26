# REF-01A — mission retry and pause ownership

Base `96ef826b8642e6f8e2a63c4080b7493acd36a461` (Airfield B prefab green). No scene, prefab, balance, visual, objective, or flight tuning changes.

- `Mission01LaunchSequence` remains the serialized mission owner and accepts R only when the tactical map is closed. It invokes the matching existing E1 flight session reset (if present), resets player/SAM/mission state, then publishes `MissionRetry` for the current frame. Recon and Overlord consume it in their original execution order; the camera consumes it in LateUpdate. Standalone sandbox/lab input stays as before. R while the map is open no longer makes the camera snap independently.
- `MissionPause` owns global timeScale/listener pause, with exclusive tactical-map or boundary-failure ownership and owner-only release. The tactical map still owns its canvas, Tab/Escape input and snapshot, not global pause state. Shared gameplay checks preserve the previous map-only gating; a failed sortie must still accept R. Existing scene-level references and execution-order attributes stay intact.
- Static state is reset at SubsystemRegistration for Play Mode with domain reload disabled. One lifecycle owner per scene; simultaneous paused scenes are not supported (Unity timeScale is global).
- Existing E1 reset is now invoked at mission order -40 instead of E1 order -80; its side effects occur before the mission resets the player. Recon -30, Overlord -20, camera 105 unchanged. No new authoring menu or scene save required.

## Local regression gate

1. Open Mission01_FirstSortie. Verify compile, no new missing references. Test normal W takeoff (Debug Air Start off) and debug air start (on), including R on the ground, during takeoff and in flight.
2. Open Tab in flight: no movement, fire, target progression or audible loop progression; R does not retry or independently snap the camera. Escape/Tab resumes. Tab+R opening ignores R; closing permits R, as before.
3. Destroy a Watcher, damage another, trigger Overlord preparation or escape, launch SAM missiles, then R. Confirm HP/fuel, all three Watchers, Overlord, SAMs, counters, smoke, projectiles, HUD/map and camera restore. Test repeated retries and Debug Near Player.
4. Cross the operation boundary: timeScale/audio pause, correct failure message, no map opening. R resumes and resets. Test terrain crash, lethal SAM hit, landing approach, autopilot, landed and R. Test Tab during landing.
5. Exit Play Mode with map open and after boundary failure; enter again. Check no stuck pause (including with domain reload disabled). Repeat in ArcadeDesertE1 and retained labs; R still resets their original compositions.

Unity compile and Play Mode are local acceptance gates. Do not commit until green. Later REF atoms will separate launch/landing, target behavior, objectives and presentation.
