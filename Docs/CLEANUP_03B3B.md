# CLEAN-03B3B - Retire legacy weapons and enemy AI

Remove WeaponController, WeaponSelection (including WeaponMode), GaussCannon, GaussEmitter, GuidedMissileLauncher, BoundedAim, WeaponLabEnemy, WeaponLabEvasion, EnemyFlightController and LockAfterburnerReaction with their metas. Their old prefabs and scenes were retired in earlier atoms. AutocannonEmitter receives comment-only changes. Retain Autocannon, ContinuousCannon, AutocannonWeapon, ArcadeMissileLauncher, ArcadeNursLauncher, current fighter AI, missile targeting, and shared visual/projectile code. No scene, prefab or gameplay tuning changes.

Exact accepted HEAD and blob checks, clean worktree, unique replacements, and a tracked text scan of surviving GUID/type references are required before any changes. A surviving reference blocks the entire batch. Text scans do not prove dynamic/binary dependencies. Reopen Unity and verify both scenes and all three sandbox modes after applying. No commit is performed by the script.
