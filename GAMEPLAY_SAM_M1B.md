# SAM1b — vertical launch / reliable range-only lock

Base: M1 install exists on `master`; SAM and missile prefabs and GroundAttackLab scene are already authored. **Do not rerun the SAM1 install.** This change preserves existing prefab GUIDs and the scene's launcher position/link.

## Apply and upgrade the model (once)

1. Apply `air-combat-sam1b-vertical.patch` on the green baseline with Unity closed.
2. Open Unity in Edit Mode; run **Air Combat > SAM1b > Upgrade Existing Launcher to Vertical** once. It edits `Prefabs/Enemies/SAM1Launcher.prefab` **in place**, replacing the old rotating assembly with a fixed 2×2 vertical silo bank. It retains the root prefab, scene instance, scripts, existing materials and the separate `SAM1Missile.prefab`. A repeated call safely does nothing.
3. Save/commit the upgraded `.prefab` with the patch. No scene installer or manual scene reference repair required. The editor-built primitive low-poly model uses existing URP materials and real shadows; no texture/FBX bake is performed at this prototype stage.

## Behaviour

- The SAM **never mechanically rotates**. Once the aircraft enters its 350-unit X/Z engagement range, it gains lock over **1.4 uninterrupted seconds**, regardless of relative angle. Slow/fast beep during lock; urgent 0.18s beep with an active incoming missile. At range exit or when destroyed, lock progress clears. **7-second reload** remains.
- Projectile is a dedicated `SurfaceToAirMissile`, not the player's `MissileProjectile`. Physical phases: **VERTICAL** for 0.34s (rocket flies straight up) → **PITCH-OVER** at 210°/s, max 0.75s (turns toward current player position) → **HOMING** at 72°/s with **145° total pursuit turn budget**. Both initial phases spend **zero** pursuit budget. No indefinitely free tracking: pitch-over has a hard time limit.
- Speed 112, lifetime 6.5s, hit radius 4.2, damage 5 unchanged. Terrain collision and relative-space jet hit test unchanged; no decoys/flares. Incoming missile appears on R1 radar; ordinary shells and player missiles do not.
- `H` now exposes launcher status (`OUT OF RANGE`, `ACQUIRING`, `LAUNCHED / VERTICAL`, `LAUNCHED / PITCH-OVER`, `LAUNCHED / HOMING`, `RELOADING`, `DESTROYED`), lock percent, remaining reload and launch count. This diagnoses launch failures without on-screen combat warnings.

## Playtest

1. Start **GroundAttackLab** and approach the SAM from any side. Within 350 world units hear ~1.4s of beeps, then observe an **actual vertical launch**. Unlike M1, turret alignment and aircraft motion cannot reset lock. With H open, verify states and launch count.
2. Observe rocket ascend vertically for ~0.34s, pitch toward aircraft and pursue. On radar, see only the inbound SAM glyph, not any player-fired rockets.
3. Try straight flight, W and Q/E. Verify first two stages do **not** spend homing budget; after the pitch-over, sharp maneuvers eventually exhaust the 145° budget. Current speed/range/hit damage remain as in M1.
4. Verify 7s reload and next launch after the previous missile finishes. R resets site, HP, missile and radar contact. Bomb/NURS destroy the site; already launched missile continues.
5. Regression: H diagnostics off by default, shared HP/fuel HUD, AA1 behavior, G4c camera, WeaponLab and Sandbox unaffected. Unity compile and Play Mode must be run locally.
