# REF-01E3 — shared air-target combat effects

Base `c7fb6139856c28bc3328d861e4ef96c1d57e5881` (E2 green).

Extract the existing Watcher/Overlord shared FX resource creation, particle setup, smoke/fire emission, hit/ground burst routing and reset/disposal to `Visual/AirTargetCombatEffects.cs`, a scene-local plain C# service implementing `IAirTargetEffects`. `Mission01ReconEncounter` retains its serialized interface bridge so existing Overlord overrides and Watcher initialization do not change. It creates the service at the former FX setup point, supplies the same camera and player transform, forwards emission, and resets the service in the existing -30 Update after player rounds are cleared. No new Update, no scene or prefab edits. Resource names are now generic; the service explicitly releases its owned FX child GameObjects if the encounter component is destroyed. The scene still has a separate player terrain-crash FX instance, intentionally unchanged. Particle settings, random draws, 350-unit ground-burst distance, and original feedback methods are preserved. Existing CS0618 warnings remain until CLEAN-04.

## Local regression gate

- Enter Mission01, check no new compile/runtime errors; Watchers and Overlord fly as before.
- Damage Watcher and Overlord: smoke, burning, hit bursts, wreck fall and ground burst look and sound as before. Test near and far ground impacts.
- Retry while smoke/wreck effects are active: trails and bursts clear; fresh targets can emit again.
- Tab pause/resume; restart after death or boundary failure. Player terrain crash FX unchanged.
- Leave/reenter Play Mode; no new missing-reference messages. Compile and Play Mode are required for acceptance.
