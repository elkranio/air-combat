# G1 — independent GroundAttackLab composition proof

**Base:** post-C1. This atom creates a NEW, intentionally minimal flight scene. It does **not** change WeaponLab/Sandbox/Frontier or any approved prefab.

1. Apply G1 patch **after completing C1**. Open `Assets/_Project/Scenes/WeaponLab.unity` in Edit Mode, no dirty scene. Run **Air Combat > Scenes > Create GroundAttackLab G1**. The command refuses to overwrite outputs.
2. Unity creates `Assets/_Project/Scenes/GroundAttackLab.unity`, a basic URP material `Assets/_Project/Art/GroundAttackLab/TestGround.mat` and their `.meta` files. Check the Hierarchy: one `PlayerJet` prefab instance, one `CombatCameraRig` prefab instance, `GroundAttackLab Session`, `Sun` and a large neutral plane. There is **no enemy, no WeaponLabSession, no target/damage logic, no ground weapon**.
3. Run Play Mode. Test WASD / Space / Q,E / BURST and GAUSS (no targets; only shot visuals/sounds) / camera at 65 degrees with look-ahead / R reset / H diagnostics. Key `3` deliberately has nothing to lock or launch against; ground ordnance is a later atom.
4. Reopen WeaponLab and Sandbox; verify existing fighting and camera. Confirm only Sandbox in Build Settings. Stage generated scene/material and all `.meta` before green.

`GroundAttackLabSession` is a **scene binder**, not a new gameplay controller. It references `JetAssembly` and `CombatCameraRig` on scene instances, binds `BoundedAim`, `WeaponSelection`, `WeaponController`, `BreakTurn` and camera target at runtime. No scene-level target reference is stored in shared prefabs. The large flat plane is a collision/scale placeholder; it is not a combat target and cannot be damaged by air-only weapons. This test deliberately avoids importing the FrontierStrata landscape until composition itself is green.

**Next gate:** G2 — decide a ground target and first air-to-ground weapon in a separate design/code atom. No new targeting rules, NURS, bombs, damage balance or HUD changes in G1.
