# FLIGHT-01 — rapid 180-degree Break Turn

Base `c98335c`. Pending Unity Play Mode green. AC-02 continuous cannon accepted;
this atom tests reducing time spent circling between attack opportunities.

- Main-map Kestrel only: Q turns left, E turns right. The real gameplay root
  rotates 180 degrees in 0.4s using the existing smooth yaw integration; the
  powered maneuver ends at 0.45s. Existing bank and engine flames remain.
- Activation costs 50% of tank capacity (1.25s of a 2.5s tank), once, up front.
  At least half a tank is required. A partial-cost turn can interrupt W boost
  if enough fuel remains; normal steering/boost resumes after the maneuver.
  No additional cooldown, fuel drain, teleport or invulnerability. Existing
  recharge delay/rate is unchanged. A new Q/E press is required for another turn.
- Scene overrides on the existing PlayerJet BreakTurn component expose
  `turnAngle`, `turnDuration`, `burstDuration`, `fuelCost01` in the Inspector.
  Shared prefab and legacy labs retain 90 degrees, 0.55s turn, 1s maneuver,
  full tank and original active-boost restriction. Cabrage emergency return
  in GroundAttackLab is unchanged. No Tiger AI or weapon changes.

## Play Mode gate

1. Open ArcadeDesertE1. At full charge press Q; check a real 180-degree heading
   change, approximately half fuel remaining, and immediate steering recovery.
   Repeat with E; both directions should reverse the heading without a teleport.
2. Press Q/E twice with full tank initially (second press after first ends).
   Below half charge, presses must not initiate a turn or spend fuel. Hold W
   with more than half remaining, press Q/E, verify the turn interrupts boost
   without a second fuel debit; normal boost resumes if W is still held.
3. Try AC-02 on both Tigers, compare time between firing opportunities. Watch
   camera framing during the fast heading change. Test R/boundary reset during
   turn, and normal A/D, W, radar, weapon switching and target marker.
4. Smoke test WeaponLab and GroundAttackLab: legacy full-tank 90-degree turn
   and cabrage emergency return; no weapon or AI changes.

Next separate atom: per-pilot timing/threshold variation and separation to
reduce synchronized attack passes and overlap.
