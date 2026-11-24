# GUN-03 — AC-02 continuous cannon (playtest)

Base: `6ef7e67` (AIR-02b pair encounter). Not GREEN until Unity playtest.

- AC-02 is equipped only on the main map Kestrel, alongside unchanged AC-01.
  Mouse wheel selects either; LMB fires nose-forward. Tiger and both labs retain their guns.
- Continuous fire is immediate, stops on release, and uses 56 rounds of charge at 14/s
  (~4 seconds). No queued burst. Partial charge replenishes immediately while idle;
  full recovery takes 2.5s from zero at a constant rate. Switching weapons also recovers.
- Running empty forces a 1s dead period, then a complete 2.5s refill before firing again.
  Releasing, switching or repeatedly clicking cannot bypass the lockout. Held fire resumes
  automatically at full recovery. HUD shows actual charge and a HOT suffix during lockout.
- AC-02 duplicates the accepted AC-01 physical round/audio/VFX profile. All new tuning is on
  `Assets/_Project/Prefabs/Weapons/AC02_Continuous.asset`. No AI or enemy HP changes.
- R / boundary encounter restart restores AC-02 charge and clears rounds as before.

## Play Mode gate

1. Open ArcadeDesertE1. Wheel selects AC-01 and AC-02; both use the current HUD.
2. Hold AC-02: uninterrupted shots, charge falls. Release at half: shots stop and charge
   rises immediately. Repress before full: fire resumes without delay beyond shot cadence.
3. Hold to empty: HOT, one second with no refill, then charge rises. Hold through recovery:
   fire resumes only at full. Repeat while releasing and switching weapons mid-lockout.
4. Switch to AC-01 during AC-02 firing; no stray AC-02 rounds are emitted, existing rounds
   finish, and charge recovers while unselected. AC-01 behavior remains unchanged.
5. R while HOT and during normal fire: no old rounds, AC-02 full, two Tigers with 5 HP each.
   Confirm damage, marker, radar, victory/defeat, and legacy labs.

Next separate AI atom: per-pilot timing/threshold variation and separation to reduce synchronized
turns and overlap. Do not conflate that tuning with this weapon comparison.
