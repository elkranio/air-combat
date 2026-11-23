# AIR-02 — arcade attack passes

AIR-01 live Tiger and AIR-01a paint are GREEN at `6302df5`. AIR-02 is authored for the next Unity playtest; no new art, weapons or flight tuning.

## Pilot and encounter

- `ArcadeFighterPilot` owns Approach / Attack / Disengage. It supplies steering and boost intent to `CombatAircraftFlight` before its movement tick, and supplies trigger intent to the existing mounted `ITriggeredWeapon` after movement. The same AC-01 cadence, physical rounds, damage, hitboxes, team filtering and visual profile are reused. No forced hits or changes to player controls during a live fight.
- Starts from the existing random 550-unit ring. Approach aims slightly ahead of the player and may boost when far away; the actual tank is consumed and recharged. Attack begins within 170 units when facing roughly toward the target, requests fire within 125 units and a 6-degree aim window. A close approach which cannot line up within 3 seconds also exits rather than circling forever. The attack exits after a close pass, a crossed target or 3.2 seconds, even if no shot landed. Disengage holds a committed exit heading, briefly requests boost if available, then returns to Approach after 1.3–2.7 seconds (distance condition 150 units). Pending rounds finish normally unless an explicit stop/boundary recovery cancels the burst. Existing bounded map return overrides the pilot and suppresses fire. No threat avoidance or break-turn requests yet.
- Settings are editable on the existing `Air01Encounter` component (`Attack` foldout), not duplicated on weapons or in the motor. H exposes current phase, enemy HP, passes and accepted gun hits/shots. Normal combat HUD is unchanged.
- Both aircraft can take real damage. At zero enemy HP, stop its weapon, unregister its radar contact and remove the enemy/its pending projectiles, with a simple test result. At zero player HP, stop player flight/trigger and end the encounter; R restores controls and HP and starts a new random encounter. Existing player map-boundary reset is preserved. No timed respawn, no new kill effects.

## Play Mode gate

1. Open `ArcadeDesertE1` and Play. No installer or scene regeneration needed. R several times: one hostile radar contact, different approach bearings, same Tiger paint and AC-01.
2. Fly and turn without firing: Tiger closes, makes visible bursts, passes and comes back. H shows Approach/Attack/Disengage (and ReturnToMap near edges). Check that it does not orbit indefinitely or shoot backwards, and that player HP decreases only on projectile hits.
3. Fight back: check enemy HP decreases, self/friendly hits remain excluded, radar vanishes on kill. Lose a fight: HP 0 and flight/trigger stop. R should clear previous rounds and restore both combatants. Check repeated R during a burst, altitude comparison and boundary resets.
4. Verify existing labs still work. No tuning changes to the 70/140 flight baseline, 20 HP, 4-shot AC-01 or current effects. Evaluate encounter tempo and hit rate before AIR-03 (evasion); AIR-04 is hit/death juice.
