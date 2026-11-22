# G4a — NURS cabrage maneuver (implemented)

GroundAttackLab-only vertical maneuver, implemented in `CabrageManeuver`. G4b subsequently added physical NURS, and G4c added an aircraft-safe camera cue **without** an impact marker. The lab installs the maneuver at runtime. `WeaponLab` and Sandbox do not equip slot 5.

## Controls

- **5** equips NURS/Cabrage; **LMB press** commits to the maneuver if its cooldown is ready and Break Turn is not active. Merely holding LMB does not repeatedly trigger it.
- For **2.5s**, the player flies straight while the physical root ascends **22m** from its starting cruise altitude (48 → 70 in the current scene). The **visual** model pitches up to 33° and settles level at the top.
- For the next **2.5s**, it returns smoothly to cruise altitude; the visual pitches down slightly. **A/D, W boost, and weapon switching stay locked until the return finishes.** Existing horizontal forward movement and speed easing continue.
- **Q/E at any point**, including recovery, tries the existing 90° Break Turn. It still requires a **full afterburner charge**. If denied, cabrage continues without a change. If accepted, it interrupts the attack and turns while simultaneously descending from the **current** height. Controls remain locked until both the break and the return to cruise altitude are complete.
- Normal cooldown **12s**, emergency cooldown **20s**, each starts **after** returning to altitude. **R** cancels the maneuver, resets flight position and clears cooldown. **H** toggles diagnostics with phase, Y and remaining cooldown.
- While cabrage is committed, no other weapon can fire; the 5th slot is not a hidden autocannon. A bomb salvo already in progress prevents switching to slot 5.

## Test checklist

1. Start `Assets/_Project/Scenes/GroundAttackLab.unity`, press **5**, then click. Verify altitude reaches ~70 and returns to ~48, with normal steering and afterburner inhibited for ~5s.
2. Press **Q** near the start, **E** near the summit, and **Q/E** during descent (separate attempts with full fuel). Confirm the real 90° turn, smooth height return, continued steering lock, and 20s cooldown.
3. Try emergency Q/E with a partially empty afterburner: should fail without cancelling cabrage. Test normal 12s cooldown, including not re-triggering from a continuously held mouse button.
4. Reset (**R**) mid-climb, during emergency return and during cooldown. Verify Y, selection and cooldown return to normal.
5. Regress bombs on slot **4**, other weapons **1–3**, the bomb nadir marker only on **4**, and WeaponLab/Sandbox without slot 5.

**Current links:** `NursRocketRack` observes the climb and launches six physical rockets. `NursCameraCue` adds a bounded forward lead; no approximate impact region or marker exists.
