# REF-01C2 — Overlord flight context and effects ports

Base `d935c49d394c75f70c2985836cc0e1fc67255f38` (FIX-02 green).

`OverlordTarget` no longer refers to Mission01LaunchSequence, its phase enum, or Mission01ReconEncounter. It resolves `IAirTargetFlightContext` and the existing `IAirTargetEffects` at Start from its existing serialized component references. `Mission01LaunchSequence` implements the flight context; its phase predicate intentionally matches the old Phase.Flight check. Operation bounds remain live reads rather than a copied rectangle. The existing recon encounter already implements the FX port.

The serialized field names `launch`, `effects`, `player` and all tuning fields are retained. The first two fields are widened from concrete MonoBehaviour subclasses to MonoBehaviour, with runtime interface validation. Existing scene prefab overrides remain in place, but Unity deserialization must be verified locally. No scene, prefab or existing GUID changes. The editor authoring method accepts generic component references. No new scene component or authoring menu is needed.

Patrol, south-to-southeast escape policy, charge timing, boost integration, damage, falling wreck, smoke, glow, radar, debug-near-player and execution order remain unchanged. The same scene retry and map-pause helpers still govern update. The existing objective label stays in the unit for now; objective ownership and presentation are deferred to REF-01D/E. A different composition can supply its own flight context and effects component, but the current escape direction policy remains a unit behavior, not a configurable route asset.

## Local regression gate

- Compile. Inspect the scene Overlord instance: Launch and Effects references must remain assigned. Enter Play Mode with no missing/incompatible reference error, no prefab assertion and no runway fallback warning.
- Check initial patrol, radar/map contacts and the secondary objective label. Get within detection range and on-screen; verify charge, turn, boost and terminal ESCAPED status (not a kill).
- Damage and destroy it; verify smoke, fall, explosions and DESTROYED status. Confirm radar contact removal and that the primary objective does not depend on Overlord.
- R during preparation, boost and falling, and after terminal states. Tab freezes motion; R while the map is open does not reset. Debug Near Player retains its previous behavior. Restore debug settings before committing.
- Unity compile, serialized binding and Play Mode are the acceptance gate.
