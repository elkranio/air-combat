# AI-04a — powered gun pursuit

Base `d74ce33`. Combined combat playtest: Tiger AAM is dangerous but dodgeable and accepted, while AC-01 often fires only on accidental head-on passes. First improve physical gun pursuit; do not introduce attack intent or change missile cadence yet.

- Production Tiger motor opts into the player's relative maneuver rules: tight turn at half cruise speed / twice normal yaw, boost at authored boost speed / 60% normal yaw. Existing Tiger base speed, yaw, boost and finite fuel tank remain intact. Both modes consume the same fuel; no teleportation, forced heading or extra resource. Legacy motor path unchanged.
- Pilot uses tight turns at close range (200 units) for substantial heading error (40 degrees start / 20 stop), and boost to close an opening gap (around 100 units, aligned heading), in both Approach and Attack. Separate on/off thresholds and 40% / 8% fuel hysteresis prevent rapid mode switching and allow recharge. Keep wingman separation and map boundary behavior.
- Keep physical nose-forward AC-01 and existing lead aim, damage, cadence and range. Default firing tolerance 10 degrees instead of 6. A hard-to-align close approach becomes a pursuit instead of an immediate disengage. Crossing behind the nose no longer automatically ends Attack. The no-fire timeout is about 5.5 seconds (per-pilot variation); real shots refresh it and an existing firing solution or pending burst keeps the attack alive. Point-blank spacing and failed-pursuit exit still apply.
- H retains each live pilot's AC hits/fired and AAM statistics, now with CRUISE / BOOST / TIGHT mode. No change to player controls, enemy AAM cooldown (8–15 s), warning, damage, visuals or radar. No global missile cap or new weapon policy.

## Play Mode gate

1. Fly the production map without firing and watch whether Tigers spend more time maneuvering into gun range and emit more bursts. Test straight chase, hard turns, close crossing and opposite-side attacks. Check H mode and AC shots/hits; compare to previous green.
2. Fight normally: check that gun pressure complements the same missile pressure, that dodging and counterattacks are possible and that enemy speed/turn transitions remain readable. Look for overshoot, prolonged circles or synchronized pursuit. Fuel is finite and recharges, so no perpetual powered turn.
3. Check both Tigers remain separated, return from map boundaries, and restart/defeat/victory clear combat normally. Regress legacy WeaponLab and GroundAttackLab.

Next: AI-04b deliberate Gun/Missile Attack Intent, after evaluating this pursuit behavior. All settings are provisional for playtest.

## AI-04a2 — imperfect aim and bounded gun pressure

Base `b340950`: powered pursuit works but sustained accurate tail fire is too punishing. Preserve the Tiger motor, powered modes, wingman separation, player weapons, enemy AAM cooldown and guidance, and AC-01 projectile damage/cadence. During gun-range pursuit only, aim the physical nose at a delayed target sample (independent 150–250 ms reaction delay per pilot, extrapolate its old velocity) plus a random signed angular error up to 15 degrees. Hold the error for the entire burst, reroll on completion. Trigger within 5 degrees of that imperfect solution, not the true target; no projectile steering or artificial hit probability. Outside gun range use the existing pursuit aim. Each attack rolls a budget of 2–3 bursts; disengage after that budget or 4 seconds since first fire, allowing pending rounds to finish. The old failed-pursuit timeout and point-blank exit still apply. H adds B started/budget per Tiger.

Play Mode: check straight pursuit remains dangerous but visible volleys miss on either side; sharp turns should defeat stale aim. Verify 2–3 burst exits or time cap, unchanged powered maneuverability and missile attacks, and counterattack windows. Tune aim error, delay and burst budget in Air01Encounter attack settings. Next AI-04b Attack Intent only after accepting gun pressure.
