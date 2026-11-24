# FLIGHT-03 — production WASD cleanup and tight-turn feedback

Base `1d9a171`; AIR-03 and FLIGHT-02 accepted. No flight or weapon balance changes. Production `E1MapFlightSession` always equips variable maneuverability and disables its legacy BreakTurn component. Shared Q/E implementation remains for WeaponLab and GroundAttackLab (including cabrage emergency abort). No scene regeneration or prefab edits.

- `Afterburner.IsManeuvering` exposes actual powered S state. `IsActive` remains thrust-only for the camera and other consumers. Fuel drain and recovery are unchanged.
- `AfterburnerVfx` reuses the existing ignition sound on entry to either powered mode, including direct W/S transitions. This is the existing one-shot ignition, not a new looping audio system. Tight-turn plumes use existing engine sockets, splay outward by 25 degrees, with 42% length, 65% width and 65% vertex alpha. The dimmer mesh is allocated once on first use. No extra thrust, new sockets or new material. Legacy break and normal W effects remain straight and unchanged.
- AC-01 BURST is now a viable baseline weapon; retain AC-02 for future loadouts. Do not change either gun or Tiger tuning. Full combat effects and sustained fuel-use audio can be revisited later.

## Unity playtest

1. E1: Q/E have no effect; WASD unchanged. Hold W: existing flames and ignition. Hold S: same ignition, shorter outward flames while fuel lasts; release or deplete tank and flames disappear. Switch directly W to S and back, check plume orientation and sound.
2. Hold S+A or S+D while firing, reset with R, test defeat and focus loss. Verify no stuck jets and no camera boost lead during S. Check both Tigers remain unchanged.
3. WeaponLab / GroundAttackLab: original boost and Q/E, including cabrage abort, still work. Next atom: discuss missiles.
