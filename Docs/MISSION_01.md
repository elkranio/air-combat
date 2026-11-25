# MISSION-01 — First Sortie (WIP)

**Source of truth:** `Assets/_Project/Scenes/Mission01_FirstSortie.unity`. Open directly and press Play; never regenerate the saved mission. Current scene uses the accepted Lynx, compact airfield B and CL06G rock kit. This is a flight/terrain slice, not yet a complete mission.

## Current slice

- X/Z bounds -1000..2200, cruise Y78. See [rock layout v3](MISSION_01_ROCK_LAYOUT_V3.md) and [technical scheme](MISSION_01_ROCK_LAYOUT_V3.svg).
- Mission01 camera yaw is 0 degrees (pitch unchanged): world, radar and tactical map share horizontal axes without a diamond-shaped map. The flat faceted ground receives shadows but does not cast them, avoiding the self-shadowing stripes seen during the yaw test. Shared prefabs and lighting settings are unchanged.
- W starts takeoff, then WASD flight; LMB AC-01; RMB empty. R retries. Terrain warning, fatal crash/explosion, and projectile-terrain blocking are implemented. Decorative base rubble is nonblocking. The radar uses cached united solid sections.
- M01-AA2 enables Picket combat after takeoff. Map and radar use the same launcher range. Silent acquisition, three pre-launch beeps and a cancelable bearing cue; physical missiles use the existing terrain collision and radar contact. Recon and optional targets remain placeholders. Map edge enforcement remains necessary to close the southern bypass.
- Accepted geography: comfortable left-side route through three SAM coverage gaps, then optional high-risk east-side sortie, return through the north and back to the airfield. No mandatory route rails. Recon unit design and optional target are provisional.

## Next atoms

1. [MAP-01](MISSION_MAP.md): static scene-derived geography baker implemented; bake and review the generated asset in Unity.
2. [MAP-02](MISSION_MAP_02.md): Tab toggles the paused tactical map; player heading, planned SAM and target markers, Mission Data. Install and review in Unity before proceeding.
3. [M01-AA2](MISSION_01_AA2.md): re-run Install Tactical Map, save scene and generated SAM01_PicketMissile prefab + meta. Verify pre-launch cue, missile direction, pause, death/retry, and tune the Picket profile. Then mission boundaries, three recon air units and optional objective.
4. Mission flow: objective tracking, return, landing/result, retry. Add landmarks and environmental detail after flight paths are tested.

See [CL06 rock library](ART_CL_06.md), [terrain crash](MISSION_01_TERRAIN_CRASH.md), [radar technical pass](TERRAIN_RADAR_TECH_PASS.md) and [CLEANUP-03](CLEANUP_03.md). Earlier construction instructions are available in Git history, not live menus.
