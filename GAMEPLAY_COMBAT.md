# Combat — current gameplay rules

Current baseline after CLEAN-04. Numbers are playtest defaults, not final balance; the actual preset, prefab and scene Inspector values are authoritative.

## Flight

A/D steer. W boosts, reducing turn authority; S slows the aircraft and increases turn authority. Both draw from one fuel resource, S takes priority. `FlightSession` supplies tight speed, steering multipliers and response; `AircraftMotor` smooths speed and bank. The fixed-world oblique camera leads along the aircraft heading/boost. No player Q/E break turn, cabrage, old throttle or Space boost. Editor-only Space fast travel is available through mission debug flight. R retries, H toggles flight diagnostics.

## Weapons

| Composition | LMB | RMB |
| --- | --- | --- |
| Mission01 | AC-01 burst | Empty |
| Sandbox Dogfight | AC-01 | AAM |
| Sandbox Ground Attack | AC-01 | NURS |
| Sandbox Interception | AC-01 | AAM |

AC-01 fires nose-forward physical rounds independently of mouse aim. AC-02 continuous/heat resource remains implemented as an optional preset, not a default loadout. AAM uses cursor acquisition and a short lock/grace, fires a guided projectile, and regenerates its two-round magazine only after both shots are spent. The weapon owns its lock/audio state; the old camera TargetLock was removed.

NURS fires physical unguided ground salvos. The predicted world-space footprint uses the same launch state as the rockets. Boost increases spread, tight turn improves precision, turning adds spread; each round samples the current pose. Ground destructibility and impact presentation remain development work. The old bombs, Gauss and cabrage implementations were retired; do not confuse their historical tuning with current mechanics.

## Sandbox threats

- Dogfight: two Tiger fighters with individual pilot states and independent AAM cycles. Fuel-aware pursuit, separation, imperfect gun aim, bounded pressure and missile warning. The Tiger prefab owns attack settings. Test against both, not only a stationary target.
- Ground Attack: Mantis mobile rocket AA follows a scene-authored route and uses the authored launch socket. Cold launch, attitude correction and smooth ignition; limited horizontal correction rather than fully guided air-to-air behavior. The rocket carrier and motor are independent. The old Mantis gun visual is retained as authoring content, not the live combat version.
- Interception: ten Sparrow targets by default, offset routes and timed spawning. Cannon or AAM can intercept them. Independent regression scenario, not a campaign objective.

## Campaign threats and objectives

Six Picket SAM sites, armed in flight. Radar/threat cues and three short prelaunch warning beeps precede an actual missile launch; projectiles and terrain collision determine hits. Watchers are required airborne recon targets. Overlord is optional: detection or damage initiates preparation and an escape attempt; destruction and escape are distinct terminal states. Three destroyed Watchers gate return/landing, irrespective of Overlord outcome.

Terrain has explicit aircraft collision and projectile blocking, with proximity cues; crossing the operation boundary fails the sortie. Aircraft collision/evasion penalties were removed from the campaign. See [Mission01](Docs/MISSION_01.md) for the mission-specific flow and test gates.

## Feedback

Hit flashes/sparks, smoke/fire, wreckage, camera shake, radar threats, selected-target marks and offscreen bearings are gameplay feedback. Warning is not a physical hit. Tab pauses the campaign through the shared pause owner. Reset clears owned projectiles and transient effects. Engine loops and warning sounds are provisional; a final audio/juice pass is deferred.
