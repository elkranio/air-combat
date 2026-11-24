# MISSILE-02 — player fire-and-forget AAM

Base `19ded7a`, MISSILE-01a accepted. Production-only module; legacy lab launchers retain their prefab settings and effects. No enemy weapon or flight balance changes.

- AAM is the third wheel slot. Acquire a visible hostile with the mouse for 0.25s; unchanged 0.5s partial / 1s full grace, including offscreen. No new locks on radar blips, no nose cone. A soft periodic locked beep continues through full grace and stops on release, loss, switch, death or focus loss. LMB fresh press with full lock launches once, then clears the lock. No shot on an incomplete lock.
- Two rounds; the magazine does not regenerate at one round. Only the second launch starts 20s rearm; both rounds restore together, including while another weapon is selected. HUD name shows ammo and the bar shows rearm progress. Both missiles may coexist and retain their independent targets when the lock changes or the weapon is switched.
- Existing Missile prefab and swept planar MissileProjectile are reused through a production instance configuration: 190 units/s, 220 degrees/s, 270-degree total yaw budget, 3s lifetime, 5-unit hit radius, 5 HP damage on accepted hit. Launch follows the aircraft nose from its missile mount, at flight altitude. No guaranteed hit; a bad launch can spend the finite yaw budget and fly straight. A dead target is not reacquired. Existing damage feedback and wreck pipeline handles the hit. No splash or flares yet.
- Body casts ordinary geometry shadow; legacy ribbon disabled on production instances. Two shared world-space soft smoke layers reuse the accepted damage trail masks and noise, with lighter/narrower exhaust and per-missile distance sampling. No particle shadows or new lights. R clears missiles, exhaust, lock, sounds and refills the magazine; defeat cancels active player missiles. No scene regeneration; one existing scene component receives the shared prefab reference.

## Play Mode gate

1. Select AAM, acquire in 0.25s, confirm full-lock beep and offscreen grace. Click early: no missile, no ammo spent. Fire on a full lock: body starts nose-forward, steers toward original target, smoke stays in world space, camera/aircraft unaffected. Hit a full-health Tiger: 5 HP, normal kill feedback and wreck.
2. Fire one, wait >20s: still 1/2. Fire second: 0/2, 20s countdown begins. Switch to a cannon during rearm; return after 20s: 2/2. Try launching twice while both are in flight. Holding LMB must not auto-launch. Check target switch and death do not redirect existing missiles.
3. Compare nose-on, side and rear launches; verify misses possible when budget/lifetime expires. R in flight and during rearm, defeat, scene exit: no orphan missile or trail. Inspect Console; regress WeaponLab and GroundAttackLab.

## Next design constraints

Enemy AAMs should be occasional but meaningful (roughly 1-2 incoming shots over a sufficiently long two-Tiger fight), with shared threat pacing for larger waves. High damage, clearly readable warnings, reliable skill-based evasion; avoid flare spam or effortless enemy counters. SAMs may launch beyond visual/radar site range, with incoming missile radar cue, audio and potentially flashing offscreen arrow. Those are separate atoms; no implementation here.
