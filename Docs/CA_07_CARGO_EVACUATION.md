# CA07 — Mission02 cargo evacuation

CA01–CA06 and BD05 accepted as good enough for the mission test; visual polish deferred. CA07 composes the accepted pickup, rotor, downwash, crash, cargo breakup and CA06 damage receivers at the real V06 C1/C2 pads. No changes to the accepted models, prefab assets, convoy routes, SAM tuning, weapons or the main campaign victory/RTB/debrief rules.

## Install and play

Open and save `Mission02_ConvoyInterdiction`, run **Air Combat > Missions > M02 - Install CA07 Cargo Evacuation**. It adds `Mission02CargoEvacuation` to the existing `Mission02RouteTrial`, binds both prefabs, the terrain and the two paths from V06 source JSON. It refuses a second install or mismatching cargo coordinates, and disables the earlier visual-only CG01 root if present. The real cargo/carriers are runtime actors, one pair per site. The airport CA01 preview is retained for CA06 but hidden during CA07; no scene regeneration. Commit the saved scene after testing.

Defaults: Test Site C1, Hold Test Aircraft ON. The player is placed at Y78 and aimed using the same nominal NURS geometry as CA06; AC01/NURS remain the normal weapons, with no lab aim. First dispatch starts 2 seconds after the test starts. Only the selected site's carrier is dispatched; both cargo exist. R applies Test Site C1/C2/Full Sortie and Inspector changes. Turn Hold Test Aircraft off for actual flight. Full Sortie uses the saved Start Airborne option or W takeoff; real convoy, escort and SAM timing continues. Existing Carryall Firing Range and Base Deployment Trial take priority over CA07. The installer turns both off, preserves other settings. Tab pauses through the existing mission clock. R clears projectiles, radar contacts, all runtime actors and FX, and recreates both pairs.

## Rules

- One CG01 on each pad; both destroyed = the **one** optional task succeeds. One escaped cargo = the optional task fails, even if the other is destroyed. These are diagnostic outcomes, not scoring/rewards or mission-end conditions.
- CA01 has 5 HP (Air), CG01 has 2 HP (Ground). Existing projectiles and hitbox geometry from CA06 determine damage. There is no artificial altitude cutoff or new weapon behavior. Cargo radar uses the existing zero-threat ground contact; CA01 uses the air contact.
- Cargo destroyed before attachment: the empty carrier aborts and returns by the same corridor if already dispatched. If not yet dispatched, the dispatch is cancelled. Grounded cargo is destroyed independently of an unloaded carrier, including after an empty crash.
- After attachment, CG01 destruction kills CA01 too; CA01 destruction kills the attached cargo and both play their accepted separate fall/breakup. Destroying empty CA01 does not count as cargo destruction or earn a reward.
- An unloaded kill with cargo still intact allows **one** reserve carrier (25 s after kill, after the old wreck's impact/hold; the same actor is reset, with cargo HP preserved). If both carriers are killed, the remaining cargo is still a valid ground target. No new dispatch after the optional task fails; an existing airborne carrier may finish its current flight. No new dispatch after that site's cargo is destroyed.
- Loaded cargo reaches the exterior end of its corridor: both actors disappear and the cargo is marked ESCAPED. Empty departure removes the carrier only. No end-of-map flight or disappearance polish yet.

## Paths and initial tuning

C1 uses the first five points of V06 `second` (pad to southwest side of the pocket entrance), C2 uses the first nine points of `exit` (pad to northwestern side of entrance). Inbound reverses these points, outgoing follows them forward. Carrier aligns over the pad before the unchanged descent/clamp/lift. The new opt-in `stopBeforeDeparture` flag leaves the old airport departure untouched and transfers mission departure to the corridor route. Cargo yaw follows the first corridor segment. Transit at Y78 (or terrain +12 at the pad), constant 60 units/s, turning presentation only; no full AI or physical flight avoidance.

Source-polyline check against V06 rock footprints, sampled at <=1 world-unit spacing: C1 length 920.44, minimum centerline clearance 120.15; C2 length 1505.73, clearance 81.58. These are authored route checks, not a dynamic collision test against later decorations. Inbound+outbound and local align/descent/clamp/lift nominal durations: about 44.5 s / 64.0 s. Full Sortie initial dispatch C1 23 s, C2 83 s from the existing sortie clock (from takeoff start or airborne spawn), giving approximate undisturbed escape at 67.5 s / 147 s. Speed, dispatch times, reserve delay and HP are Inspector tuning, not final balance. First CA01 appears at the corridor endpoint, not outside the whole map.

## Local gate

1. Test C1 with Hold Test Aircraft. Watch approach/align/descend/clamp/lift/depart; observe route with Scene view, and the real game camera. Verify cargo and both radar contacts and the status of the optional task.
2. R; kill ground cargo before dispatch, during descent, and during clamp/early lift using NURS. Confirm cancellation, abort, and linked destruction respectively. R; kill empty CA01 with AC01 and leave CG01 intact; after the reserve delay confirm one replacement and no duplicated cargo/contact. Kill the replacement empty, then destroy CG01 on the pad. Check AC01 on loaded departure.
3. Repeat C2 and observe turns in its entrance. R mid-fall, mid-pickup, during reserve delay, after escape; check no ghost actors or FX. Tab pauses. Full Sortie: both sites timed independently; escape one and ensure the other site sends no new transport, destroy both in a separate run, check optional outcome and existing convoy/SAM behavior. Toggle old test modes and R to verify isolation.

Unity compilation and in-game validation must run locally. End-of-corridor disappearance, turn quality, mission timing, collision with later placed scenery, objective/radio presentation and rewards remain subsequent work.
