# Current combat gameplay — tuning and tests

**Authoritative for the playable prototype as of 2026-11-23.** Values below describe accepted scenes and prefabs plus the G5 direct-NURS code. For serialized components, **Inspector values in the prefab/scene override C# initializers**; inspect those before balancing. Per-atom build history is preserved under [Docs/Archive](Docs/Archive/README.md).

## Flight and controls

- `WeaponLab` and `GroundAttackLab`: constant cruise **70**, hold **W** for **140** afterburner (2.5s tank), **A/D** steer, **Q/E** 90-degree Break Turn (requires full charge, cooldown), **R** reset, **H** debug panel (starts hidden). **LMB** fires the equipped weapon. `Sandbox` still uses its different legacy control scheme.
- Normal flight allows steering and boost; cabrage deliberately locks them while committed. Direct NURS does not. Finite flight/camera margins are important to dodgeability.

## Weapon selection

| Slot | Weapon | Current role / behavior |
| --- | --- | --- |
| **1** | BURST autocannon | Air-to-air; four-shot bursts, bounded aim |
| **2** | GAUSS | Air-to-air high-damage projectile; fixed-heading shot |
| **3** | Guided MISSILE | Air-to-air cursor lock with reacquisition; separate `MissileProjectile` |
| **4** | BOMBS | Ground; four gravity bombs released every **0.25s**, **3s** cooldown after final release; splash **13**. Boost worsens spread. The nadir reference is height/ground-below only, **not** predicted impact |
| **5** | CABRAGE + NURS | Ground; **2.5s** climb, **2.5s** descent, six ballistic rockets, splash **6**; normal cooldown **12s** or **20s** after emergency Q/E, only after return. Pitch/camera changes but no impact marker |
| **6** | DIRECT NURS (G5) | Ground; six **unguided** forward/down rockets, **0.15s** spacing, **4s** cooldown, small splash **8**; no steering lock, target auto-aim or impact marker |

Only GroundAttackLab equips slots **4–6**. It configures ground racks at runtime; do not install these on the shared player prefab or add them to WeaponLab by accident.

### Ground attack ballistics

- **Bombs:** physical, gravity **80**, drag **0.045**, no turn correction. Releasing during manoeuvres naturally scatters the four bombs. Intended for difficult but powerful committed runs; damage/splash can be tuned later.
- **Cabrage rockets (slot 5):** initial launch speed **170** plus aircraft motion; first shot at **0.55s** of climb, then **0.15s** spacing; yaw/pitch scatter ±5°/±3°, ballistic gravity **80**, horizontal drag **0.015**, splash **6**. Q/E emergency manoeuvre cancels unreleased shots while already airborne rockets persist. Camera gives additional look-ahead **24** (selected) / **70** (committed), constrained to keep the aircraft visible; the obsolete impact bracket was removed.
- **Direct rockets (slot 6):** muzzle speed **170** plus inherited aircraft speed, fixed **10° downward** pitch, cruise yaw/pitch scatter **±0.7° / ±0.5°**, boost spread multiplier **3×**. Six real launch points sampled from the aircraft's moving heading and pylons. Steering stays enabled, creating an organic fan; successful Q/E or weapon reselection cancels only remaining releases. Radius **8**, one ground damage per detonation target. **Known problem:** impact distance is too long for the intended arcade engagement; preserve mechanic, tune distance later.
- Rockets/bombs collide with scene geometry and damage `CombatTargetDomain.Ground`; they do not turn into cursor-guided projectiles. No NURS predicted impact reticle.

## Enemy ground defenses

| Site | Current serialized values | Combat / radar behavior |
| --- | --- | --- |
| **AA1 Autocannon** | Range **230**; yaw **85°/s**, pitch **55°/s**, velocity-lead refresh **0.45s**, 12 rounds at **18/s**, **2.5s** burst pause, speed **240**, gravity **12**, spread **±1.75°**, HP **6** | Tracks and fires physical predictive rounds; dodges punish stale lead; red dot with genuine 230 danger ring |
| **SAM1 vertical launcher** | Range **350**, acquisition **1.4s**, reload **7s**, HP **6** | Fixed upward launch; beep during lock, **three quick launch beeps**, then silence during flight; red dot with 350 ring |
| **SAM1 inbound missile** | **Committed prefab:** speed **150**, homing turn **140°/s**, total homing turn budget **180°**, lifetime **6.5s**, damage **5**, hit radius **4.2** | Vertical **0.34s** -> free pitch-over (up to **0.75s**, **210°/s**) -> finite-budget homing; amber incoming-only radar glyph. Player A2A missile unaffected |

**Source of truth:** the manually tuned SAM prefab uses **150 / 140 / 180**. This cleanup aligns the C# field initializers with those values, without changing the committed prefab or its actual flight behavior. The missile is dangerous but still dodgeable without flares; retain that gameplay goal.

Player health in GroundAttackLab is **20 HP**. Destroying a ground site stops subsequent launches/shots, but already-fired projectiles may continue; **R** resets sites, player HP and active SAM encounter. Site prefab asset links must stay scene-neutral.

## Radar / HUD / audio

- Aircraft radar range **460 X/Z units** (not an all-world minimap). North-up; centre triangle rotates with aircraft. Top-right circular UI, red ground-defense contacts with *real* weapon radius rings, amber **hostile inbound missiles only**. Ground range rings and missile contacts are clipped/filtered by the sensor. No player rockets.
- Bottom shared CombatHUD bar displays fuel and 20-HP meter. Press **H** for diagnostics including SAM acquisition, flight phase, last launch result (`BLOCKED`, `HIT PLAYER`, `EXPIRED`) and weapon release counters.
- SAM acquisition warning tones are local synthesized beeps; at the moment of launch, exactly three fast beeps (0.15s apart), then **no continuous incoming-warning beeps**. No recorded voice alerts integrated.

## Next useful playtests

1. **Direct NURS:** straight 70 vs boost 140; reduce the excessive range without auto-aim, compare rocket footprint to AA1/SAM1 and moving targets when added. Check 6-shot count, mid-salvo steering, Q/E cancellation and splash overlap.
2. **Cabrage:** keep it as a high-commitment distant/area option; later try denser rockets and wider impact area, not a precision target-assist patch in disguise.
3. **Combined encounter:** a few AA1/SAM sites and eventual mobile defenses. Check the player can meaningfully suppress threats instead of being forced into a single perfect bombing line. Do not invent flare/chaff or mission logic yet.
4. **Regression:** slots 1–3 and fighter AI in WeaponLab; 4–6, blast targets, SAM/AA, radar/health, R/H, lighting and camera in GroundAttackLab; unchanged controls, lighting/shadow expectations in Sandbox.
