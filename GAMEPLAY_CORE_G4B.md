# G4b — NURS salvo / physical rockets

Built on the green G4a cabrage, **without changing its maneuver**. GroundAttackLab attaches a runtime `NursRocketRack` to the player. No scene, prefab, art asset, WeaponLab or Sandbox changes.

## Test

1. GroundAttackLab Play Mode: press **5**, click once. The same 2.5s climb fires **six physical NURS**, alternating left/right pylons, beginning at 0.55s and spaced 0.15s. The climb, 2.5s descent, input lock and 12s cooldown are still owned by `CabrageManeuver`.
2. Each rocket inherits the aircraft's horizontal speed plus **80 units/s** along the **actual pitch angle of the climb at release**, with independent random yaw (±5°) and pitch (±3°). After release it has no thrust/homing/steering: gravity **80 units/s²**, drag **0.015/s**, swept raycast collisions. Typical open-ground impacts are roughly **250–350 units ahead of the position at attack start**, before random spread and depending on heading/speed.
3. Impact anywhere on actual geometry (e.g. bunker roof, tank or ground) produces a small blast (**radius 6**) and damages nearby independent `GroundDepotPart` colliders by 1 HP. It is deliberately weaker than bombs (radius 13); NURS do not damage aircraft. Rockets keep flying after their launcher is interrupted.
4. Try **Q/E** before all six launches. Remaining launches are cancelled; already released rockets continue, while G4a executes its full break+height return and 20s cooldown. Try Q/E after all six shots and during descent: movement/cooldown behavior remains G4a.
5. The HUD shows cumulative `Fired`, `Impacts` and `PartHits`. **G4b has no increased camera look-ahead or impact-area marker**. Rockets may land outside the Game view; use Scene view and HUD counters to verify. Those presentation aids are G4c.
6. Regression: **4** still drops four bombs and shows only the bomb nadir marker; slots 1–3 unchanged; WeaponLab and Sandbox still have no slot 5.

`NursBallistics` owns initial kick and trajectory integration in one place so G4c can derive a coarse impact envelope from the same physics. G4b's transient rocket capsules, trails and explosions are diagnostic geometry, not finished art or VFX.
