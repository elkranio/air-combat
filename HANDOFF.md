# Air Combat — current handoff

Updated 2026-11-24 for `master` at **`645081ba`**. **Next work: AIR-TO-AIR COMBAT.** This is a documentation-only handoff; no gameplay, prefab or scene changes are implied by this update.

## Goal and immediate order

Stylized, aggressive **arcade** air/ground combat: one fighter can outfight multiple enemies, destroy vehicles and wreck a base, supported later by heavy music, spectacular explosions and powerful sound. No milsim balance or photorealism; no transforming aircraft or loss of recognizable fighter silhouettes.

1. **FIRST CODE ATOM — player BURST/autocannon aim test.** Today the gun supports limited cursor-angle aiming inherited from the older top-down camera. Test **fixed forward firing along the aircraft heading on LMB**, no mouse steering/aim cone. The decisive criterion is feel and visibility **with the new production fixed-world camera**, not with the old laboratory view. Do not silently change GAUSS, missiles, NPC shooting, or all six slots. Exact wiring/extent to be agreed in the next atom.
2. **Then — one enemy EF-01 Tiger.** Replace the enemy fighter's visual chassis with the new Tiger model when visually accepted; reuse existing enemy mechanics rather than cloning a second AI. Develop one readable 1v1 dogfight on the current camera.
3. **Then — multiple enemies.** Prove that the arcade loop remains playable against 2+ fighters; tune AI/readability later, not milsim lethality. A **minimal radar** should show enemy contacts *when detected*, using/expanding existing radar systems; exact HUD/lock/detection rules remain to be specified before implementation.

Do **not** pre-plan more combat atoms here. User will set the next 2–3 tasks after the documentation patch is green.

## Current repository reality

- **Main scene** `Assets/_Project/Scenes/ArcadeDesertE1.unity` remains `E1MapFlightSession` **flight/art only**, with no live combat session. Persistent **2000×2000** Unity Terrain; quiet sand `#B8A993`, accepted ~**430×275×30 m** plateau, and production dark blue-grey Kestrel. **Y78** default; `7/8/9` compare Y78/Y110/Y150; `A/D` steer, `W` boost, `Q/E` Break Turn, `R` reset, `H` overlay.
- **Production camera** `Prefabs/Camera/ArcadeCameraRig.prefab`: fixed-world yaw **-45°**, pitch **50°**, orthographic **80**, heading look-ahead **48**, boost extra **14**, ~**0.42 s** smoothing, **20%** safe margin. It is **not** the legacy top-down/rotation camera used in combat labs.
- **Lighting**: one `CombatLightingRig`; main scene tests `Assets/_Project/Settings/ArcadeCombat_URP.asset` (**600** shadow distance / **4** cascades / **4096** atlas). In-game quality and GPU cost are **not final-approved**. Labs still use the old profile.
- **Base**: three B1b barracks, B1c HQ/courtyard, B1d access/service roads and wear, B1d3 **one sharp irregular prepared-lot mesh** (`B1 / Prepared lot - DISABLE to compare`), the B1e runway/apron/control tower/original hangar, two **parked Kestrels**, and a newer **B1f arched-hangar style trial**. Airfield and HQ area are separate zones; final deliberately curved service connections are deferred. See [base](Docs/Production/BASE_DESIGN.md) and [airfield](Docs/Production/AIRFIELD_B1E.md).
- **Enemy EF-01 Tiger D1**: latest commit includes `Resources/Blender/EF01_Tiger_D1.blend`, OBJ sections, matte URP materials, `EF01_Tiger_D1_Chassis.prefab` with `JetChassis` sockets, and an independently toggleable **static comparison** between parked Kestrels. **It has no enemy AI, weapons or flight movement yet.** Visual Game-view approval remains to be explicitly confirmed. See [Tiger D1](Docs/Production/TIGER_D1.md).

## Combat proofs (keep for regression)

- `WeaponLab` / `WeaponLabSession`: fighter pursuit, BURST (1), GAUSS (2), guided A2A missile (3), on its **legacy camera**. Existing BURST behavior still permits mouse-bounded aim; fixed-forward is **not implemented**.
- `GroundAttackLab` / `GroundAttackLabSession`: bombs (4), committed cabrage NURS (5), direct NURS (6), AA1, vertical-launch SAM1, `AircraftRadar` / `RadarHud`, HP/fuel and all six weapon slots. Cruise **70**, boost **140**, tank **2.5 s**; Q/E emergency turn. Radar working in a lab does **not** mean radar is currently on the production map.
- **Deferred debt**: direct NURS range feels too long, provisional `GroundDepotTarget` mesh-cut destruction, and functional-but-rough shot/impact/audio/particle presentation. No new general balance pass until core dogfight feel is proven.

## Rules and regression

- Reuse `PlayerJet`, `EnemyJet`, `Kestrel`, `EF01_Tiger_D1_Chassis`, `ArcadeCameraRig`, legacy `CombatCameraRig` (labs), `AA1Autocannon`, `SAM1Launcher`, `CombatHUD`, lighting and other existing scene-neutral prefabs. Do **not** casually add scene-specific weapons/AI to shared prefabs.
- Keep the main scene hand-authored; do not regenerate it or rerun accepted one-time installers. Preserve `.meta` GUIDs, source `.blend`/OBJ assets, existing material links and inspected serialized values. One approved atom → `.patch` → check/apply → Unity Play Mode → green push.
- Regression after combat changes: **main map** flight/camera/shadows; **WeaponLab** weapons 1–3, enemy and evasions; **GroundAttackLab** weapons 1–6, AA/SAM, radar/HUD and reset. Only retire labs when their behavior is explicitly proven on the combined map.
