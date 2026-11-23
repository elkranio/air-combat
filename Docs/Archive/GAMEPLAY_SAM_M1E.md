# SAM1e — missile balance and one-shot launch warning (Unity playtest required)

Built on green `master` after SAM1b vertical VLS and SAM1c/d missile diagnostics and collision fix. This is a tuning-only patch: **no scene/prefab installation, no geometry update**.

## Changes

- SAM acquisition stays **350** range, **1.4s** dwell, beep during acquisition. Once the rocket launches, the receiver plays **exactly three short beeps** separated by **0.15s**, then **no more beeps during its flight**. The next acquisition can sound again after reload.
- The dedicated `SurfaceToAirMissile` prefab and its script defaults change: **speed 112 → 134.4 (+20%)**, pursuit **turnRate 72 → 86.4°/s (+20%)**.
- **Total pursuit turn budget stays 145°**; initial vertical launch and pitch-over are still free. Vertical time **0.34s**, pitch-over turn **210°/s** up to **0.75s**, lifetime **6.5s**, damage **5 HP**, reload **7s** all unchanged. This preserves maneuver-based dodging, with a faster rocket and faster corrections.
- This does **not** change the player's air-to-air `MissileProjectile`, AA gun, player flight, radar or other weapons.

## Test

1. Open GroundAttackLab; approach the SAM from its side. Hear the regular slow-to-fast lock beeps. At launch hear **three short pips**, then **silence** while the missile approaches. Wait for reload: next acquisition may sound again. R reset should cancel any pending launch pips.
2. Fly straight without W/Q/E, including crossing the SAM's field at an angle. Note `H` diagnostics `SAM last: HIT PLAYER / EXPIRED / BLOCKED`, not only visual impression. Expect improved but not guaranteed hits (finite turn budget is intentional).
3. Repeat with A/D, W and Q/E. Verify dodges still work if timed; compare how much closer a non-evasive flight comes to being hit. If normal straight flight still consistently misses, next tweak should be target interception (lead) **separately** from speed/turn tuning.
4. Verify incoming amber missile remains on radar during flight, HP drops only on actual hit, site destruction/R reset work; check WeaponLab and Sandbox for regressions.

Only C# + the existing `SAM1Missile.prefab` numeric overrides + docs change here; Unity Play Mode must be checked locally.
