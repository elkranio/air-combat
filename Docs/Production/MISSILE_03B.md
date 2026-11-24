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

## MISSILE-03b3 — Short Vector comparison

Base `f55be1c`: full-length threat vector accepted for direction readability, visual treatment not accepted. First of three sequential presentation experiments (Short Vector, Threat Arc, Radar Sweep). Keep the existing audio clock and pre-launch-only warning lifecycle. A player-local dashed muted-green bearing is capped at 64 reference pixels (720p), with a 38 px hull gap, a 20 px contour missile glyph and 6 px inward pulse movement. Fit to the visible source/viewport, with independent cues for each pending shot. Inspector fields permit live length/gap/travel tuning. Radar, missile visuals, guidance, damage, cadence and player controls are unchanged. No in-flight screen markers.

Play Mode: compare legibility and clutter at 1080p/4K, with visible/offscreen shooters, opposite and simultaneous bearings, sharp turns, camera lead and restart. Evaluate this variant before implementing Threat Arc, then Radar Sweep.

## MISSILE-03b4 — Threat Arc comparison

Base `4c6bef1`: Short Vector accepted as a working option, not final visual direction. Default to a compact player-relative 44-degree arc on the incoming side, at 56 reference pixels (720p), with a 3 px stroke, dark contrast backing and small inward bearing notch. Existing three audio pulses contract the arc by 6 px and fade it; faint hold until launch. No text, missile glyph or in-flight overlay in arc mode. Each pending shot retains its own cue. The runtime HUD Inspector exposes Warning Style (Short Vector / Threat Arc) for immediate A/B comparison, plus arc radius, angular span and stroke. No changes to radar, sound, missile tuning, cadence or gameplay.

Play Mode: compare both styles on the same encounter, with simultaneous warnings, opposite bearings, camera motion and bright/dark terrain. Next experiment: Radar Sweep. All current presentation is provisional; establish a coherent game-wide visual language (world art, HUD, typography, effects, audio) in a dedicated art-direction pass after gameplay proof, rather than polishing individual placeholders as final assets.

## MISSILE-03b5 — Radar Sweep comparison

Base `609c3de`: both Short Vector and Threat Arc are usable; user currently prefers Short Vector. Add the third player-local pre-launch presentation, Radar Sweep, as the default for this comparison. A soft tapered green sector points toward the incoming side, with its narrow end 38 reference pixels from the player, a 62 px length and 28-degree outer spread at 720p. Each existing distress pulse sends a bright band inward through the sector; a dim sector remains until launch. No text, glyph, full-screen line or in-flight overlay. The generated mask is shared across warnings and released on teardown. The runtime HUD Inspector can select all three styles immediately and tune sweep length, angular spread and opacity. The two previous styles are unchanged. No changes to warning clock, radar, missile visuals, guidance, damage, attack schedules or player weapons.

Play Mode: compare all three styles on the same encounter, especially on sunlit terrain and simultaneous bearings. Check pulse direction, clipping, no overlap with player hull and no cues after launch/reset. Choose one working baseline before further evasion/pacing work; final UI styling is deferred to the unified art-direction pass.

## MISSILE-04 — combined air combat

Base `c750941`: Short Vector selected after all three comparisons; remove the two unused warning styles. Accepted evasion tuning stays unchanged. Both Tigers now use their existing AC-01 attack passes and unlimited AAM; missile-only remains an optional Inspector test mode, off by default. Each fighter rolls an independent 3–8 s cooldown at registration and after each launch. Once ready it waits for the existing acquisition geometry, then plays the unchanged warning before launch. No global cap or shared attack cooldown. Production player and hostile AAM no longer cast shadows; aircraft shadows, missile geometry, exhaust, radar, damage, guidance, player weapons and legacy lab settings unchanged.

Play Mode: fight both Tigers normally. Check independent gun bursts and missile warnings, simultaneous or staggered launches, Short Vector only, no missile ground shadows, radar tracking, restart and defeat cleanup. Assess combined pressure before changing balance. Final presentation remains deferred to the unified art-direction pass.
