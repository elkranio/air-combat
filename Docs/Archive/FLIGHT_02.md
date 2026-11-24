# FLIGHT-02 — variable maneuverability

Base `10808ba`. Test on `ArcadeDesertE1` only; pending Play Mode green. FLIGHT-01 improved the fight, but the fixed 180-degree maneuver commits the player to an angle. Test continuous steering instead.

| Mode | Speed target | Yaw rate (Kestrel) | Tank |
| --- | ---: | ---: | --- |
| Cruise | 70 | 125 deg/s | Recharge |
| W / boost | 140 | 75 deg/s (0.6x) | Drain |
| S / tight turn | 35 | 250 deg/s (2x) | Drain |

A/D steer freely in every mode. W and S share the existing 2.5-second tank, 5-second full recharge and existing recharge delay. S wins if both are held. At zero, a held request gives cruise behavior with no recharge until both keys are released; no fuel pulsing or second tank. Switching directly between W/S does not recharge or double-drain. Releasing both immediately restores normal yaw rate; speed approaches its new target smoothly. S does not trigger boost flames, audio or the extra boost camera lead. Q/E is ignored in this mode; the old BreakTurn implementation remains available.

Tuning lives on the existing `E1MapFlightSession` Inspector: tight speed 35, boost turn multiplier 0.6, tight turn multiplier 2, maneuver response 140 units/s^2. Existing boost acceleration 180 is retained. Disable the `Variable Maneuverability` checkbox before Play to compare FLIGHT-01. No scene regeneration, prefab changes, AI changes, weapon changes or HUD changes.

## Play Mode checklist

- Hold W + A/D: faster flight, wider turn, boost flames, one tank drain. Hold S + A/D: slower flight, sharper turn, no boost flames or extra boost camera lead. Release at arbitrary headings; no forced continuation.
- Try S alone, both keys together, alternate W/S without releasing, exhaust the tank and keep holding. Release both and confirm the existing delay/recharge; resume with partial charge.
- Fire AC-01/AC-02 while turning; wheel selection, radar arrows and camera framing remain intact. Q/E does nothing in the new mode. R and defeat/restart retain existing reset semantics.
- Disable the new mode before Play and confirm the previous 180-degree Q/E works. WeaponLab and GroundAttackLab keep their previous controls, including cabrage emergency break.
