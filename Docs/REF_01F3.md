# REF-01F3 — Sandbox finalization

Base: `11bfba25be65e078283316f13dac96eb6bcfa8c3` (F2 green).

- `ArcadeDesertE1.unity` -> `CombatSandbox.unity` (same scene GUID).
- `E1MapFlightSession` -> `FlightSession`, `Air01Encounter` -> `CombatSandboxEncounter` (same script GUIDs).
- Update only class identifiers in the sandbox, Mission01 and Terrain01Playground YAML. No FileID, prefab, transform, tuning or serialized reference changes.
- Update the mission controller, existing Terrain01 editor utility and build settings scene path. Existing build enablement is intentionally unchanged; finalize shipping scene selection separately.
- Art folder `ArcadeDesertE1` remains unchanged. Historical documentation is not rewritten in this atom.

## Local regression gate

Close Unity before applying to avoid intermediate imports. Reopen Unity, verify no missing scripts and open both working scenes. Check Mission01 takeoff / R / HUD; open CombatSandbox, select each of its three modes on CombatSandboxEncounter before Play, test R. Verify the scene path in Build Settings. Restore test mode and debug settings before committing. Review the diff with rename detection. Unity compilation / serialization and Play Mode are the local acceptance gate.
