# HUD-02 — weapon readiness + compact sortie status

**Status:** code/prefab authored; needs Unity Play Mode green. No enemy AI or damage mechanics in this atom.

- `CombatHUD.prefab` status bar: bottom-left flush with left/bottom edges, **505×21** (was 290×28; exactly 25% less height). Left-to-right: numeric HP; afterburner charge; active weapon name + short cooldown fill. The same prefab is used by E1, WeaponLab and GroundAttackLab; labs without a modular loadout hide the weapon section.
- Current player profile label: **AC-01 AUTOCANNON** (`AC01_Burst.asset`). Generic `IWeaponHudStatus` provides weapon name and readiness. `Autocannon.Readiness01()` uses existing next-shot and burst state, so the indicator stays empty during a running burst, fills during the actual inter-burst cooldown and is full only when the next burst is allowed. No duplicated HUD timer and no changes to the 14 rps / 4-round / 0.45 s baseline.
- `PlayerWeaponTrigger`: LMB fires only the selected installed `ITriggeredWeapon`; wheel moves cyclically through enabled weapons discovered on the player chassis (GUN-01 mount preferred first). If only one gun is equipped, wheel does nothing. When changed, pending rounds on the previous gun are cancelled and its cooldown is preserved. Future loadout changes may call `RefreshWeapons()`; no scene object searches per frame. AI continues calling `ITriggeredWeapon` directly.
- `E1MapFlightSession` binds its player `PlayerWeaponTrigger` into `CombatHud.Bind`; existing ground and air labs keep the optional null weapon UI binding. No new scene override / installer required, because the reusable prefab is edited in place.

## Verify in Unity

1. Import patch, open `ArcadeDesertE1`, Play. Ensure HP + BOOST + **AC-01 AUTOCANNON** appear at the **lower-left screen edge**, without the old 18px inset, and with 21px panel height. Camera remains full-frame.
2. Hold LMB: first shot empties the weapon bar; it stays empty through the 4-shot burst, then replenishes over 0.45s. On repeat fire it cycles again. No weapon tuning, muzzle direction or tracer changes.
3. Scroll up/down with one equipped module: no change; fire still works. Later, with 2+ enabled modules in player hierarchy, wheel should change the selected module and the HUD name/readiness together, without dual firing.
4. Smoke test `WeaponLab` and `GroundAttackLab`: no new weapon-specific HUD when no modular `PlayerWeaponTrigger`; old 1–6 lab weapons and AA/SAM radar stay unchanged. No missing scripts / prefab GUIDs / Unity console errors.

Next step **only after green**: collect the user's Tiger fighter behavior plan before touching AI.
