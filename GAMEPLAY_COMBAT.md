# Combat — current gameplay rules

Current accepted main-map mechanics as of CLEANUP-01. Numeric values are playtest defaults, not final balance. Serialized Inspector overrides are authoritative.

## Flight and controls

A/D steer; W increases speed and reduces turning authority; S slows and increases turning authority. W and S consume the same finite `Afterburner` resource; S takes precedence. The production flight camera is fixed-world and oblique; the aircraft fight in a common height plane. LMB and RMB control independently equipped slots. Q/E reserved for future per-slot selection. R resets the session; H shows diagnostic information. Player HP baseline 20.

## Production loadouts

| Slot | Weapon | Role |
| --- | --- | --- |
| LMB | AC-01 BURST | Nose-forward air-only gun; short bursts, independent of mouse aim |
| RMB (air test) | AAM | Cursor lock, fire-and-forget; 2 rounds, empty-magazine reload 20s |
| RMB (ground test) | Direct NURS | Physical unguided ground salvo with predicted dispersion footprint |

Player AAM baseline: speed 190, turn 220 degrees/s, cumulative turn budget 270 degrees, lifetime 3s, damage 5, hit radius 5. Lock acquisition 150ms; lock and grace cues belong to the AAM slot. AC-02 Continuous is retained but unequipped; future sustained-fire resource tradeoff to be tested. Gauss, bombs and earlier cabrage are not currently equipped on the main map.

NURS: 6 rockets, .08s between shots, 5s rearm; muzzle speed 260 plus inherited aircraft motion, 55-degree down angle, blast radius 8. Actual spread and the footprint share the same launch state. Boost multiplies spread by 2.5, tight-turn mode by .5, active turning adds up to 1.5; spread increases across the salvo and settles afterward. The footprint is projected onto visible world surfaces, with prediction based on bare terrain. Each shot samples the current pose, not the pose at the beginning of the salvo. No homing or auto-aim. Slowing for precision makes the player more exposed. Vehicle damage/destruction and polished NURS effects remain future work.

## Air threats

Two Tiger fighters in the air test; each has its own pilot state and missile cooldown. Cruise/boost/tight movement, fuel-aware pursuit, separation and imperfect aim with reaction delay and bounded gun pressure. Baseline fighter HP 5. Strong combined threat accepted; do not rebalance in a cleanup atom.

Enemy AAM: independent cooldown 8–15s, .8s warning, acquisition 90–300 units and 75-degree half-angle; speed 140, turn 95 degrees/s, total turn budget 120 degrees, lifetime 4s, damage 6, hit radius 5. Short Vector warning before launch and hostile missile radar contact; no persistent in-flight screen marker. Player and enemy missiles have no cast shadows. Air damage feedback includes hit sparks/flash, smoke/fire at low health and falling wreckage.

## Ground threat — mobile missile AA

`MantisGun` now represents only the accepted AA-01e2 cold-launch behavior; its historical gun name and component GUID remain for scene compatibility. One rocket per 2.5s cycle after detection within 210 horizontal units. Rise .48s, attitude turn .32s, ignition acceleration .22s. Rocket speed 240, range 310, physical hit damage 3. After ignition it may correct horizontally toward current player XZ at 25 degrees/s with a nonrenewing 20-degree total angular budget; zero in either field restores straight flight. No altitude tracking, predictive lead or splash damage. The rocket is silent pending the dedicated audio pass. This is an arcade convention, not a real-world specification. Tracers and Flak are retired from the main-map Mantis; older lab AA/SAM are unchanged.

The radar indicates ground defense and its threat radius. Patrol route/movement is independent of firing. The vehicle follows its authored terrain path, turns through rounded corners and can be reset by the encounter. The temporary launch canister remains until the dedicated art pass.

## Legacy regression labs

`WeaponLab` and `GroundAttackLab` retain their original controls, slot selection, camera, aircraft, GAUSS, bombs, cabrage/direct NURS and lab-only AA/SAM. Their numbers are not main-map defaults. Historical mechanics and tuning are in [the archive](Docs/Archive/README.md). Do not remove or silently port those components during cleanup.
