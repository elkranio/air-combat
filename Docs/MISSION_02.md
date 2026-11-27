# Mission02 — Convoy Interdiction

Gameplay slice accepted for now (2026-11-27). Source of truth: `Assets/_Project/Scenes/Mission02_ConvoyInterdiction.unity`, `Mission02RouteTrial`, `Mission02CargoEvacuation`, `ConvoyMarchPreview`, `Mission02ConvoyEscort`, `CarryallEvacuationSite`. No scene regeneration or historical authoring menus required. Editor-only; final mission completion/landing/debrief/rewards remain deferred.

## Full sortie and debug modes

Saved scene: `Base Assault Trial` ON, `Base Deployment Trial` OFF, `Carryall Firing Range` OFF, `Start Airborne` OFF, `Picket Sam Enabled` and `Escort Enabled` ON. Cargo `Test Site = Full Sortie`, `Hold Test Aircraft` OFF. For normal sortie disable Base Assault Trial, R, W to take off. Lynx at Y78, AC01 + NURS. Tab pauses, R rebuilds encounter. No player AAM here.

Primary design: destroy six mobile Pickets (and clear supporting enemy combatants as applicable; final scoring/completion not implemented). Intercept convoy while mobile for an easier fight or pursue optional cargo first and face deployed defenses. Two Wardens independently patrol/escort. User completed the distributed base assault on first isolated attempt; retain current balance for now. Full-sortie replay has no checkpoint/lives; optional success must not be free.

| Trial | Use |
| --- | --- |
| Base Assault Trial (BD06) | All six Pickets deployed/armed, both Wardens live, no cargo actors. Lynx starts `(1640,78,790)` facing B, outside SAM range by a checked margin. R resets. |
| Compact Depot Defense | Optional A/B switch; OFF/default = current distributed BD07, ON = older compact arrangement. R applies. |
| Base Deployment Trial | Older close-start convoy animation test, optional Hold Aircraft; no escort. |
| Carryall Firing Range | Stationary CA06 weapon/pose range. Takes precedence over other trials. |
| Cargo Test Site C1/C2 | Isolated site test; dispatch after 2 s, optional Hold Test Aircraft. Full Sortie restores normal timing. |

Base Assault overrides base deployment, cargo test pose and Start Airborne. Range overrides both. Use ordinary sortie flags when testing combined timing. Airport preview retained for range/debug only, hidden during real cargo scenario.

## Convoy and base

Six Pickets, spacing 30, march speed 24. Vehicles keep their slot when losses occur. BD07 distributed defense: slots 1–2 near buildings as before; 3–4 branch 360 units before B (+180/-145 lateral), 5–6 branch 850 units before B (+190/-135). Park then unfold then arm. Outer pairs become active earlier, with less overlap than compact layout. Two Wardens keep independent acquisition delays and patrol the convoy/base. Existing building targets and destructible warehouses are retained.

Mobile Picket 2 HP, SAM template range 430, lock 2 s, reload 5 s, warning .65 s. Existing rail and missile physics; no shots during march, radar contact swaps on arm. Death cancels owned live SAM missiles. R clears actors, shots and effects. At BD06 start closest Picket is ~520 units away. Keep BD07; no additional move or weapon nerf unless later playtests justify it.

## C1/C2 optional evacuation

Both CG01 cargo destroyed = one optional task succeeds; either cargo escapes = failure. Empty Carryall kills do not count or earn rewards. CA01 hull 5 HP (air), CG01 2 HP (ground). Before attachment, cargo kill cancels dispatch or aborts carrier pickup; after attachment either destroyed actor kills both (separate fall/crash effects). An empty carrier kill with cargo intact permits one reserve, preserves cargo HP. No new dispatch after cargo destroyed or optional failure; already airborne carrier can finish. Loaded cargo reaching off-map endpoint marks escaped. A joking radio response for camping/killing two empty carriers is a future narrative idea, not implemented.

| Setting | C1 | C2 |
| --- | --- | --- |
| Dispatch (sortie clock) | 49 s | 124 s |
| Approx. arrival | 63 s | 140–141 s |
| Approx. loaded exit at endpoint | 90–91 s | 170–171 s |
| Pad X,Z | -2130,460 | 1510,-2200 |
| Off-map X,Z | -2850,613 | 1758,-3065 |

Carryall cruise 60, acceleration 20, reserve dispatch delay 60 s after unloaded kill (also waits for wreck clearance). Arrival/exit estimates, not measured deadlines; crossing operation boundary precedes reaching endpoint. Inbound brake, align, descend, clamp, lift, depart; reverse same path at Y78, no V06 high rim crossing. Accepted V07B organic canyon openings (654/676 width at boundary); retain both direct ingress/egress paths. Tactical map baked. Carryall V8 shape / V9A+ Heavy Duty paint accepted; no new visual pass now.

## Deferred

Campaign victory/RTB/landing and results, radio lines, exact final timing/difficulty, art polish. Do not present diagnostic cargo outcomes as finished campaign progression. Timer UX: exact digits debug-only, future event-driven tactical radio. Shared engine audio already installed in M02 (AUD02).

Historical art, depot, route, evacuation and audio atoms: [snapshot index](Archive/README.md).
