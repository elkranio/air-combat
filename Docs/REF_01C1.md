# REF-01C1 — autonomous Watcher

Base `30d7834f8c237c84b9ffdce3d35353ed60d5a42b` (REF-01B3 green).

WatcherTarget owns its Update, retry reset, patrol and wreck progression. The composition injects a flight-availability predicate, IAirTargetEffects and absolute patrol speed (still .23 times player cruise, clamped as before). The target no longer references Mission01ReconEncounter. The existing encounter implements the effects port; Overlord still uses the concrete encounter until C2. Radar registration, initial binding, objective counting, shared FX lifetime and projectile cleanup remain in the encounter for now. No prefab, scene, tuning or existing GUID changes.

The encounter at -30 clears shared effects on accepted retry. Watchers at -25 reset or tick before Overlord at -20. Order among Watchers is now Unity's component update order rather than the authored array order; exact shared random smoke sequence can differ. Disabling the encounter stops Watcher motion and damage via the supplied predicate. Target components now respect their own enabled state. The accepted retry can still reset initialized enabled Watchers when the flight gate is false. Standalone composition still needs to initialize the target and provide its effects/flight gate; no new authoring component in this atom.

## Local regression gate

- Compile. Open Mission01 and confirm all three Watchers patrol and appear on radar at their original locations/speed, including after W takeoff and debug start.
- Damage and destroy targets: HP, smoke, rotor, bank, falling wreck, air/ground explosions and immediate objective count are unchanged. Verify radar contact disappears on kill and return marker appears at 3/3.
- R after damage, while wrecks are falling and after kills: all Watchers and effects reset; check projectiles, radar and counters. Tab during patrol and wreck descent pauses correctly. Test R while map is open and after crash/boundary failure/landing.
- Confirm Overlord behavior and effects are unchanged. No scene saves required. Unity compile and Play Mode are the local acceptance gate.
