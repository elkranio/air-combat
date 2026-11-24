# JUICE-02c — terrain crash explosion

Base `9ebb841`; JUICE-02b falling wrecks accepted. Production-only presentation atom.

- `CombatHitFeedback` emits the crash once, at `AircraftWreck.ImpactPoint`, only on confirmed `HitGround`. Reset and safety timeout never create explosions. Original airborne kill shake, fall trajectory, damage and encounter outcome are unchanged.
- `GroundCrashFeedback` owns five bounded shared world-space particle systems (96 fire, 192 smoke, 192 dust, 192 fragments, 128 sparks). A hot central pop, 12 fireball puffs, 18 rising smoke puffs, 20 radially spreading sand dust puffs, 22 gravity-driven dark fragments and 18 sparks. Smoke fades in up to 2.8 seconds; no lasting wreck or scorch mark. Uses existing materials and masks, no particles casting shadows, lights, physics bodies, gameplay damage or per-impact GameObjects.
- A separate 0.85s synthesized bass-heavy boom uses the existing audio source. Crash-only camera impulse defaults to 2.5 units / 0.45s, volume 0.8. Both smoothly attenuate by planar distance to the player: full within 45 units, zero at 240. Visual FX still emit at every real terrain impact. Existing stronger-impulse priority remains intact. Tune `crashScale`, `crashShake`, `crashShakeSeconds`, `crashFullDistance`, `crashMaxDistance`, `crashVolume` on `CombatHitFeedback`.
- R, map reset and owner disable clear particles, audio and shake; owner destruction frees the new systems and clip. No prefab, scene or lab edits. Ground blast is visual only; terrain slope may occlude some dust or fragments naturally.

## Play Mode gate

1. Kill Tiger over open sand and over a hill: explosion appears at the actual terrain contact, not the airborne death point. Check visible orange fireball, dark rising smoke, dust and scattered debris. All particles should fade within about 3s.
2. Check near vs distant crashes: stronger close boom/shake, none beyond the cutoff; airborne kill feedback remains as before. Try two crashes close together and player defeat. R during fall or after impact clears all effects without new blasts.
3. Confirm no additional damage, radar contacts, live weapons or behavior changes. Verify the Console and the two legacy labs. Next JUICE-03 wingtip trails, then missiles.
