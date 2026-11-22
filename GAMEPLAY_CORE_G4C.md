# G4c — NURS camera cue (marker removed after playtest)

GroundAttackLab-only runtime presentation. No scene or prefab changes.

## Current behavior

- **Select 5:** the camera adds a modest, smoothly blended look-ahead (24 units) on top of the existing flight follow/lead.
- **Start cabrage:** during the committed maneuver, extra lead increases to 70 units. Camera offset is constrained in viewport space to retain the aircraft inside a 20% screen margin. Includes sudden Q/E turns and descent.
- **Change weapon:** the extra NURS lead smoothly returns to zero; normal camera behavior remains intact. **R** resets the lead immediately.
- **No NURS ground impact-area marker, four-corner bracket, per-rocket target dots, or predicted landing area.** The earlier G4c bracket proved invisible until after launch at ~600-unit NURS range; removed along with its prediction code after green camera test.
- Slot **4** still displays the independent bomb nadir line and ring. The existing NURS salvo, real ballistic impacts, damage, maneuver, cooldowns, and Q/E emergency recovery are unchanged.

## Verification

1. In GroundAttackLab choose **5**, fly straight, turn around, then launch one cabrage. The aircraft must remain in frame during selection, climb, recovery and emergency Q/E.
2. No NURS bracket should appear at any phase or after firing. Rockets still impact at their previous range; monitor **Fired / Impacts / PartHits** in the diagnostic HUD.
3. Switch to **4** and **1/2/3**: normal framing returns and bomb nadir stays restricted to slot 4. Test **R** while the camera is transitioning.
4. WeaponLab/Sandbox behavior and shared camera prefab remain untouched.

**Future design idea, not implemented:** a proper radar could supply distant target information without moving a ground reticle into the flight view. No radar or new targeting aid is part of this change.
