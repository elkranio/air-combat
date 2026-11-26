# REF-01E2 — mission status presentation

Base `6a3a6587ffbf656998efc00c9aad5a1a96cfc93e` (E1 green).

Move the mission phase overlay and two screen warning renderers to `Mission01StatusHud`, attached once on mission Start. The status view reads existing phase, landing-test flag, boundary warning level/distance and landing approach visibility/alignment/hold progress. The original GUI text, fonts, positions, colors, pulse and progress formulas are preserved. The view gates on the mission being active and enabled and on tactical-map pause, as before. Boundary failure still renders even when timeScale is zero. No serialized fields, scenes, prefabs, combat tuning, input, flight math or reset ordering change. The audio cues and world-space landing corridor remain in their existing helpers for later presentation work. The mission still owns creation of the HUD and the combat HUD binding.

## Local regression gate

- Normal ground start: intro/ready/takeoff text, then flight; debug air start unchanged.
- Approach operation boundary: caution and critical warning text, distance, pulse and existing audio. Cross boundary: failure message and R restart.
- Enable debug landing test: approach text, alignment color and hold progress, ground corridor, autopilot and landing-test complete. Normal completed sortie shows mission complete.
- Terrain or missile death: aircraft lost and retry text. Tab hides the status overlay and closes cleanly. R clears stale warnings.
- Compile and Play Mode are acceptance gates. Existing CS0618 warnings deferred to CLEAN-04.
