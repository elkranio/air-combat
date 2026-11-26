# REF-01B1 — scripted takeoff extraction

Base `3cbc72d027910ce5d3b021f177a38aac0fabd1e5` (REF-01A green). Pure runtime extraction; no changes to scenes, prefabs, existing GUIDs, serialized fields, tuning, or execution-order attributes.

`Flight/ScriptedTakeoff.cs` is a plain C# helper, not a new Unity component. It owns the elapsed timer, analytic runway-roll/climb trajectory, aircraft root pose and visual pitch. It receives the existing launch pose and visual rest pose, and the current serialized settings/speed on every tick. The old Editor-only debug endpoint formula is moved without changing its arithmetic.

`Mission01LaunchSequence` retains W/R input, mission phase, flame presentation, pause gating, reset coordination and the pending handover flag. It ticks the helper in Update and enables the normal flight motor only in LateUpdate, as before. Retry resets the timer, restores the existing runway pose and retains the REF-01A signal ordering. Debug Air Start remains Editor-only and snaps the camera through the existing path. Landing, boundary, crash, HUD, AI, and weapon code are untouched.

## Local regression gate

- Compile. No authoring menu, new component, or scene rebuild is required.
- Debug Air Start off: W starts the usual roll/climb. Check timing, nozzle flames, visual pitch, cruise height/speed, camera, controls and HUD/AC-01 handover.
- Tab during roll and climb: position and timer freeze; resume completes normally. R during both phases returns to the runway; W starts a full fresh takeoff. Repeat after normal flight, failure and landing.
- Debug Air Start on: start and retry use the existing airborne endpoint, camera snap and fast-travel setting. Check R and Tab.
- Restore the pre-test Inspector debug flags; do not commit test-only scene changes. Unity compile and Play Mode are the acceptance gate.
