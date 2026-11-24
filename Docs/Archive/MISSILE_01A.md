# MISSILE-01a — cursor and lock proof

Base `ddfbd3c`. Production encounter only; legacy single-target TargetLock, HUD and WeaponLab launcher unchanged. No guided projectile, ammo, damage, balance, AI, radar or camera changes.

- `TargetingCursor` reads screen position from mouse, replaces OS pointer inside focused game view only, restores it outside/on disable. No gamepad yet. `TargetDesignation` registers living hostile hulls on spawn, projects cached renderer bounds, picks current target preferentially while overlapping or nearest centre otherwise. No nose cone, radar-range gate or scene search. `WeaponLock` owns progress and grace; hover another aircraft resets immediately. Partial grace 0.5 s, complete grace 1 s, freeze progress while absent; offscreen uses same grace. Death, removal, weapon change, focus loss and reset clear lock.
- A runtime third weapon slot AAM / LOCK TEST exposes progress to existing weapon HUD. LMB on a fresh press consumes completed lock, including during offscreen grace; only prints TEST RELEASE. No actual projectile. Keeping LMB held cannot consume successive locks. Cursor remains available with guns but no lock when they are selected.
- Minimal brackets, progress and status are only a test readout, not final presentation. MISSILE-01b will add animated military-style frame and audible locking/locked periodic beep (including full grace); MISSILE-02 physical nose-forward fire-and-forget with finite flight time and turn rate. No gamepad yet.

## Play Mode gate

1. Main map: wheel cycles AC-01 / AC-02 / AAM; guns unchanged. Cursor visible in all slots; OS pointer returns outside/focus loss/exit Play Mode.
2. Hover either Tiger for 1 s, see LOCKED; move off during incomplete lock for <0.5 s and back, see frozen progress resume; exceed grace, see zero. Full lock holds 1 s, then clears. Repeat at upper/lower edge, including fully offscreen; status remains LOCKED until expiration.
3. Hover another plane outside current pick area: previous lock resets even if complete. If overlapping, current target retains priority until pointer leaves its area. No forward cone.
4. LMB before full lock: nothing. New click after full lock (also during grace): TEST RELEASE and fresh acquisition; held LMB does not repeatedly consume. Switch to gun, kill target, R, defeat and alt-tab: no stale lock. Verify legacy labs unchanged.
