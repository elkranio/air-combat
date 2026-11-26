# CLEAN-03A - Retire obsolete labs

Remove WeaponLab, GroundAttackLab and Terrain01Playground scenes with their metas, the two lab session controllers and the old Terrain01 editor generator. Keep all other assets for a separate dependency audit. Build Settings now includes Mission01_FirstSortie first and CombatSandbox second (both enabled); scene and script GUIDs of retained assets are unchanged.

The preflight checks exact green HEAD, blobs, a clean worktree and surviving tracked text references by GUID and type/path name. This is not a complete Unity dependency graph: binary and dynamically loaded assets still need review before subsequent asset deletion.

Close Unity before apply. Reopen, check compilation, both remaining scenes, all sandbox modes and mission retry. Check Build Profiles scene list. Commit with git add -A after verification.
