# Air Combat — current handoff

Updated 2026-11-24. **HUD-01 preproduction authored; Unity scene installation / Play Mode verdict pending.** Next gameplay test after HUD green: fixed-forward BURST, then one Tiger and multiple fighters. See [HUD-01](Docs/Production/HUD_01.md).

## Goal and immediate order

Stylized, aggressive **arcade** air/ground combat: one fighter can outfight multiple enemies, destroy vehicles and wreck a base, supported later by heavy music, spectacular explosions and powerful sound. No milsim balance or photorealism; no transforming aircraft or loss of recognizable fighter silhouettes.

1. **HUD-01 (this patch):** reuse `CombatHUD.prefab` in sortie scenes. Compact HP `current/max`, short BOOST bar, full-size camera viewport. Main radar follows camera screen axes: all registered enemy aircraft show as rim bearing-blips beyond 460 units and directional triangles inside; AA/SAM/missiles preserve their existing detection rules. Scene installer and test checklist: [HUD-01](Docs/Production/HUD_01.md).
2. **NEXT GAMEPLAY ATOM — fixed-forward BURST.** Existing limited cursor aim is inherited from the former top-down camera. Test LMB rounds strictly along aircraft forward on the new production camera. Change no other weapons or AI until feel is evaluated.
3. **THEN — one Tiger, followed by multiple fighters.** Visually accept EF-01 Tiger before fitting it to existing `EnemyJet` mechanics. Prove aggressive, readable 1v1 and later 2+ arcade air combat; no milsim lethality.

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
