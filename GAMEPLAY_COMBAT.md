# Combat prototype — current gameplay rules

**Status:** mechanics are testable in the two retained lab scenes, **not** yet integrated into `ArcadeDesertE1`. Numbers are the existing serialized/default baseline, not final game balance. Inspector values on committed prefabs override C# initializers.

## Aircraft and controls

`WeaponLab` and `GroundAttackLab`: **A/D** steer, **W** boost, **Q/E** emergency 90-degree Break Turn (full tank required), **R** reset, **H** debug panel, **LMB** fire/commit. Cruise **70**, boost **140**, tank **2.5 seconds**. Ground attack slots 4–6 are lab-only; do not globally equip the player prefab.

## Weapons

| Slot | Weapon / role | Baseline |
| --- | --- | --- |
| 1 | BURST cannon — air | Four-shot bursts; bounded aim |
| 2 | GAUSS — air | High-damage ballistic shot with fixed heading |
| 3 | Guided A2A missile — air | Cursor lock/grace/reacquisition, separate `MissileProjectile` |
| 4 | BOMBS — ground | Four gravity releases at **0.25s** intervals, **3s** cooldown after last drop, splash **13** |
| 5 | CABRAGE + NURS — ground | **2.5s** climb, **2.5s** descent, six ballistic rockets; splash **6**; normal cooldown **12s**, **20s** after emergency Q/E |
| 6 | DIRECT NURS — ground | Six unguided forward/downward rockets at **0.15s**, **4s** cooldown, splash **8**, regular steering remains available |

Bomb ballistics: gravity **80**, drag **0.045**; boost worsens spread. Cabrage rocket speed **170** plus inherited plane motion, initial launch at **0.55s** of climb, gravity **80**, drag **0.015**, spread ±5° yaw/±3° pitch. Camera cue adds **24** lead when cabrage selected and **70** when committed, clamped to viewport. Direct NURS: **170** muzzle speed + aircraft speed, **10°** down, cruise spread **±0.7°/±0.5°**, boost spread multiplier **3×**, splash **8**. Successful Q/E or switching weapon cancels remaining shots, not launched rockets. No predictive NURS impact reticle, auto-aim or target marker. **Known issue:** direct NURS impact range feels too long; retune later, not during base art production.

## Ground threats and radar

| Threat | Existing data | Role |
| --- | --- | --- |
| AA1 cannon | Range **230**, yaw **85°/s**, pitch **55°/s**, 12 rounds at 18/s, 2.5s pause, speed 240, projectile gravity 12, HP 6 | Predictive ballistic bursts, red radar contact and danger ring |
| SAM1 vertical site | Range **350**, acquisition **1.4s**, reload **7s**, HP 6 | Fixed vertical launch, audible acquisition then three rapid launch beeps |
| SAM1 missile | Speed **150**, homing turn **140°/s**, cumulative homing budget **180°**, lifetime 6.5s, damage 5, hit radius 4.2 | Vertical rise → pitch-over → finite-budget homing; own missile class, not player A2A projectile |

Player HP in ground lab: **20**. `AircraftRadar` samples explicit `IRadarContact`s in **460** horizontal units. Radar renders threats and genuinely inbound hostile SAM missiles; player projectiles do not appear as hostile contacts. Aircraft target hits vs ground splash use distinct damage domains. SAM/AA projectiles already launched can remain active after the site is destroyed. No flares/decoys or large mission system yet.

## Quality and remaining work

Current shots, impacts and destruction are *functional proof of concept*, not final feel. Future integrated pass will add better hit feedback, punchy SFX, smoke, sparks, debris, aircraft breakup and realistic-looking but stylized falling wreckage; plan alongside FPS/performance budget. Cabrage should remain a distinct high-commitment weapon. Ground depots currently use provisional mesh-cutting destruction. Keep all current playable labs untouched while finding the base visual style.

Regression: WeaponLab fighter, cannon, gauss, lock missile, boost and evasions. GroundAttackLab slots 1–6, target damage, AA/SAM dodgeability, radar/HUD and resets. Verify play mode separately after architecture changes.
