# HUD-01 — shared flight/combat instrument

**Status:** reusable HUD prefab and aircraft radar contact support. Unity Play Mode verdict pending. Main-map Tiger is still static and unarmed; this does not start a combat encounter.

- **Source of truth:** `Assets/_Project/Prefabs/UI/CombatHUD.prefab` with `CombatHud`, `FlightHud`, `RadarHud` children. Mission supplies camera, `Afterburner`, `PlayerHitPoints`, `AircraftRadar`. No scene links are serialized on the prefab.
- **Status:** bottom-left, `HP current/max` (no HP bar) and one short afterburner bar with `BOOST %`. The camera viewport is no longer cropped. Health maximum currently defaults to 20; do not claim 10 HP.
- **Radar:** top-right. All mission-registered enemy aircraft appear. At <= `AircraftRadar.Range` (currently 460 horizontal world units) they are triangular, at correct relative position with their nose heading. Outside range they become bearing-only red edge blips (one per known aircraft; not a distance estimate). AA/SAM and incoming hostile missile behavior remains range-limited and unchanged. Use the actual oblique camera axes on main-map radar: on-screen right remains radar right.
- **Source:** `RadarAirContact` implements `IRadarAirContact`. Registration lives on the mission's player `AircraftRadar`, not in the HUD and not through frame-by-frame world scans. `IRadarContact` remains valid for existing ground and missile sources.

## One-time editor installation

1. Apply the patch, let Unity compile. Exit Play Mode, open the appropriate scene, save/discard other scene edits.
2. Open `ArcadeDesertE1.unity`; menu **Air Combat > HUD > HUD-01 - Install in Open Combat Scene**. Binds a new shared `CombatHUD` instance to `E1MapFlightSession`, and adds a radar contact to the **existing static D1 Tiger trial root**. Its inactive toggle also hides that contact. No changes to Tiger prefab, PlayerJet prefab, terrain or weapons. E1 adds runtime preview `PlayerHitPoints` (20/20, without hitbox) and `AircraftRadar` only while playing.
3. Optionally repeat the same menu on `WeaponLab.unity`; the fighter gets a scene `RadarAirContact`, and its runtime session binds the shared HUD. Enemy radar visibility follows existing `ITargetLockEligibility` while down/respawning. `GroundAttackLab.unity` already has CombatHUD and automatically benefits from the prefab update; do **not** install a duplicate.
4. Commit the two modified `.unity` scenes after testing, alongside this patch and all corresponding Unity `.meta` files. The menu refuses duplicate installation.

## Play Mode gate

- At Y78 on main map, compare player glyph/known static Tiger in the fixed −45° camera axes; rotate with A/D and check the player nose rotates exactly as it does on screen. Fly beyond 460 horizontal units from Tiger: triangle becomes red rim blip **without moving to an arbitrary north-up direction**. Return inside range: triangle and heading return. Disable D1 Tiger trial to hide its blip.
- Inspect bottom-left HP 20/20, BOOST bar drain/recharge; no bottom-wide black strip or camera resizing. Check HUD does not intercept LMB and fits the Game view.
- On WeaponLab, watch the fighter respawn (contact disappears while down), HUD stays present. On GroundAttackLab, regression-check AA threat rings and incoming SAM triangles. Radar tracks any number of explicitly registered aircraft.
- Unity compile/Play Mode still needs user verification; no change to BURST, enemy AI, damage rules or ground weapons in this atom.
