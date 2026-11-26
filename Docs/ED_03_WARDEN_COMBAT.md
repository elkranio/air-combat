# ED03 — Warden combat trial

Base `8f34c06` (accepted ED01 visual/rotor/patrol). Open CombatSandbox and press Play; mode Warden is saved. The existing authored ED01 is the test target. LMB AC-01, RMB empty; H shows HP and fired/hit counts, R retries. Other sandbox modes retain their loadouts. Mission01 unchanged.

WardenCombat binds the existing yaw/pitch/muzzle pivots and reuses BallisticProjectile, AircraftTeam, PlayerHitPoints, radar and CombatHitFeedback. It does not own movement; WardenPatrolPreview remains the opt-in circular test route, not final convoy AI. The unit is inert until the encounter binds it. Shot colors/width are local to Warden and do not alter AC-01. The provisional sound reuses the existing cannon sample. All weapon settings and maxHp are editable on the prefab/scene instance. CombatSandbox mode IDs 0–2 are preserved; 3 is Warden.

## Local acceptance

- Watch both turret pivots and the alternating muzzle flashes; verify red-orange lines are visible over desert, but visibly thinner than player AC-01.
- Check hit counters/HP, movement out of fire range, pauses and imperfect aim; fly straight vs steer. No hits from decorative lines alone.
- Shoot down Warden: radar contact disappears, wreck/FX appear, no remaining Warden rounds; R restores patrol/health/turrets and clears effects. Test R during a burst and after player defeat. 7/8/9 should move Warden with the player altitude.
- Add a second ED01 instance to test multiple independent units; no fixed encounter count. Test legacy Dogfight, GroundAttack and Interception and confirm Mission01 is untouched.
- Confirm Unity compile/import and Console. Numbers are test defaults, not accepted balance. No convoy or Mission02 content in this atom.
