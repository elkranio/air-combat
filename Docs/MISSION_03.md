# Mission03 — working design

Design discussion, not a built scene. Current implementation is [IN02 one-pass Sandbox](IN_02_INTERCEPTOR_PASS.md). No commitment to final counts/timing/map yet.

## Mission concept

Primary: intercept two groups of moving strike UAVs before they leave the operation area toward friendly targets (targets need not be on the map). First group initially unescorted; enemy airfield scrambles interceptors in response to the player's attack. They reach the second group while the player is intercepting it. Proposed optional task: destroy the two specific enemy interceptors. Once all UAVs are destroyed, player can RTB or risk staying to finish the optional task. No endless replacement of killed aircraft. Lynx AC01 + existing two-round regenerating AAM; no NURS/bombs. New air combat introduction should be approachable, not harder merely because M02 was intense.

Real enemy airfield on map: runway, infrastructure, parked aircraft, visible launches, return, landing, rearm and next sortie. Strong defensive AA discourages assault with air-only loadout. Do not add artificial invulnerability by default; decide ground attack edge cases during design. No base scene or flight logistics implemented yet. Geography candidate: open terrain with room for air combat; final biome/layout undecided.

## Interceptor archetype

Heavy fast long-dart silhouette, less maneuverable than Lynx; not a weaker Tiger dogfighter. V2 chosen shape, C graphite selected as next livery application. Attack cycle: parked -> takeoff -> approach -> two staggered AAM -> slowing breakaway turn -> fast RTB -> landing -> rearm -> next sortie. Main counterattack window is breakaway, but no invulnerability in other airborne phases. Player should be able to evade and then attack; the defense must not consume the entire window. Test early evasive maneuvers: avoid either trivially dodging all acquisition or unrealistically giving it fighter-like steering. A gun-strafe boom-and-zoom archetype is an alternative for another enemy, not the current IN02 implementation.

AAM should differ from Picket SAM by speed and limited turning rather than simply being stronger. Finite ammunition for enemy sortie and regenerating player ammo are intentional arcade asymmetry; no elaborate lore required. Rearm timing must not force players to circle an airfield for long after primary completion. Optional target destruction should actually stop repeat attacks.

## Development order

1. Accept IN02 one-pass behavior through local tests; tune acquisition, missile pressure, counterattack window. Finalize graphite livery and remove comparison artifacts after validation.
2. Prototype repeated sorties and two interceptors with readable timing; only then build functioning airfield and M03 geography/waves.
3. Tune UAV route/speed and scramble/rearm timings together. Test primary-only vs full optional route. Later use shared campaign result and tactical radio system.

Radio examples are working design only: warn of incoming enemy aircraft, UAV escape risk, and rearm/scramble changes early enough for meaningful decisions. No numeric player countdown by default; exact telemetry in debug.
