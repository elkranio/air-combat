# MISSILE-03b — incoming missile readability

Base `fcb3b47`; 03a evasion mechanically works but the warning does not reveal direction and pale missiles blend into terrain. Keep stress-test timing, unlimited ammunition, missile-only default and all flight/damage parameters unchanged.

- Three visual warning pulses use the same period, pulse length and start time as the distress cue. Nearly simultaneous attacks share the currently playing sound and pulse clock, but each has its own marker. Mark the moving shooter with red corners / LAUNCH, or a directional chevron on the screen edge when offscreen. After the third pulse hold a dim cue until launch.
- On launch replace the shooter cue with a blinking AAM chevron tracking the actual offscreen missile; no extra marker when the projectile is visible. All active hostile missiles register as incoming radar contacts, including bearing-only off-range contacts. Existing radar missile glyph is reused; friendly missiles and legacy lab behavior are unchanged. Remove contacts on impact, expiry or cancel; killing a single shooter does not cancel its fired missiles.
- Hostile-only graphite hull via property block, wider grey layered smoke with separate bounded particle pools, orange motor glow. Keep real missile shadows. Player AAM visuals, shared prefab, movement and hit radius unchanged. No flashing aircraft materials, dynamic lights or new colliders.

## Play Mode gate

1. Fly without firing: watch visible and offscreen Tiger launches, warning pulse alignment, transition from shooter to moving projectile cue, disappearance at screen entry. Repeat at screen corners and during rapid turns/camera lead.
2. Watch simultaneous warnings from both sides and multiple airborne missiles. No sound overlap; every active threat has its own marker. Check radar for distinct incoming triangles, including at radar boundary.
3. Compare hostile smoke/body contrast against bright terrain and dark terrain. Player AAM appearance and tuning unchanged. Verify H counts and existing evasion difficulty remain the same.
4. R during warning and flight; verify no stale indicators, contacts, glow, smoke or sound. Kill one shooter while its missile is airborne; warning vanishes but fired missile continues. Defeat/victory clear stress-test threats. Regression: WeaponLab and GroundAttackLab.

Next: evasion tuning and normal enemy attack pacing; no global cap yet. Q/E reserved for per-slot loadout cycling.

## MISSILE-03b1 — hostile missile visual cleanup

Base `54b0d90`. 03b warning/radar behavior retained. Hostile missile only: cool medium-light grey hull, +12% instance visual scale (explicit hit radius unchanged), short two-tone motor glow moving with the nozzle, shorter and less opaque grey exhaust. Hostile smoke lifetime factor .52 vs .8 (35% reduction); friendly missile and damaged aircraft smoke unchanged. No guidance, timing, damage, radar or warning changes. Next: refine launch marker separately.

Play Mode: compare missile body against sand and dark terrain, inspect engine glow at different frame rates, confirm shortened trail and preserved shadows, R during a volley. Confirm player AAM and legacy labs still look unchanged.

## MISSILE-03b2 — pre-launch threat vector

Base `2a5949a`; 03b1 missile visuals retained. Replace all shooter brackets, captions, edge chevrons and in-flight screen indicators with one thin green line from the shooter (or its true intersection with the viewport edge) toward the player. A missile silhouette points toward the player at the end, with a gap before the hull. Three pulses use the existing distress audio clock, followed by a faint hold until launch. Independent vectors for simultaneous threats; no overlay after launch. Radar contacts remain registered throughout hostile missile flight. No changes to damage, guidance, warning duration, cadence, player weapons or smoke.

Play Mode: check offscreen launches from each edge and corners during camera lead and sharp turns, visible shooters, two simultaneous warnings, no line covering the player, disappearance immediately at launch, radar contacts still present, and clean restart/defeat. Evaluate whether the line is readable without clutter. Next: evasion tuning and normal attack pacing.
