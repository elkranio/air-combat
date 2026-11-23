# SAM1 — ground-launched guided missile (historical M1 trial)

**Superseded by [SAM1b vertical launch](GAMEPLAY_SAM_M1B.md).** Do not repeat this original installer on an existing SAM prefab; use the one-time SAM1b upgrade command instead. The rotating muzzle lock described below is the old failed prototype.

Built against current `master` with accepted AA1, north-up R1 radar and H1 CombatHUD. This is a separate projectile prefab; **do not change** `MissileProjectile` (air-to-air) or player weapon balance.

## One-time installation

1. Apply patch with Unity **closed**. Open and save ONLY `Assets/_Project/Scenes/GroundAttackLab.unity` in Edit Mode.
2. Run **Air Combat > SAM1 > Install Missile Tower Trial** **once**.
3. It creates two **independent** prefabs, `Prefabs/Enemies/SAM1Launcher.prefab` and `SAM1Missile.prefab`, their `Art/SAM1/*.mat` materials, and one instance at `(-320, 0, 215)`. This location puts the site in radar range from spawn, but just outside firing range; AA turret is elsewhere so you can approach separately. No cross-scene player reference is stored in prefabs.
4. Verify Unity, then commit generated `.prefab`, `.mat`, `.unity` and **all new `.meta` files** with the code. Installer refuses to overwrite anything; do not rerun after installation.

The first art pass is an **editable low-poly Unity prefab** with separate yaw and pitch, two missile canisters and a physical missile model with exhaust trail. Meshes use real URP shadow maps. This does **not** create baked texture atlases/FBX; bake only after the silhouette and size are approved.

## First-pass behavior

- **350 world units** target acquisition (radar scans 460). The engagement radius is the radar danger ring; aircraft should see the site BEFORE lock. Destruction hides the site/ring.
- Turret rotates at yaw **65°/s**, pitch **40°/s**; **1.4s audible lock** when aligned to within **11°**. In-cockpit synthesized "beep-beep" at 0.38s then 0.22s intervals. While a missile is inbound, rapid 0.18s beeps replace the lock cadence. Silence outside acquisition or during reload.
- One active missile per launcher; **7s reload**. Missile: speed **112**, turn **72°/s**, total turning budget **145°**, lifetime **6.5s**, hit radius **4.2**, damage **5 HP**. Real flight and collision, not hitscan. Q/E and W ought to allow a break; no decoys/chaff.
- Missile is **not** the player's `MissileProjectile`. SAM1 is a separate prefab/script to tune speed, turning and impact without touching air-to-air balance.
- `IRadarContact` now distinguishes **GroundDefense** red dots/rings from **IncomingMissile** amber arrows. Only the latter are registered on launch and removed on impact/expiry/reset. Player missiles are not registered, and neither are ordinary shells. Both kinds use aircraft scan range and are clipped to the radar circle.
- SAM launcher is ground-domain `DamageableTarget` with **6 HP**; bombs and NURS can suppress it. A missile in flight keeps tracking and beeping if its launcher has been destroyed. R resets site, jet HP and removes the active missile. Shared HUD and radar layouts do not change.

## Test checklist

1. Spawn GroundAttackLab. Confirm the SAM site appears as a red dot with a **larger** range ring than the AA autocannon. Approach from the **left** to test SAM without flying directly over the AA site.
2. Outside range: no beep. Enter 350: turret rotates, audible slow then fast beep while locked; after ~1.4s of alignment launches one rocket. During flight: fast beeps; after impact/expiry: silence until next lock. The launched rocket must appear as an amber directional glyph moving on the radar.
3. Fly straight versus Q/E and W evasions. Rocket should hit careless approaches, but fast turns eventually exhaust its finite turn budget. Check physical travel and visual trail, and that the radar glyph vanishes on destruction.
4. Damage the launcher with bombs/NURS. Firing stops at zero HP; already launched missile stays dangerous. `R` resets HP and destroys active missile; no stale radar dot/sound/TrailRenderer.
5. H toggles diagnostics; shared HP/fuel strip, camera rect, top-right north-up radar, AA1 tracer and radar range circle remain untouched. WeaponLab/Sandbox unaffected. If installed assets aren't visible, inspect Unity Console for Missing Script/Prefab.

These values are playtest guesses, not final game balance. For multiple SAM sites later we should move cockpit beeps to a shared threat-audio aggregator to avoid duplicated beeps; do not build it prematurely for one site.
