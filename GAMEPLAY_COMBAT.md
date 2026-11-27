# Combat — current rules

Inspector/preset/scene values are authoritative; numbers are playtest defaults, not final balance.

## Flight

A/D steer, W boosts with reduced turn authority, S slows with increased turn authority (takes priority); W/S share fuel. FlightSession supplies speed/steering/response; AircraftMotor smooths speed and bank. Camera fixed world yaw and heading/boost lead. No player Q/E break turn, cabrage, old throttle or Space boost; editor-only Space fast travel where mission debug flight supports it. R retry, H diagnostics, 7/8/9 Sandbox altitude. Campaign normal cruise Y78.

## Weapons

| Composition | LMB | RMB |
| --- | --- | --- |
| M01 | AC01 | None |
| M02 | AC01 | NURS |
| Sandbox Dogfight / Interception / Interceptor | AC01 | AAM |
| Sandbox Ground Attack / Convoy | AC01 | NURS |
| Sandbox Warden | AC01 | None |
| M03 (planned) | AC01 | AAM |

AC01 nose-forward physical burst rounds, independent of cursor. AC02 continuous/heat remains optional, not equipped by default. AAM cursor designation, short lock/grace, guided projectile; two-shot magazine regenerates only after both spent. Finite steering/turn budget. NURS ground salvos, predicted footprint uses launch state; boost increases spread, tight turn improves precision, turning adds spread; each round samples pose. Ground targets/destruction active in M02. Legacy bombs, Gauss and cabrage retired.

## Sandbox modes (select before Play)

| ID | Mode | Purpose |
| --- | --- | --- |
| 0 | Dogfight | Two Tiger fighters, independent AI and AAM cooldowns, imperfect gun aim and bounded pressure |
| 1 | GroundAttack | Scene Mantis rocket AA with authored route, vertical launch and limited correction |
| 2 | Interception | Ten default Sparrow cruise targets, offset routes/timed spawning |
| 3 | Warden | ED01 patrols, independently aimed twin cannons alternating shots; no evasive AI |
| 4 | Convoy | Straight six mobile Pickets and optional two Wardens, NURS |
| 5 | Interceptor | Current IN02 single fast interceptor, two staggered AAM, vulnerable breakaway and escape |

Do not reorder enum IDs. [IN02 tuning and test](Docs/IN_02_INTERCEPTOR_PASS.md). Hostile AAM warnings/audio precede actual launch; kills cancel pending hostile shot but not its already launched AAM (SAM lifetime differs). Reset cancels owned live projectiles and cleans radar/FX.

## Campaign

M01: six live SAMs, three required Watchers, optional Overlord (destruction or escape), objective-gated assisted RTB/landing. [M01](Docs/MISSION_01.md).

M02: six Pickets march, branch into fixed slots, park/unfold/arm; two Wardens escort. AC01/NURS; base defenses distributed BD07. Two optional cargo targets both must be destroyed; either escape fails optional. Empty Carryall kills no reward; one reserve with intact cargo. See [M02](Docs/MISSION_02.md) for timing, debug modes and outcomes. M02 mission-end presentation still unfinished.

One aircraft, one sortie, no extra lives/checkpoints planned; optional tasks may compete with primary objectives. Terrain has explicit aircraft collision and projectile blocking, warning cues; operation boundary fails sortie. Aircraft collision/evasion penalties removed from campaign. M03 working design is [here](Docs/MISSION_03.md).

## Feedback

Hit flashes, sparks, smoke/fire, wreckage, camera shake, radar threats, selected-target marks and offscreen bearings communicate actual gameplay. Warning != hit. Tab pauses through shared campaign owner. M01/M02 share engine mixer; final sound/juice deferred. Future radio warnings replace exact player-facing evacuation timers, not diagnostic data.
