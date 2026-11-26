# CLEAN-03B4B - Legacy camera target lock

Remove the disabled camera-based single-target lock and HUD. The live arcade missile path (TargetDesignation, WeaponLock, ArcadeMissileLauncher, TargetingCursor) stays unchanged. Drop the two disabled components from ArcadeCameraRig and their exact m_Enabled overrides in both surviving scenes. No other scene or prefab records are edited. RadarAirContact retains enabled-state visibility; no remaining implementation of the old eligibility interface is allowed by preflight. Camera pose and execution order are preserved. Tiger BreakTurn remains outside this atom.

Preflight requires exact green HEAD and blobs, clean worktree, and no surviving tracked references to deleted GUIDs, component IDs, or types. Test both scenes, missile acquisition/release, radar, camera and retry in Unity. No commit performed.
