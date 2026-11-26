# REF-01F1 — Explicit combat sandbox mode

Base: `271a6e40c044196aae3287e360a60709e4954751`.

`ArcadeDesertE1` already contains three supported combat tests. Replace the
overlapping `groundNursTest` and `interceptionTest` flags with one Inspector
enum on `Air01Encounter`: Dogfight (0), GroundAttack (1), Interception (2).
The authored scene previously had both flags true, so migrate it to mode 2.
New components retain the previous ground-attack default. The mode is sampled
on Bind before Play; changing it mid-session is not supported.

No changes to flight, weapons, spawn settings, AI, visuals, scene object IDs,
GUIDs, or Mission01. The sandbox scene path is retained for the terrain editor.

## Local acceptance

- Interception: same Sparrow wave, missiles and reset as before.
- Dogfight: two Tigers, cannon and missiles, result and R reset.
- GroundAttack: NURS and Mantis; R reset; restore Interception if desired.
